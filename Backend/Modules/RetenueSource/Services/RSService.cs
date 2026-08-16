using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.RetenueSource.DTOs;
using MyApi.Modules.RetenueSource.Models;
using MyApi.Modules.Documents.Models;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MyApi.Modules.RetenueSource.Services
{
    public class RSService : IRSService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RSService> _logger;
        private readonly IWebHostEnvironment _env;

        // Rates live in Constants/RsRates.cs — SINGLE source of truth shared with
        // SupplierInvoiceService and the TEJ operation-code table.
        private static IReadOnlyDictionary<string, decimal> RS_RATES => Constants.RsRates.ByTypeCode;

        public RSService(ApplicationDbContext db, ILogger<RSService> logger, IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Resolve the TEJ declarant (the withholder = the current company).
        /// Priority: the active tenant's own fiscal identity (Settings → Company),
        /// then a company contact carrying a Matricule Fiscal. Returns null when
        /// neither source has an MF, so callers can surface an actionable message.
        /// </summary>
        private async Task<TEJDeclarantDto?> ResolveDeclarantAsync()
        {
            var tenantId = _db.GetTenantId();
            if (tenantId > 0)
            {
                var tenant = await _db.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant != null && !string.IsNullOrWhiteSpace(tenant.TaxId))
                {
                    var addressParts = new[]
                    {
                        tenant.CompanyAddress,
                        tenant.CompanyPostalCode,
                        tenant.CompanyCity,
                        tenant.CompanyState
                    }.Where(p => !string.IsNullOrWhiteSpace(p));

                    return new TEJDeclarantDto
                    {
                        Name = string.IsNullOrWhiteSpace(tenant.CompanyName) ? "" : tenant.CompanyName,
                        TaxId = tenant.TaxId!.Trim(),
                        Address = string.Join(", ", addressParts),
                        Email = tenant.CompanyEmail,
                        Phone = tenant.CompanyPhone
                    };
                }
            }

            var settingsCompany = await _db.Contacts.AsNoTracking()
                .Where(c => !c.IsDeleted && c.Type == "company" && !string.IsNullOrEmpty(c.MatriculeFiscale))
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();
            if (settingsCompany == null) return null;

            return new TEJDeclarantDto
            {
                Name = settingsCompany.Name,
                TaxId = settingsCompany.MatriculeFiscale ?? "",
                Address = settingsCompany.Address ?? "",
                Email = settingsCompany.Email,
                Phone = settingsCompany.Phone
            };
        }

        private const string DeclarantMissingMessage =
            "No TEJ declarant configured — set your company Matricule Fiscal (Tax ID) in Settings → Company.";

        // ─── CRUD ───

        public async Task<PaginatedRSResponse> GetRSRecordsAsync(
            string? entityType, int? entityId, int? month, int? year,
            string? status, string? supplierTaxId, string? search,
            int page, int limit)
        {
            var query = _db.RSRecords.Where(r => !r.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(r => r.EntityType == entityType);
            if (entityId.HasValue)
                query = query.Where(r => r.EntityId == entityId.Value);
            if (month.HasValue)
                query = query.Where(r => r.PaymentDate.Month == month.Value);
            if (year.HasValue)
                query = query.Where(r => r.PaymentDate.Year == year.Value);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);
            if (!string.IsNullOrEmpty(supplierTaxId))
                query = query.Where(r => r.SupplierTaxId == supplierTaxId);
            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(r =>
                    r.InvoiceNumber.ToLower().Contains(s) ||
                    r.SupplierName.ToLower().Contains(s) ||
                    r.SupplierTaxId.ToLower().Contains(s));
            }

            var total = await query.CountAsync();
            var records = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return new PaginatedRSResponse
            {
                Records = records.Select(MapToDto).ToList(),
                Pagination = new RSPaginationInfo
                {
                    Page = page,
                    Limit = limit,
                    Total = total,
                    TotalPages = (int)Math.Ceiling(total / (double)limit)
                }
            };
        }

        public async Task<RSRecordDto?> GetRSRecordByIdAsync(int id)
        {
            var record = await _db.RSRecords.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            return record == null ? null : MapToDto(record);
        }

        public async Task<RSRecordDto> CreateRSRecordAsync(CreateRSRecordDto dto, string userId)
        {
            // ─── CRITICAL COMPLIANCE VALIDATIONS ───
            
            // 1. CRITICAL: Tax ID Format Validation (Matricule Fiscal)
            if (string.IsNullOrWhiteSpace(dto.SupplierTaxId))
                throw new ArgumentException("Supplier Tax ID (Matricule Fiscal) is required");
            // Real Matricule Fiscal structure (7 digits + letters + establishment code).
            // The old ^\d{10,15}$ rejected valid MFs and accepted arbitrary digit strings.
            if (!Constants.TunisianTaxId.IsValidForIdType(dto.SupplierTaxId, dto.BeneficiaireIdType ?? 1))
                throw new ArgumentException(
                    $"Invalid supplier identifier: expected a {Constants.TunisianTaxId.DescribeExpectedFormat(dto.BeneficiaireIdType ?? 1)}");

            // 2. CRITICAL: Date Validations
            if (dto.PaymentDate > DateTime.UtcNow.Date)
                throw new ArgumentException("Payment date cannot be in the future");
            if (dto.InvoiceDate > dto.PaymentDate)
                throw new ArgumentException("Invoice date cannot be after payment date");

            // 3. Standard Validations
            if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
                throw new ArgumentException("Invoice number is required");
            if (dto.InvoiceAmount <= 0)
                throw new ArgumentException("Invoice amount must be positive");
            if (dto.AmountPaid <= 0)
                throw new ArgumentException("Amount paid must be positive");
            if (!Constants.RsRates.IsKnownTypeCode(dto.RSTypeCode))
                throw new ArgumentException($"Unknown RS type code: {dto.RSTypeCode}");

            // The declared IdTypeOperation and the applied rate must agree — the DGI
            // cross-checks MontantRS against the operation's official rate.
            var opCodeForCheck = dto.OperationCode ?? Constants.TejOperationCodes.LegacyToOperationCode(dto.RSTypeCode);
            if (Constants.RsRates.IsRateMismatch(opCodeForCheck, dto.RSTypeCode))
                throw new ArgumentException(
                    $"Operation code {opCodeForCheck} declares a different rate than RS type '{dto.RSTypeCode}' ({Constants.RsRates.GetRate(dto.RSTypeCode)}%)");

            // 4. MEDIUM PRIORITY: Supplier Type & Treaty Validations
            if (dto.IsExemptByTreaty && string.IsNullOrWhiteSpace(dto.TreatyCode))
                throw new ArgumentException("Treaty code is required when exemption by treaty is claimed");

            var rsAmount = CalculateRSAmountInternal(dto.AmountPaid, dto.RSTypeCode);

            // Check for duplicates
            var duplicate = await _db.RSRecords.AnyAsync(r =>
                !r.IsDeleted &&
                r.InvoiceNumber == dto.InvoiceNumber &&
                r.PaymentDate == dto.PaymentDate &&
                r.EntityId == dto.EntityId &&
                r.EntityType == dto.EntityType);
            if (duplicate)
                throw new InvalidOperationException("Duplicate RS entry for this invoice and payment date");

            // ─── CRITICAL COMPLIANCE: Calculate Declaration Deadline ───
            // Tunisia requirement: Declaration must be filed by 20th of month following payment
            var (declarationDeadline, isOverdue, daysLate, penaltyAmount) =
                ComputeDeadlineAndPenalty(dto.PaymentDate, rsAmount);
            if (isOverdue)
                _logger.LogWarning("RS Record {Invoice} is overdue by {DaysLate} days, penalty: {Penalty} TND",
                    dto.InvoiceNumber, daysLate, penaltyAmount);

            var record = new RSRecord
            {
                EntityType = dto.EntityType,
                EntityId = dto.EntityId,
                EntityNumber = dto.EntityNumber,
                InvoiceNumber = dto.InvoiceNumber,
                InvoiceDate = dto.InvoiceDate,
                InvoiceAmount = dto.InvoiceAmount,
                PaymentDate = dto.PaymentDate,
                AmountPaid = dto.AmountPaid,
                RSAmount = rsAmount,
                RSTypeCode = dto.RSTypeCode,
                SupplierName = dto.SupplierName,
                SupplierTaxId = dto.SupplierTaxId,
                SupplierAddress = dto.SupplierAddress,
                PayerName = dto.PayerName,
                PayerTaxId = dto.PayerTaxId,
                PayerAddress = dto.PayerAddress,
                Notes = dto.Notes,
                Status = "pending",
                TEJExported = false,

                // ─── COMPLIANCE FIELDS ───
                DeclarationDeadline = declarationDeadline,
                IsOverdue = isOverdue,
                DaysLate = daysLate,
                PenaltyAmount = penaltyAmount,
                SupplierType = dto.SupplierType,
                IsExemptByTreaty = dto.IsExemptByTreaty,
                TreatyCode = dto.TreatyCode,
                TEJTransmissionStatus = "pending",

                // ─── TEJ / RiTEJ ───
                OperationCode = dto.OperationCode ?? Constants.TejOperationCodes.LegacyToOperationCode(dto.RSTypeCode),
                Cnpc = dto.Cnpc,
                PriseEnCharge = dto.PriseEnCharge,
                AnneeFacturation = dto.AnneeFacturation ?? dto.InvoiceDate.Year,
                RefCertifChezDeclarant = dto.RefCertifChezDeclarant,
                RsTvaCode = dto.RsTvaCode,
                RsTvaTaux = dto.RsTvaTaux,
                RsTvaAmount = dto.RsTvaAmount,
                MontantNetServi = Math.Round(dto.AmountPaid - rsAmount - dto.RsTvaAmount, 2),
                BeneficiaireCategorie = dto.BeneficiaireCategorie,
                BeneficiaireIsResident = dto.BeneficiaireIsResident,
                BeneficiaireIdType = dto.BeneficiaireIdType,
                BeneficiaireDateNaissance = dto.BeneficiaireDateNaissance,
                BeneficiairePaysCode = dto.BeneficiairePaysCode ?? "TN",
                Acte = dto.Acte,

                // ─── AUDIT TRAIL ───
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            _db.RSRecords.Add(record);
            await _db.SaveChangesAsync();

            _logger.LogInformation("RS record created: ID={Id}, Invoice={Invoice}, Amount={Amount}, OpCode={OpCode}",
                record.Id, record.InvoiceNumber, record.RSAmount, record.OperationCode);

            return MapToDto(record);
        }

        public async Task<RSRecordDto> UpdateRSRecordAsync(int id, UpdateRSRecordDto dto, string userId)
        {
            var record = await _db.RSRecords.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (record == null)
                throw new KeyNotFoundException("RS record not found");

            if (record.TEJExported)
                throw new InvalidOperationException("Cannot update an already-exported RS record");

            if (dto.InvoiceNumber != null) record.InvoiceNumber = dto.InvoiceNumber;
            if (dto.InvoiceDate.HasValue) record.InvoiceDate = dto.InvoiceDate.Value;
            if (dto.InvoiceAmount.HasValue) record.InvoiceAmount = dto.InvoiceAmount.Value;
            if (dto.PaymentDate.HasValue) record.PaymentDate = dto.PaymentDate.Value;
            if (dto.AmountPaid.HasValue) record.AmountPaid = dto.AmountPaid.Value;
            if (dto.RSTypeCode != null) record.RSTypeCode = dto.RSTypeCode;
            if (dto.SupplierName != null) record.SupplierName = dto.SupplierName;
            if (dto.SupplierTaxId != null) record.SupplierTaxId = dto.SupplierTaxId;
            if (dto.SupplierAddress != null) record.SupplierAddress = dto.SupplierAddress;
            if (dto.PayerName != null) record.PayerName = dto.PayerName;
            if (dto.PayerTaxId != null) record.PayerTaxId = dto.PayerTaxId;
            if (dto.PayerAddress != null) record.PayerAddress = dto.PayerAddress;
            if (dto.Notes != null) record.Notes = dto.Notes;
            if (dto.Status != null) record.Status = dto.Status;

            // Recalculate RS amount if amount or type changed
            if (dto.AmountPaid.HasValue || dto.RSTypeCode != null)
            {
                record.RSAmount = CalculateRSAmountInternal(record.AmountPaid, record.RSTypeCode);
                record.MontantNetServi = Math.Round(record.AmountPaid - record.RSAmount - record.RsTvaAmount, 2);
                var (deadline, overdue, late, penalty) = ComputeDeadlineAndPenalty(record.PaymentDate, record.RSAmount);
                record.DeclarationDeadline = deadline;
                record.IsOverdue = overdue;
                record.DaysLate = late;
                record.PenaltyAmount = penalty;
            }

            // ─── Update Compliance Fields ───
            if (dto.SupplierType != null) record.SupplierType = dto.SupplierType;
            if (dto.IsExemptByTreaty.HasValue) record.IsExemptByTreaty = dto.IsExemptByTreaty.Value;
            if (dto.TreatyCode != null) record.TreatyCode = dto.TreatyCode;
            if (dto.TEJAcceptanceNumber != null) record.TEJAcceptanceNumber = dto.TEJAcceptanceNumber;
            if (dto.TEJTransmissionStatus != null) record.TEJTransmissionStatus = dto.TEJTransmissionStatus;

            record.ModifiedAt = DateTime.UtcNow;
            record.ModifiedBy = userId;

            await _db.SaveChangesAsync();
            return MapToDto(record);
        }

        public async Task<bool> DeleteRSRecordAsync(int id)
        {
            var record = await _db.RSRecords.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (record == null) return false;
            if (record.TEJExported)
                throw new InvalidOperationException("Cannot delete an already-exported RS record");

            // Soft delete: RS records are fiscal-declaration data and must stay
            // auditable. Also unlink any supplier invoice pointing at it so the
            // invoice can be re-declared cleanly.
            record.IsDeleted = true;
            record.DeletedAt = DateTime.UtcNow;
            record.Status = "cancelled";
            var linked = await _db.SupplierInvoices.Where(i => i.RsRecordId == record.Id).ToListAsync();
            foreach (var inv in linked)
            {
                inv.RsRecordId = null;
                inv.TejSynced = false;
                inv.TejSyncStatus = "not_synced";
            }
            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Tunisia: the declaration is due by the 20th of the month following payment;
        /// late filing carries 5% of the withheld amount per started month.
        /// Shared by every creation path so penalties are never silently zero.
        /// </summary>
        private static (DateTime deadline, bool isOverdue, int daysLate, decimal penalty)
            ComputeDeadlineAndPenalty(DateTime paymentDate, decimal rsAmount)
        {
            var next = paymentDate.AddMonths(1);
            var deadline = DateTime.SpecifyKind(new DateTime(next.Year, next.Month, 20), DateTimeKind.Utc);
            var isOverdue = DateTime.UtcNow > deadline;
            var daysLate = isOverdue ? (int)(DateTime.UtcNow - deadline).TotalDays : 0;
            decimal penalty = 0m;
            if (isOverdue && daysLate > 0)
            {
                var monthsLate = (daysLate / 30) + (daysLate % 30 > 0 ? 1 : 0);
                penalty = Math.Round(rsAmount * 0.05m * monthsLate, 2);
            }
            return (deadline, isOverdue, daysLate, penalty);
        }

        // ─── Calculation ───

        public RSCalculationDto CalculateRS(decimal amountPaid, string rsTypeCode)
        {
            if (!Constants.RsRates.TryGetRate(rsTypeCode, out var rate))
                throw new ArgumentException($"Unknown RS type code: {rsTypeCode}");

            var rsAmount = CalculateRSAmountInternal(amountPaid, rsTypeCode);
            return new RSCalculationDto
            {
                AmountPaid = amountPaid,
                RSTypeCode = rsTypeCode,
                RSRate = rate,
                RSAmount = rsAmount,
                NetPayment = Math.Round(amountPaid - rsAmount, 2)
            };
        }

        // ─── TEJ Export ───

        /// <summary>
        /// CRITICAL COMPLIANCE: Validate all records meet Tunisia tax authority requirements before export
        /// </summary>
        private void ValidateComplianceBeforeExport(List<RSRecord> records)
        {
            var complianceErrors = new List<string>();

            foreach (var record in records)
            {
                var tag = $"Invoice {record.InvoiceNumber}";

                // Required-field validation (same rules as the per-invoice download) so
                // the generated XML is never structurally invalid / rejected by TEJ.
                foreach (var fieldErr in CollectRecordFieldErrors(record))
                    complianceErrors.Add($"{tag}: {fieldErr}");

                // Check for overdue records
                if (record.IsOverdue)
                    complianceErrors.Add($"{tag}: Past declaration deadline by {record.DaysLate} days (penalty: {record.PenaltyAmount:F2} TND)");

                // Check declaration deadline is set
                if (record.DeclarationDeadline == null)
                    complianceErrors.Add($"{tag}: Declaration deadline not calculated");

                // Warn if supplier type not classified (medium priority, not blocking)
                if (string.IsNullOrEmpty(record.SupplierType))
                    _logger.LogWarning("RS Record {Invoice}: Supplier type not classified", record.InvoiceNumber);
            }

            // Duplicate certificates: same invoice number + payment date → TEJ rejects the file.
            var dupKeys = records
                .GroupBy(r => $"{r.InvoiceNumber}|{r.PaymentDate:yyyy-MM-dd}")
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.Split('|')[0])
                .Distinct()
                .ToList();
            if (dupKeys.Count > 0)
                complianceErrors.Add($"Duplicate certificates detected for invoice(s): {string.Join(", ", dupKeys)}");

            // Block export if critical compliance issues found
            if (complianceErrors.Count > 0)
            {
                throw new InvalidOperationException($"Compliance validation failed:\n{string.Join("\n", complianceErrors)}");
            }

            _logger.LogInformation("Compliance validation passed for {Count} records", records.Count);
        }

        public async Task<TEJExportResponseDto> ExportTEJAsync(TEJExportRequestDto request, string userId)
        {
            var scoped = !string.IsNullOrWhiteSpace(request.EntityType) && request.EntityId.HasValue;

            // Materialize declarations for RS-applicable supplier invoices of the period
            // that were never downloaded individually — otherwise they'd silently be
            // missing from the monthly DGI declaration. Skipped for a scoped (single
            // entity) export so we never pull unrelated invoices into the scope.
            if (!scoped)
                await MaterializePeriodInvoiceRecordsAsync(request.Month, request.Year, userId);

            var query = _db.RSRecords
                .Where(r => r.PaymentDate.Month == request.Month &&
                            r.PaymentDate.Year == request.Year &&
                            r.Status == "pending" &&
                            !r.TEJExported);

            if (scoped)
            {
                var entityType = request.EntityType!;
                var entityId = request.EntityId!.Value;
                query = query.Where(r => r.EntityType == entityType && r.EntityId == entityId);
            }

            var records = await query.ToListAsync();

            if (records.Count == 0)
                throw new InvalidOperationException(scoped
                    ? "No pending RS records found for this document in the selected month"
                    : "No pending RS records found for the selected month");

            // ─── CRITICAL: Validate all records comply before export ───
            try
            {
                ValidateComplianceBeforeExport(records);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Export blocked: Compliance validation failed for {Month}/{Year}", request.Month, request.Year);
                throw new InvalidOperationException($"Cannot export: {ex.Message}");
            }

            // Determine declarant: explicit request → company fiscal identity → payer on records.
            var declarant = request.Declarant
                ?? await ResolveDeclarantAsync()
                ?? new TEJDeclarantDto
                {
                    Name = records.First().PayerName,
                    TaxId = records.First().PayerTaxId,
                    Address = records.First().PayerAddress ?? ""
                };

            // Generate TEJ XML
            var fileName = $"{declarant.TaxId}-{request.Year}-{request.Month:D2}-0.xml";
            string xmlContent;
            try
            {
                xmlContent = GenerateTEJXml(declarant, records);
            }
            catch (Exception ex)
            {
                var errorLog = new TEJExportLog
                {
                    FileName = fileName,
                    ExportDate = DateTime.UtcNow,
                    ExportedBy = userId,
                    Month = request.Month,
                    Year = request.Year,
                    RecordCount = records.Count,
                    TotalRSAmount = records.Sum(r => r.RSAmount),
                    Status = "error",
                    ErrorMessage = ex.Message
                };
                _db.TEJExportLogs.Add(errorLog);
                await _db.SaveChangesAsync();

                return new TEJExportResponseDto
                {
                    LogId = errorLog.Id,
                    FileName = fileName,
                    RecordCount = records.Count,
                    TotalRSAmount = errorLog.TotalRSAmount,
                    Status = "error",
                    ErrorMessage = ex.Message
                };
            }

            // Save XML file as a Document
            int? documentId = null;
            try
            {
                documentId = await SaveTEJFileAsDocument(fileName, xmlContent, request, userId, records);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save TEJ file as document, continuing without document link");
            }

            // Mark records as exported
            foreach (var r in records)
            {
                r.Status = "exported";
                r.TEJExported = true;
                r.TEJFileName = fileName;
                r.ModifiedAt = DateTime.UtcNow;
                r.ModifiedBy = userId;
            }

            var log = new TEJExportLog
            {
                FileName = fileName,
                ExportDate = DateTime.UtcNow,
                ExportedBy = userId,
                Month = request.Month,
                Year = request.Year,
                RecordCount = records.Count,
                TotalRSAmount = records.Sum(r => r.RSAmount),
                Status = "success",
                DocumentId = documentId
            };

            _db.TEJExportLogs.Add(log);
            await _db.SaveChangesAsync();

            _logger.LogInformation("TEJ export completed: {FileName}, {Count} records, total RS={Total}",
                fileName, records.Count, log.TotalRSAmount);

            return new TEJExportResponseDto
            {
                LogId = log.Id,
                FileName = fileName,
                RecordCount = records.Count,
                TotalRSAmount = log.TotalRSAmount,
                Status = "success",
                DocumentId = documentId
            };
        }

        public async Task<List<TEJExportLogDto>> GetTEJExportLogsAsync(int? year = null)
        {
            var query = _db.TEJExportLogs.AsQueryable();
            if (year.HasValue)
                query = query.Where(l => l.Year == year.Value);

            var logs = await query.OrderByDescending(l => l.ExportDate).ToListAsync();
            return logs.Select(l => new TEJExportLogDto
            {
                Id = l.Id,
                FileName = l.FileName,
                ExportDate = l.ExportDate,
                ExportedBy = l.ExportedBy,
                Month = l.Month,
                Year = l.Year,
                RecordCount = l.RecordCount,
                TotalRSAmount = l.TotalRSAmount,
                Status = l.Status,
                ErrorMessage = l.ErrorMessage,
                DocumentId = l.DocumentId
            }).ToList();
        }

        // ─── Cross-module: Supplier Invoice → RS Record ───

        /// <summary>
        /// Build an RSRecord from a supplier invoice (NOT persisted). Shared by the
        /// TEJ sync (which saves it) and the per-invoice XML download (which uses a
        /// transient copy). RS is declared on the amount paid when a payment exists,
        /// otherwise on the full invoice.
        /// </summary>
        private RSRecord BuildRsRecordFromInvoice(
            int supplierInvoiceId,
            MyApi.Modules.Purchases.Models.SupplierInvoice invoice,
            MyApi.Modules.Contacts.Models.Contact supplier,
            TEJDeclarantDto declarant,
            string userId)
        {
            static DateTime ToUtcKind(DateTime dt) => dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            };

            // invoice.GrandTotal is ALREADY net of the withholding (HT + TVA + stamp − RS),
            // and AmountPaid is what the supplier actually cashes (the net). The TEJ
            // certificate wants the gross invoice amount as the basis and the net served
            // AFTER the withholding, so deriving netServi as "basis − RS" off the net
            // GrandTotal deducted the RS twice (328 paid was declared as 298 served).
            var grossPayable = invoice.GrandTotal + invoice.RsAmount + invoice.RsTvaAmount;
            var hasPayment = invoice.AmountPaid > 0;
            var paidRatio  = (hasPayment && invoice.GrandTotal > 0)
                ? Math.Min(invoice.AmountPaid / invoice.GrandTotal, 1m)
                : 1m;
            var basis         = Math.Round(grossPayable * paidRatio, 2);
            var declaredRs    = Math.Round(invoice.RsAmount    * paidRatio, 2);
            var declaredRsTva = Math.Round(invoice.RsTvaAmount * paidRatio, 2);
            var netServi      = Math.Round(basis - declaredRs - declaredRsTva, 2);

            // Real invoice HT / VAT, pro-rated to the declared basis (TEJ MontantHT / MontantTVA).
            var discountValue = invoice.DiscountType == "percentage"
                ? Math.Round(invoice.SubTotal * invoice.Discount / 100m, 2)
                : invoice.Discount;
            var invoiceHt     = Math.Round((invoice.SubTotal - discountValue) * paidRatio, 2);
            var invoiceTva    = Math.Round(invoice.TaxAmount * paidRatio, 2);

            var operationCode = invoice.RsOperationCode
                ?? Constants.TejOperationCodes.LegacyToOperationCode(invoice.RsTypeCode);

            var paymentDate = invoice.PaymentDate.HasValue ? ToUtcKind(invoice.PaymentDate.Value) : DateTime.UtcNow;
            var invoiceDate = ToUtcKind(invoice.InvoiceDate);
            var (declarationDeadline, isOverdue, daysLate, penaltyAmount) =
                ComputeDeadlineAndPenalty(paymentDate, declaredRs);

            return new RSRecord
            {
                EntityType = "supplier_invoice",
                EntityId = supplierInvoiceId,
                EntityNumber = invoice.InvoiceNumber,
                InvoiceNumber = invoice.SupplierInvoiceRef ?? invoice.InvoiceNumber,
                InvoiceDate = invoiceDate,
                InvoiceAmount = invoice.GrandTotal,
                PaymentDate = paymentDate,
                AmountPaid = basis,
                RSAmount = declaredRs,
                RSTypeCode = invoice.RsTypeCode ?? "10",
                SupplierName = supplier.Name ?? supplier.Company ?? $"{supplier.FirstName} {supplier.LastName}".Trim(),
                SupplierTaxId = supplier.MatriculeFiscale ?? supplier.Cin ?? "",
                SupplierAddress = supplier.Address,
                PayerName = declarant.Name,
                PayerTaxId = declarant.TaxId,
                PayerAddress = declarant.Address,
                Status = "pending",
                TEJExported = false,
                DeclarationDeadline = declarationDeadline,
                IsOverdue = isOverdue,
                DaysLate = daysLate,
                // Was hardcoded 0 — late filings silently declared no penalty.
                PenaltyAmount = penaltyAmount,
                OperationCode = operationCode,
                Cnpc = invoice.Cnpc,
                PriseEnCharge = invoice.PriseEnCharge,
                AnneeFacturation = invoice.AnneeFacturation ?? invoice.InvoiceDate.Year,
                RefCertifChezDeclarant = invoice.RefCertifChezDeclarant ?? $"SI-{invoice.Id}",
                RsTvaCode = invoice.RsTvaCode,
                RsTvaTaux = invoice.RsTvaTaux,
                RsTvaAmount = declaredRsTva,
                MontantNetServi = netServi,
                MontantHT = invoiceHt,
                MontantTvaFacture = invoiceTva,
                BeneficiaireCategorie = supplier.CategorieContribuable ?? "PM",
                BeneficiaireIsResident = supplier.IsResident,
                BeneficiaireIdType = supplier.IdTaxpayerType ?? 1,
                BeneficiaireDateNaissance = supplier.DateNaissance.HasValue ? ToUtcKind(supplier.DateNaissance.Value) : null,
                BeneficiairePaysCode = supplier.PaysCode ?? "TN",
                Acte = invoice.TejActe,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
        }

        /// <summary>
        /// Persist a built RS record for an invoice (idempotent) and mark the invoice
        /// as registered for TEJ. If the invoice already has a record, that existing
        /// "declaration of record" is returned unchanged (no duplicate). This is what
        /// makes the on-demand download ALSO count toward the monthly declaration +
        /// deadline tracking — so a separate "Sync TEJ" step is no longer needed.
        /// </summary>
        private async Task<RSRecord> EnsureRsRecordPersistedAsync(int supplierInvoiceId, RSRecord builtRecord)
        {
            var trackedInvoice = await _db.SupplierInvoices.FirstOrDefaultAsync(i => i.Id == supplierInvoiceId);
            if (trackedInvoice?.RsRecordId is int existingId)
            {
                var existing = await _db.RSRecords.FindAsync(existingId);
                if (existing != null)
                {
                    // Refresh the declared figures from the invoice: a payment recorded
                    // (or an amount corrected) after the first download must be reflected,
                    // otherwise the certificate keeps declaring stale amounts forever.
                    if (!existing.TEJExported)
                    {
                        existing.AmountPaid       = builtRecord.AmountPaid;
                        existing.RSAmount         = builtRecord.RSAmount;
                        existing.RsTvaAmount      = builtRecord.RsTvaAmount;
                        existing.MontantNetServi  = builtRecord.MontantNetServi;
                        existing.MontantHT        = builtRecord.MontantHT;
                        existing.MontantTvaFacture= builtRecord.MontantTvaFacture;
                        existing.PaymentDate      = builtRecord.PaymentDate;
                        existing.OperationCode    = builtRecord.OperationCode;
                        existing.RSTypeCode       = builtRecord.RSTypeCode;
                        existing.PenaltyAmount    = builtRecord.PenaltyAmount;
                        existing.IsOverdue        = builtRecord.IsOverdue;
                        existing.DaysLate         = builtRecord.DaysLate;
                        await _db.SaveChangesAsync();
                    }
                    return existing;
                }
            }


            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                try
                {
                    _db.RSRecords.Add(builtRecord);
                    await _db.SaveChangesAsync();
                    if (trackedInvoice != null)
                    {
                        trackedInvoice.RsRecordId = builtRecord.Id;
                        trackedInvoice.TejSynced = true;
                        trackedInvoice.TejSyncDate = DateTime.UtcNow;
                        trackedInvoice.TejSyncStatus = "synced";
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
            return builtRecord;
        }

        /// <summary>
        /// Collect human-readable "please fill X" messages for any TEJ-required field
        /// that is missing/invalid on a record. Empty list = ready to export.
        /// </summary>
        private static List<string> CollectRecordFieldErrors(RSRecord r)
        {
            return CollectRecordFieldErrorsCore(r);
        }

        /// <summary>
        /// Ensure every RS-applicable supplier invoice paid during the given period has
        /// a persisted RSRecord, so the monthly TEJ export is exhaustive even when the
        /// user never downloaded the per-invoice XML. Invoices with incomplete fiscal
        /// data are skipped (they are surfaced by the per-invoice "missing info" flow).
        /// </summary>
        private async Task MaterializePeriodInvoiceRecordsAsync(int month, int year, string userId)
        {
            var declarant = await ResolveDeclarantAsync();
            if (declarant == null) return;

            var candidates = await _db.SupplierInvoices.AsNoTracking()
                .Where(i => !i.IsDeleted
                            && i.RsApplicable
                            && i.RsAmount > 0
                            && i.RsRecordId == null
                            && i.PaymentDate.HasValue
                            && i.PaymentDate.Value.Month == month
                            && i.PaymentDate.Value.Year == year)
                .ToListAsync();

            foreach (var inv in candidates)
            {
                var supplier = await _db.Contacts.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == inv.SupplierId);
                if (supplier == null)
                {
                    _logger.LogWarning("TEJ materialization skipped invoice {Id}: supplier not found", inv.Id);
                    continue;
                }

                var rec = BuildRsRecordFromInvoice(inv.Id, inv, supplier, declarant, userId);
                var errs = CollectRecordFieldErrors(rec);
                if (errs.Count > 0)
                {
                    _logger.LogWarning("TEJ materialization skipped invoice {Id}: {Errors}", inv.Id, string.Join("; ", errs));
                    continue;
                }

                await EnsureRsRecordPersistedAsync(inv.Id, rec);
            }
        }

        private static List<string> CollectRecordFieldErrorsCore(RSRecord r)
        {
            var e = new List<string>();
            if (string.IsNullOrWhiteSpace(r.SupplierTaxId))
                e.Add("Supplier is missing a Matricule Fiscal / CIN — edit the supplier and add it.");
            if (string.IsNullOrWhiteSpace(r.SupplierName))
                e.Add("Supplier name (raison sociale) is missing.");
            if (string.IsNullOrWhiteSpace(r.PayerTaxId))
                e.Add("Your company (TEJ declarant) needs a Matricule Fiscal — set it on your company contact in Settings.");
            if (string.IsNullOrWhiteSpace(r.InvoiceNumber))
                e.Add("Invoice number is missing.");
            if (r.InvoiceDate == default)
                e.Add("Invoice date is missing.");
            if (r.PaymentDate == default)
                e.Add("Payment date is missing.");
            if (r.RSAmount <= 0)
                e.Add("Withheld amount (RS) must be positive — set the Retenue à la Source type/amount.");
            if (r.AmountPaid <= 0)
                e.Add("Gross amount must be positive.");
            if (r.RSAmount > r.AmountPaid)
                e.Add("The withholding exceeds the gross amount — check the RS configuration.");
            if (!string.IsNullOrEmpty(r.BeneficiaireCategorie)
                && r.BeneficiaireCategorie != "PM" && r.BeneficiaireCategorie != "PP")
                e.Add("Supplier tax category must be PM (legal entity) or PP (individual).");
            if (string.IsNullOrWhiteSpace(r.OperationCode) && string.IsNullOrWhiteSpace(r.RSTypeCode))
                e.Add("RS operation type is missing.");
            return e;
        }

        /// <summary>
        /// Build the TEJ/RiTEJ XML for a SINGLE supplier invoice, on demand. Returns the
        /// list of missing fields (so the UI can tell the user what to fill) when the
        /// invoice isn't ready, or the ready-to-download XML when it is. Does NOT persist
        /// anything — this is a download helper that works at any time.
        /// </summary>
        public async Task<TejInvoiceXmlResult> BuildTejXmlForSupplierInvoiceAsync(int supplierInvoiceId, string userId)
        {
            var invoice = await _db.SupplierInvoices
                .FirstOrDefaultAsync(i => i.Id == supplierInvoiceId && !i.IsDeleted)
                ?? throw new KeyNotFoundException($"SupplierInvoice {supplierInvoiceId} not found");

            var missing = new List<string>();

            if (!invoice.RsApplicable || invoice.RsAmount <= 0)
                missing.Add("Retenue à la Source is not enabled on this invoice — set the RS type so an amount is computed.");

            var supplier = await _db.Contacts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == invoice.SupplierId);
            if (supplier == null)
                missing.Add("Supplier (beneficiary) not found.");

            // Declarant = the current company (Settings → Company), needs an MF.
            var declarant = await ResolveDeclarantAsync();
            if (declarant == null)
                missing.Add(DeclarantMissingMessage);

            // If we can't even build the record, return what's missing so far.
            if (supplier == null || declarant == null || !invoice.RsApplicable || invoice.RsAmount <= 0)
                return new TejInvoiceXmlResult { Ok = false, Missing = missing };


            var record = BuildRsRecordFromInvoice(supplierInvoiceId, invoice, supplier, declarant, userId);

            // Field-level completeness (same rules used by the monthly export).
            missing.AddRange(CollectRecordFieldErrors(record));
            if (missing.Count > 0)
                return new TejInvoiceXmlResult { Ok = false, Missing = missing.Distinct().ToList() };

            // Register the declaration (idempotent) so it also counts toward the
            // monthly TEJ file + deadline tracking — the download IS the sync now.
            record = await EnsureRsRecordPersistedAsync(supplierInvoiceId, record);

            var xml = GenerateTEJXml(declarant, new List<RSRecord> { record });
            var safeNumber = string.Concat((invoice.InvoiceNumber ?? $"SI{invoice.Id}")
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
            var fileName = $"RS-{safeNumber}-{record.PaymentDate:yyyy-MM}.xml";

            return new TejInvoiceXmlResult { Ok = true, Xml = xml, FileName = fileName };
        }

        /// <summary>
        /// Build the TEJ/RiTEJ XML for a purchase order, on demand. RS is declared on
        /// invoices, so this aggregates every RS-applicable supplier invoice generated
        /// from the PO into one declaration. Returns user-actionable "please fill X"
        /// messages when nothing is ready. Does NOT persist anything.
        /// </summary>
        public async Task<TejInvoiceXmlResult> BuildTejXmlForPurchaseOrderAsync(int purchaseOrderId, string userId)
        {
            var invoices = await _db.SupplierInvoices.AsNoTracking()
                .Where(i => i.PurchaseOrderId == purchaseOrderId && !i.IsDeleted)
                .OrderBy(i => i.Id)
                .ToListAsync();

            if (invoices.Count == 0)
                return new TejInvoiceXmlResult { Ok = false, Missing = new()
                {
                    "No supplier invoice has been created from this purchase order yet. Create the supplier invoice (where Retenue à la Source is configured), then download the TEJ XML."
                } };

            var rsInvoices = invoices.Where(i => i.RsApplicable && i.RsAmount > 0).ToList();
            if (rsInvoices.Count == 0)
                return new TejInvoiceXmlResult { Ok = false, Missing = new()
                {
                    "None of the invoices for this order have Retenue à la Source enabled — open the invoice and set the RS type."
                } };

            var declarant = await ResolveDeclarantAsync();
            if (declarant == null)
                return new TejInvoiceXmlResult { Ok = false, Missing = new() { DeclarantMissingMessage } };

            var missing = new List<string>();
            var records = new List<RSRecord>();
            foreach (var inv in rsInvoices)
            {
                var supplier = await _db.Contacts.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == inv.SupplierId);
                if (supplier == null)
                {
                    missing.Add($"Invoice {inv.InvoiceNumber}: supplier not found.");
                    continue;
                }
                var rec = BuildRsRecordFromInvoice(inv.Id, inv, supplier, declarant, userId);
                var errs = CollectRecordFieldErrors(rec);
                if (errs.Count > 0)
                    missing.AddRange(errs.Select(e => $"Invoice {inv.InvoiceNumber}: {e}"));
                else
                    // Register each invoice's declaration (idempotent) as we include it.
                    records.Add(await EnsureRsRecordPersistedAsync(inv.Id, rec));
            }

            if (missing.Count > 0 || records.Count == 0)
                return new TejInvoiceXmlResult { Ok = false, Missing = missing.Distinct().ToList() };

            var xml = GenerateTEJXml(declarant, records);
            var fileName = $"RS-PO{purchaseOrderId}-{records[0].PaymentDate:yyyy-MM}.xml";
            return new TejInvoiceXmlResult { Ok = true, Xml = xml, FileName = fileName };
        }

        // ─── Stats ───

        public async Task<RSStatsDto> GetRSStatsAsync(string? entityType, int? entityId, int? month, int? year)
        {
            var query = _db.RSRecords.AsQueryable();
            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(r => r.EntityType == entityType);
            if (entityId.HasValue)
                query = query.Where(r => r.EntityId == entityId.Value);
            if (month.HasValue)
                query = query.Where(r => r.PaymentDate.Month == month.Value);
            if (year.HasValue)
                query = query.Where(r => r.PaymentDate.Year == year.Value);

            return new RSStatsDto
            {
                TotalRecords = await query.CountAsync(),
                PendingRecords = await query.CountAsync(r => r.Status == "pending"),
                ExportedRecords = await query.CountAsync(r => r.Status == "exported"),
                TotalRSAmount = await query.SumAsync(r => r.RSAmount),
                TotalAmountPaid = await query.SumAsync(r => r.AmountPaid)
            };
        }

        // ─── Private Helpers ───

        private decimal CalculateRSAmountInternal(decimal amountPaid, string rsTypeCode)
        {
            if (!Constants.RsRates.TryGetRate(rsTypeCode, out var rate))
                throw new ArgumentException($"Unknown RS type code: {rsTypeCode}");
            return Math.Round(amountPaid * rate / 100m, 2);
        }

        /// <summary>
        /// Generate TEJ XML conformant with the official DGI / RiTEJ cahier de charges v1.0.
        /// Key rules:
        ///   * Root element <c>&lt;DeclarationsRS VersionSchema="1.0"&gt;</c>
        ///   * Dates formatted DD/MM/YYYY
        ///   * Amounts in MILLIMES (xs:integer = value * 1000), no decimals
        ///   * Structured Beneficiaire: TypeIdentifiant + Identifiant + CategorieContribuable + Resident
        ///   * IdTypeOperation = OperationCode (RS1_xxxxxx). Falls back to legacy-code mapping.
        ///   * Per-certificate totals (TotalMontantHT / TotalMontantTVA / TotalMontantNetServi)
        /// </summary>
        private string GenerateTEJXml(TEJDeclarantDto declarant, List<RSRecord> records, int depotSequence = 0)
        {
            var first = records.First();
            var year  = first.PaymentDate.Year;
            var month = first.PaymentDate.Month;

            // Group certificates by Acte (0=Ajouter, 1=Modifier, 2=Annuler)
            var byActe = records.GroupBy(r => r.Acte).OrderBy(g => g.Key).ToList();

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false),  // no BOM (TEJ rejects BOM)
                OmitXmlDeclaration = false
            };

            using var ms = new MemoryStream();
            using (var writer = XmlWriter.Create(ms, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("DeclarationsRS");
                writer.WriteAttributeString("VersionSchema", "1.0");

                // ── Déclarant ──
                writer.WriteStartElement("Declarant");
                WriteIdentifiant(writer, 1, declarant.TaxId);                       // 1=MF
                writer.WriteElementString("CategorieContribuable",
                    declarant.Categorie is "PP" ? "PP" : "PM");
                writer.WriteElementString("NometprenonOuRaisonsociale", Trunc(declarant.Name, 200));
                writer.WriteStartElement("InfosContact");
                writer.WriteElementString("Adresse", Trunc(declarant.Address ?? "", 200));
                if (!string.IsNullOrWhiteSpace(declarant.Email))
                    writer.WriteElementString("Email", declarant.Email);
                if (!string.IsNullOrWhiteSpace(declarant.Phone))
                    writer.WriteElementString("Telephone", declarant.Phone);
                writer.WriteEndElement();                                            // /InfosContact
                writer.WriteEndElement();                                            // /Declarant

                // ── Référence Déclaration ──
                writer.WriteStartElement("ReferenceDeclaration");
                // 0 = dépôt initial, 1..n = dépôt rectificatif for the same period.
                // Hardcoding "0" made every corrective filing look like a first filing,
                // which the DGI rejects as a duplicate declaration.
                writer.WriteElementString("ActeDepot", Math.Max(0, depotSequence).ToString());
                writer.WriteElementString("AnneeDepot", year.ToString());
                writer.WriteElementString("MoisDepot", month.ToString("D2"));
                writer.WriteEndElement();

                // ── Certificats (par Acte) ──
                foreach (var grp in byActe)
                {
                    var wrapper = grp.Key switch
                    {
                        1 => "ModifierCertificats",
                        2 => "AnnulerCertificats",
                        _ => "AjouterCertificats"
                    };
                    writer.WriteStartElement(wrapper);

                    long totalHT = 0, totalTVA = 0, totalNet = 0, totalRS = 0;
                    foreach (var r in grp)
                    {
                        WriteCertificat(writer, r);
                        // Real invoice HT / TVA (AmountPaid is a TTC figure and RsTvaAmount is
                        // VAT *withheld*, not the invoice VAT — using them here misstated the base).
                        totalHT  += ToMillimes(r.MontantHT > 0 ? r.MontantHT : r.AmountPaid);
                        totalTVA += ToMillimes(r.MontantTvaFacture);
                        totalRS  += ToMillimes(r.RSAmount + r.RsTvaAmount);
                        totalNet += ToMillimes(r.MontantNetServi > 0
                            ? r.MontantNetServi
                            : r.AmountPaid - r.RSAmount - r.RsTvaAmount);
                    }
                    writer.WriteElementString("TotalMontantHT",         totalHT.ToString());
                    writer.WriteElementString("TotalMontantTVA",        totalTVA.ToString());
                    writer.WriteElementString("TotalMontantRS",         totalRS.ToString());
                    writer.WriteElementString("TotalMontantNetServi",   totalNet.ToString());

                    writer.WriteEndElement();                                        // /AjouterCertificats etc.
                }

                writer.WriteEndElement();                                            // /DeclarationsRS
                writer.WriteEndDocument();
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private void WriteCertificat(XmlWriter w, RSRecord r)
        {
            w.WriteStartElement("Certificat");

            // RefCertifChezDeclarant: stable per-certif id provided by declarant
            w.WriteElementString("RefCertifChezDeclarant",
                Trunc(r.RefCertifChezDeclarant ?? $"CRT-{r.Id}", 50));
            w.WriteElementString("AnneeFacturation",
                (r.AnneeFacturation ?? r.InvoiceDate.Year).ToString());
            w.WriteElementString("IdTypeOperation",
                r.OperationCode ?? Constants.TejOperationCodes.LegacyToOperationCode(r.RSTypeCode));
            if (!string.IsNullOrWhiteSpace(r.Cnpc))
                w.WriteElementString("Cnpc", r.Cnpc);

            // ── Bénéficiaire ──
            w.WriteStartElement("Beneficiaire");
            WriteIdentifiant(w, r.BeneficiaireIdType ?? 1, r.SupplierTaxId);
            w.WriteElementString("CategorieContribuable", r.BeneficiaireCategorie ?? "PM");
            w.WriteElementString("Resident", r.BeneficiaireIsResident ? "1" : "0");
            w.WriteElementString("NometprenonOuRaisonsociale", Trunc(r.SupplierName, 200));
            if (r.BeneficiaireDateNaissance.HasValue)
                w.WriteElementString("DateNaissance",
                    r.BeneficiaireDateNaissance.Value.ToString("dd/MM/yyyy"));
            w.WriteElementString("Pays", r.BeneficiairePaysCode ?? "TN");
            w.WriteStartElement("InfosContact");
            w.WriteElementString("Adresse", Trunc(r.SupplierAddress ?? "", 200));
            w.WriteEndElement();
            w.WriteEndElement();                                                     // /Beneficiaire

            // ── Facture ──
            w.WriteStartElement("Facture");
            w.WriteElementString("NumeroFacture", Trunc(r.InvoiceNumber, 50));
            w.WriteElementString("DateFacture", r.InvoiceDate.ToString("dd/MM/yyyy"));
            w.WriteElementString("DatePayement", r.PaymentDate.ToString("dd/MM/yyyy"));
            w.WriteElementString("MontantHT",
                ToMillimes(r.MontantHT > 0 ? r.MontantHT : r.AmountPaid).ToString());
            w.WriteElementString("MontantTVA",         ToMillimes(r.MontantTvaFacture).ToString());
            // Rate must match the declared IdTypeOperation (DGI cross-check).
            w.WriteElementString("TauxRS",
                ((int)Math.Round(Constants.RsRates.GetEffectiveRate(r.OperationCode, r.RSTypeCode) * 100)).ToString()); // 10.00% -> 1000
            w.WriteElementString("MontantRS",          ToMillimes(r.RSAmount).ToString());
            if (r.RsTvaAmount > 0)
                w.WriteElementString("MontantRSTVA",   ToMillimes(r.RsTvaAmount).ToString());
            var net = r.MontantNetServi > 0 ? r.MontantNetServi : r.AmountPaid - r.RSAmount - r.RsTvaAmount;
            w.WriteElementString("MontantNetServi",    ToMillimes(net).ToString());
            w.WriteElementString("PriseEnCharge",      r.PriseEnCharge ? "1" : "0");
            w.WriteEndElement();                                                     // /Facture

            w.WriteEndElement();                                                     // /Certificat
        }

        /// <summary>
        /// Write the TEJ identifier choice block:
        ///   1 = MatriculeFiscal, 2 = CIN, 3 = Passeport, 4 = CarteSejour, 5 = AutreIdentifiantFiscal.
        /// </summary>
        private static void WriteIdentifiant(XmlWriter w, short type, string value)
        {
            w.WriteStartElement("TypeIdentifiant");
            w.WriteString(type.ToString());
            w.WriteEndElement();
            var elementName = type switch
            {
                2 => "Cin",
                3 => "Passeport",
                4 => "CarteSejour",
                5 => "AutreIdentifiantFiscal",
                _ => "MatriculeFiscal"
            };
            w.WriteElementString(elementName, (value ?? "").Trim());
        }

        /// <summary>Convert TND decimal to TEJ millimes (xs:integer = value * 1000).</summary>
        private static long ToMillimes(decimal amount) =>
            (long)Math.Round(amount * 1000m, MidpointRounding.AwayFromZero);

        private string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            _logger.LogWarning("TEJ field truncated from {Len} to {Max} chars: '{Value}'", s.Length, max, s);
            return s.Substring(0, max);
        }

        private async Task<int> SaveTEJFileAsDocument(
            string fileName, string xmlContent,
            TEJExportRequestDto request, string userId,
            List<RSRecord> records)
        {
            // Save the XML file to disk
            var backendRoot = _env.ContentRootPath;
            var parentDir = Directory.GetParent(backendRoot)?.FullName ?? backendRoot;
            var uploadsDir = Path.Combine(parentDir, "uploads", "tej_exports");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var diskPath = Path.Combine(uploadsDir, fileName);
            await File.WriteAllTextAsync(diskPath, xmlContent, Encoding.UTF8);

            var fileSize = new FileInfo(diskPath).Length;

            // Determine moduleType based on records
            var entityTypes = records.Select(r => r.EntityType).Distinct().ToList();
            var moduleType = entityTypes.Count == 1 ? entityTypes[0] + "s" : "retenue_source"; // "offers" or "sales"
            var moduleId = entityTypes.Count == 1 && records.Select(r => r.EntityId).Distinct().Count() == 1
                ? records.First().EntityId.ToString()
                : null;

            var doc = new Document
            {
                FileName = fileName,
                OriginalName = fileName,
                FilePath = $"/uploads/tej_exports/{fileName}",
                FileSize = fileSize,
                ContentType = "application/xml",
                ModuleType = moduleType,
                ModuleId = moduleId,
                ModuleName = $"TEJ Export {request.Year}-{request.Month:D2}",
                Category = "fiscal",
                Description = $"TEJ XML export for {request.Month:D2}/{request.Year} - {records.Count} records, total RS: {records.Sum(r => r.RSAmount):F2} TND",
                Tags = "tej,retenue-source,fiscal",
                IsPublic = false,
                UploadedBy = userId,
                UploadedAt = DateTime.UtcNow
            };

            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();

            return doc.Id;
        }

        private decimal GetRSRate(string typeCode)
        {
            return Constants.RsRates.GetRate(typeCode);
        }

        private static RSRecordDto MapToDto(RSRecord r) => new()
        {
            Id = r.Id,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            EntityNumber = r.EntityNumber,
            InvoiceNumber = r.InvoiceNumber,
            InvoiceDate = r.InvoiceDate,
            InvoiceAmount = r.InvoiceAmount,
            PaymentDate = r.PaymentDate,
            AmountPaid = r.AmountPaid,
            RSAmount = r.RSAmount,
            RSTypeCode = r.RSTypeCode,
            SupplierName = r.SupplierName,
            SupplierTaxId = r.SupplierTaxId,
            SupplierAddress = r.SupplierAddress,
            PayerName = r.PayerName,
            PayerTaxId = r.PayerTaxId,
            PayerAddress = r.PayerAddress,
            Status = r.Status,
            TEJExported = r.TEJExported,
            TEJFileName = r.TEJFileName,
            Notes = r.Notes,

            DeclarationDeadline = r.DeclarationDeadline,
            IsOverdue = r.IsOverdue,
            DaysLate = r.DaysLate,
            PenaltyAmount = r.PenaltyAmount,

            SupplierType = r.SupplierType,
            IsExemptByTreaty = r.IsExemptByTreaty,
            TreatyCode = r.TreatyCode,
            TEJAcceptanceNumber = r.TEJAcceptanceNumber,
            TEJTransmissionStatus = r.TEJTransmissionStatus,

            OperationCode = r.OperationCode,
            Cnpc = r.Cnpc,
            PriseEnCharge = r.PriseEnCharge,
            AnneeFacturation = r.AnneeFacturation,
            RefCertifChezDeclarant = r.RefCertifChezDeclarant,
            RsTvaCode = r.RsTvaCode,
            RsTvaTaux = r.RsTvaTaux,
            RsTvaAmount = r.RsTvaAmount,
            MontantNetServi = r.MontantNetServi,
            BeneficiaireCategorie = r.BeneficiaireCategorie,
            BeneficiaireIsResident = r.BeneficiaireIsResident,
            BeneficiaireIdType = r.BeneficiaireIdType,
            BeneficiaireDateNaissance = r.BeneficiaireDateNaissance,
            BeneficiairePaysCode = r.BeneficiairePaysCode,
            Acte = r.Acte,

            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy,
            ModifiedAt = r.ModifiedAt,
            ModifiedBy = r.ModifiedBy
        };
    }
}
