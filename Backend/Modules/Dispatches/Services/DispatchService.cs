using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Dispatches.DTOs;
using MyApi.Modules.Dispatches.Models;
using MyApi.Modules.Dispatches.Mapping;
using MyApi.Modules.WorkflowEngine.Services;
using MyApi.Modules.Articles.Services;
using MyApi.Modules.Articles.DTOs;
using MyApi.Modules.ServiceOrders.Services;

namespace MyApi.Modules.Dispatches.Services
{
    public class DispatchService : IDispatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DispatchService> _logger;
        private readonly IWorkflowTriggerService? _workflowTriggerService;
        private readonly IBusinessWorkflowService? _businessWorkflowService;
        private readonly MyApi.Modules.Numbering.Services.INumberingService? _numberingService;
        private readonly MyApi.Modules.Planning.Services.IPlannedLineEntryService? _plannedEntries;
        private readonly IStockTransactionService? _stockTransactionService;
        private readonly MyApi.Modules.Shared.Services.IActivityLogger? _activityLogger;
        private readonly MyApi.Modules.Contacts.Services.IContactActivityService? _contactActivity;
        private readonly MyApi.Modules.Shared.Services.IUploadThingService? _uploadThing;

        public DispatchService(
            ApplicationDbContext db,
            ILogger<DispatchService> logger,
            IWorkflowTriggerService? workflowTriggerService = null,
            MyApi.Modules.Numbering.Services.INumberingService? numberingService = null,
            MyApi.Modules.Planning.Services.IPlannedLineEntryService? plannedEntries = null,
            IStockTransactionService? stockTransactionService = null,
            MyApi.Modules.Shared.Services.IActivityLogger? activityLogger = null,
            MyApi.Modules.Contacts.Services.IContactActivityService? contactActivity = null,
            MyApi.Modules.Shared.Services.IUploadThingService? uploadThing = null,
            IBusinessWorkflowService? businessWorkflowService = null)
        {
            _db = db;
            _logger = logger;
            _workflowTriggerService = workflowTriggerService;
            _numberingService = numberingService;
            _plannedEntries = plannedEntries;
            _stockTransactionService = stockTransactionService;
            _activityLogger = activityLogger;
            _contactActivity = contactActivity;
            _uploadThing = uploadThing;
            _businessWorkflowService = businessWorkflowService;
        }

        /// <summary>
        /// Domain-level dispatch → service-order cascade.
        ///
        /// This runs unconditionally on every dispatch status change, independently
        /// of the Workflow Engine. The workflow engine remains available for
        /// *custom, tenant-configured* automation (notifications, approvals, extra
        /// actions), but the core business propagation (dispatch in progress /
        /// completed / rejected → service order state) is guaranteed here so that
        /// disabling or misconfiguring a workflow can never break it.
        ///
        /// Safe to run twice: the underlying handlers are no-ops when the service
        /// order is already in the target state, so a workflow graph that also
        /// invokes them cannot double-apply.
        /// </summary>
        private async Task ApplyDispatchCascadeAsync(Dispatch d, string? oldStatus, string newStatus, string userId)
        {
            if (d.ServiceOrderId == null) return;
            var status = (newStatus ?? string.Empty).ToLowerInvariant();

            try
            {
                if (status == "in_progress")
                {
                    if (_businessWorkflowService != null)
                        await _businessWorkflowService.HandleDispatchInProgressAsync(d.Id, userId);
                }
                else if (status == "completed" || status == "technically_completed")
                {
                    // Close out the linked jobs first so the service order evaluation and the
                    // UI both see finished jobs instead of jobs stuck on "dispatched".
                    await MarkJobsCompletedAfterDispatchCompletionAsync(d);

                    if (_businessWorkflowService != null)
                        await _businessWorkflowService.HandleDispatchTechnicallyCompletedAsync(d.Id, userId);
                }

                else if (status == "rejected")
                {
                    await HandleDispatchRejectedCascadeAsync(d, userId);
                }
            }
            catch (Exception ex)
            {
                // Never fail the dispatch status change because of a cascade error:
                // the dispatch state itself is already committed and authoritative.
                _logger.LogError(ex,
                    "[DISPATCH-CASCADE] Failed to cascade dispatch {DispatchId} status '{OldStatus}' -> '{NewStatus}' onto service order {ServiceOrderId}",
                    d.Id, oldStatus, newStatus, d.ServiceOrderId);
            }
        }

