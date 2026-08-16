using System.Data;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Payments.DTOs;
using MyApi.Modules.Payments.Models;
using MyApi.Modules.Invoices.Services;



namespace MyApi.Modules.Payments.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentService> _logger;
        private readonly IInvoiceService? _invoiceService;

        public PaymentService(
            ApplicationDbContext context,
            ILogger<PaymentService> logger,
            IInvoiceService? invoiceService = null)
        {
            _context = context;
            _logger = logger;
            _invoiceService = invoiceService;
        }

        // ── Payments ──────────────────────────────────
        public async Task<List<PaymentDto>> GetPaymentsAsync(string entityType, string entityId)
        {
            var payments = await _context.Payments
                .Include(p => p.ItemAllocations)
                .Include(p => p.ProofDocuments)
                .Where(p => p.EntityType == entityType && p.EntityId == entityId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            return payments.Select(MapToDto).ToList();
        }

        public async Task<PaymentDto> CreatePaymentAsync(string entityType, string entityId, CreatePaymentDto dto, string userId, string userName)
        {
            if (dto.Amount <= 0m)
                throw new ArgumentException("Payment amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(entityType) || entityType.Length < 3)
                throw new ArgumentException("Invalid entityType.");
            // Currency is managed globally via preferences — no per-payment currency check.

            // Fix §1.1/§1.5: read-check-insert MUST run inside a Serializable
            // transaction with a row lock on the parent entity, otherwise two
            // concurrent calls can both pass the "remaining" check and overpay.
            // Uses the EF execution strategy for transient-retry safety.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                // Invoice-specific status guards. A draft invoice is AUTO-POSTED so the
                // user is never blocked when recording a payment.
                if (entityType == "invoice" && int.TryParse(entityId, out var invoiceIdParsed))
                {
                    const string trigger = "auto:payment_recording";
                    using var logScope = _logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["Operation"] = "PaymentInvoiceAutoPost",
                        ["InvoiceId"] = invoiceIdParsed,
                        ["Trigger"] = trigger,
                        ["UserId"] = userId,
                    });

                    var invoice = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.Id == invoiceIdParsed && !i.IsDeleted);
                    if (invoice == null)
                        throw new KeyNotFoundException($"Invoice {invoiceIdParsed} not found");
                    if (invoice.Status == "void")
                        throw new InvalidOperationException($"Invoice {invoiceIdParsed} is voided — cannot record payments.");
                    if (invoice.Status == "draft")
                    {
                        if (_invoiceService == null)
                        {
                            _logger.LogError(
                                "Cannot auto-post draft invoice {InvoiceId}: invoice service is not available (sale {SaleId})",
                                invoiceIdParsed, invoice.SaleId);
                            throw new InvalidOperationException($"Invoice {invoiceIdParsed} is a draft and cannot be posted automatically right now. Please retry.");
                        }
                        _logger.LogInformation(
                            "Auto-posting draft invoice {InvoiceId} (sale {SaleId}) before recording payment of {Amount} {Currency}",
                            invoiceIdParsed, invoice.SaleId, dto.Amount, dto.Currency);
                        try
                        {
                            await _invoiceService.PostAsync(invoiceIdParsed, new MyApi.Modules.Invoices.DTOs.PostInvoiceDto(), userId, trigger);
                            _logger.LogInformation("Draft invoice {InvoiceId} auto-posted for payment recording", invoiceIdParsed);
                        }
                        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                        {
                            _logger.LogWarning(ex,
                                "Auto-post failed for draft invoice {InvoiceId} — payment rejected: {Reason}",
                                invoiceIdParsed, ex.Message);
                            await _invoiceService.LogAutoPostSkippedAsync(invoiceIdParsed, userId, trigger, ex.Message);
                            throw;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Invoice {InvoiceId} already '{Status}' — no auto-post needed for this payment",
                            invoiceIdParsed, invoice.Status);
                    }
                }


                // ── Cross-check: entity exists and is billable ──
                //     Overpay guard applies for EVERY entityType (sale/offer/invoice),
                //     not just invoice (was §1.5). Read AFTER any auto-post so totals are fresh.
                var (entityTotal, entityCurrency) = await GetEntityTotalAndCurrencyAsync(entityType, entityId);
                if (entityTotal <= 0m && entityType != "offer")
                    throw new KeyNotFoundException($"{entityType} {entityId} not found or has zero total.");


                // Overpay guard for ALL entity types. SUM re-read inside the serializable
                // transaction so a concurrent payment cannot slip past this check.
                var alreadyPaid = await _context.Payments
                    .Where(p => p.EntityType == entityType && p.EntityId == entityId && p.Status == "completed")
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                var remaining = entityTotal - alreadyPaid;
                if (dto.Amount > remaining + 0.009m)
                {
                    throw new InvalidOperationException(
                        $"Payment of {dto.Amount:0.##} {dto.Currency} exceeds the outstanding balance " +
                        $"({Math.Max(0m, remaining):0.##} {entityCurrency ?? dto.Currency}). Enter the remaining amount or issue a credit/refund instead.");
                }

                // Fix §1.2: derive receipt number from GUID; keep human-friendly prefix but
                // append a short unique suffix so concurrent inserts cannot collide even
                // without a DB uniqueness constraint. `entityType` length is guarded above.
                var prefix = entityType.ToUpper();
                if (prefix.Length > 3) prefix = prefix.Substring(0, 3);
                var uniq = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var receiptNumber = $"REC-{prefix}-{entityId}-{uniq}";

                var payment = new Payment
                {
                    Id = Guid.NewGuid().ToString(),
                    EntityType = entityType,
                    EntityId = entityId,
                    Amount = dto.Amount,
                    Currency = dto.Currency,
                    PaymentMethod = dto.PaymentMethod,
                    PaymentReference = dto.PaymentReference,
                    PaymentDate = dto.PaymentDate ?? DateTime.UtcNow,
                    Status = "completed",
                    Notes = dto.Notes,
                    ReceiptNumber = receiptNumber,
                    ProofDocumentId = dto.ProofDocumentId,
                    ProofDocumentName = dto.ProofDocumentName,
                    ProofDocumentUrl = dto.ProofDocumentUrl,
                    InstallmentId = dto.InstallmentId,
                    CreatedBy = userId,
                    CreatedByName = userName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                // Link to plan if installment is specified
                if (!string.IsNullOrEmpty(dto.InstallmentId))
                {
                    var installment = await _context.PaymentPlanInstallments
                        .Include(i => i.Plan)
                        .FirstOrDefaultAsync(i => i.Id == dto.InstallmentId);
                    if (installment != null)
                    {
                        // Guard against installment overpay too (was §1.5 sub-finding)
                        if (installment.PaidAmount + dto.Amount > installment.Amount + 0.009m)
                        {
                            throw new InvalidOperationException(
                                $"Payment of {dto.Amount:0.##} would overpay installment #{installment.InstallmentNumber} " +
                                $"(remaining {Math.Max(0m, installment.Amount - installment.PaidAmount):0.##}).");
                        }
                        payment.PlanId = installment.PlanId;
                        installment.PaidAmount += dto.Amount;
                        if (installment.PaidAmount >= installment.Amount)
                        {
                            installment.Status = "paid";
                            installment.PaidAt = DateTime.UtcNow;
                        }
                        else
                        {
                            installment.Status = "partially_paid";
                        }

                        var plan = installment.Plan;
                        if (plan != null)
                        {
                            var allInstallments = await _context.PaymentPlanInstallments
                                .Where(i => i.PlanId == plan.Id)
                                .ToListAsync();
                            if (allInstallments.All(i => i.Status == "paid"))
                            {
                                plan.Status = "completed";
                                plan.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }

                _context.Payments.Add(payment);

                if (dto.ItemAllocations != null)
                {
                    foreach (var alloc in dto.ItemAllocations)
                    {
                        _context.PaymentItemAllocations.Add(new PaymentItemAllocation
                        {
                            Id = Guid.NewGuid().ToString(),
                            PaymentId = payment.Id,
                            ItemId = alloc.ItemId,
                            ItemName = alloc.ItemName,
                            AllocatedAmount = alloc.AllocatedAmount,
                            ItemTotal = alloc.ItemTotal,
                            CreatedAt = DateTime.UtcNow,
                        });
                    }
                }

                // Proof documents (multi-file). Accept both the legacy single-field
                // shape and the new list; de-duplicate by document id so a client
                // sending both does not create two links to the same file.
                var proofInputs = new List<CreatePaymentProofDocumentDto>();
                if (dto.ProofDocuments != null) proofInputs.AddRange(dto.ProofDocuments);
                if (dto.ProofDocumentId.HasValue &&
                    !proofInputs.Any(p => p.DocumentId == dto.ProofDocumentId))
                {
                    proofInputs.Add(new CreatePaymentProofDocumentDto
                    {
                        DocumentId = dto.ProofDocumentId,
                        DocumentName = dto.ProofDocumentName,
                        DocumentUrl = dto.ProofDocumentUrl,
                    });
                }

                foreach (var proof in proofInputs
                             .Where(p => p.DocumentId.HasValue)
                             .GroupBy(p => p.DocumentId)
                             .Select(g => g.First()))
                {
                    var link = new PaymentProofDocument
                    {
                        Id = Guid.NewGuid().ToString(),
                        PaymentId = payment.Id,
                        DocumentId = proof.DocumentId,
                        DocumentName = proof.DocumentName,
                        DocumentUrl = proof.DocumentUrl,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    _context.PaymentProofDocuments.Add(link);
                    payment.ProofDocuments.Add(link);
                }

                // Keep the legacy single-proof columns in sync with the first proof.
                var first = payment.ProofDocuments.FirstOrDefault();
                payment.ProofDocumentId = first?.DocumentId;
                payment.ProofDocumentName = first?.DocumentName;
                payment.ProofDocumentUrl = first?.DocumentUrl;

                // Persist the payment (and allocations) BEFORE recalculating: the
                // recalculation SUMs payments straight from the database, so an
                // unflushed insert would be ignored and AmountPaid would stay stale.
                await _context.SaveChangesAsync();
                await UpdateEntityPaymentStatusAsync(entityType, entityId);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return MapToDto(payment);
            });
        }

        public async Task<bool> DeletePaymentAsync(string entityType, string entityId, string paymentId)
        {
            // Fix §1.3: wrap the payment removal + parent recalculation in ONE
            // transaction. Previously ran as two independent SaveChangesAsync calls,
            // leaving the parent Sale/Invoice PaidAmount stale if the process died
            // between them.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                var payment = await _context.Payments
                    .Include(p => p.ItemAllocations)
                    .Include(p => p.ProofDocuments)
                    .FirstOrDefaultAsync(p => p.Id == paymentId && p.EntityType == entityType && p.EntityId == entityId);
                if (payment == null) return false;

                if (!string.IsNullOrEmpty(payment.InstallmentId))
                {
                    var installment = await _context.PaymentPlanInstallments.FindAsync(payment.InstallmentId);
                    if (installment != null)
                    {
                        installment.PaidAmount -= payment.Amount;
                        if (installment.PaidAmount <= 0)
                        {
                            installment.PaidAmount = 0;
                            installment.Status = "pending";
                            installment.PaidAt = null;
                        }
                        else
                        {
                            installment.Status = "partially_paid";
                        }
                    }
                }

                _context.PaymentItemAllocations.RemoveRange(payment.ItemAllocations);
                // Only the links are removed; the uploaded files stay in the
                // Documents module so the parent record keeps its paper trail.
                _context.PaymentProofDocuments.RemoveRange(payment.ProofDocuments);
                _context.Payments.Remove(payment);
                // Flush the deletion first — the recalculation reads payments from the
                // database and would otherwise still count the removed payment.
                await _context.SaveChangesAsync();
                await UpdateEntityPaymentStatusAsync(entityType, entityId);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            });
        }

        // ── Proof documents ───────────────────────────
        public async Task<List<PaymentProofDocumentDto>> GetPaymentProofsAsync(
            string entityType, string entityId, string paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.ProofDocuments)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.EntityType == entityType && p.EntityId == entityId);
            if (payment == null) throw new KeyNotFoundException("Payment not found");

            return payment.ProofDocuments.OrderBy(d => d.CreatedAt).Select(MapProofToDto).ToList();
        }

        public async Task<List<PaymentProofDocumentDto>> AddPaymentProofsAsync(
            string entityType, string entityId, string paymentId,
            List<CreatePaymentProofDocumentDto> proofs, string userId)
        {
            var payment = await _context.Payments
                .Include(p => p.ProofDocuments)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.EntityType == entityType && p.EntityId == entityId);
            if (payment == null) throw new KeyNotFoundException("Payment not found");

            var existingIds = payment.ProofDocuments.Select(d => d.DocumentId).ToHashSet();
            foreach (var proof in proofs.Where(p => p.DocumentId.HasValue))
            {
                if (existingIds.Contains(proof.DocumentId)) continue;
                existingIds.Add(proof.DocumentId);

                var link = new PaymentProofDocument
                {
                    Id = Guid.NewGuid().ToString(),
                    PaymentId = payment.Id,
                    DocumentId = proof.DocumentId,
                    DocumentName = proof.DocumentName,
                    DocumentUrl = proof.DocumentUrl,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _context.PaymentProofDocuments.Add(link);
                payment.ProofDocuments.Add(link);
            }

            SyncLegacyProofColumns(payment);
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return payment.ProofDocuments.OrderBy(d => d.CreatedAt).Select(MapProofToDto).ToList();
        }

        public async Task<PaymentProofDocumentDto?> UpdatePaymentProofAsync(
            string entityType, string entityId, string paymentId, string proofId, string documentName)
        {
            var payment = await _context.Payments
                .Include(p => p.ProofDocuments)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.EntityType == entityType && p.EntityId == entityId);
            if (payment == null) throw new KeyNotFoundException("Payment not found");

            var proof = payment.ProofDocuments.FirstOrDefault(d => d.Id == proofId);
            if (proof == null) return null;

            proof.DocumentName = documentName;
            proof.UpdatedAt = DateTime.UtcNow;
            SyncLegacyProofColumns(payment);
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapProofToDto(proof);
        }

        public async Task<bool> DeletePaymentProofAsync(
            string entityType, string entityId, string paymentId, string proofId)
        {
            var payment = await _context.Payments
                .Include(p => p.ProofDocuments)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.EntityType == entityType && p.EntityId == entityId);
            if (payment == null) throw new KeyNotFoundException("Payment not found");

            var proof = payment.ProofDocuments.FirstOrDefault(d => d.Id == proofId);
            if (proof == null) return false;

            // Detach only. The uploaded file itself is managed by the Documents
            // module; deleting it there is a separate, explicit user action.
            _context.PaymentProofDocuments.Remove(proof);
            payment.ProofDocuments.Remove(proof);
            SyncLegacyProofColumns(payment);
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static void SyncLegacyProofColumns(Payment payment)
        {
            var first = payment.ProofDocuments.OrderBy(d => d.CreatedAt).FirstOrDefault();
            payment.ProofDocumentId = first?.DocumentId;
            payment.ProofDocumentName = first?.DocumentName;
            payment.ProofDocumentUrl = first?.DocumentUrl;
        }

        private static PaymentProofDocumentDto MapProofToDto(PaymentProofDocument d) => new()
        {
            Id = d.Id,
            PaymentId = d.PaymentId,
            DocumentId = d.DocumentId,
            DocumentName = d.DocumentName,
            DocumentUrl = d.DocumentUrl,
            CreatedBy = d.CreatedBy,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
        };

        // ── Summary ───────────────────────────────────
        public async Task<PaymentSummaryDto> GetPaymentSummaryAsync(string entityType, string entityId)
        {
            var payments = await _context.Payments
                .Where(p => p.EntityType == entityType && p.EntityId == entityId && p.Status == "completed")
                .ToListAsync();

            // Fix §1.4: use the parent entity's own currency (Sale.Currency /
            // Invoice.Currency / Offer.Currency) rather than the first payment's
            // currency, which was arbitrary and misleading for mixed-currency data.
            var (totalAmount, entityCurrency) = await GetEntityTotalAndCurrencyAsync(entityType, entityId);
            var paidAmount = payments.Sum(p => p.Amount);
            var remaining = totalAmount - paidAmount;

            string paymentStatus = "unpaid";
            if (paidAmount >= totalAmount && totalAmount > 0) paymentStatus = "fully_paid";
            else if (paidAmount > 0) paymentStatus = "partially_paid";

            return new PaymentSummaryDto
            {
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                RemainingAmount = Math.Max(0, remaining),
                PaymentStatus = paymentStatus,
                PaymentCount = payments.Count,
                LastPaymentDate = payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate,
                Currency = !string.IsNullOrWhiteSpace(entityCurrency)
                    ? entityCurrency!
                    : (payments.FirstOrDefault()?.Currency ?? "TND"),
            };
        }

        // Combined helper: returns total + currency for the parent entity in one
        // pass. Used by CreatePaymentAsync (currency + overpay guard) and by
        // GetPaymentSummaryAsync (accurate summary currency).
        private async Task<(decimal total, string? currency)> GetEntityTotalAndCurrencyAsync(string entityType, string entityId)
        {
            if (entityType == "sale")
            {
                var s = await _context.Sales.FirstOrDefaultAsync(x => x.Id.ToString() == entityId && !x.IsDeleted);
                return (s?.TotalAmount ?? 0m, s?.Currency);
            }
            if (entityType == "invoice")
            {
                var i = await _context.Invoices.FirstOrDefaultAsync(x => x.Id.ToString() == entityId && !x.IsDeleted);
                return (i?.GrandTotal ?? 0m, i?.Currency);
            }
            var o = await _context.Offers.FirstOrDefaultAsync(x => x.Id.ToString() == entityId && !x.IsDeleted);
            return (o?.TotalAmount ?? 0m, o?.Currency);
        }

        // ── Payment Plans ─────────────────────────────
        public async Task<List<PaymentPlanDto>> GetPaymentPlansAsync(string entityType, string entityId)
        {
            var plans = await _context.PaymentPlans
                .Include(p => p.Installments.OrderBy(i => i.InstallmentNumber))
                .Where(p => p.EntityType == entityType && p.EntityId == entityId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return plans.Select(MapPlanToDto).ToList();
        }

        public async Task<PaymentPlanDto> CreatePaymentPlanAsync(string entityType, string entityId, CreatePaymentPlanDto dto, string userId)
        {
            var plan = new PaymentPlan
            {
                Id = Guid.NewGuid().ToString(),
                EntityType = entityType,
                EntityId = entityId,
                Name = dto.Name,
                Description = dto.Description,
                TotalAmount = dto.TotalAmount,
                Currency = dto.Currency,
                InstallmentCount = dto.Installments.Count,
                Status = "active",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.PaymentPlans.Add(plan);

            for (int i = 0; i < dto.Installments.Count; i++)
            {
                var inst = dto.Installments[i];
                _context.PaymentPlanInstallments.Add(new PaymentPlanInstallment
                {
                    Id = Guid.NewGuid().ToString(),
                    PlanId = plan.Id,
                    InstallmentNumber = i + 1,
                    Amount = inst.Amount,
                    DueDate = inst.DueDate,
                    Status = "pending",
                    PaidAmount = 0,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await _context.SaveChangesAsync();

            return MapPlanToDto(plan);
        }

        public async Task<bool> DeletePaymentPlanAsync(string entityType, string entityId, string planId)
        {
            var plan = await _context.PaymentPlans
                .Include(p => p.Installments)
                .FirstOrDefaultAsync(p => p.Id == planId && p.EntityType == entityType && p.EntityId == entityId);
            if (plan == null) return false;

            _context.PaymentPlanInstallments.RemoveRange(plan.Installments);
            _context.PaymentPlans.Remove(plan);
            await _context.SaveChangesAsync();
            return true;
        }

        // ── Statement ─────────────────────────────────
        public async Task<PaymentStatementDto> GetPaymentStatementAsync(string entityType, string entityId)
        {
            var payments = await GetPaymentsAsync(entityType, entityId);
            var plans = await GetPaymentPlansAsync(entityType, entityId);
            var summary = await GetPaymentSummaryAsync(entityType, entityId);

            var (entityTitle, contactName) = await GetEntityInfoAsync(entityType, entityId);

            // Build item breakdown from allocations
            var itemMap = new Dictionary<string, ItemPaymentBreakdownDto>();
            foreach (var p in payments)
            {
                foreach (var a in p.ItemAllocations)
                {
                    if (!itemMap.ContainsKey(a.ItemId))
                    {
                        itemMap[a.ItemId] = new ItemPaymentBreakdownDto
                        {
                            Id = a.ItemId,
                            Name = a.ItemName,
                            TotalPrice = a.ItemTotal,
                            PaidAmount = 0,
                        };
                    }
                    itemMap[a.ItemId].PaidAmount += a.AllocatedAmount;
                }
            }
            foreach (var item in itemMap.Values)
            {
                item.RemainingAmount = Math.Max(0, item.TotalPrice - item.PaidAmount);
            }

            return new PaymentStatementDto
            {
                EntityType = entityType,
                EntityId = entityId,
                EntityTitle = entityTitle,
                ContactName = contactName,
                TotalAmount = summary.TotalAmount,
                PaidAmount = summary.PaidAmount,
                RemainingAmount = summary.RemainingAmount,
                Currency = summary.Currency,
                Payments = payments,
                Plan = plans.FirstOrDefault(p => p.Status == "active"),
                Items = itemMap.Values.ToList(),
                GeneratedAt = DateTime.UtcNow,
            };
        }

        // ── Upcoming Installments (for reminders) ─────
        public async Task<List<InstallmentReminderInfo>> GetUpcomingInstallmentsAsync(int daysAhead = 3)
        {
            var cutoff = DateTime.UtcNow.AddDays(daysAhead);
            var now = DateTime.UtcNow;

            var installments = await _context.PaymentPlanInstallments
                .Include(i => i.Plan)
                .Where(i => i.Status == "pending" && i.DueDate <= cutoff && i.DueDate >= now)
                .OrderBy(i => i.DueDate)
                .ToListAsync();

            var results = new List<InstallmentReminderInfo>();
            foreach (var inst in installments)
            {
                if (inst.Plan == null) continue;
                var (entityTitle, contactName) = await GetEntityInfoAsync(inst.Plan.EntityType, inst.Plan.EntityId);
                var contactEmail = await GetContactEmailAsync(inst.Plan.EntityType, inst.Plan.EntityId);

                results.Add(new InstallmentReminderInfo
                {
                    PlanId = inst.PlanId,
                    PlanName = inst.Plan.Name,
                    InstallmentId = inst.Id,
                    InstallmentNumber = inst.InstallmentNumber,
                    Amount = inst.Amount,
                    Currency = inst.Plan.Currency,
                    DueDate = inst.DueDate,
                    EntityType = inst.Plan.EntityType,
                    EntityId = inst.Plan.EntityId,
                    EntityTitle = entityTitle,
                    ContactName = contactName,
                    ContactEmail = contactEmail,
                });
            }

            return results;
        }

        // ── Private helpers ───────────────────────────
        private async Task<decimal> GetEntityTotalAmountAsync(string entityType, string entityId)
        {
            if (entityType == "sale")
            {
                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id.ToString() == entityId && !s.IsDeleted);
                return sale?.TotalAmount ?? 0;
            }
            else if (entityType == "invoice")
            {
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id.ToString() == entityId && !i.IsDeleted);
                return invoice?.GrandTotal ?? 0;
            }
            else
            {
                var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id.ToString() == entityId && !o.IsDeleted);
                return offer?.TotalAmount ?? 0;
            }
        }

        private async Task<(string title, string contactName)> GetEntityInfoAsync(string entityType, string entityId)
        {
            if (entityType == "sale")
            {
                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id.ToString() == entityId && !s.IsDeleted);
                if (sale != null)
                {
                    var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == sale.ContactId && !c.IsDeleted);
                    return (sale.Title ?? $"Sale #{sale.SaleNumber}", contact?.Name ?? "");
                }
            }
            else if (entityType == "invoice")
            {
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id.ToString() == entityId && !i.IsDeleted);
                if (invoice != null)
                {
                    var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == invoice.ContactId && !c.IsDeleted);
                    return (invoice.Title ?? $"Invoice #{invoice.InvoiceNumber ?? invoice.Id.ToString()}", contact?.Name ?? "");
                }
            }
            else
            {
                var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id.ToString() == entityId && !o.IsDeleted);
                if (offer != null)
                {
                    var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == offer.ContactId && !c.IsDeleted);
                    return (offer.Title ?? $"Offer #{offer.Id}", contact?.Name ?? "");
                }
            }
            return ("", "");
        }

        private async Task<string?> GetContactEmailAsync(string entityType, string entityId)
        {
            int? contactId = null;
            if (entityType == "sale")
            {
                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id.ToString() == entityId && !s.IsDeleted);
                contactId = sale?.ContactId;
            }
            else if (entityType == "invoice")
            {
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id.ToString() == entityId && !i.IsDeleted);
                contactId = invoice?.ContactId;
            }
            else
            {
                var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id.ToString() == entityId && !o.IsDeleted);
                contactId = offer?.ContactId;
            }
            if (contactId == null) return null;
            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId.Value && !c.IsDeleted);
            return contact?.Email;
        }

        private async Task UpdateEntityPaymentStatusAsync(string entityType, string entityId)
        {
            var summary = await GetPaymentSummaryAsync(entityType, entityId);
            // Push the recomputed paid-amount into the invoice ledger so its
            // AmountPaid / Status columns stay in sync with Payments. Sale and
            // offer paid-amount columns are still computed on the fly.
            if (entityType == "invoice" && _invoiceService != null
                && int.TryParse(entityId, out var invoiceId))
            {
                try { await _invoiceService.RecalculatePaymentStateAsync(invoiceId); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to sync invoice {Id} payment state", invoiceId); }
            }
        }

        // ── Mappers ───────────────────────────────────
        private PaymentDto MapToDto(Payment p) => new()
        {
            Id = p.Id,
            EntityType = p.EntityType,
            EntityId = p.EntityId,
            PlanId = p.PlanId,
            InstallmentId = p.InstallmentId,
            Amount = p.Amount,
            Currency = p.Currency,
            PaymentMethod = p.PaymentMethod,
            PaymentReference = p.PaymentReference,
            PaymentDate = p.PaymentDate,
            Status = p.Status,
            Notes = p.Notes,
            ReceiptNumber = p.ReceiptNumber,
            ProofDocumentId = p.ProofDocumentId,
            ProofDocumentName = p.ProofDocumentName,
            ProofDocumentUrl = p.ProofDocumentUrl,
            ProofDocuments = (p.ProofDocuments ?? new List<PaymentProofDocument>())
                .OrderBy(d => d.CreatedAt)
                .Select(MapProofToDto)
                .ToList(),
            ItemAllocations = (p.ItemAllocations ?? new List<PaymentItemAllocation>()).Select(a => new PaymentItemAllocationDto
            {
                Id = a.Id,
                PaymentId = a.PaymentId,
                ItemId = a.ItemId,
                ItemName = a.ItemName,
                AllocatedAmount = a.AllocatedAmount,
                ItemTotal = a.ItemTotal,
                CreatedAt = a.CreatedAt,
            }).ToList(),
            CreatedBy = p.CreatedBy,
            CreatedByName = p.CreatedByName,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        };

        private PaymentPlanDto MapPlanToDto(PaymentPlan p) => new()
        {
            Id = p.Id,
            EntityType = p.EntityType,
            EntityId = p.EntityId,
            Name = p.Name,
            Description = p.Description,
            TotalAmount = p.TotalAmount,
            Currency = p.Currency,
            InstallmentCount = p.InstallmentCount,
            Status = p.Status,
            Installments = (p.Installments ?? new List<PaymentPlanInstallment>()).Select(i => new PaymentPlanInstallmentDto
            {
                Id = i.Id,
                PlanId = i.PlanId,
                InstallmentNumber = i.InstallmentNumber,
                Amount = i.Amount,
                DueDate = i.DueDate,
                Status = i.Status,
                PaidAmount = i.PaidAmount,
                PaidAt = i.PaidAt,
                Notes = i.Notes,
                CreatedAt = i.CreatedAt,
            }).ToList(),
            CreatedBy = p.CreatedBy,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        };
    }
}
