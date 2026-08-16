using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.HR.DTOs;
using MyApi.Modules.HR.Models;
using MyApi.Modules.Planning.Models;

namespace MyApi.Modules.HR.Services
{
    public partial class HrService : IHrService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<HrService>? _logger;

        public HrService(ApplicationDbContext db, ILogger<HrService>? logger = null)
        {
            _db = db;
            _logger = logger;
        }

        // ===========================================================================
        // VALIDATION HELPERS — safe handling of nullable / out-of-range decimals.
        // Keeps non-nullable DB columns from receiving NaN-like, negative, or absurd values.
        // ===========================================================================
        private const decimal HoursMin = 0m;
        private const decimal HoursMax = 24m;
        private const decimal SalaryMax = 10_000_000m;

        /// <summary>Safely resolves a nullable decimal to a finite, in-range value.</summary>
        private static decimal SafeDecimal(decimal? value, decimal fallback = 0m, decimal? min = null, decimal? max = null)
        {
            var v = value ?? fallback;
            if (min.HasValue && v < min.Value) v = min.Value;
            if (max.HasValue && v > max.Value) v = max.Value;
            return v;
        }

        /// <summary>Safely resolves an hours value (0..24, rounded to 2 decimals).</summary>
        private static decimal SafeHours(decimal? value, decimal fallback = 0m)
            => Math.Round(SafeDecimal(value, fallback, HoursMin, HoursMax), 2);

        /// <summary>Safely resolves a salary value (>=0, capped, rounded to 3 decimals for TND).</summary>
        private static decimal SafeSalary(decimal? value, decimal fallback = 0m)
            => Math.Round(SafeDecimal(value, fallback, 0m, SalaryMax), 3);

        // ===========================================================================
        // EMPLOYEES
        // ===========================================================================
        public async Task<List<object>> GetEmployeesAsync()
        {
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && !u.IsDeleted)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();