        /// <summary>
        /// A rejected dispatch sends its service order back to planning so the
        /// dispatcher can re-assign it — unless another dispatch is still active
        /// or the order has already moved past planning.
        /// </summary>
        private async Task HandleDispatchRejectedCascadeAsync(Dispatch d, string userId)
        {
            var serviceOrderId = d.ServiceOrderId!.Value;
            var so = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (so == null) return;

            var current = (so.Status ?? string.Empty).ToLowerInvariant();
            // Only pull back orders that are still in the scheduling phase.
            var reschedulable = new[] { "pending", "planned", "scheduled", "in_progress" };
            if (!reschedulable.Contains(current)) return;

            // If any sibling dispatch is still live, the order stays where it is.
            var siblingActive = await _db.Dispatches.AnyAsync(x =>
                x.ServiceOrderId == serviceOrderId
                && x.Id != d.Id
                && !x.IsDeleted
                && x.Status != "rejected"
                && x.Status != "cancelled");
            if (siblingActive) return;

            so.Status = "ready_for_planning";
            so.ModifiedBy = userId;
            so.ModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[DISPATCH-CASCADE] Dispatch {DispatchId} rejected → service order {ServiceOrderId} moved '{Old}' -> 'ready_for_planning'",
                d.Id, serviceOrderId, current);
        }

        /// <summary>
        /// Returns to inventory every quantity this dispatch took out through its
        /// material lines. Called when a dispatch is cancelled (or soft-deleted):
        /// the work will not happen, so the goods are back on the shelf.
        ///
        /// Idempotent: the restore is written as a <c>return</c> transaction keyed on
        /// <c>(article, dispatch_material, materialId)</c>, which the stock ledger's
        /// partial unique index rejects on a second attempt — so re-cancelling a
        /// dispatch cannot inflate stock.
        /// </summary>
        private async Task RestoreDispatchMaterialStockAsync(Dispatch d, string userId)
        {
            if (_stockTransactionService == null) return;

            try
            {
                var materials = await _db.DispatchMaterials
                    .Where(m => m.DispatchId == d.Id && m.ArticleId != null && m.Quantity > 0)
                    .ToListAsync();
                if (materials.Count == 0) return;

                foreach (var m in materials)
                {
                    // A rejected line already handed its goods back — restoring again
                    // on cancellation would inflate stock.
                    if (string.Equals(m.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase)) continue;

                    // Reject/re-approve round trips move the ledger reference to a
                    // suffixed key; restore against the reference of the current cycle.
                    var cycle = await GetMaterialStockCycleAsync(m.Id);
                    var deductRef = MaterialStockRef(m.Id, "deduct", cycle);
                    var returnRef = MaterialStockRef(m.Id, "return", cycle);

                    // Only restore what was actually deducted by this dispatch. If the
                    // parent sale covered the goods, no dispatch-level deduction was
                    // written and there is nothing to give back here.
                    var wasDeducted = await _db.StockTransactions.AnyAsync(t =>
                        t.ArticleId == m.ArticleId!.Value
                        && t.ReferenceType == "dispatch_material"
                        && t.ReferenceId == deductRef
                        && t.TransactionType == "remove");
                    if (!wasDeducted) continue;

                    try
                    {
                        await _stockTransactionService.CreateTransactionAsync(new CreateStockTransactionDto
                        {
                            ArticleId = m.ArticleId!.Value,
                            TransactionType = "return",
                            Quantity = m.Quantity,
                            Reason = "Dispatch cancelled - material returned to stock",
                            ReferenceType = "dispatch_material",
                            ReferenceId = returnRef,
                            ReferenceNumber = d.DispatchNumber,
                            Notes = m.Description,
                        }, userId);


                        _logger.LogInformation(
                            "[DISPATCH-CANCEL] Restored {Qty} of article {ArticleId} from material {MaterialId} (dispatch {DispatchId})",
                            m.Quantity, m.ArticleId, m.Id, d.Id);
                    }
                    catch (Exception ex)
                    {
                        // One bad line must not block the remaining restores.
                        _logger.LogError(ex,
                            "[DISPATCH-CANCEL] Failed to restore stock for material {MaterialId} on dispatch {DispatchId}",
                            m.Id, d.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DISPATCH-CANCEL] Stock restoration failed for dispatch {DispatchId}", d.Id);
            }
        }


        // Reject obviously invalid schedule windows before any DB work.
        // Runs on every create path so non-UI callers (mobile app, scripts) cannot
        // persist zero/negative-duration or past-dated dispatches.
        private void ValidateScheduleWindow(DateTime scheduledDate, TimeSpan? start, TimeSpan? end)
        {
            if (start.HasValue && end.HasValue && end.Value <= start.Value)
                throw new ArgumentException("ScheduledEndTime must be strictly after ScheduledStartTime.");
            if (start.HasValue && end.HasValue && (end.Value - start.Value) < TimeSpan.FromMinutes(1))
                throw new ArgumentException("Dispatch duration must be at least one minute.");
            // Allow same-day past times within a small grace window, but reject
            // dispatches scheduled clearly in the past (> 1 day before today UTC).
            var todayUtc = DateTime.UtcNow.Date;
            if (scheduledDate.Date < todayUtc.AddDays(-1))
                throw new ArgumentException("ScheduledDate cannot be more than one day in the past.");
        }

        // Helper to build a map of technicianId -> display name for a dispatch
        private async Task<System.Collections.Generic.Dictionary<int, string>> GetTechnicianNameMapForDispatchAsync(int dispatchId)
        {
            var techIds = await _db.Set<DispatchTechnician>()
                .Where(dt => dt.DispatchId == dispatchId)
                .Select(dt => dt.TechnicianId)
                .Distinct()
                .ToListAsync();

            return await GetTechnicianNameMapAsync(techIds);
        }

        // Resolve display names for a set of technician (user) ids. Shared by DTO mapping and by
        // the scheduling-conflict messages, so both spell technician names the same way.
        private async Task<System.Collections.Generic.Dictionary<int, string>> GetTechnicianNameMapAsync(
            System.Collections.Generic.List<int> techIds)
        {
            if (techIds == null || techIds.Count == 0) return new System.Collections.Generic.Dictionary<int, string>();

            var users = await _db.Users
                .Where(u => techIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync();

            var map = new System.Collections.Generic.Dictionary<int, string>();
            foreach (var u in users)
            {
                var name = (!string.IsNullOrWhiteSpace(u.FirstName) || !string.IsNullOrWhiteSpace(u.LastName))
                    ? $"{u.FirstName} {u.LastName}".Trim()
                    : u.Email;
                map[u.Id] = name ?? string.Empty;
            }
            return map;
        }


        // Union two skill arrays case-insensitively; returns null when the result is empty
        // so the DB column stays NULL instead of an empty array.
        private static string[]? MergeSkills(params IEnumerable<string>?[] sources)
        {
            var merged = sources
                .Where(s => s != null)
                .SelectMany(s => s!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return merged.Length > 0 ? merged : null;
        }

        public async Task<DispatchDto> CreateFromJobAsync(int jobId, CreateDispatchFromJobDto dto, string userId)
        {
            _logger.LogInformation("CreateFromJobAsync called by {UserId} for Job {JobId} (AutoCreate technicians: {HasTech})", userId, jobId, dto.AssignedTechnicianIds?.Count ?? 0);
            ValidateScheduleWindow(dto.ScheduledDate, dto.ScheduledStartTime, dto.ScheduledEndTime);
            // Get the job to find the related ServiceOrder and Contact
            var job = await _db.ServiceOrderJobs
                .Include(j => j.ServiceOrder)
                .FirstOrDefaultAsync(j => j.Id == jobId);
            
            if (job == null)
                throw new KeyNotFoundException($"Job {jobId} not found");
            
            // Get ContactId from the ServiceOrder
            var contactId = dto.ContactId ?? job.ServiceOrder?.ContactId ?? 0;
            var serviceOrderId = dto.ServiceOrderId ?? job.ServiceOrderId;
            
            // If still no contact, try to get any valid contact
            if (contactId == 0)
            {
                var anyContact = await _db.Contacts.FirstOrDefaultAsync(c => !c.IsDeleted);
                if (anyContact != null)
                    contactId = anyContact.Id;
            }
            
            // Dispatches always start as "assigned" — the workflow is
            // assigned -> confirmed -> in_progress -> completed.
            var hasTechnicians = dto.AssignedTechnicianIds != null && dto.AssignedTechnicianIds.Count > 0;
            var status = "assigned";

            // Multiple dispatches per job/service order stay allowed, but the assigned technicians
            // must actually be free in this window. Checked before the dispatch number is drawn.
            await EnsureTechnicianAvailabilityAsync(
                dto.AssignedTechnicianIds, dto.ScheduledDate, dto.ScheduledStartTime, dto.ScheduledEndTime);


            // Multiple dispatches per job are allowed: each call creates a new,
            // independent dispatch even if the job already has one or more.


            
            string dispatchNumber;
            try
            {
                dispatchNumber = _numberingService != null
                    ? await _numberingService.GetNextAsync("Dispatch")
                    : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Dispatch");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Numbering service failed for Dispatch, using GUID fallback");
                dispatchNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Dispatch");
            }

            var dispatch = new Dispatch
            {
                DispatchNumber = dispatchNumber,
                JobId = jobId.ToString(),
                ContactId = contactId,
                ServiceOrderId = serviceOrderId,
                ProjectId = job.ServiceOrder?.ProjectId,
                Status = status,
                Priority = dto.Priority ?? job.Priority ?? "medium",
                ScheduledDate = dto.ScheduledDate,
                ScheduledStartTime = dto.ScheduledStartTime,
                ScheduledEndTime = dto.ScheduledEndTime,
                SiteAddress = dto.SiteAddress ?? string.Empty,
                Description = job.JobDescription ?? job.Description,
                // Union of skills required on the specific job and the parent service
                // order's preferred skills, so the dispatcher can match technicians.
                RequiredSkills = MergeSkills(job.RequiredSkills, job.ServiceOrder?.PreferredSkills),
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId,
                DispatchedBy = userId,
                DispatchedAt = DateTime.UtcNow
            };

            // Wrap in execution strategy to be compatible with EnableRetryOnFailure
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    // Multiple dispatches per job allowed; no race re-check needed.

                    // Insert the dispatch first so it gets an Id before the
                    // dependent DispatchJob / DispatchTechnician rows reference it.
                    _db.Dispatches.Add(dispatch);
                    await _db.SaveChangesAsync();

                    // Always insert a DispatchJob row so GetPlanVsActualAsync (which queries
                    // through the join table) can aggregate actual time/expenses for this job.
                    _db.Set<DispatchJob>().Add(new DispatchJob
                    {
                        DispatchId = dispatch.Id,
                        JobId = jobId,
                        CreatedDate = DateTime.UtcNow
                    });

                    // Add assigned technicians to the DispatchTechnicians table
                    if (hasTechnicians)
                    {
                        foreach (var techIdStr in dto.AssignedTechnicianIds!)
                        {
                            if (int.TryParse(techIdStr, out var techId))
                            {
                                _db.Set<DispatchTechnician>().Add(new DispatchTechnician
                                {
                                    DispatchId = dispatch.Id,
                                    TechnicianId = techId,
                                    AssignedDate = DateTime.UtcNow,
                                    Role = "technician"
                                });
                            }
                        }
                        // Only flip job to "dispatched" on the first planning; later
                        // dispatches leave the job's own status intact.
                        if (job.Status != "dispatched") job.Status = "dispatched";
                    }

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Dispatch created from job {JobId} with ID {DispatchId}, Status: {Status}, Technicians: {TechCount}",
                jobId, dispatch.Id, status, dto.AssignedTechnicianIds?.Count ?? 0);

            if (_contactActivity != null && contactId > 0)
            {
                await _contactActivity.LogAsync(
                    contactId: contactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.DispatchCreated,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Dispatch,
                    relatedEntityId: dispatch.Id,
                    description: $"Dispatch {dispatch.DispatchNumber} was created",
                    metadata: new { number = dispatch.DispatchNumber, status = dispatch.Status, priority = dispatch.Priority, scheduledDate = dispatch.ScheduledDate },
                    createdBy: userId);
            }

            // Reload dispatch with technicians for the DTO mapping
            var createdDispatch = await _db.Dispatches
                .Include(d => d.AssignedTechnicians)
                .FirstAsync(d => d.Id == dispatch.Id);

            var nameMap = await GetTechnicianNameMapForDispatchAsync(createdDispatch.Id);
            return DispatchMapping.ToDto(createdDispatch, nameMap);
        }

        public Task<DispatchDto> CreateFromInstallationAsync(CreateDispatchFromInstallationDto dto, string userId)
            => CreateFromInstallationCoreAsync(dto, userId, insideTransaction: false);

        /// <summary>
        /// Shared implementation for both the public CreateFromInstallationAsync entry point and the
        /// merge-into-installation-dispatch fallback in AddJobsToInstallationDispatchAsync.
        ///
        /// When <paramref name="insideTransaction"/> is true, the writes participate in the caller's
        /// already-open transaction — we MUST NOT wrap them in a nested ExecutionStrategy /
        /// BeginTransactionAsync (EF throws "A transaction is already started" and retrying execution
        /// strategies do not support user-initiated nested transactions). When false, we open our own
        /// serializable transaction under the standard execution strategy.
        /// </summary>
        private async Task<DispatchDto> CreateFromInstallationCoreAsync(
            CreateDispatchFromInstallationDto dto,
            string userId,
            bool insideTransaction)
        {
            _logger.LogInformation("CreateFromInstallationCoreAsync called by {UserId} for Installation {InstallationId} with {JobCount} jobs (nested={Nested})",
                userId, dto.InstallationId, dto.JobIds.Count, insideTransaction);
            ValidateScheduleWindow(dto.ScheduledDate, dto.ScheduledStartTime, dto.ScheduledEndTime);

            // Validate all jobs exist
            var jobs = await _db.ServiceOrderJobs
                .Include(j => j.ServiceOrder)
                .Where(j => dto.JobIds.Contains(j.Id))
                .ToListAsync();

            if (dto.JobIds.Count == 0)
                throw new ArgumentException("At least one job is required to create an installation dispatch");

            if (jobs.Count != dto.JobIds.Count)
            {
                var foundIds = jobs.Select(j => j.Id).ToHashSet();
                var missingIds = dto.JobIds.Where(id => !foundIds.Contains(id)).ToList();
                throw new KeyNotFoundException($"Jobs not found: {string.Join(", ", missingIds)}");
            }

            // Get contact from DTO or from first job's service order
            var contactId = dto.ContactId ?? jobs.First().ServiceOrder?.ContactId ?? 0;
            var serviceOrderId = dto.ServiceOrderId ?? jobs.First().ServiceOrderId;

            if (contactId == 0)
            {
                var anyContact = await _db.Contacts.FirstOrDefaultAsync(c => !c.IsDeleted);
                if (anyContact != null) contactId = anyContact.Id;
            }

            var hasTechnicians = dto.AssignedTechnicianIds != null && dto.AssignedTechnicianIds.Count > 0;
            var status = "assigned";

            // Fast path for accidental double-submits: an identical dispatch created seconds ago is
            // returned as-is, before the availability check would flag it as a conflict with itself.
            if (!insideTransaction)
            {
                var recent = await FindRecentIdenticalDispatchAsync(dto, serviceOrderId, userId);
                if (recent != null)
                {
                    var recentNames = await GetTechnicianNameMapForDispatchAsync(recent.Id);
                    return DispatchMapping.ToDto(recent, recentNames);
                }
            }

            // Reject double-booking before we burn a dispatch number.
            await EnsureTechnicianAvailabilityAsync(
                dto.AssignedTechnicianIds, dto.ScheduledDate, dto.ScheduledStartTime, dto.ScheduledEndTime);


            string dispatchNumber;
            try
            {
                dispatchNumber = _numberingService != null
                    ? await _numberingService.GetNextAsync("Dispatch")
                    : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Dispatch");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Numbering service failed for Dispatch, using GUID fallback");
                dispatchNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Dispatch");
            }

            var dispatch = new Dispatch
            {
                DispatchNumber = dispatchNumber,
                JobId = null,
                ContactId = contactId,
                ServiceOrderId = serviceOrderId,
                ProjectId = jobs.FirstOrDefault()?.ServiceOrder?.ProjectId,
                InstallationId = dto.InstallationId > 0 ? dto.InstallationId : (int?)null,
                InstallationName = string.IsNullOrWhiteSpace(dto.InstallationName) ? null : dto.InstallationName,
                Status = status,
                Priority = dto.Priority ?? "medium",
                ScheduledDate = dto.ScheduledDate,
                ScheduledStartTime = dto.ScheduledStartTime,
                ScheduledEndTime = dto.ScheduledEndTime,
                SiteAddress = dto.SiteAddress ?? string.Empty,
                Description = dto.Notes ?? (dto.InstallationId > 0
                    ? $"Installation: {dto.InstallationName} ({dto.JobIds.Count} jobs)"
                    : $"Service order dispatch ({dto.JobIds.Count} jobs)"),
                RequiredSkills = MergeSkills(
                    jobs.SelectMany(j => j.RequiredSkills ?? Array.Empty<string>()).ToArray(),
                    jobs.FirstOrDefault()?.ServiceOrder?.PreferredSkills),
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId,
                DispatchedBy = userId,
                DispatchedAt = DateTime.UtcNow
            };

            // Local write function — used by both branches so the SQL is identical.
            Dispatch? duplicateOf = null;
            async Task WriteAsync()
            {
                _db.Dispatches.Add(dispatch);
                await _db.SaveChangesAsync();

                foreach (var jobId in dto.JobIds)
                {
                    _db.Set<DispatchJob>().Add(new DispatchJob
                    {
                        DispatchId = dispatch.Id,
                        JobId = jobId,
                        CreatedDate = DateTime.UtcNow
                    });
                }
                await _db.SaveChangesAsync();

                if (hasTechnicians)
                {
                    foreach (var techIdStr in dto.AssignedTechnicianIds!)
                    {
                        if (int.TryParse(techIdStr, out var techId))
                        {
                            _db.Set<DispatchTechnician>().Add(new DispatchTechnician
                            {
                                DispatchId = dispatch.Id,
                                TechnicianId = techId,
                                AssignedDate = DateTime.UtcNow,
                                Role = "technician"
                            });
                        }
                    }
                    await _db.SaveChangesAsync();

                    // Only flip a job's status to "dispatched" on its first planning.
                    foreach (var job in jobs)
                    {
                        if (job.Status != "dispatched") job.Status = "dispatched";
                    }
                    await _db.SaveChangesAsync();
                }
            }

            if (insideTransaction)
            {
                // Caller already owns the transaction — writes enlist automatically.
                await WriteAsync();
            }
            else
            {
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    try
                    {
                        // Accidental double-submit guard. Planning the same job / service order
                        // several times on purpose stays fully allowed — we only collapse
                        // byte-identical requests that arrive within a few seconds of each other
                        // (double-click, retried POST). Serialize concurrent callers on an
                        // advisory lock keyed on (installation|service order, day).
                        var scopeKey = dto.InstallationId > 0
                            ? (long)dto.InstallationId
                            : -(long)serviceOrderId;
                        long lockKey = (scopeKey << 32)
                                     | (uint)(dto.ScheduledDate.Date - new DateTime(1970, 1, 1)).Days;
                        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

                        duplicateOf = await FindRecentIdenticalDispatchAsync(dto, serviceOrderId, userId);
                        if (duplicateOf == null)
                            await WriteAsync();
                        await tx.CommitAsync();
                    }
                    catch
                    {
                        await tx.RollbackAsync();
                        throw;
                    }
                });
            }

            if (duplicateOf != null)
            {
                _logger.LogInformation(
                    "Duplicate submit detected — returning existing dispatch {DispatchId} instead of creating a new one",
                    duplicateOf.Id);
                var dupNameMap = await GetTechnicianNameMapForDispatchAsync(duplicateOf.Id);
                return DispatchMapping.ToDto(duplicateOf, dupNameMap);
            }

            _logger.LogInformation(
                "Dispatch created from installation {InstallationId} with ID {DispatchId}, {JobCount} jobs, Status: {Status}",
                dto.InstallationId, dispatch.Id, dto.JobIds.Count, status);

            var createdDispatch = await _db.Dispatches
                .Include(d => d.AssignedTechnicians)
                .Include(d => d.DispatchJobs)
                .FirstAsync(d => d.Id == dispatch.Id);

            if (_contactActivity != null && createdDispatch.ContactId > 0)
            {
                await _contactActivity.LogAsync(
                    contactId: createdDispatch.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.DispatchCreated,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Dispatch,
                    relatedEntityId: createdDispatch.Id,
                    description: $"Dispatch {createdDispatch.DispatchNumber} was created",
                    metadata: new { number = createdDispatch.DispatchNumber, status = createdDispatch.Status, priority = createdDispatch.Priority, scheduledDate = createdDispatch.ScheduledDate },
                    createdBy: userId);
            }

            var nameMap = await GetTechnicianNameMapForDispatchAsync(createdDispatch.Id);
            return DispatchMapping.ToDto(createdDispatch, nameMap);
        }

        /// <summary>
        /// Returns a dispatch created moments ago (within the idempotency window) by the same user
        /// with exactly the same scope, schedule and job set — i.e. an accidental duplicate submit.
        /// Deliberate re-planning of the same job or service order later is unaffected.
        /// </summary>
        private static readonly TimeSpan DuplicateSubmitWindow = TimeSpan.FromSeconds(20);

        private async Task<Dispatch?> FindRecentIdenticalDispatchAsync(
            CreateDispatchFromInstallationDto dto, int? serviceOrderId, string userId)
        {
            var since = DateTime.UtcNow - DuplicateSubmitWindow;
            var installationId = dto.InstallationId > 0 ? (int?)dto.InstallationId : null;
            var day = dto.ScheduledDate.Date;
            var nextDay = day.AddDays(1);

            var candidates = await _db.Dispatches
                .Include(d => d.AssignedTechnicians)
                .Include(d => d.DispatchJobs)
                .Where(d => !d.IsDeleted
                    && d.CreatedBy == userId
                    && d.CreatedDate >= since
                    && d.InstallationId == installationId
                    && d.ServiceOrderId == serviceOrderId
                    && d.ScheduledDate >= day && d.ScheduledDate < nextDay
                    && d.ScheduledStartTime == dto.ScheduledStartTime
                    && d.ScheduledEndTime == dto.ScheduledEndTime)
                .ToListAsync();

            var wantedJobs = dto.JobIds.ToHashSet();
            return candidates.FirstOrDefault(d =>
            {
                var jobs = d.DispatchJobs.Where(dj => !dj.IsDeleted).Select(dj => dj.JobId).ToHashSet();
                return jobs.SetEquals(wantedJobs);
            });
        }

        /// <summary>
        /// Guard against double-booking a technician. Planning multiple dispatches per service
        /// order is fully supported, but the same technician may not be on two live dispatches whose
        /// scheduled windows overlap on the same day. Dispatches that no longer occupy the
        /// technician's calendar (cancelled / rejected / done) are ignored.
        /// </summary>
        private async Task EnsureTechnicianAvailabilityAsync(
            IEnumerable<string>? technicianIds,
            DateTime scheduledDate,
            TimeSpan? scheduledStartTime,
            TimeSpan? scheduledEndTime,
            int? excludeDispatchId = null)
        {
            var techIds = (technicianIds ?? Enumerable.Empty<string>())
                .Select(s => int.TryParse(s, out var v) ? v : 0)
                .Where(v => v > 0)
                .Distinct()
                .ToList();

            if (techIds.Count == 0) return;

            var dayStart = scheduledDate.Date;
            var dayEnd = dayStart.AddDays(1);

            var sameDay = await (
                from dt in _db.Set<DispatchTechnician>()
                join d in _db.Dispatches on dt.DispatchId equals d.Id
                where !dt.IsDeleted && !d.IsDeleted
                    && techIds.Contains(dt.TechnicianId)
                    && d.ScheduledDate >= dayStart && d.ScheduledDate < dayEnd
                    && d.Status != "cancelled" && d.Status != "rejected"
                    && d.Status != "completed" && d.Status != "technically_completed"
                    && (excludeDispatchId == null || d.Id != excludeDispatchId.Value)
                select new
                {
                    dt.TechnicianId,
                    d.DispatchNumber,
                    d.ScheduledStartTime,
                    d.ScheduledEndTime
                }).ToListAsync();

            if (sameDay.Count == 0) return;

            // A dispatch without explicit times occupies the whole day. A degenerate window
            // (end <= start) is widened to one minute so two identical point-in-time slots still
            // register as a clash.
            static (TimeSpan Start, TimeSpan End) Window(TimeSpan? s, TimeSpan? e)
            {
                var start = s ?? TimeSpan.Zero;
                var end = e ?? TimeSpan.FromDays(1);
                if (end <= start) end = start.Add(TimeSpan.FromMinutes(1));
                return (start, end);
            }

            var (newStart, newEnd) = Window(scheduledStartTime, scheduledEndTime);

            var clashes = sameDay
                .Where(x =>
                {
                    var (s, e) = Window(x.ScheduledStartTime, x.ScheduledEndTime);
                    return newStart < e && s < newEnd; // half-open interval overlap
                })
                .ToList();

            if (clashes.Count == 0) return;

            var nameMap = await GetTechnicianNameMapAsync(clashes.Select(c => c.TechnicianId).Distinct().ToList());

            var details = clashes
                .GroupBy(c => c.TechnicianId)
                .Select(g =>
                {
                    var who = nameMap.TryGetValue(g.Key, out var n) && !string.IsNullOrWhiteSpace(n)
                        ? n
                        : $"Technician #{g.Key}";
                    return $"{who} is already on {string.Join(", ", g.Select(x => x.DispatchNumber).Distinct())}";
                });

            throw new InvalidOperationException(
                $"Scheduling conflict on {dayStart:yyyy-MM-dd}: {string.Join("; ", details)}. " +
                "Pick another time slot or another technician.");
        }



        public async Task<DispatchDto> CreateFromServiceOrderAsync(CreateDispatchFromServiceOrderDto dto, string userId)
        {
            _logger.LogInformation("CreateFromServiceOrderAsync called by {UserId} for ServiceOrder {ServiceOrderId}",
                userId, dto.ServiceOrderId);

            // Resolve job ids: explicit list, or every non-deleted job on the service order.
            var jobIds = dto.JobIds != null && dto.JobIds.Count > 0
                ? dto.JobIds
                : await _db.ServiceOrderJobs
                    .Where(j => j.ServiceOrderId == dto.ServiceOrderId)
                    .Select(j => j.Id)
                    .ToListAsync();

            if (jobIds.Count == 0)
                throw new ArgumentException($"Service order {dto.ServiceOrderId} has no jobs to dispatch");

            // Reuse the multi-job creation path. InstallationId is left at 0 so the
            // dispatch is stored with InstallationId = NULL (a service-order dispatch).
            var installationDto = new CreateDispatchFromInstallationDto
            {
                InstallationId = 0,
                InstallationName = string.Empty,
                JobIds = jobIds,
                AssignedTechnicianIds = dto.AssignedTechnicianIds ?? new(),
                ScheduledDate = dto.ScheduledDate,
                ScheduledStartTime = dto.ScheduledStartTime,
                ScheduledEndTime = dto.ScheduledEndTime,
                Priority = dto.Priority,
                Notes = dto.Notes,
                SiteAddress = dto.SiteAddress,
                ContactId = dto.ContactId,
                ServiceOrderId = dto.ServiceOrderId
            };

            return await CreateFromInstallationAsync(installationDto, userId);
        }

        public async Task<DispatchDto> AddJobsToInstallationDispatchAsync(
            int installationId,
            string installationName,
            List<int> jobIds,
            List<string> technicianIds,
            DateTime scheduledDate,
            TimeSpan? scheduledStartTime,
            TimeSpan? scheduledEndTime,
            string priority,
            string? notes,
            string? siteAddress,
            int? contactId,
            int? serviceOrderId,
            string userId)
        {
            if (jobIds == null || jobIds.Count == 0)
                throw new ArgumentException("At least one job id is required", nameof(jobIds));

            // Parse technician ids once
            var techIdInts = (technicianIds ?? new List<string>())
                .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            // ── Concurrency guard ────────────────────────────────────────────
            // Two simultaneous merge calls for the same (installation, date) could both
            // miss the candidate query and create duplicate dispatches. Acquire a
            // transaction-scoped Postgres advisory lock so they serialize. The lock is
            // released automatically on COMMIT/ROLLBACK. Key = (installationId << 32) | dayNumber.
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                long lockKey = ((long)installationId << 32)
                             | (uint)(scheduledDate.Date - new DateTime(1970, 1, 1)).Days;
                await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

                // Look for an existing non-deleted dispatch on the same date for this installation
                // whose technician set matches the requested set (when technicians are provided).
                var openStatuses = new[] { "planned", "assigned", "scheduled" };

                var candidates = await _db.Dispatches
                    .Include(d => d.AssignedTechnicians)
                    .Include(d => d.DispatchJobs)
                    .Where(d => !d.IsDeleted
                        && d.InstallationId == installationId
                        && d.ScheduledDate.Date == scheduledDate.Date
                        && openStatuses.Contains(d.Status))
                    .ToListAsync();

                Dispatch? existing = null;
                if (techIdInts.Count == 0)
                {
                    existing = candidates.FirstOrDefault();
                }
                else
                {
                    existing = candidates.FirstOrDefault(d =>
                    {
                        var ids = d.AssignedTechnicians.Select(at => at.TechnicianId).ToHashSet();
                        return techIdInts.All(id => ids.Contains(id));
                    });
                }

                if (existing != null)
                {
                    // Skip jobs already attached to this dispatch
                    var alreadyAttached = existing.DispatchJobs.Select(dj => dj.JobId).ToHashSet();
                    var newJobIds = jobIds.Where(id => !alreadyAttached.Contains(id)).ToList();

                    if (newJobIds.Count > 0)
                    {
                        // Multiple dispatches per job are allowed — no cross-dispatch conflict check.


                        foreach (var jid in newJobIds)
                        {
                            _db.Set<DispatchJob>().Add(new DispatchJob
                            {
                                DispatchId = existing.Id,
                                JobId = jid,
                                CreatedDate = DateTime.UtcNow
                            });
                        }

                        // Mark jobs dispatched
                        var jobsToUpdate = await _db.ServiceOrderJobs.Where(j => newJobIds.Contains(j.Id)).ToListAsync();
                        foreach (var job in jobsToUpdate) job.Status = "dispatched";

                        existing.ModifiedBy = userId;
                        existing.ModifiedDate = DateTime.UtcNow;
                        await _db.SaveChangesAsync();

                        _logger.LogInformation(
                            "Appended {Count} job(s) to existing installation dispatch {DispatchId} (installation {InstallationId})",
                            newJobIds.Count, existing.Id, installationId);
                    }

                    var reloaded = await _db.Dispatches
                        .Include(d => d.AssignedTechnicians)
                        .Include(d => d.DispatchJobs)
                        .FirstAsync(d => d.Id == existing.Id);
                    var nameMap = await GetTechnicianNameMapForDispatchAsync(reloaded.Id);
                    await tx.CommitAsync();
                    return DispatchMapping.ToDto(reloaded, nameMap);
                }

                // No existing dispatch — create a new one via the canonical path.
                var createDto = new CreateDispatchFromInstallationDto
                {
                    InstallationId = installationId,
                    InstallationName = installationName,
                    JobIds = jobIds,
                    AssignedTechnicianIds = technicianIds ?? new List<string>(),
                    ScheduledDate = scheduledDate,
                    ScheduledStartTime = scheduledStartTime,
                    ScheduledEndTime = scheduledEndTime,
                    Priority = priority ?? "medium",
                    Notes = notes,
                    SiteAddress = siteAddress,
                    ContactId = contactId,
                    ServiceOrderId = serviceOrderId,
                };
                // Reuse the core writer with insideTransaction: true so we DO NOT open a nested
                // ExecutionStrategy / BeginTransactionAsync on the same DbContext (that would throw
                // "A transaction is already started" and 500 the whole request).
                var created = await CreateFromInstallationCoreAsync(createDto, userId, insideTransaction: true);
                await tx.CommitAsync();
                return created;
            });
        }

        public async Task<PagedResult<DispatchListItemDto>> GetAllAsync(DispatchQueryParams query)
        {
            var q = _db.Dispatches.AsNoTracking().AsQueryable().Where(d => !d.IsDeleted);

            if (!string.IsNullOrEmpty(query.Status)) q = q.Where(d => d.Status == query.Status);
            if (!string.IsNullOrEmpty(query.Priority)) q = q.Where(d => d.Priority == query.Priority);
            if (query.ServiceOrderId.HasValue) q = q.Where(d => d.ServiceOrderId == query.ServiceOrderId);
            if (query.DateFrom.HasValue) q = q.Where(d => d.ScheduledDate >= query.DateFrom.Value);
            if (query.DateTo.HasValue) q = q.Where(d => d.ScheduledDate <= query.DateTo.Value);

            var total = await q.CountAsync();
            var pageNumber = Math.Max(1, query.PageNumber);
            var pageSize = Math.Min(100, Math.Max(1, query.PageSize));

            var dispatches = await q
                .OrderByDescending(d => d.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Include(d => d.AssignedTechnicians)
                .Include(d => d.Contact)
                .Include(d => d.DispatchJobs)
                .AsSingleQuery()
                .ToListAsync();

            // Get all technician IDs to fetch user names
            var allTechnicianIds = dispatches
                .SelectMany(d => d.AssignedTechnicians.Select(at => at.TechnicianId))
                .Distinct()
                .ToList();

            // Fetch user names for all technicians in one query
            var technicianUsers = await _db.Users
                .Where(u => allTechnicianIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToDictionaryAsync(u => u.Id);

            var items = dispatches.Select(d => new DispatchListItemDto
            {
                Id = d.Id,
                DispatchNumber = d.DispatchNumber,
                JobId = int.TryParse(d.JobId, out var jid) ? jid : null,
                ServiceOrderId = d.ServiceOrderId,
                ProjectId = d.ProjectId,
                ContactId = d.ContactId,
                ContactName = d.Contact != null 
                    ? (!string.IsNullOrEmpty(d.Contact.FirstName) || !string.IsNullOrEmpty(d.Contact.LastName)
                        ? $"{d.Contact.FirstName} {d.Contact.LastName}".Trim()
                        : d.Contact.Company)
                    : null,
                SiteAddress = d.SiteAddress,
                Status = d.Status,
                Priority = d.Priority,
                AssignedTechnicians = d.AssignedTechnicians.Select(at => {
                    var user = technicianUsers.GetValueOrDefault(at.TechnicianId);
                    return new UserLightDto 
                    { 
                        Id = at.TechnicianId,
                        Name = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null,
                        Email = user?.Email
                    };
                }).ToList(),
                Scheduling = new SchedulingDto
                {
                    ScheduledDate = d.ScheduledDate,
                    ScheduledStartTime = d.ScheduledStartTime,
                    ScheduledEndTime = d.ScheduledEndTime,
                    EstimatedDuration = d.ScheduledStartTime.HasValue && d.ScheduledEndTime.HasValue
                        ? (int)(d.ScheduledEndTime.Value - d.ScheduledStartTime.Value).TotalMinutes
                        : d.ActualDuration
                },
                ScheduledDate = d.ScheduledDate,
                ScheduledStartTime = d.ScheduledStartTime?.ToString(@"hh\:mm"),
                ScheduledEndTime = d.ScheduledEndTime?.ToString(@"hh\:mm"),
                Notes = d.Description,
                DispatchedBy = d.DispatchedBy,
                CreatedDate = d.CreatedDate,
                ModifiedDate = d.ModifiedDate,
                InstallationId = d.InstallationId,
                InstallationName = d.InstallationName,
                JobIds = d.DispatchJobs?.Select(dj => dj.JobId).ToList() ?? new()
            }).ToList();

            // Enrich multi-job dispatches with job summaries (one extra query) so the
            // planning board can show a single card per dispatch and reveal its jobs on hover.
            var allJobIds = items.SelectMany(i => i.JobIds).Distinct().ToList();
            if (allJobIds.Count > 0)
            {
                var jobSummaries = await _db.ServiceOrderJobs
                    .AsNoTracking()
                    .Where(j => allJobIds.Contains(j.Id))
                    .Select(j => new DispatchJobSummaryDto
                    {
                        Id = j.Id,
                        Title = j.Title ?? j.JobDescription,
                        Status = j.Status,
                        EstimatedDuration = j.EstimatedDuration,
                        Priority = j.Priority
                    })
                    .ToDictionaryAsync(j => j.Id);

                foreach (var item in items)
                {
                    item.Jobs = item.JobIds
                        .Where(jobSummaries.ContainsKey)
                        .Select(id => jobSummaries[id])
                        .ToList();
                }
            }

            return new PagedResult<DispatchListItemDto>
            {
                Data = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<DispatchDto> GetByIdAsync(int dispatchId)
        {
            var d = await _db.Dispatches
                .AsNoTracking()
                .Include(x => x.TimeEntries)
                .Include(x => x.Expenses)
                .Include(x => x.MaterialsUsed)
                .Include(x => x.Attachments)
                .Include(x => x.Notes)
                .Include(x => x.AssignedTechnicians)
                .Include(x => x.DispatchJobs)
                .FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);

            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");
            var nameMap = await GetTechnicianNameMapForDispatchAsync(d.Id);
            var dto = DispatchMapping.ToDto(d, nameMap);

            // Attach job summaries so the UI can list the dispatch's jobs.
            if (dto.JobIds.Count > 0)
            {
                dto.Jobs = await _db.ServiceOrderJobs
                    .AsNoTracking()
                    .Where(j => dto.JobIds.Contains(j.Id))
                    .Select(j => new DispatchJobSummaryDto
                    {
                        Id = j.Id,
                        Title = j.Title ?? j.JobDescription,
                        Status = j.Status,
                        EstimatedDuration = j.EstimatedDuration,
                        Priority = j.Priority
                    })
                    .ToListAsync();
            }

            return dto;
        }

        public async Task<DispatchDto> UpdateAsync(int dispatchId, UpdateDispatchDto dto, string userId)
        {
            var d = await _db.Dispatches.Include(x => x.AssignedTechnicians).FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");

            if (dto.ScheduledDate.HasValue) d.ScheduledDate = dto.ScheduledDate.Value;
            if (dto.ScheduledStartTime.HasValue) d.ScheduledStartTime = dto.ScheduledStartTime.Value;
            if (dto.ScheduledEndTime.HasValue) d.ScheduledEndTime = dto.ScheduledEndTime.Value;
            if (!string.IsNullOrEmpty(dto.Priority)) d.Priority = dto.Priority;
            if (dto.RequiredSkills != null) d.RequiredSkills = dto.RequiredSkills.ToArray();

            // Reschedule/reassign validation: the technician roster is whatever the caller sent,
            // falling back to the roster already on the dispatch. Checked against the *new* window
            // so moving a dispatch onto a slot a technician already works cannot silently
            // double-book them.
            var rosterIds = dto.AssignedTechnicianIds ?? d.AssignedTechnicians
                .Where(t => !t.IsDeleted)
                .Select(t => t.TechnicianId.ToString())
                .ToList();

            await EnsureTechnicianAvailabilityAsync(
                rosterIds, d.ScheduledDate, d.ScheduledStartTime, d.ScheduledEndTime, excludeDispatchId: d.Id);

            // AssignedTechnicianIds used to be silently ignored here, so reassigning a dispatch
            // through this endpoint had no effect. Apply it as a full replacement of the roster.
            if (dto.AssignedTechnicianIds != null)
            {
                var desired = dto.AssignedTechnicianIds
                    .Select(s => int.TryParse(s, out var v) ? v : 0)
                    .Where(v => v > 0)
                    .Distinct()
                    .ToHashSet();

                foreach (var existing in d.AssignedTechnicians.Where(t => !t.IsDeleted).ToList())
                {
                    if (!desired.Contains(existing.TechnicianId))
                    {
                        existing.IsDeleted = true;
                        existing.DeletedAt = DateTime.UtcNow;
                        existing.DeletedBy = userId;
                    }
                }

                var current = d.AssignedTechnicians.Where(t => !t.IsDeleted).Select(t => t.TechnicianId).ToHashSet();
                foreach (var techId in desired.Where(id => !current.Contains(id)))
                {
                    _db.Set<DispatchTechnician>().Add(new DispatchTechnician
                    {
                        DispatchId = d.Id,
                        TechnicianId = techId,
                        AssignedDate = DateTime.UtcNow,
                        Role = "technician"
                    });
                }

                // Legacy rows created as "planned" rejoin the active flow at "assigned".
                if (d.Status == "planned" || d.Status == "pending") d.Status = "assigned";
            }

            d.ModifiedDate = DateTime.UtcNow;
            d.ModifiedBy = userId;
            await _db.SaveChangesAsync();

            var reloaded = await _db.Dispatches
                .Include(x => x.AssignedTechnicians)
                .Include(x => x.DispatchJobs)
                .FirstAsync(x => x.Id == d.Id);
            var nameMap = await GetTechnicianNameMapForDispatchAsync(reloaded.Id);
            return DispatchMapping.ToDto(reloaded, nameMap);
        }


        public async Task<DispatchDto> UpdateStatusAsync(int dispatchId, UpdateDispatchStatusDto dto, string userId)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");

            var oldStatus = d.Status;

            // Guard: do not allow cancelling a dispatch that is already closed or technically completed
            if (dto.Status == "cancelled"
                && (oldStatus == "closed" || oldStatus == "technically_completed" || oldStatus == "completed" || oldStatus == "invoiced"))
            {
                throw new InvalidOperationException(
                    $"Cannot cancel dispatch {d.DispatchNumber}: it is already {oldStatus}.");
            }

            d.Status = dto.Status;
            d.ModifiedDate = DateTime.UtcNow;
            d.ModifiedBy = userId;
            if (dto.Status == "in_progress") d.ActualStartTime = DateTime.UtcNow;
            if (dto.Status == "technically_completed" || dto.Status == "completed") d.ActualEndTime = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // When a dispatch is cancelled, revert its linked jobs back to
            // 'unscheduled' so they reappear in the dispatcher's unassigned
            // queue. Mirrors DeleteAsync so cancel and delete behave the
            // same for downstream consumers (dispatcher board, reports).
            // "rejected" is just as terminal as "cancelled" for job coverage: the work is
            // not going to happen on this dispatch, so its jobs must go back to the
            // unassigned queue too (sibling-aware, so duplicate dispatches are safe).
            if ((dto.Status == "cancelled" || dto.Status == "rejected") && oldStatus != dto.Status)
            {
                try
                {
                    var allJobIds = new HashSet<int>();
                    var djs = await _db.DispatchJobs
                        .Where(dj => dj.DispatchId == d.Id && !dj.IsDeleted)
                        .Select(dj => dj.JobId)
                        .ToListAsync();
                    foreach (var jid in djs) allJobIds.Add(jid);
                    if (!string.IsNullOrEmpty(d.JobId) && int.TryParse(d.JobId, out var legacyJobId))
                        allJobIds.Add(legacyJobId);

                    if (allJobIds.Count > 0)
                    {
                        await ReleaseJobsAfterDispatchRemovalAsync(allJobIds, d.Id, "DISPATCH-CANCEL");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DISPATCH-CANCEL] Failed to revert job statuses for dispatch {DispatchId}", dispatchId);
                }
            }

            // Restore inventory consumed by this dispatch's material lines.
            // Without this, cancelling a dispatch that already recorded material
            // usage leaves the deducted quantity permanently missing from stock.
            if (dto.Status == "cancelled" && oldStatus != "cancelled")
            {
                await RestoreDispatchMaterialStockAsync(d, userId);
            }

            // Dedicated audit record for cancellations (persisted separately from notes)
            if (dto.Status == "cancelled" && oldStatus != "cancelled")
            {
                try
                {
                    string? saleId = null;
                    string? offerId = null;
                    if (d.ServiceOrderId.HasValue)
                    {
                        var soIds = await _db.ServiceOrders
                            .Where(so => so.Id == d.ServiceOrderId.Value)
                            .Select(so => new { so.SaleId, so.OfferId })
                            .FirstOrDefaultAsync();
                        if (soIds != null)
                        {
                            saleId = soIds.SaleId;
                            offerId = soIds.OfferId;
                        }
                        if (string.IsNullOrEmpty(offerId) && !string.IsNullOrEmpty(saleId) && int.TryParse(saleId, out var saleIdInt))
                        {
                            offerId = await _db.Sales
                                .Where(s => s.Id == saleIdInt)
                                .Select(s => s.OfferId)
                                .FirstOrDefaultAsync();
                        }
                    }

                    _db.DispatchAuditLogs.Add(new MyApi.Modules.Dispatches.Models.DispatchAuditLog
                    {
                        DispatchId = d.Id,
                        DispatchNumber = d.DispatchNumber,
                        EventType = "cancelled",
                        OldStatus = oldStatus,
                        NewStatus = dto.Status,
                        Reason = dto.Notes,
                        ServiceOrderId = d.ServiceOrderId,
                        SaleId = saleId,
                        OfferId = offerId,
                        ActorUserId = userId,
                        ActorName = userId,
                        CreatedAt = DateTime.UtcNow,
                    });
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DISPATCH-AUDIT] Failed to persist cancellation audit for dispatch {DispatchId}", dispatchId);
                }
            }

            // Log status change to the contact activity feed
            if (_contactActivity != null && oldStatus != dto.Status)
            {
                var contactId = d.ContactId;
                if (contactId <= 0 && d.ServiceOrderId.HasValue)
                {
                    contactId = await _db.ServiceOrders
                        .Where(so => so.Id == d.ServiceOrderId.Value)
                        .Select(so => so.ContactId)
                        .FirstOrDefaultAsync();
                }
                if (contactId > 0)
                {
                    await _contactActivity.LogAsync(
                        contactId: contactId,
                        type: MyApi.Modules.Contacts.Models.ContactActivityTypes.DispatchStatusChanged,
                        relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Dispatch,
                        relatedEntityId: d.Id,
                        description: $"Dispatch {d.DispatchNumber} status: {oldStatus} → {dto.Status}",
                        metadata: new { number = d.DispatchNumber, oldStatus, status = dto.Status },
                        createdBy: userId);
                }
            }

            // Upward propagation: Log activity to parent Sale (and Offer)
            if (oldStatus != dto.Status && d.ServiceOrderId.HasValue)
            {
                await PropagateDispatchStatusToSaleAsync(d, oldStatus, dto.Status, userId);
            }

            // Trigger workflow automation for status change
            // The workflow engine is the SINGLE SOURCE OF TRUTH for all status cascading logic
            // Users configure triggers in the Workflow module (from status -> to status)
            // The engine dynamically evaluates conditions and executes actions
            if (oldStatus != dto.Status && _workflowTriggerService != null)
            {
                try
                {
                    _logger.LogInformation(
                        "[DISPATCH-STATUS] Dispatch #{DispatchId} status changing: '{OldStatus}' -> '{NewStatus}'. " +
                        "ServiceOrderId: {ServiceOrderId}. Triggering workflow...",
                        dispatchId, oldStatus ?? "NULL", dto.Status, d.ServiceOrderId);
                    
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "dispatch",
                        dispatchId,
                        oldStatus ?? "",
                        dto.Status,
                        userId,
                        new { 
                            dispatchId, 
                            dispatchNumber = d.DispatchNumber, 
                            jobId = d.JobId,
                            serviceOrderId = d.ServiceOrderId 
                        }
                    );
                    _logger.LogInformation(
                        "[DISPATCH-STATUS] Workflow trigger completed for dispatch {DispatchId}",
                        dispatchId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "[DISPATCH-STATUS] Failed to trigger workflow for dispatch {DispatchId} status change: {OldStatus} -> {NewStatus}",
                        dispatchId, oldStatus, dto.Status);
                }
            }
            else if (oldStatus != dto.Status)
            {
                _logger.LogWarning(
                    "[DISPATCH-STATUS] No workflow trigger service available for dispatch {DispatchId} status change: {OldStatus} -> {NewStatus}. " +
                    "Custom automation is skipped; the domain cascade below still runs.",
                    dispatchId, oldStatus, dto.Status);
            }

            // Domain cascade: always applied, workflow engine or not.
            if (oldStatus != dto.Status)
            {
                await ApplyDispatchCascadeAsync(d, oldStatus, dto.Status, userId);
            }

            var nameMap = await GetTechnicianNameMapForDispatchAsync(d.Id);
            return DispatchMapping.ToDto(d, nameMap);
        }

        /// <summary>
        /// Propagate dispatch status changes to parent Sale and Offer activities
        /// </summary>
        private async Task PropagateDispatchStatusToSaleAsync(Dispatch dispatch, string? oldStatus, string newStatus, string userId)
        {
            try
            {
                var serviceOrder = await _db.ServiceOrders.FindAsync(dispatch.ServiceOrderId);
                if (serviceOrder == null || string.IsNullOrEmpty(serviceOrder.SaleId)) return;

                if (!int.TryParse(serviceOrder.SaleId, out int saleId)) return;

                var sale = await _db.Sales.FindAsync(saleId);
                if (sale == null) return;

                // Create SaleActivity for dispatch status change
                var saleActivity = new MyApi.Modules.Sales.Models.SaleActivity
                {
                    SaleId = saleId,
                    Type = "dispatch_status_changed",
                    Description = $"Dispatch #{dispatch.DispatchNumber} status: {oldStatus} → {newStatus}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = sale.AssignedToName ?? "System"
                };
                _db.SaleActivities.Add(saleActivity);

                // Propagate to Offer if sale came from an offer
                if (!string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                {
                    var offerActivity = new MyApi.Modules.Offers.Models.OfferActivity
                    {
                        OfferId = offerId,
                        Type = "dispatch_status_changed",
                        Description = $"Dispatch #{dispatch.DispatchNumber} status: {oldStatus} → {newStatus} (Sale #{saleId})",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    };
                    _db.OfferActivities.Add(offerActivity);
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to propagate dispatch status to sale activities for dispatch {DispatchId}", dispatch.Id);
            }
        }

        /// <summary>
        /// Propagate time entry additions to parent Sale and Offer activities
        /// </summary>
        private async Task PropagateTimeEntryToSaleAsync(Dispatch dispatch, TimeEntry timeEntry, string userId)
        {
            try
            {
                if (!dispatch.ServiceOrderId.HasValue) return;

                var serviceOrder = await _db.ServiceOrders.FindAsync(dispatch.ServiceOrderId);
                if (serviceOrder == null || string.IsNullOrEmpty(serviceOrder.SaleId)) return;

                if (!int.TryParse(serviceOrder.SaleId, out int saleId)) return;

                var sale = await _db.Sales.FindAsync(saleId);
                if (sale == null) return;

                var durationHours = (timeEntry.Duration ?? 0) / 60m;
                var description = $"Time entry added: {timeEntry.WorkType} ({durationHours:F1}h) on Dispatch #{dispatch.DispatchNumber}";

                var saleActivity = new MyApi.Modules.Sales.Models.SaleActivity
                {
                    SaleId = saleId,
                    Type = "time_entry_added",
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = sale.AssignedToName ?? "System"
                };
                _db.SaleActivities.Add(saleActivity);

                // Propagate to Offer if sale came from an offer
                if (!string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                {
                    var offerActivity = new MyApi.Modules.Offers.Models.OfferActivity
                    {
                        OfferId = offerId,
                        Type = "time_entry_added",
                        Description = $"{description} (Sale #{saleId})",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    };
                    _db.OfferActivities.Add(offerActivity);
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to propagate time entry to sale activities for dispatch {DispatchId}", dispatch.Id);
            }
        }

        /// <summary>
        /// Propagate expense additions to parent Sale and Offer activities
        /// </summary>
        private async Task PropagateExpenseToSaleAsync(Dispatch dispatch, Expense expense, string userId)
        {
            try
            {
                if (!dispatch.ServiceOrderId.HasValue) return;

                var serviceOrder = await _db.ServiceOrders.FindAsync(dispatch.ServiceOrderId);
                if (serviceOrder == null || string.IsNullOrEmpty(serviceOrder.SaleId)) return;

                if (!int.TryParse(serviceOrder.SaleId, out int saleId)) return;

                var sale = await _db.Sales.FindAsync(saleId);
                if (sale == null) return;

                var description = $"Expense added: {expense.ExpenseType} ({expense.Amount:C}) on Dispatch #{dispatch.DispatchNumber}";

                var saleActivity = new MyApi.Modules.Sales.Models.SaleActivity
                {
                    SaleId = saleId,
                    Type = "expense_added",
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = sale.AssignedToName ?? "System"
                };
                _db.SaleActivities.Add(saleActivity);

                // Propagate to Offer if sale came from an offer
                if (!string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                {
                    var offerActivity = new MyApi.Modules.Offers.Models.OfferActivity
                    {
                        OfferId = offerId,
                        Type = "expense_added",
                        Description = $"{description} (Sale #{saleId})",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    };
                    _db.OfferActivities.Add(offerActivity);
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to propagate expense to sale activities for dispatch {DispatchId}", dispatch.Id);
            }
        }

        /// <summary>
        /// Propagate material usage to parent Sale and Offer activities
        /// </summary>
        private async Task PropagateMaterialToSaleAsync(Dispatch dispatch, MaterialUsage material, string userId)
        {
            try
            {
                if (!dispatch.ServiceOrderId.HasValue) return;

                var serviceOrder = await _db.ServiceOrders.FindAsync(dispatch.ServiceOrderId);
                if (serviceOrder == null || string.IsNullOrEmpty(serviceOrder.SaleId)) return;

                if (!int.TryParse(serviceOrder.SaleId, out int saleId)) return;

                var sale = await _db.Sales.FindAsync(saleId);
                if (sale == null) return;

                var description = $"Material used: {material.Description} (Qty: {material.Quantity}, Total: {material.TotalPrice:C}) on Dispatch #{dispatch.DispatchNumber}";

                var saleActivity = new MyApi.Modules.Sales.Models.SaleActivity
                {
                    SaleId = saleId,
                    Type = "material_used",
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = sale.AssignedToName ?? "System"
                };
                _db.SaleActivities.Add(saleActivity);

                // Propagate to Offer if sale came from an offer
                if (!string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                {
                    var offerActivity = new MyApi.Modules.Offers.Models.OfferActivity
                    {
                        OfferId = offerId,
                        Type = "material_used",
                        Description = $"{description} (Sale #{saleId})",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    };
                    _db.OfferActivities.Add(offerActivity);
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to propagate material usage to sale activities for dispatch {DispatchId}", dispatch.Id);
            }
        }

        /// <summary>
        /// Cross-service idempotency probe used by <see cref="AddMaterialUsageAsync"/>.
        /// Returns true when the parent Sale of the dispatch (resolved via
        /// Dispatch → ServiceOrder → Sale) has already recorded a
        /// <c>sale_deduction</c> stock transaction for the given article. In that
        /// case the physical goods have already been taken out of inventory and
        /// the dispatch-side deduction must be skipped to avoid double-counting.
        /// </summary>
        private async Task<bool> IsArticleAlreadyDeductedForParentSaleAsync(Dispatch dispatch, int articleId)
        {
            try
            {
                if (!dispatch.ServiceOrderId.HasValue) return false;
                var serviceOrder = await _db.ServiceOrders.FindAsync(dispatch.ServiceOrderId);
                if (serviceOrder == null || string.IsNullOrEmpty(serviceOrder.SaleId)) return false;

                var saleRefId = serviceOrder.SaleId;
                // Sale deductions store ReferenceId as "{saleId}:{itemId}" (per-line
                // idempotency). Legacy rows may still be the bare saleId, so accept
                // either shape. Prefix match is safe because saleId is numeric and
                // the separator ':' cannot appear in a bare id.
                var prefix = saleRefId + ":";
                return await _db.StockTransactions.AnyAsync(t =>
                    t.ArticleId == articleId
                    && t.TransactionType == "sale_deduction"
                    && t.ReferenceType == "sale"
                    && (t.ReferenceId == saleRefId || t.ReferenceId!.StartsWith(prefix)));
            }
            catch (Exception ex)
            {
                // Fail CLOSED: a lookup error means we cannot tell whether the parent
                // sale already took these goods out of inventory. Deducting anyway
                // would risk double-counting real stock, so abort the material line
                // instead and let the caller compensate + surface the error.
                _logger.LogError(ex, "Failed to resolve parent sale for dispatch {DispatchId} while checking stock idempotency", dispatch.Id);
                throw new InvalidOperationException(
                    "dispatches.material.stockCheckFailed: could not verify whether the parent sale already deducted this article; material not recorded.",
                    ex);
            }
        }

        public async Task<DispatchDto> StartDispatchAsync(int dispatchId, StartDispatchDto dto, string userId)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");

            var oldStatus = d.Status;
            d.Status = "in_progress";
            d.ActualStartTime = dto.ActualStartTime;
            d.ModifiedDate = DateTime.UtcNow;
            d.ModifiedBy = userId;
            await _db.SaveChangesAsync();

            // Trigger workflow automation for status change to in_progress
            if (oldStatus != "in_progress" && _workflowTriggerService != null)
            {
                try
                {
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "dispatch",
                        dispatchId,
                        oldStatus ?? "",
                        "in_progress",
                        userId,
                        new { dispatchId, dispatchNumber = d.DispatchNumber, jobId = d.JobId }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to trigger workflow for dispatch {DispatchId} start", dispatchId);
                }
            }

            // Domain cascade: guaranteed regardless of workflow configuration.
            if (oldStatus != "in_progress")
            {
                await ApplyDispatchCascadeAsync(d, oldStatus, "in_progress", userId);
            }



            var nameMap = await GetTechnicianNameMapForDispatchAsync(d.Id);
            return DispatchMapping.ToDto(d, nameMap);
        }

        public async Task<DispatchDto> CompleteDispatchAsync(int dispatchId, CompleteDispatchDto dto, string userId)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");

            var oldStatus = d.Status;
            d.Status = "completed";
            d.ActualEndTime = dto.ActualEndTime;
            d.CompletionPercentage = dto.CompletionPercentage;
            d.ModifiedDate = DateTime.UtcNow;
            d.ModifiedBy = userId;
            await _db.SaveChangesAsync();

            // Trigger workflow automation for completion
            // Track workflow trigger result (used for logging)
            if (oldStatus != "completed" && _workflowTriggerService != null)
            {
                try
                {
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "dispatch",
                        dispatchId,
                        oldStatus ?? "",
                        "completed",
                        userId,
                        new { dispatchId, dispatchNumber = d.DispatchNumber, jobId = d.JobId }
                    );
                    _logger.LogInformation("Workflow triggered for dispatch {DispatchId} completion", dispatchId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to trigger workflow for dispatch {DispatchId} completion", dispatchId);
                }
            }

            // Service order roll-up is applied by the domain cascade below. The
            // workflow engine above only layers optional, tenant-configured
            // automation on top; it is never required for correctness.
            if (oldStatus != "completed")
            {
                await ApplyDispatchCascadeAsync(d, oldStatus, "completed", userId);
            }

            var nameMap = await GetTechnicianNameMapForDispatchAsync(d.Id);
            return DispatchMapping.ToDto(d, nameMap);
        }

        public async Task DeleteAsync(int dispatchId, string userId)
        {
            var dispatch = await _db.Dispatches
                .Include(d => d.AssignedTechnicians)
                .Include(d => d.TimeEntries)
                .Include(d => d.Expenses)
                .Include(d => d.MaterialsUsed)
                .Include(d => d.Attachments)
                .Include(d => d.Notes)
                .Include(d => d.DispatchJobs)
                .FirstOrDefaultAsync(x => x.Id == dispatchId);

            if (dispatch == null) return;

            // Capture references before deletion
            var jobIdStr = dispatch.JobId;
            var serviceOrderId = dispatch.ServiceOrderId;

            // Collect ALL linked job IDs (from DispatchJobs join table + legacy JobId)
            var allJobIds = new HashSet<int>();
            if (dispatch.DispatchJobs != null && dispatch.DispatchJobs.Any())
            {
                foreach (var dj in dispatch.DispatchJobs)
                    allJobIds.Add(dj.JobId);
            }
            if (!string.IsNullOrEmpty(jobIdStr) && int.TryParse(jobIdStr, out int legacyJobId))
            {
                allJobIds.Add(legacyJobId);
            }

            // Soft delete all child records first
            if (dispatch.DispatchJobs != null && dispatch.DispatchJobs.Any())
            {
                foreach (var dj in dispatch.DispatchJobs) dj.IsDeleted = true;
            }
            if (dispatch.AssignedTechnicians.Any())
            {
                foreach (var dt in dispatch.AssignedTechnicians) dt.IsDeleted = true;
            }

            // Soft delete the dispatch itself
            dispatch.IsDeleted = true;
            dispatch.DeletedAt = DateTime.UtcNow;
            dispatch.DeletedBy = userId;
            
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[DISPATCH-DELETE] Soft deleted dispatch {DispatchId} (JobId: {JobId}, SO: {ServiceOrderId}, LinkedJobs: {JobCount}) by user {UserId}",
                dispatchId, jobIdStr, serviceOrderId, allJobIds.Count, userId);

            // Release the linked jobs — but only those with no other live dispatch left.
            if (allJobIds.Any())
            {
                await ReleaseJobsAfterDispatchRemovalAsync(allJobIds, dispatchId, "DISPATCH-DELETE");
            }

            // Recalculate the Service Order status based on remaining dispatches
            if (serviceOrderId.HasValue)
            {
                await RecalculateServiceOrderStatusAsync(serviceOrderId.Value, userId);
            }
        }

        /// <summary>
        /// Recalculate the parent Service Order status after a dispatch was removed, delegating the
        /// decision to <see cref="ServiceOrderStatusCalculator"/> so that deleting a dispatch and
        /// completing a dispatch can never produce two different answers for the same dispatch set.
        /// </summary>
        private async Task RecalculateServiceOrderStatusAsync(int serviceOrderId, string userId)
        {
            var serviceOrder = await _db.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null) return;

            // Remaining non-deleted dispatches for this SO (cancelled/rejected ones are excluded
            // from the completion denominator inside the calculator, not here).
            var remainingDispatches = await _db.Dispatches
                .Where(d => d.ServiceOrderId == serviceOrderId && !d.IsDeleted)
                .Select(d => d.Status)
                .ToListAsync();

            var oldStatus = serviceOrder.Status;
            var evaluation = ServiceOrderStatusCalculator.Compute(oldStatus, remainingDispatches);
            var newStatus = evaluation.Status;

            // Invoiced/closed/cancelled orders keep their status; refresh the counter only.
            if (evaluation.IsTerminal)
            {
                if (serviceOrder.CompletedDispatchCount != evaluation.CompletedDispatchCount)
                {
                    serviceOrder.CompletedDispatchCount = evaluation.CompletedDispatchCount;
                    await _db.SaveChangesAsync();
                }
                _logger.LogInformation(
                    "[DISPATCH-DELETE] SO {ServiceOrderId} is in final status '{Status}', counter refreshed only",
                    serviceOrderId, oldStatus);
                return;
            }

            if (serviceOrder.CompletedDispatchCount != evaluation.CompletedDispatchCount)
            {
                serviceOrder.CompletedDispatchCount = evaluation.CompletedDispatchCount;
                await _db.SaveChangesAsync();
            }

            if (oldStatus != newStatus)
            {
                serviceOrder.Status = newStatus;
                serviceOrder.ModifiedDate = DateTime.UtcNow;
                serviceOrder.ModifiedBy = userId;
                serviceOrder.CompletedDispatchCount = evaluation.CompletedDispatchCount;

                if (newStatus == ServiceOrderStatusCalculator.FieldWorkCompleteStatus)
                {
                    serviceOrder.TechnicallyCompletedAt ??= DateTime.UtcNow;
                    serviceOrder.ActualCompletionDate ??= DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "[DISPATCH-DELETE] SO {ServiceOrderId} status recalculated: '{OldStatus}' → '{NewStatus}' (remaining dispatches: {Count})",
                    serviceOrderId, oldStatus, newStatus, remainingDispatches.Count);


                // Trigger workflow for SO status change
                if (_workflowTriggerService != null)
                {
                    try
                    {
                        await _workflowTriggerService.TriggerStatusChangeAsync(
                            "service_order",
                            serviceOrderId,
                            oldStatus,
                            newStatus,
                            userId,
                            new { serviceOrderId, orderNumber = serviceOrder.OrderNumber }
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[DISPATCH-DELETE] Failed to trigger workflow for SO {ServiceOrderId} status change",
                            serviceOrderId);
                    }
                }
            }
        }

        // Ensure a job id (when provided) actually belongs to this dispatch so per-job
        // attribution of time/expenses/materials can't point at an unrelated job.
        private async Task ValidateJobBelongsToDispatchAsync(int dispatchId, int? serviceOrderJobId)
        {
            if (serviceOrderJobId is null) return;

            var belongs = await _db.Set<DispatchJob>()
                .AnyAsync(dj => dj.DispatchId == dispatchId && dj.JobId == serviceOrderJobId.Value && !dj.IsDeleted);
            if (belongs) return;

            // Legacy single-job dispatches store the job id on Dispatch.JobId (CSV-capable).
            var legacy = await _db.Dispatches
                .Where(x => x.Id == dispatchId)
                .Select(x => x.JobId)
                .FirstOrDefaultAsync();
            var legacyMatch = !string.IsNullOrWhiteSpace(legacy)
                && legacy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Any(p => int.TryParse(p, out var jid) && jid == serviceOrderJobId.Value);
            if (!legacyMatch)
                throw new InvalidOperationException($"Job {serviceOrderJobId} does not belong to dispatch {dispatchId}.");
        }

        public async Task<TimeEntryDto> AddTimeEntryAsync(int dispatchId, CreateTimeEntryDto dto, string userId)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");
            await ValidateJobBelongsToDispatchAsync(dispatchId, dto.ServiceOrderJobId);

            var newMinutes = (decimal)(dto.EndTime - dto.StartTime).TotalMinutes;
            if (newMinutes < 0) newMinutes = 0;

            // Denormalize InstallationId so per-installation roll-ups don't need to
            // traverse Dispatch -> Job. Prefer the dispatch's installation (installation-
            // scoped dispatch); fall back to the specific job's installation.
            int? resolvedInstallationId = d.InstallationId;
            if (resolvedInstallationId == null && dto.ServiceOrderJobId.HasValue)
            {
                resolvedInstallationId = await _db.ServiceOrderJobs
                    .Where(j => j.Id == dto.ServiceOrderJobId.Value)
                    .Select(j => j.InstallationId)
                    .FirstOrDefaultAsync();
            }

            TimeEntry te = null!;
            // Serializable tx closes the TOCTOU window between the overrun read and the insert.
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure.
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using (var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                {
                    var (plannedMin, actualMin) = await GetPlannedAndActualMinutesAsync(dispatchId, dto.ServiceOrderJobId);
                    bool willOverrun = plannedMin > 0 && (actualMin + newMinutes) > plannedMin;
                    if (willOverrun && string.IsNullOrWhiteSpace(dto.OverrunReason))
                    {
                        throw new InvalidOperationException(
                            $"Logging {newMinutes} min would exceed planned budget ({actualMin}/{plannedMin} min already logged). " +
                            "Provide 'overrunReason' to confirm.");
                    }

                    te = new TimeEntry
                    {
                        DispatchId = dispatchId,
                        ServiceOrderJobId = dto.ServiceOrderJobId,
                        InstallationId = resolvedInstallationId,
                        TechnicianId = int.TryParse(dto.TechnicianId, out var tid) ? tid : 0,
                        WorkType = dto.WorkType,
                        StartTime = dto.StartTime,
                        EndTime = dto.EndTime,
                        Duration = newMinutes,
                        Description = dto.Description,
                        CreatedDate = DateTime.UtcNow,
                        Billable = dto.Billable,
                        OverrunFlag = willOverrun,
                        OverrunReason = willOverrun ? dto.OverrunReason : null,
                    };
                    _db.TimeEntries.Add(te);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            });

            // Auto-rollup the parent ServiceOrderJob (ActualHours / ActualDuration / ActualCost / CompletionPercentage).
            await RecalculateServiceOrderJobRollupAsync(dto.ServiceOrderJobId);

            // Propagate time entry to parent Sale/Offer activities
            await PropagateTimeEntryToSaleAsync(d, te, userId);

            return new TimeEntryDto 
            { 
                Id = te.Id,
                DispatchId = te.DispatchId,
                ServiceOrderJobId = te.ServiceOrderJobId,
                TechnicianId = te.TechnicianId.ToString(),
                WorkType = te.WorkType, 
                StartTime = te.StartTime,
                EndTime = te.EndTime,
                Duration = (int)(te.Duration ?? 0), 
                Description = te.Description,
                CreatedAt = te.CreatedDate,
                Billable = te.Billable,
                OverrunFlag = te.OverrunFlag,
                OverrunReason = te.OverrunReason,
            };
        }

        /// <summary>
        /// Recompute ServiceOrderJob.ActualHours / ActualDuration / ActualCost /
        /// CompletionPercentage from live TimeEntries + Expenses + Materials whose parent
        /// Dispatch is NOT soft-deleted. Called after every add/delete of a child record so
        /// plan-vs-actual UI stays truthful without a background job.
        /// </summary>
        private async Task RecalculateServiceOrderJobRollupAsync(int? serviceOrderJobId)
        {
            if (!serviceOrderJobId.HasValue) return;
            var jobId = serviceOrderJobId.Value;
            var job = await _db.ServiceOrderJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return;

            // Cancelled dispatches must not contribute actuals — otherwise cancelling one
            // of a job's several dispatches leaves its time/expenses/materials in the roll-up.
            var totalMinutes = await (from te in _db.TimeEntries
                                      join dp in _db.Dispatches on te.DispatchId equals dp.Id
                                      where te.ServiceOrderJobId == jobId && !dp.IsDeleted && dp.Status != "cancelled" && dp.Status != "rejected"
                                      select (te.Duration ?? 0m)).SumAsync();

            var totalExp = await (from e in _db.DispatchExpenses
                                  join dp in _db.Dispatches on e.DispatchId equals dp.Id
                                  where e.ServiceOrderJobId == jobId && !dp.IsDeleted && dp.Status != "cancelled" && dp.Status != "rejected"
                                  select e.Amount).SumAsync();

            var totalMat = await (from m in _db.DispatchMaterials
                                  join dp in _db.Dispatches on m.DispatchId equals dp.Id
                                  where m.ServiceOrderJobId == jobId && !dp.IsDeleted && dp.Status != "cancelled" && dp.Status != "rejected"
                                  select m.TotalPrice).SumAsync();

            job.ActualHours = Math.Round(totalMinutes / 60m, 2);
            job.ActualDuration = (int)totalMinutes;
            job.ActualCost = totalExp + totalMat;

            // CompletionPercentage: derive from planned minutes when known, cap at 100.
            // Don't override a manual 100% completion set through the status flow.
            if (job.EstimatedDuration.HasValue && job.EstimatedDuration.Value > 0 && job.Status != "completed")
            {
                var pct = (int)Math.Min(100m, Math.Round((totalMinutes / job.EstimatedDuration.Value) * 100m));
                job.CompletionPercentage = pct;
            }
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// A job may be planned across several dispatches. When one dispatch completes, the job
        /// is only finished once every live (non-cancelled, non-rejected) dispatch covering it is
        /// completed; otherwise it stays "dispatched" because field work is still outstanding.
        /// </summary>
        private async Task MarkJobsCompletedAfterDispatchCompletionAsync(Dispatch d)
        {
            var jobIds = await _db.DispatchJobs
                .Where(dj => dj.DispatchId == d.Id && !dj.IsDeleted)
                .Select(dj => dj.JobId)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(d.JobId))
            {
                foreach (var part in d.JobId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (int.TryParse(part, out var parsed)) jobIds.Add(parsed);
            }

            var ids = jobIds.Distinct().ToList();
            if (ids.Count == 0) return;

            // Jobs that still have an unfinished live dispatch — join table…
            var pending = await (from dj in _db.DispatchJobs
                                 join dp in _db.Dispatches on dj.DispatchId equals dp.Id
                                 where !dj.IsDeleted && !dp.IsDeleted
                                       && dp.Status != "cancelled" && dp.Status != "rejected"
                                       && dp.Status != "completed" && dp.Status != "technically_completed"
                                       && ids.Contains(dj.JobId)
                                 select dj.JobId).Distinct().ToListAsync();

            // …and legacy single-job dispatches.
            var idStrings = ids.Select(i => i.ToString()).ToList();
            var legacyPending = await _db.Dispatches
                .Where(dp => !dp.IsDeleted
                             && dp.Status != "cancelled" && dp.Status != "rejected"
                             && dp.Status != "completed" && dp.Status != "technically_completed"
                             && dp.JobId != null && idStrings.Contains(dp.JobId))
                .Select(dp => dp.JobId!)
                .ToListAsync();
            foreach (var s in legacyPending)
                if (int.TryParse(s, out var parsed)) pending.Add(parsed);

            var pendingSet = pending.ToHashSet();

            var jobs = await _db.ServiceOrderJobs.Where(j => ids.Contains(j.Id)).ToListAsync();
            var closed = new List<int>();
            foreach (var job in jobs)
            {
                var js = (job.Status ?? string.Empty).ToLowerInvariant();
                if (js == "completed" || js == "cancelled") continue;
                if (pendingSet.Contains(job.Id)) continue; // more field work still planned
                job.Status = "completed";
                job.CompletionPercentage = 100;
                closed.Add(job.Id);
            }

            if (closed.Count > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation(
                    "[DISPATCH-COMPLETE] Dispatch {DispatchId} completed → jobs marked completed: [{JobIds}]",
                    d.Id, string.Join(", ", closed));
            }
        }


        /// <summary>
        /// Multiple concurrent dispatches per job is a supported workflow, so removing ONE
        /// dispatch must never blind-reset the job's actuals — the work logged through the
        /// job's other live dispatches would vanish. For each job we check whether any other
        /// non-deleted, non-cancelled dispatch still covers it:
        ///   - siblings remain  -> keep the job scheduled, recompute actuals from what's left
        ///   - no siblings left -> legacy reset back to the unassigned queue
        /// Terminal jobs (completed / cancelled) are never touched.
        /// </summary>
        private async Task ReleaseJobsAfterDispatchRemovalAsync(IEnumerable<int> jobIds, int removedDispatchId, string tag)
        {
            var ids = jobIds.Distinct().ToList();
            if (ids.Count == 0) return;

            // Jobs still covered by another live dispatch — via the join table…
            var jobsWithSiblings = await (from dj in _db.DispatchJobs
                                          join dp in _db.Dispatches on dj.DispatchId equals dp.Id
                                          where !dj.IsDeleted && !dp.IsDeleted
                                                && dp.Id != removedDispatchId
                                                && dp.Status != "cancelled" && dp.Status != "rejected"
                                                && ids.Contains(dj.JobId)
                                          select dj.JobId).Distinct().ToListAsync();

            // …and via the legacy single-job Dispatch.JobId column.
            var idStrings = ids.Select(i => i.ToString()).ToList();
            var legacySiblings = await _db.Dispatches
                .Where(dp => !dp.IsDeleted && dp.Id != removedDispatchId && dp.Status != "cancelled" && dp.Status != "rejected"
                             && dp.JobId != null && idStrings.Contains(dp.JobId))
                .Select(dp => dp.JobId!)
                .ToListAsync();
            foreach (var s in legacySiblings)
                if (int.TryParse(s, out var parsed)) jobsWithSiblings.Add(parsed);

            var siblingSet = jobsWithSiblings.ToHashSet();

            var jobs = await _db.ServiceOrderJobs.Where(j => ids.Contains(j.Id)).ToListAsync();
            var released = new List<int>();
            foreach (var job in jobs)
            {
                var js = (job.Status ?? "").ToLowerInvariant();
                if (js == "completed" || js == "cancelled") continue;
                if (siblingSet.Contains(job.Id)) continue; // still dispatched elsewhere
                job.Status = "unscheduled";
                job.CompletionPercentage = 0;
                job.ActualDuration = null;
                job.ActualCost = 0;
                job.CompletedDate = null;
                job.UpdatedAt = DateTime.UtcNow;
                released.Add(job.Id);
            }
            await _db.SaveChangesAsync();

            // Jobs that still have live dispatches keep their status; their actuals are
            // recomputed from the remaining dispatches instead of being zeroed.
            foreach (var jobId in ids.Where(i => siblingSet.Contains(i)))
                await RecalculateServiceOrderJobRollupAsync(jobId);

            _logger.LogInformation(
                "[{Tag}] Dispatch {DispatchId} removed: {ReleasedCount} job(s) released to 'unscheduled' ({Released}); {KeptCount} job(s) kept with other live dispatches and recalculated.",
                tag, removedDispatchId, released.Count, string.Join(", ", released), siblingSet.Count);
        }

        /// <summary>
        /// Planned vs already-logged actual minutes for the soft-cap overrun check.
        /// When <paramref name="serviceOrderJobId"/> is supplied (multi-job dispatch),
        /// the budget and actuals are scoped to THAT job, so each job is capped against
        /// its own plan. Otherwise the whole dispatch (all its jobs) is summed.
        /// </summary>
        private async Task<(decimal plannedMinutes, decimal actualMinutes)> GetPlannedAndActualMinutesAsync(int dispatchId, int? serviceOrderJobId = null)
        {
            List<int> jobIds;
            if (serviceOrderJobId.HasValue)
            {
                jobIds = new List<int> { serviceOrderJobId.Value };
            }
            else
            {
                jobIds = await _db.Set<DispatchJob>()
                    .Where(dj => dj.DispatchId == dispatchId && !dj.IsDeleted)
                    .Select(dj => dj.JobId)
                    .Distinct()
                    .ToListAsync();

                // G5 fallback: legacy dispatches have no DispatchJob rows — parse Dispatch.JobId (CSV-capable string).
                if (jobIds.Count == 0)
                {
                    var legacy = await _db.Dispatches
                        .Where(x => x.Id == dispatchId)
                        .Select(x => x.JobId)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(legacy))
                    {
                        foreach (var part in legacy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            if (int.TryParse(part, out var jid)) jobIds.Add(jid);
                    }
                    if (jobIds.Count == 0) return (0, 0);
                }
            }

            var planned = await _db.Set<MyApi.Modules.Planning.Models.PlannedLineEntry>()
                .Where(p => p.ParentType == "service_order_job" && jobIds.Contains(p.ParentId) && p.Kind == "time")
                .Select(p => new { p.PlannedMinutes, p.TechnicianCount })
                .ToListAsync();
            decimal plannedTotal = planned.Sum(p => (decimal)((p.PlannedMinutes ?? 0) * (p.TechnicianCount ?? 1)));

            decimal actualTotal = await _db.TimeEntries
                .Where(t => t.DispatchId == dispatchId && t.Duration != null
                    && (serviceOrderJobId == null || t.ServiceOrderJobId == serviceOrderJobId))
                .SumAsync(t => (decimal?)t.Duration ?? 0m);
            return (plannedTotal, actualTotal);
        }

        /// <summary>
        /// Planned vs actual amount for an expense type, for the soft-cap overrun check.
        /// When <paramref name="serviceOrderJobId"/> is supplied (multi-job dispatch) the
        /// budget and actuals are scoped to THAT job; otherwise the whole dispatch is summed.
        /// </summary>
        private async Task<(decimal plannedAmount, decimal actualAmount)> GetPlannedAndActualExpenseAsync(int dispatchId, string expenseType, int? serviceOrderJobId = null)
        {
            List<int> jobIds;
            if (serviceOrderJobId.HasValue)
            {
                jobIds = new List<int> { serviceOrderJobId.Value };
            }
            else
            {
                jobIds = await _db.Set<DispatchJob>()
                    .Where(dj => dj.DispatchId == dispatchId && !dj.IsDeleted)
                    .Select(dj => dj.JobId)
                    .Distinct()
                    .ToListAsync();
                if (jobIds.Count == 0) return (0, 0);
            }

            var et = (expenseType ?? "").ToLower();
            decimal plannedTotal = await _db.Set<MyApi.Modules.Planning.Models.PlannedLineEntry>()
                .Where(p => p.ParentType == "service_order_job"
                    && jobIds.Contains(p.ParentId)
                    && p.Kind == "expense"
                    && p.ExpenseType != null
                    && p.ExpenseType.ToLower() == et)
                .SumAsync(p => (decimal?)p.PlannedAmount ?? 0m);

            decimal actualTotal = await _db.DispatchExpenses
                .Where(e => e.DispatchId == dispatchId && e.ExpenseType.ToLower() == et
                    && (serviceOrderJobId == null || e.ServiceOrderJobId == serviceOrderJobId))
                .SumAsync(e => (decimal?)e.Amount ?? 0m);
            return (plannedTotal, actualTotal);
        }

        public async Task<IEnumerable<TimeEntryDto>> GetTimeEntriesAsync(int dispatchId)
        {
            var items = await _db.TimeEntries.AsNoTracking().Where(t => t.DispatchId == dispatchId).ToListAsync();
            return items.Select(t => new TimeEntryDto 
            { 
                Id = t.Id,
                DispatchId = t.DispatchId,
                ServiceOrderJobId = t.ServiceOrderJobId,
                TechnicianId = t.TechnicianId.ToString(),
                WorkType = t.WorkType, 
                StartTime = t.StartTime,
                EndTime = t.EndTime,
                Duration = (int)(t.Duration ?? 0), 
                Description = t.Description,
                CreatedAt = t.CreatedDate,
                Billable = t.Billable,
                OverrunFlag = t.OverrunFlag,
                OverrunReason = t.OverrunReason,
            }).ToList();
        }

        public async Task ApproveTimeEntryAsync(int dispatchId, int timeEntryId, ApproveTimeEntryDto dto, string userId)
        {
            var te = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == timeEntryId && t.DispatchId == dispatchId);
            if (te == null) throw new KeyNotFoundException("Time entry not found");
            // Status column doesn't exist in database - approval is tracked elsewhere or not needed
            await _db.SaveChangesAsync();
        }

        public async Task<TimeEntryDto> UpdateTimeEntryAsync(int dispatchId, int timeEntryId, UpdateTimeEntryDto dto, string userId)
        {
            var te = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == timeEntryId && t.DispatchId == dispatchId);
            if (te == null) throw new KeyNotFoundException("Time entry not found");

            if (dto.WorkType != null) te.WorkType = dto.WorkType;
            if (dto.StartTime.HasValue) te.StartTime = dto.StartTime.Value;
            if (dto.EndTime.HasValue) te.EndTime = dto.EndTime.Value;
            if (dto.Description != null) te.Description = dto.Description;
            if (dto.Billable.HasValue) te.Billable = dto.Billable.Value;
            
            // Recalculate duration if times changed
            if (te.EndTime.HasValue)
            {
                te.Duration = (decimal)(te.EndTime.Value - te.StartTime).TotalMinutes;
            }

            await _db.SaveChangesAsync();

            return new TimeEntryDto 
            { 
                Id = te.Id,
                DispatchId = te.DispatchId,
                ServiceOrderJobId = te.ServiceOrderJobId,
                TechnicianId = te.TechnicianId.ToString(),
                WorkType = te.WorkType, 
                StartTime = te.StartTime,
                EndTime = te.EndTime,
                Duration = (int)(te.Duration ?? 0), 
                Description = te.Description,
                CreatedAt = te.CreatedDate,
                Billable = te.Billable,
                OverrunFlag = te.OverrunFlag,
                OverrunReason = te.OverrunReason,
            };
        }

        public async Task DeleteTimeEntryAsync(int dispatchId, int timeEntryId, string userId)
        {
            var te = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == timeEntryId && t.DispatchId == dispatchId);
            if (te == null) throw new KeyNotFoundException("Time entry not found");

            var jobIdForRollup = te.ServiceOrderJobId;
            _db.TimeEntries.Remove(te);
            await _db.SaveChangesAsync();

            await RecalculateServiceOrderJobRollupAsync(jobIdForRollup);
        }

        public async Task<ExpenseDto> AddExpenseAsync(int dispatchId, CreateExpenseDto dto, string userId)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");
            await ValidateJobBelongsToDispatchAsync(dispatchId, dto.ServiceOrderJobId);

            int? expInstallationId = d.InstallationId;
            if (expInstallationId == null && dto.ServiceOrderJobId.HasValue)
            {
                expInstallationId = await _db.ServiceOrderJobs
                    .Where(j => j.Id == dto.ServiceOrderJobId.Value)
                    .Select(j => j.InstallationId)
                    .FirstOrDefaultAsync();
            }

            Expense exp = null!;
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure.
            var expStrategy = _db.Database.CreateExecutionStrategy();
            await expStrategy.ExecuteAsync(async () =>
            {
                using (var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                {
                    var (plannedAmt, actualAmt) = await GetPlannedAndActualExpenseAsync(dispatchId, dto.Type, dto.ServiceOrderJobId);
                    bool willOverrun = plannedAmt > 0 && (actualAmt + dto.Amount) > plannedAmt;
                    if (willOverrun && string.IsNullOrWhiteSpace(dto.OverrunReason))
                    {
                        throw new InvalidOperationException(
                            $"Expense of {dto.Amount} would exceed planned '{dto.Type}' budget ({actualAmt}/{plannedAmt}). " +
                            "Provide 'overrunReason' to confirm.");
                    }

                    exp = new Expense
                    {
                        DispatchId = dispatchId,
                        ServiceOrderJobId = dto.ServiceOrderJobId,
                        InstallationId = expInstallationId,
                        ExpenseType = dto.Type,
                        TechnicianId = dto.TechnicianId,
                        Amount = dto.Amount,
                        // Persist the declared currency so downstream invoice validation
                        // can reject cross-currency lines. Falls back to null (interpreted
                        // as the sale's currency) when the caller omits it.
                        Currency = string.IsNullOrWhiteSpace(dto.Currency) ? null : dto.Currency.Trim().ToUpperInvariant(),
                        Description = dto.Description,
                        ExpenseDate = dto.Date ?? DateTime.UtcNow,
                        RecordedBy = userId,
                        CreatedDate = DateTime.UtcNow,
                        OverrunFlag = willOverrun,
                        OverrunReason = willOverrun ? dto.OverrunReason : null,
                    };
                    _db.DispatchExpenses.Add(exp);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            });

            await RecalculateServiceOrderJobRollupAsync(dto.ServiceOrderJobId);

            // Propagate expense to parent Sale/Offer activities
            await PropagateExpenseToSaleAsync(d, exp, userId);

            return new ExpenseDto 
            { 
                Id = exp.Id, 
                DispatchId = exp.DispatchId,
                ServiceOrderJobId = exp.ServiceOrderJobId,
                TechnicianId = exp.TechnicianId ?? dto.TechnicianId ?? userId,
                Type = exp.ExpenseType, 
                Amount = exp.Amount,
                // Round-trip the persisted currency so the client sees exactly what was stored.
                Currency = exp.Currency,
                Description = exp.Description,
                Date = exp.ExpenseDate,
                Status = "pending", 
                CreatedAt = exp.CreatedDate,
                OverrunFlag = exp.OverrunFlag,
                OverrunReason = exp.OverrunReason,
            };
        }

        public async Task<IEnumerable<ExpenseDto>> GetExpensesAsync(int dispatchId)
        {
            var items = await _db.DispatchExpenses.AsNoTracking().Where(e => e.DispatchId == dispatchId).ToListAsync();
            return items.Select(e => new ExpenseDto 
            { 
                Id = e.Id, 
                DispatchId = e.DispatchId,
                ServiceOrderJobId = e.ServiceOrderJobId,
                TechnicianId = e.TechnicianId ?? e.RecordedBy,
                Type = e.ExpenseType, 
                Amount = e.Amount,
                Currency = e.Currency,
                Description = e.Description,
                Date = e.ExpenseDate,
                Status = "pending", 
                CreatedAt = e.CreatedDate,
                OverrunFlag = e.OverrunFlag,
                OverrunReason = e.OverrunReason,
            }).ToList();
        }

        public async Task ApproveExpenseAsync(int dispatchId, int expenseId, ApproveExpenseDto dto, string userId)
        {
            var exp = await _db.DispatchExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.DispatchId == dispatchId);
            if (exp == null) throw new KeyNotFoundException("Expense not found");
            await _db.SaveChangesAsync();
        }

        public async Task<ExpenseDto> UpdateExpenseAsync(int dispatchId, int expenseId, UpdateExpenseDto dto, string userId)
        {
            var exp = await _db.DispatchExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.DispatchId == dispatchId);
            if (exp == null) throw new KeyNotFoundException("Expense not found");

            if (dto.Type != null) exp.ExpenseType = dto.Type;
            if (dto.TechnicianId != null) exp.TechnicianId = dto.TechnicianId;
            if (dto.Amount.HasValue) exp.Amount = dto.Amount.Value;
            if (dto.Description != null) exp.Description = dto.Description;
            if (dto.Date.HasValue) exp.ExpenseDate = dto.Date.Value;
            if (!string.IsNullOrWhiteSpace(dto.Currency))
                exp.Currency = dto.Currency.Trim().ToUpperInvariant();

            await _db.SaveChangesAsync();

            return new ExpenseDto 
            { 
                Id = exp.Id,
                DispatchId = exp.DispatchId,
                ServiceOrderJobId = exp.ServiceOrderJobId,
                TechnicianId = exp.RecordedBy,
                Type = exp.ExpenseType, 
                Amount = exp.Amount,
                Currency = exp.Currency,
                Description = exp.Description,
                Date = exp.ExpenseDate,
                Status = "pending", 
                CreatedAt = exp.CreatedDate 
            };
        }

        public async Task DeleteExpenseAsync(int dispatchId, int expenseId, string userId)
        {
            var exp = await _db.DispatchExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.DispatchId == dispatchId);
            if (exp == null) throw new KeyNotFoundException("Expense not found");

            var jobIdForRollup = exp.ServiceOrderJobId;
            _db.DispatchExpenses.Remove(exp);
            await _db.SaveChangesAsync();

            await RecalculateServiceOrderJobRollupAsync(jobIdForRollup);
        }

        public async Task<MaterialDto> AddMaterialUsageAsync(int dispatchId, CreateMaterialUsageDto dto, string userId)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");
            await ValidateJobBelongsToDispatchAsync(dispatchId, dto.ServiceOrderJobId);

            // Get a valid article ID if not provided or invalid
            int? articleId = null;
            if (!string.IsNullOrEmpty(dto.ArticleId) && int.TryParse(dto.ArticleId, out var aid))
            {
                var articleExists = await _db.Articles.AnyAsync(a => a.Id == aid);
                if (articleExists)
                    articleId = aid;
            }
            
            // If no valid article, try to find any article
            if (articleId == null)
            {
                var anyArticle = await _db.Articles.FirstOrDefaultAsync();
                articleId = anyArticle?.Id;
            }

            // Determine unit from article or DTO
            var unitValue = dto.Unit ?? "piece";
            if (string.IsNullOrEmpty(dto.Unit) && articleId.HasValue)
            {
                var articleForUnit = await _db.Articles.FirstOrDefaultAsync(a => a.Id == articleId.Value);
                if (articleForUnit != null && !string.IsNullOrEmpty(articleForUnit.Unit))
                    unitValue = articleForUnit.Unit;
            }

            int? matInstallationId = d.InstallationId;
            if (matInstallationId == null && dto.ServiceOrderJobId.HasValue)
            {
                matInstallationId = await _db.ServiceOrderJobs
                    .Where(j => j.Id == dto.ServiceOrderJobId.Value)
                    .Select(j => j.InstallationId)
                    .FirstOrDefaultAsync();
            }

            var lineTotal = dto.Quantity * (dto.UnitPrice ?? 0);
            MaterialUsage mat = null!;
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure.
            var matStrategy = _db.Database.CreateExecutionStrategy();
            await matStrategy.ExecuteAsync(async () =>
            {
                using (var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                {
                    // Soft-cap overrun: mirror TimeEntry/Expense. Compares against planned material
                    // total on the job (Kind="material"). NULL / zero planned = no cap.
                    var (plannedMatAmt, actualMatAmt) = await GetPlannedAndActualMaterialAsync(dispatchId, dto.ServiceOrderJobId);
                    bool willOverrun = plannedMatAmt > 0 && (actualMatAmt + lineTotal) > plannedMatAmt;
                    if (willOverrun && string.IsNullOrWhiteSpace(dto.OverrunReason))
                    {
                        throw new InvalidOperationException(
                            $"Material of {lineTotal:0.##} would exceed planned material budget ({actualMatAmt:0.##}/{plannedMatAmt:0.##}). " +
                            "Provide 'overrunReason' to confirm.");
                    }

                    mat = new MaterialUsage
                    {
                        DispatchId = dispatchId,
                        ServiceOrderJobId = dto.ServiceOrderJobId,
                        InstallationId = matInstallationId,
                        ArticleId = articleId,
                        Quantity = dto.Quantity,
                        Description = dto.Description ?? string.Empty,
                        UnitPrice = dto.UnitPrice ?? 0,
                        TotalPrice = lineTotal,
                        RecordedBy = userId,
                        UsedDate = DateTime.UtcNow,
                        Unit = unitValue,
                        OverrunFlag = willOverrun,
                        OverrunReason = willOverrun ? dto.OverrunReason : null,
                        ApprovalStatus = "pending",
                    };
                    _db.DispatchMaterials.Add(mat);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            });

            // --- Task 2: deduct stock when the line references a real article ---
            // Free-text materials (no ArticleId) skip inventory. The stock service
            // manages its own transaction (execution strategy) so it must run AFTER
            // the material tx commits. On failure we compensate by deleting the
            // material row so caller sees an atomic error.
            if (articleId.HasValue && dto.Quantity > 0 && _stockTransactionService != null)
            {
                // Cross-service idempotency: if the parent Sale of this dispatch
                // has already deducted stock for this exact article at sale close,
                // do not deduct again here — that would double-count the same
                // physical goods. Sale-level deduction wins because it covers the
                // full sold quantity up front; any additional (unplanned) material
                // on top of the sale would use a different article or a free-text
                // line and therefore fall through to the deduction below.
                bool coveredBySale = await IsArticleAlreadyDeductedForParentSaleAsync(d, articleId.Value);
                if (coveredBySale)
                {
                    _logger.LogInformation(
                        "Skipping dispatch stock deduction for material {MaterialId} article {ArticleId}: already deducted by parent Sale of dispatch {DispatchId}",
                        mat.Id, articleId.Value, dispatchId);
                }
                else
                {
                try
                {
                    await _stockTransactionService.CreateTransactionAsync(new CreateStockTransactionDto
                    {
                        ArticleId = articleId.Value,
                        TransactionType = "remove",
                        Quantity = dto.Quantity,
                        Reason = "Material used on dispatch",
                        ReferenceType = "dispatch_material",
                        ReferenceId = mat.Id.ToString(),
                        ReferenceNumber = d.DispatchNumber,
                        Notes = dto.Description,
                    }, userId);
                }
                catch (Exception ex)
                {
                    // Compensate: remove the material row so the API call is atomic.
                    try
                    {
                        _db.DispatchMaterials.Remove(mat);
                        await _db.SaveChangesAsync();
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Failed to compensate MaterialUsage {MaterialId} after stock deduction error", mat.Id);
                    }

                    if (ex is InvalidOperationException iox && ex.Message.Contains("Insufficient stock", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"dispatches.material.insufficientStock: {ex.Message}", ex);
                    }
                    throw;
                }
                }
            }

            await RecalculateServiceOrderJobRollupAsync(dto.ServiceOrderJobId);

            // Propagate material usage to parent Sale/Offer activities
            await PropagateMaterialToSaleAsync(d, mat, userId);

            // Also record on the Dispatch's own audit stream (SystemLog) so the
            // dispatch drawer's Activity tab shows the material line, matching
            // the requirement that "every action" be logged.
            if (_activityLogger != null)
            {
                await _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
                {
                    Module = "Dispatches",
                    Action = "material_added",
                    EntityType = "MaterialUsage",
                    EntityId = mat.Id.ToString(),
                    ParentEntityType = null,
                    ParentEntityId = null,
                    UserId = userId,
                    Message = $"Material added to dispatch {d.DispatchNumber}: {mat.Description ?? mat.ArticleId?.ToString()} (qty {mat.Quantity})",
                    Details = System.Text.Json.JsonSerializer.Serialize(new { dispatchId, mat.Id, mat.ArticleId, mat.Quantity, mat.UnitPrice, mat.TotalPrice }),
                });
            }


            return new MaterialDto
            {
                Id = mat.Id,
                DispatchId = mat.DispatchId,
                ServiceOrderJobId = mat.ServiceOrderJobId,
                TechnicianId = dto.UsedBy ?? userId,
                ArticleId = mat.ArticleId?.ToString(),
                Description = mat.Description,
                Quantity = (int)mat.Quantity,
                UnitPrice = mat.UnitPrice,
                TotalPrice = mat.TotalPrice,
                Status = mat.ApprovalStatus,
                CreatedAt = mat.UsedDate,
                Unit = mat.Unit,
                OverrunFlag = mat.OverrunFlag,
                OverrunReason = mat.OverrunReason,
                ApprovalStatus = mat.ApprovalStatus,
                ApprovedBy = mat.ApprovedBy,
                ApprovedAt = mat.ApprovedAt,
                RejectionReason = mat.RejectionReason,
            };
        }

        /// <summary>
        /// Planned vs actual material spend for the soft-cap overrun check. Mirrors
        /// <see cref="GetPlannedAndActualExpenseAsync"/> but for PlannedLineEntry.Kind = "material".
        /// </summary>
        private async Task<(decimal plannedAmount, decimal actualAmount)> GetPlannedAndActualMaterialAsync(int dispatchId, int? serviceOrderJobId = null)
        {
            List<int> jobIds;
            if (serviceOrderJobId.HasValue)
            {
                jobIds = new List<int> { serviceOrderJobId.Value };
            }
            else
            {
                jobIds = await _db.Set<DispatchJob>()
                    .Where(dj => dj.DispatchId == dispatchId && !dj.IsDeleted)
                    .Select(dj => dj.JobId)
                    .Distinct()
                    .ToListAsync();
                if (jobIds.Count == 0) return (0, 0);
            }

            decimal plannedTotal = await _db.Set<MyApi.Modules.Planning.Models.PlannedLineEntry>()
                .Where(p => p.ParentType == "service_order_job"
                    && jobIds.Contains(p.ParentId)
                    && p.Kind == "material")
                .SumAsync(p => (decimal?)p.PlannedAmount ?? 0m);

            decimal actualTotal = await _db.DispatchMaterials
                .Where(m => m.DispatchId == dispatchId
                    && (serviceOrderJobId == null || m.ServiceOrderJobId == serviceOrderJobId))
                .SumAsync(m => (decimal?)m.TotalPrice ?? 0m);
            return (plannedTotal, actualTotal);
        }

        public async Task<IEnumerable<MaterialDto>> GetMaterialsAsync(int dispatchId)
        {
            var items = await _db.DispatchMaterials.Where(m => m.DispatchId == dispatchId).ToListAsync();
            return items.Select(m => new MaterialDto
            {
                Id = m.Id,
                DispatchId = m.DispatchId,
                ServiceOrderJobId = m.ServiceOrderJobId,
                TechnicianId = m.RecordedBy,
                ArticleId = m.ArticleId?.ToString(),
                Description = m.Description,
                Quantity = (int)m.Quantity,
                UnitPrice = m.UnitPrice,
                TotalPrice = m.TotalPrice,
                Status = m.ApprovalStatus,
                CreatedAt = m.UsedDate,
                Unit = m.Unit,
                OverrunFlag = m.OverrunFlag,
                OverrunReason = m.OverrunReason,
                ApprovalStatus = m.ApprovalStatus,
                ApprovedBy = m.ApprovedBy,
                ApprovedAt = m.ApprovedAt,
                RejectionReason = m.RejectionReason,
            }).ToList();
        }

        /// <summary>
        /// Number of completed deduct/restore cycles already written for a material line.
        /// Cycle 0 uses the bare material id as reference (what AddMaterialUsageAsync wrote);
        /// every later reject → re-approve round trip gets its own suffixed reference so the
        /// ledger's idempotency guard does not swallow a legitimate second movement.
        /// </summary>
        private async Task<int> GetMaterialStockCycleAsync(int materialId)
        {
            var prefix = materialId.ToString();
            return await _db.StockTransactions.CountAsync(t =>
                t.ReferenceType == "dispatch_material"
                && t.TransactionType == "return"
                && (t.ReferenceId == prefix || t.ReferenceId!.StartsWith(prefix + "#return")));
        }

        private static string MaterialStockRef(int materialId, string kind, int cycle)
            => cycle == 0 ? materialId.ToString() : $"{materialId}#{kind}{cycle}";

        /// <summary>
        /// Puts a rejected material line's goods back on the shelf (and takes them out
        /// again when the same line is later approved). Without this, rejecting a
        /// material silently kept the stock deducted forever.
        /// </summary>
        private async Task SyncMaterialStockForApprovalAsync(MaterialUsage m, string dispatchNumber, bool approved, string userId)
        {
            if (_stockTransactionService == null || m.ArticleId == null || m.Quantity <= 0) return;

            try
            {
                var cycle = await GetMaterialStockCycleAsync(m.Id);

                if (!approved)
                {
                    // Only give back what this dispatch actually took out.
                    var deductRef = MaterialStockRef(m.Id, "deduct", cycle);
                    var wasDeducted = await _db.StockTransactions.AnyAsync(t =>
                        t.ArticleId == m.ArticleId!.Value
                        && t.ReferenceType == "dispatch_material"
                        && t.ReferenceId == deductRef
                        && t.TransactionType == "remove");
                    if (!wasDeducted) return;

                    await _stockTransactionService.CreateTransactionAsync(new CreateStockTransactionDto
                    {
                        ArticleId = m.ArticleId!.Value,
                        TransactionType = "return",
                        Quantity = m.Quantity,
                        Reason = "Dispatch material rejected - returned to stock",
                        ReferenceType = "dispatch_material",
                        ReferenceId = MaterialStockRef(m.Id, "return", cycle),
                        ReferenceNumber = dispatchNumber,
                        Notes = m.Description,
                    }, userId);
                }
                else
                {
                    // Re-approval after a rejection: take the goods out again.
                    if (cycle == 0) return; // never restored → still deducted, nothing to do
                    var deductRef = MaterialStockRef(m.Id, "deduct", cycle);
                    var alreadyDeducted = await _db.StockTransactions.AnyAsync(t =>
                        t.ArticleId == m.ArticleId!.Value
                        && t.ReferenceType == "dispatch_material"
                        && t.ReferenceId == deductRef
                        && t.TransactionType == "remove");
                    if (alreadyDeducted) return;

                    await _stockTransactionService.CreateTransactionAsync(new CreateStockTransactionDto
                    {
                        ArticleId = m.ArticleId!.Value,
                        TransactionType = "remove",
                        Quantity = m.Quantity,
                        Reason = "Dispatch material re-approved - deducted from stock",
                        ReferenceType = "dispatch_material",
                        ReferenceId = deductRef,
                        ReferenceNumber = dispatchNumber,
                        Notes = m.Description,
                    }, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[DISPATCH-MATERIAL] Stock sync failed for material {MaterialId} (approved={Approved})", m.Id, approved);
            }
        }

        public async Task ApproveMaterialAsync(int dispatchId, int materialId, ApproveMaterialDto dto, string userId)
        {
            var m = await _db.DispatchMaterials.FirstOrDefaultAsync(x => x.Id == materialId && x.DispatchId == dispatchId);
            if (m == null) throw new KeyNotFoundException("Material not found");
            var previousStatus = m.ApprovalStatus;

            if (dto.Approved)
            {
                m.ApprovalStatus = "approved";
                m.ApprovedBy = userId;
                m.ApprovedAt = DateTime.UtcNow;
                m.RejectionReason = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                    throw new ArgumentException("RejectionReason is required when rejecting a material line");

                m.ApprovalStatus = "rejected";
                m.RejectionReason = dto.RejectionReason?.Trim();
                m.ApprovedBy = null;
                m.ApprovedAt = null;
            }

            await _db.SaveChangesAsync();

            // Keep inventory in step with the decision (only on an actual transition).
            if (!string.Equals(previousStatus, m.ApprovalStatus, StringComparison.OrdinalIgnoreCase))
            {
                var dispatchNumber = await _db.Dispatches
                    .Where(x => x.Id == dispatchId).Select(x => x.DispatchNumber).FirstOrDefaultAsync() ?? string.Empty;
                await SyncMaterialStockForApprovalAsync(m, dispatchNumber, dto.Approved, userId);
            }

            _logger.LogInformation(
                "Material {MaterialId} on Dispatch {DispatchId} set to {Status} by {UserId}",
                materialId, dispatchId, m.ApprovalStatus, userId);


            if (_activityLogger != null)
            {
                await _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
                {
                    Module = "Dispatches",
                    Action = dto.Approved ? "material_approved" : "material_rejected",
                    EntityType = "MaterialUsage",
                    EntityId = materialId.ToString(),
                    ParentEntityType = null,
                    ParentEntityId = null,
                    UserId = userId,
                    Message = dto.Approved
                        ? $"Material line #{materialId} approved on dispatch {dispatchId}"
                        : $"Material line #{materialId} rejected on dispatch {dispatchId}: {dto.RejectionReason}",
                    Details = dto.RejectionReason,
                });
            }
        }

        // Max size and allow-list for dispatch attachments. Anything larger or of a
        // non-listed content type is rejected before we touch storage or the DB —
        // previously there were no limits and the file bytes were silently discarded.
        private const long MaxAttachmentBytes = 25L * 1024 * 1024; // 25 MB
        private static readonly HashSet<string> AllowedAttachmentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/gif", "image/webp", "image/heic", "image/heif",
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain", "text/csv",
            "video/mp4", "video/quicktime",
        };

        public async Task<AttachmentUploadResponseDto> UploadAttachmentAsync(int dispatchId, Microsoft.AspNetCore.Http.IFormFile file, string category, string? description, double? latitude, double? longitude, string userId)
        {
            if (file == null || file.Length <= 0)
                throw new ArgumentException("File is required and must be non-empty", nameof(file));
            if (file.Length > MaxAttachmentBytes)
                throw new InvalidOperationException($"File exceeds maximum size of {MaxAttachmentBytes / (1024 * 1024)} MB");
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            if (!AllowedAttachmentTypes.Contains(contentType))
                throw new InvalidOperationException($"Content type '{contentType}' is not allowed for dispatch attachments");

            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");

            // Persist the bytes. Prefer UploadThing (used by the rest of the app) so
            // attachments are reachable from mobile/web without exposing the API host's
            // filesystem. Fall back to a per-dispatch folder under wwwroot only when
            // UploadThing is not configured.
            string storedPath;
            if (_uploadThing != null && _uploadThing.IsConfigured)
            {
                var up = await _uploadThing.UploadFileAsync(file);
                if (!up.Success || string.IsNullOrEmpty(up.FileUrl))
                    throw new InvalidOperationException(up.Error ?? "Upload failed");
                storedPath = up.FileUrl!;
            }
            else
            {
                var safeName = Path.GetFileName(file.FileName);
                var uploadsRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads", "dispatches", dispatchId.ToString());
                Directory.CreateDirectory(uploadsRoot);
                var uniqueName = $"{Guid.NewGuid():N}_{safeName}";
                var fullPath = Path.Combine(uploadsRoot, uniqueName);
                using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fs);
                }
                storedPath = $"/uploads/dispatches/{dispatchId}/{uniqueName}";
            }

            var att = new Attachment
            {
                DispatchId = dispatchId,
                FileName = file.FileName,
                FilePath = storedPath,
                FileSize = file.Length,
                ContentType = contentType,
                Category = category ?? string.Empty,
                UploadedBy = userId,
                UploadedDate = DateTime.UtcNow,
                Latitude = latitude,
                Longitude = longitude,
            };
            _db.DispatchAttachments.Add(att);
            await _db.SaveChangesAsync();

            if (_activityLogger != null)
            {
                await _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
                {
                    Module = "Dispatches",
                    Action = "attachment_uploaded",
                    EntityType = "DispatchAttachment",
                    EntityId = att.Id.ToString(),
                    ParentEntityType = null,
                    ParentEntityId = null,
                    UserId = userId,
                    Message = $"Attachment uploaded to dispatch {d.DispatchNumber}: {att.FileName} ({att.FileSize} bytes)",
                    Details = category,
                });
            }

            return new AttachmentUploadResponseDto
            {
                Id = att.Id,
                FileName = att.FileName,
                FileType = att.ContentType,
                FileSizeBytes = att.FileSize,
                Category = att.Category,
                UploadedAt = att.UploadedDate,
            };
        }

        public async Task<NoteDto> AddNoteAsync(int dispatchId, CreateNoteDto dto, string userId)
        {
            _logger.LogInformation("AddNoteAsync called by {UserId} for Dispatch {DispatchId}: {NotePreview}", userId, dispatchId, dto.Content?.Length > 200 ? dto.Content.Substring(0,200) + "..." : dto.Content);
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId && !x.IsDeleted);
            if (d == null) throw new KeyNotFoundException($"Dispatch {dispatchId} not found");

            var note = new Note
            {
                DispatchId = dispatchId,
                Content = dto.Content ?? string.Empty,
                NoteType = dto.Category ?? "general",
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            };
            _db.DispatchNotes.Add(note);
            await _db.SaveChangesAsync();

            return new NoteDto { Id = note.Id, DispatchId = note.DispatchId, Content = note.Content ?? string.Empty, Category = note.NoteType ?? "general", CreatedBy = note.CreatedBy ?? string.Empty, CreatedAt = note.CreatedDate };
        }

        public async Task<List<NoteDto>> GetNotesAsync(int dispatchId)
        {
            var notes = await _db.DispatchNotes
                .Where(n => n.DispatchId == dispatchId)
                .OrderByDescending(n => n.CreatedDate)
                .ToListAsync();

            return notes.Select(n => new NoteDto
            {
                Id = n.Id,
                DispatchId = n.DispatchId,
                Content = n.Content ?? string.Empty,
                Category = n.NoteType ?? "general",
                CreatedBy = n.CreatedBy ?? string.Empty,
                CreatedAt = n.CreatedDate
            }).ToList();
        }

        public async Task<DispatchStatisticsDto> GetStatisticsAsync(StatisticsQueryParams query)
        {
            var q = _db.Dispatches.Where(d => !d.IsDeleted);
            if (query.DateFrom.HasValue) q = q.Where(d => d.ScheduledDate >= query.DateFrom.Value);
            if (query.DateTo.HasValue) q = q.Where(d => d.ScheduledDate <= query.DateTo.Value);

            var dispatches = await q.ToListAsync();
            return new DispatchStatisticsDto
            {
                TotalDispatches = dispatches.Count,
                CompletedDispatches = dispatches.Count(d => d.Status == "completed"),
                PendingDispatches = dispatches.Count(d => d.Status == "pending" || d.Status == "planned" || d.Status == "assigned" || d.Status == "confirmed"),
                InProgressDispatches = dispatches.Count(d => d.Status == "in_progress"),
                CancelledDispatches = dispatches.Count(d => d.Status == "cancelled"),
                HighPriorityCount = dispatches.Count(d => d.Priority == "high"),
                MediumPriorityCount = dispatches.Count(d => d.Priority == "medium"),
                LowPriorityCount = dispatches.Count(d => d.Priority == "low"),
                ByStatus = dispatches.GroupBy(d => d.Status).ToDictionary(g => g.Key, g => g.Count()),
                ByPriority = dispatches.GroupBy(d => d.Priority).ToDictionary(g => g.Key, g => g.Count()),
                GeneratedAt = DateTime.UtcNow
            };
        }

        public async Task<List<MyApi.Modules.Dispatches.Models.DispatchAuditLog>> GetAuditLogsAsync(int dispatchId)
        {
            return await _db.DispatchAuditLogs
                .Where(a => a.DispatchId == dispatchId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
    }
}