            var userIds = users.Select(u => u.Id).ToList();
            var salaryConfigMap = (await _db.Set<HrEmployeeSalaryConfig>()
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .ToListAsync())
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            return users.Select(u =>
            {
                salaryConfigMap.TryGetValue(u.Id, out var cfg);
                return new
                {
                    user = MapSafeUser(u),
                    salaryConfig = cfg != null ? MapSalaryConfigDto(cfg) : null
                };
            }).Cast<object>().ToList();
        }

        public async Task<object> GetEmployeeDetailAsync(int userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null) throw new KeyNotFoundException("hr.employee_not_found");

            var salaryConfig = await _db.Set<HrEmployeeSalaryConfig>().FirstOrDefaultAsync(x => x.UserId == userId);
            var leaves = await _db.Set<UserLeave>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.StartDate)
                .Take(20)
                .ToListAsync();
            var documents = await _db.Set<HrEmployeeDocument>()
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new HrEmployeeDocumentDto
                {
                    Id = x.Id, UserId = x.UserId, DocType = x.DocType, Title = x.Title,
                    FileUrl = x.FileUrl, FileName = x.FileName, MimeType = x.MimeType,
                    FileSize = x.FileSize, IssuedDate = x.IssuedDate, ExpiresAt = x.ExpiresAt,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
            var salaryHistory = await _db.Set<HrSalaryHistory>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.EffectiveDate)
                .Take(50)
                .Select(x => new HrSalaryHistoryDto
                {
                    Id = x.Id, UserId = x.UserId, PreviousGross = x.PreviousGross,
                    NewGross = x.NewGross, Currency = x.Currency, EffectiveDate = x.EffectiveDate,
                    Reason = x.Reason, ChangedBy = x.ChangedBy
                })
                .ToListAsync();

            return new
            {
                user = MapSafeUser(user),
                salaryConfig = salaryConfig != null ? MapSalaryConfigDto(salaryConfig) : null,
                leaves,
                documents,
                salaryHistory
            };
        }

        public async Task<HrEmployeeSalaryConfigDto> UpsertSalaryConfigAsync(int userId, UpsertSalaryConfigDto dto, int actorUserId)
        {
            // EnableRetryOnFailure is on for Npgsql, so user-initiated transactions
            // MUST run inside an execution strategy, otherwise BeginTransactionAsync
            // throws "does not support user-initiated transactions" (HTTP 400).
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => UpsertSalaryConfigCoreAsync(userId, dto, actorUserId));
        }

        private async Task<HrEmployeeSalaryConfigDto> UpsertSalaryConfigCoreAsync(int userId, UpsertSalaryConfigDto dto, int actorUserId)
        {
            // Serialize concurrent salary edits for the same employee. Without this,
            // two overlapping calls would each read the same `previousGross`, both
            // write their own `newGross`, and both insert HrSalaryHistory rows
            // claiming the same "previous" value — corrupting the audit trail
            // (a jump from A to C would be recorded as two A→B and A→C rows
            // instead of A→B and B→C). A transaction-scoped Postgres advisory
            // lock keyed on (namespace, userId) queues concurrent writers so
            // the second read observes the first writer's committed value.
            // The lock covers both existing-row updates and first-time inserts,
            // and is auto-released on COMMIT/ROLLBACK.
            await using var tx = await _db.Database.BeginTransactionAsync();
            const int SalaryLockNamespace = 0x48525F53; // 'HR_S'
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0}, {1})",
                SalaryLockNamespace, userId);

            var entity = await _db.Set<HrEmployeeSalaryConfig>().FirstOrDefaultAsync(x => x.UserId == userId);
            var isNew = entity == null;
            var previousGross = entity?.GrossSalary;

            if (entity == null)
            {
                entity = new HrEmployeeSalaryConfig { UserId = userId };
                _db.Set<HrEmployeeSalaryConfig>().Add(entity);
            }

            entity.GrossSalary = dto.GrossSalary.HasValue ? SafeSalary(dto.GrossSalary, entity.GrossSalary) : entity.GrossSalary;
            entity.IsHeadOfFamily = dto.IsHeadOfFamily ?? entity.IsHeadOfFamily;
            entity.ChildrenCount = dto.ChildrenCount ?? entity.ChildrenCount;
            entity.CustomDeductions = dto.CustomDeductions.HasValue ? SafeSalary(dto.CustomDeductions, entity.CustomDeductions ?? 0m) : entity.CustomDeductions;
            entity.BankAccount = dto.BankAccount ?? entity.BankAccount;
            entity.CnssNumber = dto.CnssNumber ?? entity.CnssNumber;
            entity.HireDate = dto.HireDate ?? entity.HireDate;
            entity.Department = dto.Department ?? entity.Department;
            entity.Position = dto.Position ?? entity.Position;
            entity.EmploymentType = dto.EmploymentType ?? entity.EmploymentType;
            entity.ContractType = dto.ContractType ?? entity.ContractType;
            entity.ContractEndDate = dto.ContractEndDate ?? entity.ContractEndDate;
            entity.Cin = dto.Cin ?? entity.Cin;
            entity.BirthDate = dto.BirthDate ?? entity.BirthDate;
            entity.MaritalStatus = dto.MaritalStatus ?? entity.MaritalStatus;
            entity.AddressLine1 = dto.AddressLine1 ?? entity.AddressLine1;
            entity.AddressLine2 = dto.AddressLine2 ?? entity.AddressLine2;
            entity.City = dto.City ?? entity.City;
            entity.PostalCode = dto.PostalCode ?? entity.PostalCode;
            entity.EmergencyContactName = dto.EmergencyContactName ?? entity.EmergencyContactName;
            entity.EmergencyContactPhone = dto.EmergencyContactPhone ?? entity.EmergencyContactPhone;
            entity.UpdatedAt = DateTime.UtcNow;

            // ---- Versioning: salary history + audit ----
            var newGross = entity.GrossSalary;
            if (isNew || (previousGross.HasValue && previousGross.Value != newGross) || (!previousGross.HasValue && dto.GrossSalary.HasValue))
            {
                _db.Set<HrSalaryHistory>().Add(new HrSalaryHistory
                {
                    UserId = userId,
                    PreviousGross = previousGross,
                    NewGross = newGross,
                    EffectiveDate = DateTime.UtcNow,
                    Reason = dto.SalaryChangeReason,
                    ChangedBy = actorUserId
                });
                await LogAsync(userId, "salary_change", $"Salary set to {newGross:0.000} TND",
                    new { previous = previousGross, current = newGross, reason = dto.SalaryChangeReason }, actorUserId);
            }
            else
            {
                await LogAsync(userId, "profile_updated", "Employee profile updated", null, actorUserId);
            }

            try
            {
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException ex) when (isNew && IsUniqueViolation(ex))
            {
                // A concurrent writer created the row after our lock window
                // (only possible if the lock was bypassed). Roll back and
                // surface a retryable conflict rather than a duplicate row.
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    "Salary configuration was modified concurrently. Please retry.", ex);
            }
            return MapSalaryConfigDto(entity);
        }

        public async Task<List<HrSalaryHistoryDto>> GetSalaryHistoryAsync(int userId)
        {
            return await _db.Set<HrSalaryHistory>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.EffectiveDate)
                .Select(x => new HrSalaryHistoryDto
                {
                    Id = x.Id, UserId = x.UserId, PreviousGross = x.PreviousGross,
                    NewGross = x.NewGross, Currency = x.Currency, EffectiveDate = x.EffectiveDate,
                    Reason = x.Reason, ChangedBy = x.ChangedBy
                })
                .ToListAsync();
        }

        // ===========================================================================
        // LEAVES (HR-owned; uses Planning's UserLeave for actual events)
        // ===========================================================================
        public async Task<List<HrLeaveBalanceDto>> GetLeaveBalancesAsync(int year)
        {
            var balances = await _db.Set<HrLeaveBalance>().AsNoTracking().Where(x => x.Year == year).ToListAsync();
            var leaves = await _db.Set<UserLeave>().AsNoTracking()
                .Where(x => x.StartDate.Year == year || x.EndDate.Year == year)
                .ToListAsync();

            var leavesByKey = leaves
                .GroupBy(x => (x.UserId, x.LeaveType))
                .ToDictionary(g => g.Key, g => g.ToList());

            return balances.Select(bal =>
            {
                var related = leavesByKey.TryGetValue((bal.UserId, bal.LeaveType), out var list) ? list : new List<UserLeave>();
                var used = related.Where(x => x.Status == "approved").Sum(x => (decimal)(x.EndDate.Date - x.StartDate.Date).TotalDays + 1);
                var pending = related.Where(x => x.Status == "pending").Sum(x => (decimal)(x.EndDate.Date - x.StartDate.Date).TotalDays + 1);
                return new HrLeaveBalanceDto
                {
                    UserId = bal.UserId, LeaveType = bal.LeaveType, AnnualAllowance = bal.AnnualAllowance,
                    Used = used, Pending = pending, Remaining = bal.AnnualAllowance - used - pending
                };
            }).ToList();
        }

        public async Task<List<HrLeaveBalanceDto>> SetLeaveAllowanceAsync(int userId, SetLeaveAllowanceDto dto)
        {
            var balance = await _db.Set<HrLeaveBalance>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == dto.Year && x.LeaveType == dto.LeaveType);
            if (balance == null)
            {
                balance = new HrLeaveBalance { UserId = userId, Year = dto.Year, LeaveType = dto.LeaveType };
                _db.Set<HrLeaveBalance>().Add(balance);
            }
            balance.AnnualAllowance = dto.AnnualAllowance;
            balance.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return await GetLeaveBalancesAsync(dto.Year);
        }

        // ===========================================================================
        // ATTENDANCE
        // ===========================================================================
        public async Task<List<HrAttendanceDto>> GetAttendanceAsync(int year, int month, int? userId = null)
        {
            // PG column is `timestamp with time zone` — Npgsql requires DateTimeKind.Utc for query parameters.
            // We treat the (year, month) as a calendar boundary at midnight UTC for filtering purposes.
            var start = DateTime.SpecifyKind(new DateTime(year, month, 1), DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var q = _db.Set<HrAttendance>()
                .Where(x => x.Date >= start && x.Date < end);
            if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);

            // Only the primary keys are read through EF. Legacy/imported rows in
            // `hr_attendance` can hold values that break Npgsql materialization
            // (NULLs in non-nullable CLR columns, `infinity` timestamps, numerics
            // that overflow the mapped precision) — any single bad row used to 500
            // the entire month. Selecting ids cannot cast anything, and the payload
            // itself is then read as text and parsed defensively below, so a corrupt
            // row degrades to defaults instead of failing the request.
            var ids = await q.Select(x => x.Id).ToListAsync();
            var rows = new List<HrAttendance>();

            if (ids.Count > 0)
            {
                var conn = _db.Database.GetDbConnection();
                var opened = conn.State != System.Data.ConnectionState.Open;
                if (opened) await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText =
                        "SELECT id, user_id::text, date::text, check_in::text, check_out::text, " +
                        "break_minutes::text, total_hours::text, overtime_hours::text, status, notes, source " +
                        "FROM hr_attendance WHERE id IN (" + string.Join(",", ids) + ")";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string? S(int i) => reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                        DateTime? D(int i)
                        {
                            var s = S(i);
                            return DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                                out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : (DateTime?)null;
                        }
                        decimal Dec(int i) => decimal.TryParse(S(i), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
                        int I(int i) => int.TryParse(S(i), out var v) ? v : 0;

                        rows.Add(new HrAttendance
                        {
                            Id = reader.GetInt32(0),
                            UserId = I(1),
                            Date = D(2) ?? start,
                            CheckIn = D(3),
                            CheckOut = D(4),
                            BreakMinutes = I(5),
                            TotalHours = Dec(6),
                            OvertimeHours = Dec(7),
                            Status = string.IsNullOrWhiteSpace(S(8)) ? "present" : S(8)!,
                            Notes = S(9),
                            Source = string.IsNullOrWhiteSpace(S(10)) ? "manual" : S(10)!,
                        });
                    }
                }
                finally
                {
                    if (opened) await conn.CloseAsync();
                }
            }

            rows = rows.OrderBy(x => x.Date).ThenBy(x => x.UserId).ToList();

            var userIds = rows.Select(x => x.UserId).Distinct().ToList();
            var userMap = await _db.Users.Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim()) ? (u.Email ?? $"#{u.Id}") : ($"{u.FirstName} {u.LastName}").Trim());

            return rows.Select(x => MapAttendanceDto(x, userMap)).ToList();
        }


        public async Task<HrAttendanceDto> UpsertAttendanceAsync(UpsertHrAttendanceDto dto, int actorUserId)
        {
            var settings = await GetAttendanceSettingsEntityAsync();
            // PG columns are `timestamp with time zone` — Npgsql requires DateTimeKind.Utc.
            // Treat the supplied calendar day as midnight UTC; check-in/out are anchored to the same UTC day.
            var date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
            var checkIn = NormalizeWallClock(dto.CheckIn, date);
            var checkOut = NormalizeWallClock(dto.CheckOut, date);

            // ---- Validation ----
            if (dto.UserId <= 0)
                throw new ArgumentException("attendance.invalid_user", nameof(dto.UserId));
            if (date == default)
                throw new ArgumentException("attendance.invalid_date", nameof(dto.Date));
            if (date > DateTime.Today.AddDays(1))
                throw new ArgumentException("attendance.future_date", nameof(dto.Date));
            if (dto.BreakMinutes < 0 || dto.BreakMinutes > 24 * 60)
                throw new ArgumentException("attendance.invalid_break", nameof(dto.BreakMinutes));
            if (checkIn.HasValue && checkOut.HasValue)
            {
                if (checkOut.Value <= checkIn.Value)
                    throw new ArgumentException("attendance.checkout_before_checkin", nameof(dto.CheckOut));
                var workedMinutes = (checkOut.Value - checkIn.Value).TotalMinutes - dto.BreakMinutes;
                if (workedMinutes <= 0)
                    throw new ArgumentException("attendance.break_exceeds_worked", nameof(dto.BreakMinutes));
                if ((checkOut.Value - checkIn.Value).TotalHours > 24)
                    throw new ArgumentException("attendance.range_too_long", nameof(dto.CheckOut));
            }
            else if (checkOut.HasValue && !checkIn.HasValue)
            {
                throw new ArgumentException("attendance.checkout_without_checkin", nameof(dto.CheckIn));
            }
            var allowedStatuses = new[] { "present", "absent", "late", "half_day", "leave", "holiday" };
            if (!string.IsNullOrWhiteSpace(dto.Status) && Array.IndexOf(allowedStatuses, dto.Status.Trim()) < 0)
                throw new ArgumentException("attendance.invalid_status", nameof(dto.Status));

            var set = _db.Set<HrAttendance>();
            // Match the stored row on the whole calendar day rather than on an exact
            // timestamp: `date` is a `timestamp with time zone` column, so a row written
            // with a non-UTC midnight (older imports/seeds) never equals our UTC midnight
            // and the equality lookup silently fell through to an INSERT, which then hit
            // the (tenant, user, date) unique index and surfaced as a 500.
            var dayEnd = date.AddDays(1);
            var target = await set.FirstOrDefaultAsync(x => x.UserId == dto.UserId && x.Date >= date && x.Date < dayEnd);
            // The edit dialog posts the row id. Honour it so changing the employee or the
            // date of an existing entry moves that row instead of creating a duplicate.
            var byId = dto.Id > 0 ? await set.FirstOrDefaultAsync(x => x.Id == dto.Id) : null;
            if (target != null && byId != null && target.Id != byId.Id)
            {
                // Entry was moved onto a day that already has a record — keep one row per day.
                set.Remove(byId);
            }
            var row = target ?? byId;
            var isNew = row == null;
            if (row == null)
            {
                row = new HrAttendance { UserId = dto.UserId, Date = date };
                set.Add(row);
            }

            row.UserId = dto.UserId;
            row.Date = date;
            row.CheckIn = checkIn;
            row.CheckOut = checkOut;
            row.BreakMinutes = Math.Max(0, dto.BreakMinutes);
            row.Status = string.IsNullOrWhiteSpace(dto.Status)
                ? (string.IsNullOrWhiteSpace(row.Status) ? "present" : row.Status)
                : dto.Status.Trim();
            row.Notes = dto.Notes;
            row.Source = string.IsNullOrWhiteSpace(dto.Source)
                ? (string.IsNullOrWhiteSpace(row.Source) ? "manual" : row.Source)
                : dto.Source.Trim();
            var computed = ComputeAttendanceHours(checkIn, checkOut, row.BreakMinutes, settings);
            row.TotalHours = SafeHours(dto.TotalHours ?? computed.totalHours);
            row.OvertimeHours = SafeHours(dto.OvertimeHours ?? computed.overtimeHours);
            if (row.OvertimeHours > row.TotalHours) row.OvertimeHours = row.TotalHours;
            row.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Either a concurrent insert won the (user, date) race, or the row we tried
                // to insert already exists under a different timestamp. Reload the day's row
                // and re-apply the values as an update instead of failing with a 500.
                var pending = new
                {
                    row.CheckIn, row.CheckOut, row.BreakMinutes, row.Status,
                    row.Notes, row.Source, row.TotalHours, row.OvertimeHours,
                };
                _db.Entry(row).State = EntityState.Detached;
                var existing = await set.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == dto.UserId && x.Date >= date && x.Date < dayEnd);
                if (existing == null) throw new InvalidOperationException("attendance.save_failed");
                var tracked = await set.FirstOrDefaultAsync(x => x.Id == existing.Id);
                if (tracked == null) throw new InvalidOperationException("attendance.save_failed");
                tracked.CheckIn = pending.CheckIn;
                tracked.CheckOut = pending.CheckOut;
                tracked.BreakMinutes = pending.BreakMinutes;
                tracked.Status = pending.Status;
                tracked.Notes = pending.Notes;
                tracked.Source = pending.Source;
                tracked.TotalHours = pending.TotalHours;
                tracked.OvertimeHours = pending.OvertimeHours;
                tracked.UpdatedAt = DateTime.UtcNow;
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    throw new InvalidOperationException("attendance.save_failed");
                }
                row = tracked;
                isNew = false;
            }

            await LogAsync(dto.UserId, isNew ? "attendance_added" : "attendance_updated", $"Attendance saved for {date:yyyy-MM-dd}", new { dto.UserId, date, row.TotalHours, row.OvertimeHours }, actorUserId);

            var userMap = await _db.Users.Where(u => u.Id == dto.UserId)
                .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim()) ? (u.Email ?? $"#{u.Id}") : ($"{u.FirstName} {u.LastName}").Trim());
            return MapAttendanceDto(row, userMap);
        }

        public async Task DeleteAttendanceAsync(int id, int actorUserId)
        {
            // Read the row through raw SQL (same defensive reasoning as GetAttendanceAsync):
            // legacy/imported rows can hold values that break EF materialization, which
            // turned every delete into an opaque 500. Text casts can never throw.
            var exists = await _db.Set<HrAttendance>().AnyAsync(x => x.Id == id);
            if (!exists) throw new KeyNotFoundException("hr.attendance_not_found");

            int userId = 0;
            DateTime day = DateTime.UtcNow;
            try
            {
                var conn = _db.Database.GetDbConnection();
                var opened = conn.State != System.Data.ConnectionState.Open;
                if (opened) await conn.OpenAsync();
                try
                {
                    using var read = conn.CreateCommand();
                    read.CommandText = $"SELECT user_id::text, date::text FROM hr_attendance WHERE id = {id}";
                    using (var reader = await read.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0) && int.TryParse(reader.GetValue(0)?.ToString(), out var uid)) userId = uid;
                            if (!reader.IsDBNull(1) && DateTime.TryParse(reader.GetValue(1)?.ToString(),
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                                    out var d))
                                day = DateTime.SpecifyKind(d, DateTimeKind.Utc);
                        }
                    }

                    using var del = conn.CreateCommand();
                    del.CommandText = $"DELETE FROM hr_attendance WHERE id = {id}";
                    var affected = await del.ExecuteNonQueryAsync();
                    if (affected == 0) throw new KeyNotFoundException("hr.attendance_not_found");
                }
                finally
                {
                    if (opened) await conn.CloseAsync();
                }
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Row is referenced elsewhere (or the DB rejected the delete) — report a
                // translatable business error instead of an opaque 500.
                _logger?.LogError(ex, "HR: attendance delete failed for id={Id}", id);
                throw new InvalidOperationException("attendance.delete_failed");
            }

            // A failed audit write must never fail an already-committed delete.
            try
            {
                await LogAsync(userId, "attendance_deleted", $"Attendance deleted for {day:yyyy-MM-dd}", new { id }, actorUserId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "HR: attendance delete audit log failed for id={Id}", id);
            }
        }

        public async Task<HrAttendanceSettingsDto> GetAttendanceSettingsAsync()
        {
            return MapAttendanceSettingsDto(await GetAttendanceSettingsEntityAsync());
        }

        public async Task<HrAttendanceSettingsDto> UpsertAttendanceSettingsAsync(UpsertHrAttendanceSettingsDto dto, int actorUserId)
        {
            var row = await GetAttendanceSettingsEntityAsync();
            row.WorkDaysJson = JsonSerializer.Serialize((dto.WorkDays ?? new List<int>()).Distinct().OrderBy(x => x).ToList());
            row.StandardHoursPerDay = dto.StandardHoursPerDay;
            row.OvertimeThresholdHours = dto.OvertimeThresholdHours;
            row.OvertimeMultiplier = dto.OvertimeMultiplier;
            row.LateThresholdMinutes = dto.LateThresholdMinutes;
            row.RoundingMethod = dto.RoundingMethod;
            row.CalculationMethod = dto.CalculationMethod;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogAsync(0, "attendance_settings_updated", "Attendance settings updated", dto, actorUserId);
            return MapAttendanceSettingsDto(row);
        }

        public async Task<HrAttendanceImportResultDto> ImportAttendanceAsync(List<ImportHrAttendanceRowDto> rows, int actorUserId)
        {
            var result = new HrAttendanceImportResultDto();
            if (rows == null || rows.Count == 0) return result;

            var settings = await GetAttendanceSettingsEntityAsync();
            var allowedStatuses = new[] { "present", "absent", "late", "half_day", "leave", "holiday" };
            var validRows = new List<ImportHrAttendanceRowDto>();
            foreach (var r in rows)
            {
                if (r.UserId <= 0) { result.Skipped++; continue; }
                if (r.Date.Date == default || r.Date.Date > DateTime.Today.AddDays(1)) { result.Skipped++; continue; }
                if (r.BreakMinutes < 0 || r.BreakMinutes > 24 * 60) { result.Skipped++; continue; }
                var ci = NormalizeWallClock(r.CheckIn, r.Date.Date);
                var co = NormalizeWallClock(r.CheckOut, r.Date.Date);
                if (ci.HasValue && co.HasValue && co.Value <= ci.Value) { result.Skipped++; continue; }
                if (co.HasValue && !ci.HasValue) { result.Skipped++; continue; }
                if (!string.IsNullOrWhiteSpace(r.Status) && Array.IndexOf(allowedStatuses, r.Status.Trim()) < 0) { result.Skipped++; continue; }
                validRows.Add(r);
            }
            if (validRows.Count == 0) return result;
            var userIds = validRows.Select(r => r.UserId).Distinct().ToList();
            var dates = validRows.Select(r => DateTime.SpecifyKind(r.Date.Date, DateTimeKind.Unspecified)).Distinct().ToList();
            var existing = await _db.Set<HrAttendance>()
                .Where(x => userIds.Contains(x.UserId) && dates.Contains(x.Date))
                .ToListAsync();

            foreach (var dto in validRows)
            {
                var date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
                var checkIn = NormalizeWallClock(dto.CheckIn, date);
                var checkOut = NormalizeWallClock(dto.CheckOut, date);
                var row = existing.FirstOrDefault(x => x.UserId == dto.UserId && x.Date == date);
                var isNew = row == null;
                if (row == null)
                {
                    row = new HrAttendance { UserId = dto.UserId, Date = date };
                    _db.Set<HrAttendance>().Add(row);
                    existing.Add(row);
                    result.Created++;
                }
                else result.Updated++;

                row.Date = date;
                row.CheckIn = checkIn;
                row.CheckOut = checkOut;
                row.BreakMinutes = Math.Max(0, dto.BreakMinutes);
                row.Status = string.IsNullOrWhiteSpace(dto.Status) ? "present" : dto.Status.Trim();
                row.Notes = dto.Notes;
                row.Source = string.IsNullOrWhiteSpace(dto.Source) ? "csv_import" : dto.Source.Trim();
                var computed = ComputeAttendanceHours(checkIn, checkOut, row.BreakMinutes, settings);
                row.TotalHours = SafeHours(dto.TotalHours ?? computed.totalHours);
                row.OvertimeHours = SafeHours(dto.OvertimeHours ?? computed.overtimeHours);
                if (row.OvertimeHours > row.TotalHours) row.OvertimeHours = row.TotalHours;
                row.UpdatedAt = DateTime.UtcNow;
                result.Imported++;
            }

            await _db.SaveChangesAsync();
            await LogAsync(0, "attendance_imported", $"Imported {result.Imported} attendance rows", result, actorUserId);
            return result;
        }

        // ===========================================================================
        // PAYROLL (Tunisia: Gross + Allowances + Bonuses → CNSS, IRPP, CSS → Net)
        // ===========================================================================
        public async Task<HrPayrollRunDto> GeneratePayrollRunAsync(CreatePayrollRunDto dto, int createdByUserId)
        {
            // Retrying execution strategy requires user-initiated transactions to be
            // wrapped, otherwise payroll generation fails on the first attempt.
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => GeneratePayrollRunCoreAsync(dto, createdByUserId));
        }

        private async Task<HrPayrollRunDto> GeneratePayrollRunCoreAsync(CreatePayrollRunDto dto, int createdByUserId)
        {
            if (dto.Month < 1 || dto.Month > 12) throw new InvalidOperationException("hr.invalid_month");
            if (dto.Year < 2000 || dto.Year > 2100) throw new InvalidOperationException("hr.invalid_year");

            // Duplicate guard: prevent a second run for the same tenant/year/month
            // (blocks double-clicks and concurrent admin submissions).
            // The check-then-insert is serialized by a transaction-scoped advisory
            // lock keyed on (year, month) — same pattern as UpsertSalaryConfigAsync —
            // and additionally backed by the unique index
            // ux_hr_payroll_runs_tenant_year_month on (TenantId, year, month).
            await using var runTx = await _db.Database.BeginTransactionAsync();
            const int PayrollLockNamespace = 0x48525F50; // 'HR_P'
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0}, {1})",
                PayrollLockNamespace, dto.Year * 100 + dto.Month);

            var existing = await _db.Set<HrPayrollRun>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Year == dto.Year && x.Month == dto.Month);
            if (existing != null)
            {
                throw new InvalidOperationException(
                    "hr.payroll_run_exists");
            }

            var rate = await GetActiveCnssRateEntityAsync();
            var attendanceSettings = await GetAttendanceSettingsEntityAsync();
            var brackets = ParseBrackets(rate.IrppBracketsJson);

            var run = new HrPayrollRun
            {
                Month = dto.Month, Year = dto.Year, Status = "draft",
                CreatedBy = createdByUserId, CreatedAt = DateTime.UtcNow
            };
            _db.Set<HrPayrollRun>().Add(run);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Concurrent request won the race — surface a clean 4xx instead of a 500.
                throw new InvalidOperationException(
                    "hr.payroll_run_exists");
            }


            var users = await _db.Users.AsNoTracking().Where(u => u.IsActive && !u.IsDeleted).ToListAsync();
            var userIds = users.Select(u => u.Id).ToList();
            var salaryConfigMap = (await _db.Set<HrEmployeeSalaryConfig>().AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToListAsync())
                .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.First());
            // Exclude employees whose contract ended before the payroll period starts.
            // Npgsql rejects DateTime with Kind=Unspecified for `timestamp with time zone`
            // parameters, which made payroll generation fail with a 400. Anchor every
            // period boundary to UTC.
            var periodStart = new DateTime(dto.Year, dto.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            users = users.Where(u => !salaryConfigMap.TryGetValue(u.Id, out var c)
                                     || c.ContractEndDate == null
                                     || c.ContractEndDate.Value.Date >= periodStart).ToList();
            userIds = users.Select(u => u.Id).ToList();
            // Load every approved leave of the YEAR (not just the month): paid-leave
            // entitlement is annual, so days already consumed in earlier months
            // determine how much of this month's leave is still paid.
            var yearStart = new DateTime(dto.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearEnd = new DateTime(dto.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
            var leaves = await _db.Set<UserLeave>().AsNoTracking()
                .Where(x => x.Status == "approved" && x.StartDate <= yearEnd && x.EndDate >= yearStart)
                .ToListAsync();
            // Annual paid allowance per user (all paid leave types summed).
            var allowanceByUser = (await _db.Set<HrLeaveBalance>().AsNoTracking()
                .Where(x => x.Year == dto.Year)
                .ToListAsync())
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.AnnualAllowance));

            var bonusesByUser = (await _db.Set<HrBonusCost>().AsNoTracking()
                .Where(x => !x.IsDeleted && x.Year == dto.Year && x.Month == dto.Month && x.AffectsPayroll)
                .ToListAsync())
                .GroupBy(b => b.UserId).ToDictionary(g => g.Key, g => g.ToList());
            // Reuse the hardened attendance reader: it parses every payload column as
            // text, so corrupt legacy rows cannot break payroll generation either.
            var attendanceByUser = (await GetAttendanceAsync(dto.Year, dto.Month))
                .Select(x => new HrAttendance
                {
                    UserId = x.UserId,
                    TotalHours = x.TotalHours,
                    OvertimeHours = x.OvertimeHours,
                    Status = x.Status,
                })
                .GroupBy(a => a.UserId).ToDictionary(g => g.Key, g => g.ToList());


            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            const decimal PayrollDaysPerMonth = 26m; // same divisor used for the hourly rate

            foreach (var user in users)
            {
                salaryConfigMap.TryGetValue(user.Id, out var cfg);
                var baseGross = cfg?.GrossSalary ?? 0m;
                var userAttendance = attendanceByUser.TryGetValue(user.Id, out var att) ? att : new List<HrAttendance>();

                var userBonuses = bonusesByUser.TryGetValue(user.Id, out var bon) ? bon : new List<HrBonusCost>();
                var allowances = userBonuses.Where(b => b.Kind == "allowance").Sum(b => b.Amount);
                var bonusAmount = userBonuses.Where(b => b.Kind == "bonus").Sum(b => b.Amount);
                var subjectExtra = userBonuses.Where(b => b.SubjectToCnss).Sum(b => b.Amount);
                var totalHours = userAttendance.Sum(a => a.TotalHours);
                var overtimeHours = userAttendance.Sum(a => a.OvertimeHours);
                var workedDays = userAttendance.Count(a => a.Status != "absent" && a.Status != "leave");
                var hourlyRate = attendanceSettings.StandardHoursPerDay > 0
                    ? Math.Round(baseGross / PayrollDaysPerMonth / attendanceSettings.StandardHoursPerDay, 6)
                    : 0m;
                var overtimeAmount = Math.Round(overtimeHours * hourlyRate * Math.Max(1m, attendanceSettings.OvertimeMultiplier), 3);

                // ---- Paid vs unpaid absence -------------------------------------
                // Leave is only paid while the employee still has annual entitlement
                // left. Days beyond the allowance (and unjustified absences recorded
                // in attendance) are deducted from the monthly salary.
                var userLeaves = leaves.Where(x => x.UserId == user.Id).ToList();
                decimal DaysInRange(DateTime rangeStart, DateTime rangeEnd) =>
                    userLeaves.Sum(l =>
                    {
                        var s = l.StartDate.Date < rangeStart ? rangeStart : l.StartDate.Date;
                        var e = l.EndDate.Date > rangeEnd ? rangeEnd : l.EndDate.Date;
                        return e < s ? 0m : (decimal)(e - s).TotalDays + 1m;
                    });

                var leaveDays = DaysInRange(periodStart, periodEnd);
                var leaveDaysBefore = periodStart > yearStart
                    ? DaysInRange(yearStart, periodStart.AddDays(-1))
                    : 0m;
                var annualAllowance = allowanceByUser.TryGetValue(user.Id, out var allow) ? allow : 0m;
                var allowanceRemaining = Math.Max(0m, annualAllowance - leaveDaysBefore);
                var paidLeaveDays = Math.Min(leaveDays, allowanceRemaining);
                var unpaidLeaveDays = Math.Max(0m, leaveDays - paidLeaveDays);
                var absentDays = (decimal)userAttendance.Count(a => a.Status == "absent");
                var unpaidDays = unpaidLeaveDays + absentDays;
                var dailyRate = Math.Round(baseGross / PayrollDaysPerMonth, 6);
                var absenceDeduction = Math.Min(baseGross, Math.Round(unpaidDays * dailyRate, 3));
                var payableBase = baseGross - absenceDeduction;

                var grossSubjectToCnss = payableBase + subjectExtra + overtimeAmount;
                var grossTotal = payableBase + allowances + bonusAmount + overtimeAmount;

                var cnssBase = rate.SalaryCeiling > 0 ? Math.Min(grossSubjectToCnss, rate.SalaryCeiling) : grossSubjectToCnss;
                var cnss = Math.Round(cnssBase * rate.EmployeeRate, 3);
                var employerCnss = Math.Round(cnssBase * rate.EmployerRate, 3);


                var taxableGross = grossTotal - cnss;
                var abattement = (cfg?.IsHeadOfFamily ?? false ? rate.AbattementHeadOfFamily : 0m)
                                 + (cfg?.ChildrenCount ?? 0) * rate.AbattementPerChild;
                var taxableBase = Math.Max(0, taxableGross - abattement);
                var irpp = Math.Round(ComputeIrpp(taxableBase, brackets), 3);
                var css = Math.Round(taxableGross * rate.CssRate, 3);

                var net = grossTotal - cnss - irpp - css - (cfg?.CustomDeductions ?? 0m);

                _db.Set<HrPayrollEntry>().Add(new HrPayrollEntry
                {
                    PayrollRunId = run.Id,
                    UserId = user.Id,
                    GrossSalary = grossTotal,
                    Cnss = cnss,
                    TaxableGross = taxableGross,
                    Abattement = abattement,
                    TaxableBase = taxableBase,
                    Irpp = irpp,
                    Css = css,
                    NetSalary = Math.Round(net, 3),
                    WorkedDays = workedDays,
                    TotalHours = totalHours,
                    OvertimeHours = overtimeHours,
                    LeaveDays = leaveDays,
                    Details = JsonSerializer.Serialize(new
                    {
                        formula = "tn_v3",
                        baseGross,
                        payableBase,
                        allowances,
                        bonuses = bonusAmount,
                        overtimeAmount,
                        overtimeHours,
                        hourlyRate,
                        dailyRate,
                        payrollDaysPerMonth = PayrollDaysPerMonth,
                        leaveDays,
                        paidLeaveDays,
                        unpaidLeaveDays,
                        absentDays,
                        unpaidDays,
                        absenceDeduction,
                        annualLeaveAllowance = annualAllowance,
                        leaveAllowanceRemainingBefore = allowanceRemaining,
                        overtimeMultiplier = attendanceSettings.OvertimeMultiplier,
                        employerCnss,
                        cnssBase,
                        rate = new { rate.EmployeeRate, rate.EmployerRate, rate.CssRate },
                        customDeductions = cfg?.CustomDeductions ?? 0m
                    }),
                    CreatedAt = DateTime.UtcNow
                });
            }


            await _db.SaveChangesAsync();
            await LogAsync(0, "payroll_generated", $"Payroll run {dto.Month}/{dto.Year} generated", new { runId = run.Id, dto.Month, dto.Year }, createdByUserId);
            await runTx.CommitAsync();
            return await GetPayrollRunAsync(run.Id);

        }

        public async Task<List<HrPayrollRunDto>> ListPayrollRunsAsync(int year)
        {
            var runs = await _db.Set<HrPayrollRun>()
                .AsNoTracking()
                .Where(x => x.Year == year)
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .ToListAsync();

            var runIds = runs.Select(x => x.Id).ToList();
            var entries = await _db.Set<HrPayrollEntry>().AsNoTracking().Where(x => runIds.Contains(x.PayrollRunId)).ToListAsync();
            var userIds = entries.Select(e => e.UserId).Distinct().ToList();
            var userMap = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            return runs.Select(run =>
            {
                run.TotalGross = entries.Where(e => e.PayrollRunId == run.Id).Sum(e => e.GrossSalary);
                run.TotalNet = entries.Where(e => e.PayrollRunId == run.Id).Sum(e => e.NetSalary);
                return MapPayrollRunDto(run, entries.Where(e => e.PayrollRunId == run.Id).ToList(), userMap);
            }).ToList();
        }

        public async Task<HrPayrollRunDto> GetPayrollRunAsync(int id)
        {
            var run = await _db.Set<HrPayrollRun>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (run == null) throw new KeyNotFoundException("hr.payroll_run_not_found");

            var entries = await _db.Set<HrPayrollEntry>().AsNoTracking().Where(x => x.PayrollRunId == id).ToListAsync();
            var entryUserIds = entries.Select(e => e.UserId).Distinct().ToList();
            var userMap = await _db.Users
                .AsNoTracking()
                .Where(u => entryUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            run.TotalGross = entries.Sum(x => x.GrossSalary);
            run.TotalNet = entries.Sum(x => x.NetSalary);

            return MapPayrollRunDto(run, entries, userMap);
        }

        public async Task<HrPayrollRunDto> ConfirmPayrollRunAsync(int id, int actorUserId)
        {
            var run = await _db.Set<HrPayrollRun>().FirstOrDefaultAsync(x => x.Id == id);
            if (run == null) throw new KeyNotFoundException("hr.payroll_run_not_found");
            if (run.Status == "paid") throw new InvalidOperationException("hr.payroll_run_already_paid");
            run.Status = "confirmed";
            run.ConfirmedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogAsync(0, "payroll_confirmed", $"Payroll run {run.Month}/{run.Year} confirmed", new { runId = id }, actorUserId);
            return await GetPayrollRunAsync(id);
        }

        public async Task<HrPayrollRunDto> MarkPayrollRunPaidAsync(int id, int actorUserId)
        {
            var run = await _db.Set<HrPayrollRun>().FirstOrDefaultAsync(x => x.Id == id);
            if (run == null) throw new KeyNotFoundException("hr.payroll_run_not_found");
            if (run.Status != "confirmed") throw new InvalidOperationException("hr.payroll_run_not_confirmed");
            run.Status = "paid";
            await _db.SaveChangesAsync();
            await LogAsync(0, "payroll_paid", $"Payroll run {run.Month}/{run.Year} marked paid", new { runId = id }, actorUserId);
            return await GetPayrollRunAsync(id);
        }

        public async Task<object> GetPayslipAsync(int entryId)
        {
            var entry = await _db.Set<HrPayrollEntry>().FirstOrDefaultAsync(x => x.Id == entryId);
            if (entry == null) throw new KeyNotFoundException("hr.payroll_entry_not_found");
            var run = await _db.Set<HrPayrollRun>().FirstOrDefaultAsync(x => x.Id == entry.PayrollRunId);
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == entry.UserId);

            return new
            {
                entryId = entry.Id,
                payrollRunId = entry.PayrollRunId,
                month = run?.Month, year = run?.Year,
                userId = entry.UserId,
                userName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : string.Empty,
                breakdown = new
                {
                    entry.GrossSalary, entry.Cnss, entry.TaxableGross, entry.Abattement,
                    entry.TaxableBase, entry.Irpp, entry.Css, entry.NetSalary, entry.LeaveDays,
                    entry.WorkedDays, entry.TotalHours, entry.OvertimeHours
                },
                details = ParseJsonObject(entry.Details)
            };
        }

        // ===========================================================================
        // DEPARTMENTS
        // ===========================================================================
        public async Task<List<HrDepartmentDto>> GetDepartmentsAsync()
        {
            var rows = await _db.Set<HrDepartment>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Position ?? int.MaxValue).ThenBy(x => x.Name)
                .ToListAsync();
            return rows.Select(MapDepartmentDto).ToList();
        }

        public async Task<HrDepartmentDto> CreateDepartmentAsync(UpsertDepartmentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("hr.department_name_required");
            var row = new HrDepartment
            {
                Name = dto.Name.Trim(), Code = dto.Code, ParentId = dto.ParentId,
                ManagerId = dto.ManagerId, Description = dto.Description, Position = dto.Position,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            _db.Set<HrDepartment>().Add(row);
            await _db.SaveChangesAsync();
            return MapDepartmentDto(row);
        }

        public async Task<HrDepartmentDto> UpdateDepartmentAsync(int id, UpsertDepartmentDto dto)
        {
            var row = await _db.Set<HrDepartment>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (row == null) throw new KeyNotFoundException("hr.department_not_found");
            if (!string.IsNullOrWhiteSpace(dto.Name)) row.Name = dto.Name.Trim();
            if (dto.Code != null) row.Code = dto.Code;
            if (dto.ParentId.HasValue) row.ParentId = dto.ParentId;
            if (dto.ManagerId.HasValue) row.ManagerId = dto.ManagerId;
            if (dto.Description != null) row.Description = dto.Description;
            if (dto.Position.HasValue) row.Position = dto.Position;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return MapDepartmentDto(row);
        }

        public async Task DeleteDepartmentAsync(int id)
        {
            var row = await _db.Set<HrDepartment>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (row == null) throw new KeyNotFoundException("hr.department_not_found");
            row.IsDeleted = true;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // ===========================================================================
        // BONUSES & COSTS
        // ===========================================================================
        public async Task<List<HrBonusCostDto>> GetBonusCostsAsync(int? userId, int? year, int? month, string? kind)
        {
            var q = _db.Set<HrBonusCost>().Where(x => !x.IsDeleted);
            if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
            if (year.HasValue) q = q.Where(x => x.Year == year.Value);
            if (month.HasValue) q = q.Where(x => x.Month == month.Value);
            if (!string.IsNullOrWhiteSpace(kind)) q = q.Where(x => x.Kind == kind);

            var rows = await q.AsNoTracking().OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ThenByDescending(x => x.CreatedAt).ToListAsync();
            var ids = rows.Select(r => r.UserId).Distinct().ToList();
            var users = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            return rows.Select(r => new HrBonusCostDto
            {
                Id = r.Id, UserId = r.UserId, UserName = users.ContainsKey(r.UserId) ? users[r.UserId] : null,
                Kind = r.Kind, Category = r.Category, Label = r.Label, Amount = r.Amount,
                Frequency = r.Frequency, Year = r.Year, Month = r.Month,
                AffectsPayroll = r.AffectsPayroll, SubjectToCnss = r.SubjectToCnss,
                Notes = r.Notes, CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task<HrBonusCostDto> CreateBonusCostAsync(UpsertBonusCostDto dto, int actorUserId)
        {
            if (dto.Amount < 0)
                throw new ArgumentException("bonus.negative_amount", nameof(dto.Amount));
            var row = new HrBonusCost
            {
                UserId = dto.UserId, Kind = dto.Kind, Category = dto.Category, Label = dto.Label,
                Amount = dto.Amount, Frequency = dto.Frequency, Year = dto.Year, Month = dto.Month,
                AffectsPayroll = dto.AffectsPayroll, SubjectToCnss = dto.SubjectToCnss,
                Notes = dto.Notes, CreatedBy = actorUserId
            };
            _db.Set<HrBonusCost>().Add(row);
            await _db.SaveChangesAsync();
            await LogAsync(dto.UserId, "bonus_added", $"{dto.Kind}: {dto.Label} ({dto.Amount:0.000})", dto, actorUserId);
            return (await GetBonusCostsAsync(dto.UserId, dto.Year, dto.Month, null)).First(b => b.Id == row.Id);
        }

        public async Task<HrBonusCostDto> UpdateBonusCostAsync(int id, UpsertBonusCostDto dto, int actorUserId)
        {
            if (dto.Amount < 0)
                throw new ArgumentException("bonus.negative_amount", nameof(dto.Amount));
            var row = await _db.Set<HrBonusCost>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (row == null) throw new KeyNotFoundException("hr.bonus_not_found");
            row.UserId = dto.UserId; row.Kind = dto.Kind; row.Category = dto.Category;
            row.Label = dto.Label; row.Amount = dto.Amount; row.Frequency = dto.Frequency;
            row.Year = dto.Year; row.Month = dto.Month; row.AffectsPayroll = dto.AffectsPayroll;
            row.SubjectToCnss = dto.SubjectToCnss; row.Notes = dto.Notes; row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogAsync(dto.UserId, "bonus_updated", $"{dto.Kind}: {dto.Label}", dto, actorUserId);
            return (await GetBonusCostsAsync(dto.UserId, dto.Year, dto.Month, null)).First(b => b.Id == row.Id);
        }

        public async Task DeleteBonusCostAsync(int id, int actorUserId)
        {
            var row = await _db.Set<HrBonusCost>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (row == null) throw new KeyNotFoundException("hr.bonus_not_found");
            row.IsDeleted = true;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogAsync(row.UserId, "bonus_removed", row.Label, new { id }, actorUserId);
        }

        // ===========================================================================
        // CNSS RATES
        // ===========================================================================
        public async Task<List<HrCnssRateDto>> GetCnssRatesAsync()
        {
            var rows = await _db.Set<HrCnssRate>().OrderByDescending(x => x.EffectiveFrom).ToListAsync();
            return rows.Select(MapCnssRateDto).ToList();
        }

        public async Task<HrCnssRateDto> GetActiveCnssRateAsync()
        {
            var row = await GetActiveCnssRateEntityAsync();
            return MapCnssRateDto(row);
        }

        private async Task<HrCnssRate> GetActiveCnssRateEntityAsync()
        {
            var row = await _db.Set<HrCnssRate>()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.EffectiveFrom)
                .FirstOrDefaultAsync();
            if (row == null)
            {
                // Seed defaults (Tunisia 2025)
                row = new HrCnssRate
                {
                    EffectiveFrom = DateTime.UtcNow,
                    EmployeeRate = 0.0918m,
                    EmployerRate = 0.1657m,
                    CssRate = 0.01m,
                    AbattementHeadOfFamily = 150m,
                    AbattementPerChild = 100m,
                    IrppBracketsJson = JsonSerializer.Serialize(new[]
                    {
                        new { from = 0m, to = (decimal?)416.67m, rate = 0m },
                        new { from = 416.67m, to = (decimal?)833.33m, rate = 0.15m },
                        new { from = 833.33m, to = (decimal?)1666.67m, rate = 0.25m },
                        new { from = 1666.67m, to = (decimal?)2500m, rate = 0.30m },
                        new { from = 2500m, to = (decimal?)3333.33m, rate = 0.33m },
                        new { from = 3333.33m, to = (decimal?)4166.67m, rate = 0.36m },
                        new { from = 4166.67m, to = (decimal?)5833.33m, rate = 0.38m },
                        new { from = 5833.33m, to = (decimal?)null, rate = 0.40m }
                    }),
                    IsActive = true
                };
                _db.Set<HrCnssRate>().Add(row);
                await _db.SaveChangesAsync();
            }
            return row;
        }

        public async Task<HrCnssRateDto> UpsertCnssRateAsync(UpsertCnssRateDto dto, int actorUserId)
        {
            // Deactivate any existing active rate
            var existingActives = await _db.Set<HrCnssRate>().Where(x => x.IsActive).ToListAsync();
            foreach (var r in existingActives) r.IsActive = false;

            var row = new HrCnssRate
            {
                // PG column is `timestamp with time zone` — Npgsql rejects Unspecified/Local kinds.
                EffectiveFrom = DateTime.SpecifyKind(dto.EffectiveFrom == default ? DateTime.UtcNow : dto.EffectiveFrom, DateTimeKind.Utc),
                EmployeeRate = dto.EmployeeRate,
                EmployerRate = dto.EmployerRate,
                CssRate = dto.CssRate,
                SalaryCeiling = dto.SalaryCeiling,
                AbattementHeadOfFamily = dto.AbattementHeadOfFamily,
                AbattementPerChild = dto.AbattementPerChild,
                // camelCase so the persisted keys match ParseBrackets' expectations
                IrppBracketsJson = JsonSerializer.Serialize(
                    (dto.IrppBrackets ?? new List<IrppBracketDto>())
                        .Select(b => new { from = b.From, to = b.To, rate = b.Rate })),
                IsActive = dto.IsActive,
                Notes = dto.Notes
            };
            _db.Set<HrCnssRate>().Add(row);
            await _db.SaveChangesAsync();
            await LogAsync(0, "cnss_rate_changed", "CNSS rates updated", dto, actorUserId);
            return MapCnssRateDto(row);
        }

        // ===========================================================================
        // PUBLIC HOLIDAYS
        // ===========================================================================
        public async Task<List<HrPublicHolidayDto>> GetPublicHolidaysAsync(int? year)
        {
            var q = _db.Set<HrPublicHoliday>().AsQueryable();
            if (year.HasValue) q = q.Where(x => x.Date.Year == year.Value || x.IsRecurring);
            var rows = await q.OrderBy(x => x.Date).ToListAsync();
            return rows.Select(MapHolidayDto).ToList();
        }

        public async Task<HrPublicHolidayDto> CreatePublicHolidayAsync(UpsertPublicHolidayDto dto)
        {
            var row = new HrPublicHoliday { Date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc), Name = dto.Name, Category = dto.Category, IsRecurring = dto.IsRecurring };
            _db.Set<HrPublicHoliday>().Add(row);
            await _db.SaveChangesAsync();
            return MapHolidayDto(row);
        }

        public async Task<HrPublicHolidayDto> UpdatePublicHolidayAsync(int id, UpsertPublicHolidayDto dto)
        {
            var row = await _db.Set<HrPublicHoliday>().FirstOrDefaultAsync(x => x.Id == id);
            if (row == null) throw new KeyNotFoundException("hr.holiday_not_found");
            row.Date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc); row.Name = dto.Name; row.Category = dto.Category; row.IsRecurring = dto.IsRecurring;
            await _db.SaveChangesAsync();
            return MapHolidayDto(row);
        }

        public async Task DeletePublicHolidayAsync(int id)
        {
            var row = await _db.Set<HrPublicHoliday>().FirstOrDefaultAsync(x => x.Id == id);
            if (row == null) throw new KeyNotFoundException("hr.holiday_not_found");
            _db.Set<HrPublicHoliday>().Remove(row);
            await _db.SaveChangesAsync();
        }

        // ===========================================================================
        // EMPLOYEE DOCUMENTS
        // ===========================================================================
        public async Task<List<HrEmployeeDocumentDto>> GetEmployeeDocumentsAsync(int userId)
        {
            return await _db.Set<HrEmployeeDocument>()
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new HrEmployeeDocumentDto
                {
                    Id = x.Id, UserId = x.UserId, DocType = x.DocType, Title = x.Title,
                    FileUrl = x.FileUrl, FileName = x.FileName, MimeType = x.MimeType,
                    FileSize = x.FileSize, IssuedDate = x.IssuedDate, ExpiresAt = x.ExpiresAt,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<HrEmployeeDocumentDto> CreateEmployeeDocumentAsync(UpsertEmployeeDocumentDto dto, int actorUserId)
        {
            var row = new HrEmployeeDocument
            {
                UserId = dto.UserId, DocType = dto.DocType, Title = dto.Title, FileUrl = dto.FileUrl,
                FileName = dto.FileName, MimeType = dto.MimeType, FileSize = dto.FileSize,
                IssuedDate = dto.IssuedDate, ExpiresAt = dto.ExpiresAt, UploadedBy = actorUserId
            };
            _db.Set<HrEmployeeDocument>().Add(row);
            await _db.SaveChangesAsync();
            await LogAsync(dto.UserId, "document_uploaded", $"{dto.DocType}: {dto.Title}", new { dto.DocType, dto.Title }, actorUserId);
            return new HrEmployeeDocumentDto
            {
                Id = row.Id, UserId = row.UserId, DocType = row.DocType, Title = row.Title,
                FileUrl = row.FileUrl, FileName = row.FileName, MimeType = row.MimeType,
                FileSize = row.FileSize, IssuedDate = row.IssuedDate, ExpiresAt = row.ExpiresAt,
                CreatedAt = row.CreatedAt
            };
        }

        public async Task DeleteEmployeeDocumentAsync(int id)
        {
            var row = await _db.Set<HrEmployeeDocument>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (row == null) throw new KeyNotFoundException("hr.document_not_found");
            row.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        // ===========================================================================
        // AUDIT LOG
        // ===========================================================================
        public async Task<List<HrAuditLogDto>> GetAuditLogAsync(int? userId, int take = 100)
        {
            var q = _db.Set<HrAuditLog>().AsQueryable();
            if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
            var rows = await q.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take, 1, 500)).ToListAsync();
            return rows.Select(x => new HrAuditLogDto
            {
                Id = x.Id, UserId = x.UserId, EventType = x.EventType, Description = x.Description,
                Payload = ParseJsonObject(x.Payload), ActorUserId = x.ActorUserId, ActorName = x.ActorName,
                CreatedAt = x.CreatedAt
            }).ToList();
        }

        private async Task LogAsync(int userId, string eventType, string? description, object? payload, int actorUserId)
        {
            var actor = actorUserId > 0 ? await _db.Users.FirstOrDefaultAsync(u => u.Id == actorUserId) : null;
            _db.Set<HrAuditLog>().Add(new HrAuditLog
            {
                UserId = userId,
                EventType = eventType,
                Description = description,
                Payload = payload != null ? JsonSerializer.Serialize(payload) : null,
                ActorUserId = actorUserId > 0 ? actorUserId : (int?)null,
                ActorName = actor != null ? $"{actor.FirstName} {actor.LastName}".Trim() : null,
                CreatedAt = DateTime.UtcNow
            });
            // Persist here: almost every caller logs AFTER its own SaveChangesAsync (so the
            // generated row id is available), which meant the queued audit entry was never
            // written. Saving inside LogAsync makes the trail reliable for every HR action.
            // A failed audit write must never fail the business operation it describes.
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                foreach (var entry in _db.ChangeTracker.Entries<HrAuditLog>().ToList())
                {
                    if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
                }
            }
        }

        // ===========================================================================
        // REPORTS
        // ===========================================================================
        public async Task<List<HrEmployeeCostDto>> GetEmployeeCostReportAsync(int year, int? month)
        {
            var users = await _db.Users.Where(u => u.IsActive && !u.IsDeleted).ToListAsync();
            var userIds = users.Select(u => u.Id).ToList();
            var configs = await _db.Set<HrEmployeeSalaryConfig>().Where(x => userIds.Contains(x.UserId)).ToListAsync();
            // Exclude employees whose contract ended before the reported period.
            var reportStart = new DateTime(year, month ?? 1, 1);
            users = users.Where(u =>
            {
                var c = configs.FirstOrDefault(x => x.UserId == u.Id);
                return c?.ContractEndDate == null || c.ContractEndDate.Value.Date >= reportStart;
            }).ToList();
            var rate = await GetActiveCnssRateEntityAsync();

            var bonusQ = _db.Set<HrBonusCost>().Where(x => !x.IsDeleted && x.Year == year);
            if (month.HasValue) bonusQ = bonusQ.Where(x => x.Month == month.Value);
            var bonuses = await bonusQ.ToListAsync();

            var monthsCount = month.HasValue ? 1 : 12;
            // YTD: from January up to (and including) selected month, or whole year if month is null
            var ytdMonthsCount = month.HasValue ? month.Value : 12;
            var ytdBonusQ = _db.Set<HrBonusCost>().Where(x => !x.IsDeleted && x.Year == year && (!month.HasValue || x.Month <= month.Value));
            var ytdBonuses = await ytdBonusQ.ToListAsync();
            return users.Select(u =>
            {
                var cfg = configs.FirstOrDefault(c => c.UserId == u.Id);
                var baseGross = (cfg?.GrossSalary ?? 0m) * monthsCount;
                var b = bonuses.Where(x => x.UserId == u.Id).ToList();
                var bonusAmt = b.Where(x => x.Kind == "bonus").Sum(x => x.Amount);
                var allowanceAmt = b.Where(x => x.Kind == "allowance").Sum(x => x.Amount);
                var subjectExtra = b.Where(x => x.SubjectToCnss).Sum(x => x.Amount);
                var cnssBase = baseGross + subjectExtra;
                // Ceiling cap must match RunPayrollAsync / GetCnssDeclarationAsync,
                // otherwise the cost report drifts from the payroll figures.
                var cappedBase = rate.SalaryCeiling > 0 ? Math.Min(cnssBase, rate.SalaryCeiling * monthsCount) : cnssBase;
                var employerCnss = Math.Round(cappedBase * rate.EmployerRate, 3);

                // YTD aggregates
                var ytdGross = (cfg?.GrossSalary ?? 0m) * ytdMonthsCount;
                var ytdB = ytdBonuses.Where(x => x.UserId == u.Id).ToList();
                var ytdBonusAmt = ytdB.Where(x => x.Kind == "bonus" || x.Kind == "allowance").Sum(x => x.Amount);
                var ytdSubjectExtra = ytdB.Where(x => x.SubjectToCnss).Sum(x => x.Amount);
                var ytdCnssBase = ytdGross + ytdSubjectExtra;
                var ytdCapped = rate.SalaryCeiling > 0 ? Math.Min(ytdCnssBase, rate.SalaryCeiling * ytdMonthsCount) : ytdCnssBase;
                var ytdEmployerCnss = Math.Round(ytdCapped * rate.EmployerRate, 3);

                return new HrEmployeeCostDto
                {
                    UserId = u.Id,
                    UserName = $"{u.FirstName} {u.LastName}".Trim(),
                    Department = cfg?.Department,
                    Gross = baseGross,
                    Bonuses = bonusAmt,
                    Allowances = allowanceAmt,
                    EmployerCnss = employerCnss,
                    TotalCost = baseGross + bonusAmt + allowanceAmt + employerCnss,
                    YtdGross = ytdGross,
                    YtdBonuses = ytdBonusAmt,
                    YtdEmployerCnss = ytdEmployerCnss,
                    YtdTotalCost = ytdGross + ytdBonusAmt + ytdEmployerCnss
                };
            }).ToList();
        }

        public async Task<HrCnssMonthlyDeclarationDto> GetCnssDeclarationAsync(int year, int month)
        {
            var rate = await GetActiveCnssRateEntityAsync();
            var users = await _db.Users.Where(u => u.IsActive && !u.IsDeleted).ToListAsync();
            var userIds = users.Select(u => u.Id).ToList();
            var configs = await _db.Set<HrEmployeeSalaryConfig>().Where(x => userIds.Contains(x.UserId)).ToListAsync();
            // Pull ALL bonuses for the period (not only CNSS-subject ones) — the
            // payroll engine includes non-CNSS allowances/bonuses in the CSS taxable
            // gross, so the declaration must do the same to stay reconciled.
            var allBonuses = await _db.Set<HrBonusCost>()
                .Where(x => !x.IsDeleted && x.Year == year && x.Month == month)
                .ToListAsync();

            var lines = new List<HrCnssEmployeeLineDto>();
            decimal totalSubject = 0, totalEmpCnss = 0, totalErCnss = 0, totalCss = 0;

            foreach (var u in users)
            {
                var cfg = configs.FirstOrDefault(c => c.UserId == u.Id);
                if (cfg == null) continue;
                var userBonuses = allBonuses.Where(b => b.UserId == u.Id).ToList();
                var subjectExtra = userBonuses.Where(b => b.SubjectToCnss).Sum(b => b.Amount);
                var nonSubjectExtra = userBonuses.Where(b => !b.SubjectToCnss).Sum(b => b.Amount);

                // CNSS base: only CNSS-subject earnings, capped by ceiling if any.
                var subject = cfg.GrossSalary + subjectExtra;
                var capped = rate.SalaryCeiling > 0 ? Math.Min(subject, rate.SalaryCeiling) : subject;
                var emp = Math.Round(capped * rate.EmployeeRate, 3);
                var er = Math.Round(capped * rate.EmployerRate, 3);

                // CSS base: same as payroll engine — full gross (incl. non-CNSS items)
                // minus the employee CNSS contribution. Keeps declaration reconciled
                // with HrPayrollEntry.Css produced by RunPayrollAsync.
                var grossTotal = cfg.GrossSalary + subjectExtra + nonSubjectExtra;
                var taxableGross = grossTotal - emp;
                var css = Math.Round(taxableGross * rate.CssRate, 3);

                lines.Add(new HrCnssEmployeeLineDto
                {
                    UserId = u.Id,
                    UserName = $"{u.FirstName} {u.LastName}".Trim(),
                    CnssNumber = cfg.CnssNumber,
                    SalarySubject = subject,
                    EmployeeCnss = emp,
                    EmployerCnss = er,
                    Css = css
                });
                totalSubject += subject; totalEmpCnss += emp; totalErCnss += er; totalCss += css;
            }

            return new HrCnssMonthlyDeclarationDto
            {
                Year = year, Month = month,
                TotalSalarySubject = totalSubject,
                TotalEmployeeCnss = totalEmpCnss,
                TotalEmployerCnss = totalErCnss,
                TotalCss = totalCss,
                Lines = lines
            };
        }

        // ===========================================================================
        // MAPPERS / HELPERS
        // ===========================================================================
        private static List<(decimal from, decimal? to, decimal rate)> ParseBrackets(string? json)
        {
            var defaults = new List<(decimal, decimal?, decimal)>
            {
                (0m, 416.67m, 0m), (416.67m, 833.33m, 0.15m), (833.33m, 1666.67m, 0.25m),
                (1666.67m, 2500m, 0.30m), (2500m, 3333.33m, 0.33m), (3333.33m, 4166.67m, 0.36m),
                (4166.67m, 5833.33m, 0.38m), (5833.33m, null, 0.40m)
            };
            if (string.IsNullOrWhiteSpace(json)) return defaults;
            try
            {
                var doc = JsonDocument.Parse(json);
                var list = new List<(decimal, decimal?, decimal)>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    decimal from = TryDecimal(el, "from") ?? 0m;
                    decimal rate = TryDecimal(el, "rate") ?? 0m;
                    decimal? to = TryDecimal(el, "to");
                    list.Add((from, to, rate));
                }
                // A bracket set where every rate is 0 means the JSON keys didn't match
                // (legacy PascalCase rows) or the set is meaningless — fall back.
                if (list.Count == 0 || list.All(b => b.Item3 == 0m)) return defaults;
                return list;
            }
            catch { return defaults; }
        }

        /// <summary>Case-insensitive numeric property read (handles legacy PascalCase JSON).</summary>
        private static decimal? TryDecimal(JsonElement el, string name)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Null) return null;
                if (prop.Value.ValueKind == JsonValueKind.Number) return prop.Value.GetDecimal();
                if (prop.Value.ValueKind == JsonValueKind.String
                    && decimal.TryParse(prop.Value.GetString(), out var parsed)) return parsed;
                return null;
            }
            return null;
        }

        private static decimal ComputeIrpp(decimal taxableBase, List<(decimal from, decimal? to, decimal rate)> brackets)
        {
            decimal irpp = 0;
            foreach (var (from, to, rate) in brackets)
            {
                if (taxableBase <= from) break;
                var ceiling = to ?? decimal.MaxValue;
                var portion = Math.Min(taxableBase, ceiling) - from;
                if (portion > 0) irpp += portion * rate;
            }
            return Math.Max(0, irpp);
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message ?? string.Empty;
            return msg.Contains("23505") || msg.Contains("duplicate key value violates unique constraint");
        }

        private static HrCnssRateDto MapCnssRateDto(HrCnssRate x) => new()
        {
            Id = x.Id, EffectiveFrom = x.EffectiveFrom,
            EmployeeRate = x.EmployeeRate, EmployerRate = x.EmployerRate, CssRate = x.CssRate,
            SalaryCeiling = x.SalaryCeiling,
            AbattementHeadOfFamily = x.AbattementHeadOfFamily,
            AbattementPerChild = x.AbattementPerChild,
            IrppBrackets = ParseBrackets(x.IrppBracketsJson)
                .Select(b => new IrppBracketDto { From = b.from, To = b.to, Rate = b.rate }).ToList(),
            IsActive = x.IsActive, Notes = x.Notes
        };

        private static HrPublicHolidayDto MapHolidayDto(HrPublicHoliday x) => new()
        {
            Id = x.Id, Date = x.Date, Name = x.Name, Category = x.Category, IsRecurring = x.IsRecurring
        };

        private static HrEmployeeSalaryConfigDto MapSalaryConfigDto(HrEmployeeSalaryConfig x) => new()
        {
            Id = x.Id, UserId = x.UserId, GrossSalary = x.GrossSalary,
            IsHeadOfFamily = x.IsHeadOfFamily, ChildrenCount = x.ChildrenCount,
            CustomDeductions = x.CustomDeductions, BankAccount = x.BankAccount,
            CnssNumber = x.CnssNumber, HireDate = x.HireDate, Department = x.Department,
            Position = x.Position, EmploymentType = x.EmploymentType, Cin = x.Cin,
            ContractType = x.ContractType, ContractEndDate = x.ContractEndDate,
            BirthDate = x.BirthDate, MaritalStatus = x.MaritalStatus,
            AddressLine1 = x.AddressLine1, AddressLine2 = x.AddressLine2,
            City = x.City, PostalCode = x.PostalCode,
            EmergencyContactName = x.EmergencyContactName,
            EmergencyContactPhone = x.EmergencyContactPhone
        };

        private static HrAttendanceDto MapAttendanceDto(HrAttendance x, Dictionary<int, string> userMap) => new()
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = userMap.TryGetValue(x.UserId, out var name) ? name : $"#{x.UserId}",
            // Force Unspecified kind so JSON serialization stays as a wall-clock
            // value (no trailing "Z"), preventing client-side TZ rollover.
            Date = DateTime.SpecifyKind(x.Date.Date, DateTimeKind.Unspecified),
            CheckIn = x.CheckIn.HasValue ? DateTime.SpecifyKind(x.CheckIn.Value, DateTimeKind.Unspecified) : (DateTime?)null,
            CheckOut = x.CheckOut.HasValue ? DateTime.SpecifyKind(x.CheckOut.Value, DateTimeKind.Unspecified) : (DateTime?)null,
            BreakMinutes = x.BreakMinutes,
            TotalHours = x.TotalHours,
            OvertimeHours = x.OvertimeHours,
            Status = string.IsNullOrWhiteSpace(x.Status) ? "present" : x.Status!,
            Notes = x.Notes,
            Source = string.IsNullOrWhiteSpace(x.Source) ? "manual" : x.Source!,
        };

        private static HrAttendanceSettingsDto MapAttendanceSettingsDto(HrAttendanceSettings x) => new()
        {
            Id = x.Id,
            WorkDays = ParseWorkDays(x.WorkDaysJson),
            StandardHoursPerDay = x.StandardHoursPerDay,
            OvertimeThresholdHours = x.OvertimeThresholdHours,
            OvertimeMultiplier = x.OvertimeMultiplier,
            LateThresholdMinutes = x.LateThresholdMinutes,
            RoundingMethod = x.RoundingMethod,
            CalculationMethod = x.CalculationMethod,
        };

        private static HrPayrollRunDto MapPayrollRunDto(HrPayrollRun run, List<HrPayrollEntry> entries, Dictionary<int, string> userMap)
        {
            decimal totalCnss = 0, totalErCnss = 0;
            var entryDtos = entries.Select(e =>
            {
                var details = ParseJsonObject(e.Details) as JsonElement?;
                decimal allowances = 0, bonuses = 0, employerCnss = 0;
                try
                {
                    if (details.HasValue)
                    {
                        if (details.Value.TryGetProperty("allowances", out var a)) allowances = a.GetDecimal();
                        if (details.Value.TryGetProperty("bonuses", out var b)) bonuses = b.GetDecimal();
                        if (details.Value.TryGetProperty("employerCnss", out var er)) employerCnss = er.GetDecimal();
                    }
                }
                catch { }
                totalCnss += e.Cnss; totalErCnss += employerCnss;
                return new HrPayrollEntryDto
                {
                    Id = e.Id, PayrollRunId = e.PayrollRunId, UserId = e.UserId,
                    UserName = userMap.ContainsKey(e.UserId) ? userMap[e.UserId] : string.Empty,
                    GrossSalary = e.GrossSalary, Allowances = allowances, Bonuses = bonuses,
                    Cnss = e.Cnss, EmployerCnss = employerCnss,
                    TaxableGross = e.TaxableGross, Abattement = e.Abattement, TaxableBase = e.TaxableBase,
                    Irpp = e.Irpp, Css = e.Css, NetSalary = e.NetSalary,
                    WorkedDays = e.WorkedDays, TotalHours = e.TotalHours, OvertimeHours = e.OvertimeHours,
                    LeaveDays = e.LeaveDays,
                    Details = details
                };
            }).ToList();

            return new HrPayrollRunDto
            {
                Id = run.Id, Month = run.Month, Year = run.Year, Status = run.Status,
                TotalGross = run.TotalGross, TotalNet = run.TotalNet,
                TotalCnss = totalCnss, TotalEmployerCnss = totalErCnss,
                CreatedBy = run.CreatedBy, CreatedAt = run.CreatedAt, ConfirmedAt = run.ConfirmedAt,
                Entries = entryDtos
            };
        }

        private static HrDepartmentDto MapDepartmentDto(HrDepartment x) => new()
        {
            Id = x.Id, Name = x.Name, Code = x.Code, ParentId = x.ParentId,
            ManagerId = x.ManagerId, Description = x.Description, Position = x.Position
        };

        private static object? ParseJsonObject(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonDocument.Parse(json).RootElement.Clone(); } catch { return null; }
        }

        private async Task<HrAttendanceSettings> GetAttendanceSettingsEntityAsync()
        {
            var row = await _db.Set<HrAttendanceSettings>().OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (row != null) return row;

            row = new HrAttendanceSettings();
            _db.Set<HrAttendanceSettings>().Add(row);
            await _db.SaveChangesAsync();
            return row;
        }

        private static List<int> ParseWorkDays(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<int> { 1, 2, 3, 4, 5 };
            try
            {
                var parsed = JsonSerializer.Deserialize<List<int>>(json);
                return parsed?.Distinct().OrderBy(x => x).ToList() ?? new List<int> { 1, 2, 3, 4, 5 };
            }
            catch
            {
                return new List<int> { 1, 2, 3, 4, 5 };
            }
        }

        private static (decimal totalHours, decimal overtimeHours) ComputeAttendanceHours(DateTime? checkIn, DateTime? checkOut, int breakMinutes, HrAttendanceSettings settings)
        {
            if (!checkIn.HasValue || !checkOut.HasValue || checkOut <= checkIn) return (0m, 0m);
            var total = (decimal)(checkOut.Value - checkIn.Value).TotalHours - (breakMinutes / 60m);
            total = Math.Max(0m, total);
            var threshold = settings.OvertimeThresholdHours > 0 ? settings.OvertimeThresholdHours : settings.StandardHoursPerDay;
            var overtime = Math.Max(0m, total - threshold);
            return (Math.Round(total, 2), Math.Round(overtime, 2));
        }

        // ---------------------------------------------------------------
        // Timezone-safe helpers
        // ---------------------------------------------------------------
        // Treat incoming check-in/out as wall-clock on the row's calendar day.
        // If the value arrived as UTC ("...Z") it is converted to local first,
        // then re-anchored to the target date. Returned value is Unspecified
        // kind so the JSON serializer never appends a "Z" suffix that could
        // shift the day on the client.
        private static DateTime? NormalizeWallClock(DateTime? value, DateTime targetDate)
        {
            if (!value.HasValue) return null;
            var v = value.Value.Kind == DateTimeKind.Utc ? value.Value.ToLocalTime() : value.Value;
            // Anchor wall-clock H:M:S onto target calendar day. Stamp as UTC so Npgsql can write
            // it to `timestamp with time zone` columns without throwing.
            return DateTime.SpecifyKind(
                new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, v.Hour, v.Minute, v.Second),
                DateTimeKind.Utc);
        }

        private static object MapSafeUser(MyApi.Modules.Users.Models.User user) => new
        {
            user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber,
            user.Role, user.IsActive, user.ProfilePictureUrl, user.CurrentStatus,
            user.CreatedDate, user.ModifiedDate
        };

        // ===========================================================================
        // ACTIVE LEAVES + CONTRACT EXPIRY (Round 1 — Planning integration & alerts)
        // ===========================================================================
        public async Task<List<HrActiveLeaveDto>> GetActiveLeavesAsync(DateTime date)
        {
            var d = date.Date;
            var leaves = await _db.Set<UserLeave>()
                .Where(l => l.Status == "approved" && l.StartDate.Date <= d && l.EndDate.Date >= d)
                .ToListAsync();
            if (leaves.Count == 0) return new List<HrActiveLeaveDto>();
            var userIds = leaves.Select(l => l.UserId).Distinct().ToList();
            var users = await _db.Users.Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync();
            var nameMap = users.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim())
                        ? (u.Email ?? $"#{u.Id}")
                        : ($"{u.FirstName} {u.LastName}").Trim());
            return leaves.Select(l => new HrActiveLeaveDto
            {
                UserId = l.UserId,
                UserName = nameMap.TryGetValue(l.UserId, out var n) ? n : $"#{l.UserId}",
                LeaveType = l.LeaveType,
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                Status = l.Status,
            }).ToList();
        }

        public async Task<List<HrContractExpiryDto>> GetExpiringContractsAsync(int withinDays = 60)
        {
            var today = DateTime.UtcNow.Date;
            var limit = today.AddDays(Math.Max(1, withinDays));
            var configs = await _db.Set<HrEmployeeSalaryConfig>()
                .Where(c => c.ContractEndDate != null && c.ContractEndDate >= today && c.ContractEndDate <= limit)
                .ToListAsync();
            if (configs.Count == 0) return new List<HrContractExpiryDto>();
            var userIds = configs.Select(c => c.UserId).ToList();
            var users = await _db.Users.Where(u => userIds.Contains(u.Id) && !u.IsDeleted)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync();
            var nameMap = users.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim())
                        ? (u.Email ?? $"#{u.Id}")
                        : ($"{u.FirstName} {u.LastName}").Trim());
            return configs
                .Where(c => nameMap.ContainsKey(c.UserId))
                .OrderBy(c => c.ContractEndDate)
                .Select(c => new HrContractExpiryDto
                {
                    UserId = c.UserId,
                    UserName = nameMap[c.UserId],
                    ContractType = c.ContractType,
                    ContractEndDate = c.ContractEndDate!.Value,
                    DaysUntilExpiry = (int)(c.ContractEndDate!.Value.Date - today).TotalDays,
                }).ToList();
        }
    }
}
