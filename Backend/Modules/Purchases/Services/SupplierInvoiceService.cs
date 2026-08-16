using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Purchases.DTOs;
using MyApi.Modules.Purchases.Models;
using MyApi.Modules.RetenueSource.Constants;

namespace MyApi.Modules.Purchases.Services
{
    public class SupplierInvoiceService : ISupplierInvoiceService
    {
        // Npgsql rejects non-UTC DateTimes for `timestamp with time zone` columns.
        // Purchase invoice dates come from HTML date inputs, so normalize them
        // before persisting to avoid SaveChanges failures.
        private static DateTime? AsUtc(DateTime? dt) => dt.HasValue ? AsUtc(dt.Value) : (DateTime?)null;
        private static DateTime AsUtc(DateTime dt) => dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };

        // Shared duplicate-reference guard: used both as a friendly pre-check and
        // again inside the create transaction so concurrent submissions can't race
        // past it and surface a raw 23505 unique-violation as a 500.
        private async Task AssertNoDuplicateSupplierRefAsync(int supplierId, string? supplierInvoiceRef)
        {
            if (string.IsNullOrWhiteSpace(supplierInvoiceRef)) return;
            var dupExists = await _context.SupplierInvoices.AsNoTracking()
                .AnyAsync(i => i.SupplierId == supplierId
                            && i.SupplierInvoiceRef == supplierInvoiceRef
                            && !i.IsDeleted);
            if (dupExists)
                throw new InvalidOperationException($"[DUPLICATE_SUPPLIER_REF] An invoice with reference '{supplierInvoiceRef}' already exists for this supplier");
        }

        private readonly ApplicationDbContext _context;
        private readonly ILogger<SupplierInvoiceService> _logger;
        private readonly MyApi.Modules.Numbering.Services.INumberingService? _numberingService;

        public SupplierInvoiceService(ApplicationDbContext context, ILogger<SupplierInvoiceService> logger,
            MyApi.Modules.Numbering.Services.INumberingService? numberingService = null)
        {
            _context = context;
            _logger = logger;
            _numberingService = numberingService;
        }



        public async Task<PaginatedSupplierInvoiceResponse> GetInvoicesAsync(
            string? status, string? supplierId, bool? rsApplicable,
            DateTime? dateFrom, DateTime? dateTo, string? search,
            int page, int limit, string sortBy, string sortOrder, bool? overdueOnly)
        {
            // Clamp paging: a negative page yields a negative OFFSET (SQL error) and an
            // unbounded limit lets a single request pull the whole table.
            if (page < 1) page = 1;
            if (limit < 1) limit = 20;
            if (limit > 200) limit = 200;

            var query = _context.SupplierInvoices.AsNoTracking().Where(i => !i.IsDeleted).AsQueryable();
            if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);
            if (!string.IsNullOrEmpty(supplierId) && int.TryParse(supplierId, out int sid))
                query = query.Where(i => i.SupplierId == sid);
            if (rsApplicable.HasValue) query = query.Where(i => i.RsApplicable == rsApplicable.Value);
            if (dateFrom.HasValue) query = query.Where(i => i.InvoiceDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(i => i.InvoiceDate <= dateTo.Value);
            // Overdue = past its DUE date and still owed. Filtering on InvoiceDate
            // (as the UI used to) counted every older invoice, including paid ones.
            if (overdueOnly == true)
            {
                var nowUtc = DateTime.UtcNow;
                query = query.Where(i => i.DueDate != null && i.DueDate < nowUtc
                                      && i.Status != "paid" && i.Status != "cancelled");
            }
            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(i => (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s)) ||
                    (i.SupplierName != null && i.SupplierName.ToLower().Contains(s)));
            }
            var total = await query.CountAsync();
            query = sortOrder == "asc" ? query.OrderBy(i => i.CreatedDate) : query.OrderByDescending(i => i.CreatedDate);
            var invoices = await query.Skip((page - 1) * limit).Take(limit).Include(i => i.Items).ToListAsync();

            var poIds = invoices.Where(i => i.PurchaseOrderId.HasValue).Select(i => i.PurchaseOrderId!.Value).Distinct().ToList();
            var poNumbers = await _context.PurchaseOrders.AsNoTracking().Where(p => poIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.OrderNumber);

            return new PaginatedSupplierInvoiceResponse
            {
                Invoices = invoices.Select(i => MapToDto(i, i.PurchaseOrderId.HasValue ? poNumbers.GetValueOrDefault(i.PurchaseOrderId.Value) : null)).ToList(),
                Pagination = new PurchasePaginationInfo { Page = page, Limit = limit, Total = total, TotalPages = (int)Math.Ceiling((double)total / limit) }
            };
        }

        public async Task<SupplierInvoiceDto?> GetInvoiceByIdAsync(int id)
        {
            var inv = await _context.SupplierInvoices.AsNoTracking().Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (inv == null) return null;
            var poNumber = inv.PurchaseOrderId.HasValue
                ? await _context.PurchaseOrders.AsNoTracking().Where(p => p.Id == inv.PurchaseOrderId.Value).Select(p => p.OrderNumber).FirstOrDefaultAsync()
                : null;
            return MapToDto(inv, poNumber);
        }

        public async Task<SupplierInvoiceDto> CreateInvoiceAsync(CreateSupplierInvoiceDto dto, string userId, string? userName = null, string? idempotencyKey = null)
        {
            // ── Idempotency short-circuit ─────────────────────────────────
            // A retried POST (double-click, mobile flakiness, reverse-proxy
            // retry) carrying the same Idempotency-Key MUST NOT create a
            // duplicate invoice. When we see a prior row with the same key
            // for this tenant, return the existing DTO as-is.
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existingByKey = await _context.SupplierInvoices.AsNoTracking()
                    .Where(i => i.IdempotencyKey == idempotencyKey && !i.IsDeleted)
                    .Select(i => i.Id).FirstOrDefaultAsync();
                if (existingByKey > 0)
                    return (await GetInvoiceByIdAsync(existingByKey))!;
            }

            // ── Natural-key idempotency: (Supplier, SupplierInvoiceRef) ──
            // Same supplier's invoice reference cannot be booked twice.
            // Enforced at the DB layer by ux_supplier_invoices_tenant_supplier_ref;
            // pre-check here so we can throw a structured error instead of a
            // raw 23505 unique-violation string.
            await AssertNoDuplicateSupplierRefAsync(dto.SupplierId, dto.SupplierInvoiceRef);

            // Server-side re-validation of line-item bounds. DTO attributes
            // already reject bad payloads at model binding, but any caller
            // bypassing model binding still can't slip a negative quantity /
            // price / tax-rate into the totals recalculation.
            ValidateInvoiceItems(dto.Items);

            // Filter out soft-deleted contacts: Contact has no global IsDeleted query
            // filter, so FindAsync would let a tombstoned supplier be re-used on a new
            // invoice (copying its stale Name / MatriculeFiscale onto the header).
            var supplier = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == dto.SupplierId && !c.IsDeleted)
                ?? throw new KeyNotFoundException($"Supplier {dto.SupplierId} not found");

            // Cross-entity integrity: linked PO and GR must belong to the same supplier
            // as the invoice. Otherwise an invoice for Supplier A could reference
            // Supplier B's PO/items, breaking reporting and the items integrity below.
            if (dto.PurchaseOrderId.HasValue)
            {
                var po = await _context.PurchaseOrders.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == dto.PurchaseOrderId.Value && !p.IsDeleted)
                    ?? throw new KeyNotFoundException($"PurchaseOrder {dto.PurchaseOrderId} not found");
                if (po.SupplierId != dto.SupplierId)
                    throw new InvalidOperationException($"PurchaseOrder {po.Id} belongs to a different supplier");
            }
            if (dto.GoodsReceiptId.HasValue)
            {
                // BUG FIX: soft-delete filter was missing on this lookup while the PO
                // lookup two lines above filtered it. That allowed a new invoice to be
                // linked to a soft-deleted goods receipt, corrupting the paid-vs-received
                // reconciliation view. Match the PO filter.
                var gr = await _context.GoodsReceipts.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == dto.GoodsReceiptId.Value && !g.IsDeleted)
                    ?? throw new KeyNotFoundException($"GoodsReceipt {dto.GoodsReceiptId} not found");
                if (gr.SupplierId != dto.SupplierId)
                    throw new InvalidOperationException($"GoodsReceipt {gr.Id} belongs to a different supplier");
                if (dto.PurchaseOrderId.HasValue && gr.PurchaseOrderId != dto.PurchaseOrderId.Value)
                    throw new InvalidOperationException($"GoodsReceipt {gr.Id} does not belong to PO {dto.PurchaseOrderId}");
            }


            string invoiceNumber;
            try { invoiceNumber = _numberingService != null ? await _numberingService.GetNextAsync("SupplierInvoice") : $"SI-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}"; }
            catch { invoiceNumber = $"SI-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}"; }

            var invoice = new SupplierInvoice
            {
                InvoiceNumber = invoiceNumber,
                SupplierInvoiceRef = dto.SupplierInvoiceRef,
                SupplierId = dto.SupplierId,
                SupplierName = supplier.Name ?? string.Empty,
                SupplierMatriculeFiscale = supplier.MatriculeFiscale,
                PurchaseOrderId = dto.PurchaseOrderId,
                GoodsReceiptId = dto.GoodsReceiptId,
                InvoiceDate = AsUtc(dto.InvoiceDate),
                DueDate = AsUtc(dto.DueDate),
                Status = "draft",
                Currency = dto.Currency,
                Discount = dto.Discount,
                DiscountType = dto.DiscountType,
                FiscalStamp = dto.FiscalStamp,
                Notes = dto.Notes,
                RsApplicable = dto.RsApplicable,
                RsTypeCode = dto.RsTypeCode,
                RsOperationCode = dto.RsOperationCode,
                Cnpc = dto.Cnpc,
                PriseEnCharge = dto.PriseEnCharge,
                AnneeFacturation = dto.AnneeFacturation ?? dto.InvoiceDate.Year,
                RsTvaCode = dto.RsTvaCode,
                RsTvaTaux = dto.RsTvaTaux,
                IdempotencyKey = idempotencyKey,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            };


            // Validate PO-item linkage BEFORE inserting anything so a bad batch doesn't
            // leave an orphan invoice header. Was: header was saved, items validated,
            // throw → empty invoice persisted forever.
            // Also memoize each linked PO item's LineTotal (post-line-discount,
            // tax-exclusive) so invoice items copy the PO's discounted base instead
            // of recomputing Quantity*UnitPrice — otherwise a PO with per-line
            // discounts and the invoice generated from it would disagree on
            // SubTotal / TaxAmount / GrandTotal, defeating reconciliation.
            // Value = the PO line's post-discount tax-exclusive total AND its ordered
            // quantity, so a partial invoice can pro-rate instead of copying the full
            // PO line total (which over-billed every partial invoice).
            var linkedPoItemLineTotals = new Dictionary<int, (decimal LineTotal, decimal Quantity)>();
            if (dto.Items?.Any() == true)
            {
                var linkedPoItemIds = dto.Items.Where(i => i.PurchaseOrderItemId.HasValue)
                                               .Select(i => i.PurchaseOrderItemId!.Value)
                                               .Distinct().ToList();
                if (linkedPoItemIds.Count > 0)
                {
                    if (!dto.PurchaseOrderId.HasValue)
                        throw new InvalidOperationException("Cannot link PO items to an invoice that has no PurchaseOrderId");
                    var poItems = await _context.PurchaseOrderItems
                        .Where(p => linkedPoItemIds.Contains(p.Id) && p.PurchaseOrderId == dto.PurchaseOrderId.Value)
                        .Select(p => new { p.Id, p.LineTotal, p.Quantity, p.ReceivedQty, p.Description })
                        .ToListAsync();
                    var validIds = poItems.Select(p => p.Id).ToList();
                    var orphans = linkedPoItemIds.Except(validIds).ToList();
                    if (orphans.Count > 0)
                        throw new InvalidOperationException($"PurchaseOrderItem(s) [{string.Join(",", orphans)}] do not belong to PO {dto.PurchaseOrderId}");
                    foreach (var p in poItems) linkedPoItemLineTotals[p.Id] = (p.LineTotal, p.Quantity);

                    // ─── Three-way match guard (PO ↔ Goods Receipt ↔ Invoice) ───
                    // Never let the cumulative invoiced quantity of a PO line exceed
                    // what was ordered, nor what was actually received once at least
                    // one goods receipt exists for that line.
                    var alreadyInvoiced = await (
                        from it in _context.SupplierInvoiceItems
                        join inv in _context.SupplierInvoices on it.SupplierInvoiceId equals inv.Id
                        where it.PurchaseOrderItemId != null
                              && linkedPoItemIds.Contains(it.PurchaseOrderItemId.Value)
                              && !inv.IsDeleted && inv.Status != "cancelled"
                        group it by it.PurchaseOrderItemId into g
                        select new { PoItemId = g.Key!.Value, Qty = g.Sum(x => x.Quantity) }
                    ).ToListAsync();
                    var invoicedByPoItem = alreadyInvoiced.ToDictionary(x => x.PoItemId, x => x.Qty);

                    foreach (var grp in dto.Items.Where(i => i.PurchaseOrderItemId.HasValue)
                                                 .GroupBy(i => i.PurchaseOrderItemId!.Value))
                    {
                        var po = poItems.First(p => p.Id == grp.Key);
                        var newQty = grp.Sum(i => i.Quantity);
                        var prevQty = invoicedByPoItem.GetValueOrDefault(grp.Key, 0m);
                        var total = prevQty + newQty;
                        var label = po.Description ?? $"line {po.Id}";

                        if (total > po.Quantity)
                            throw new InvalidOperationException(
                                $"Cannot invoice {total} of \"{label}\": only {po.Quantity} was ordered ({prevQty} already invoiced).");

                        if (po.ReceivedQty > 0 && total > po.ReceivedQty)
                            throw new InvalidOperationException(
                                $"Cannot invoice {total} of \"{label}\": only {po.ReceivedQty} has been received ({prevQty} already invoiced). Record the goods receipt first.");
                    }
                }
            }

            // EnableRetryOnFailure requires user-initiated transactions to go through
            // an execution strategy (same fix as PurchaseOrderService.CreateOrderAsync).
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Serializable: the duplicate-ref and three-way-match guards above run
                // on a pre-transaction snapshot. Two concurrent creates against the same
                // PO line could both pass them and jointly over-invoice. Re-checking under
                // Serializable makes the read set part of the conflict detection, so one
                // of the two transactions is aborted instead of double-booking.
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    await AssertNoDuplicateSupplierRefAsync(dto.SupplierId, dto.SupplierInvoiceRef);
                    _context.SupplierInvoices.Add(invoice);
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex) when (
                        (ex.InnerException?.Message ?? string.Empty).Contains("ux_supplier_invoices_tenant_supplier_ref"))
                    {
                        throw new InvalidOperationException(
                            $"[DUPLICATE_SUPPLIER_REF] An invoice with reference '{dto.SupplierInvoiceRef}' already exists for this supplier");
                    }

                    if (dto.Items?.Any() == true)
                    {
                        var items = dto.Items.Select((item, idx) => new SupplierInvoiceItem
                        {
                            SupplierInvoiceId = invoice.Id,
                            PurchaseOrderItemId = item.PurchaseOrderItemId,
                            ArticleId = item.ArticleId,
                            ArticleName = item.ArticleName,
                            Description = item.Description,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TaxRate = item.TaxRate,
                            // LineTotal is tax-EXCLUSIVE so Sum(LineTotal) reconciles to SubTotal.
                            // When the line is linked to a PurchaseOrderItem, copy the PO's
                            // post-line-discount LineTotal verbatim so invoice totals reconcile
                            // with the originating PO even when the PO used per-line discounts.
                            // Pro-rate the PO line total by the quantity actually being
                            // invoiced: invoicing 2 of 10 ordered units must bill 20% of
                            // the PO line, not 100% of it.
                            LineTotal = item.PurchaseOrderItemId.HasValue
                                        && linkedPoItemLineTotals.TryGetValue(item.PurchaseOrderItemId.Value, out var poLine)
                                ? ProRatePoLineTotal(poLine.LineTotal, poLine.Quantity, item.Quantity, item.UnitPrice)
                                : item.Quantity * item.UnitPrice,
                            DisplayOrder = idx
                        }).ToList();
                        _context.SupplierInvoiceItems.AddRange(items);
                        await _context.SaveChangesAsync();

                        // Single source of truth for totals.
                        await RecalculateInvoiceTotalsAsync(invoice.Id);
                    }

                    _context.PurchaseActivities.Add(new PurchaseActivity
                    {
                        EntityType = "supplier_invoice", EntityId = invoice.Id, ActivityType = "created",
                        Description = $"Supplier invoice {invoiceNumber} created",
                        PerformedBy = userId, PerformedByName = userName, PerformedAt = DateTime.UtcNow
                    });

                    if (invoice.PurchaseOrderId.HasValue)
                        LogPoActivity(invoice.PurchaseOrderId.Value, "invoice_created",
                            $"Supplier invoice {invoiceNumber} created ({invoice.GrandTotal} {invoice.Currency})",
                            userId, userName);
                    await _context.SaveChangesAsync();

                    // Keep PO.PaymentStatus derived from the sum of its non-deleted,
                    // non-cancelled invoices (totalPaid vs totalDue).
                    if (invoice.PurchaseOrderId.HasValue)
                        await SyncPurchaseOrderPaymentStatusAsync(invoice.PurchaseOrderId.Value, userId, userName);

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return (await GetInvoiceByIdAsync(invoice.Id))!;
        }

        public async Task<SupplierInvoiceDto> UpdateInvoiceAsync(int id, UpdateSupplierInvoiceDto dto, string userId, string? userName = null)
        {
            // Wrap the entire mutation in a transaction with a SELECT...FOR UPDATE lock
            // on the invoice row. Without this, two concurrent PATCHes that both set
            // AmountPaid (e.g., two payment recordings posted from different tabs)
            // each read a stale snapshot, compute "paid" status against the same
            // pre-payment AmountPaid, and the second SaveChangesAsync overwrites the
            // first — silently losing a payment and producing wrong status.
            //
            // Recalc lives inside the same transaction so GrandTotal-driven status
            // derivation also sees a consistent base.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Postgres row-level lock; serializes any concurrent UpdateInvoiceAsync
                    // for the same invoice id. Other invoices remain unblocked.
                    var tenantId = _context.GetTenantId();
                    var invoice = await _context.SupplierInvoices
                        .FromSqlInterpolated($"SELECT * FROM \"SupplierInvoices\" WHERE \"Id\" = {id} AND \"TenantId\" = {tenantId} AND \"IsDeleted\" = false FOR UPDATE")
                        .FirstOrDefaultAsync()
                        ?? throw new KeyNotFoundException($"SupplierInvoice {id} not found");

                    var oldStatus = invoice.Status;
                    var oldAmountPaid = invoice.AmountPaid;
                    var oldFelId = invoice.FactureEnLigneId;
                    var oldFelStatus = invoice.FactureEnLigneStatus;
                    // Field-level audit snapshot: every header edit is recorded, not just
                    // the status transition.
                    var beforeSnapshot = SnapshotInvoice(invoice);

                    // ── TEJ compliance guard ───────────────────────────────────────
                    // Once the invoice has been declared to the DGI (TejSynced), editing
                    // any figure that feeds the certificate silently invalidates the filed
                    // declaration. We do not block the edit (real-world corrections happen)
                    // but we force it back into the rectification lane: the certificate
                    // must be re-filed as a "Modifier" deposit.
                    var fiscallyMaterialEdit =
                        dto.Discount.HasValue || dto.DiscountType != null || dto.FiscalStamp.HasValue
                        || dto.RsApplicable.HasValue || dto.RsTypeCode != null || dto.RsOperationCode != null
                        || dto.RsTvaCode != null || dto.RsTvaTaux.HasValue
                        || dto.AmountPaid.HasValue || dto.PaymentDate.HasValue
                        || dto.SupplierInvoiceRef != null
                        || dto.PriseEnCharge.HasValue || dto.AnneeFacturation.HasValue;
                    var requiresTejResync = invoice.TejSynced && fiscallyMaterialEdit
                                            && dto.TejSynced != true; // explicit re-sync marking wins
                    if (dto.SupplierInvoiceRef != null) invoice.SupplierInvoiceRef = dto.SupplierInvoiceRef;
                    if (dto.Status != null)
                    {
                        if (oldStatus == "cancelled" && dto.Status != "cancelled")
                            throw new InvalidOperationException("[INVALID_TRANSITION] Cancelled invoices cannot change status");
                        if (oldStatus == "paid" && dto.Status != "paid" && dto.Status != "cancelled")
                            throw new InvalidOperationException($"[INVALID_TRANSITION] Paid invoices cannot transition to '{dto.Status}'");
                        invoice.Status = dto.Status;
                    }
                    if (dto.DueDate.HasValue) invoice.DueDate = AsUtc(dto.DueDate.Value);
                    if (dto.Discount.HasValue) invoice.Discount = dto.Discount.Value;
                    if (dto.DiscountType != null) invoice.DiscountType = dto.DiscountType;
                    if (dto.FiscalStamp.HasValue) invoice.FiscalStamp = dto.FiscalStamp.Value;
                    if (dto.PaymentMethod != null) invoice.PaymentMethod = dto.PaymentMethod;

                    // Recalc totals BEFORE validating AmountPaid / deriving payment-status.
                    // Otherwise a single PATCH that changes Discount AND AmountPaid would:
                    //   1. validate AmountPaid against the OLD GrandTotal
                    //   2. derive "paid"/"partially_paid" from the OLD GrandTotal
                    //   3. recalc totals at the end → status/overpayment guard now stale
                    // Persist the field changes first so RecalculateInvoiceTotalsAsync sees them.
                    var totalsAffected = dto.Discount.HasValue || dto.DiscountType != null
                        || dto.FiscalStamp.HasValue
                        || dto.RsApplicable.HasValue || dto.RsTypeCode != null;
                    if (totalsAffected)
                    {
                        // Apply RS fields here too so recalc has the right inputs.
                        if (dto.RsApplicable.HasValue) invoice.RsApplicable = dto.RsApplicable.Value;
                        if (dto.RsTypeCode != null) invoice.RsTypeCode = dto.RsTypeCode;
                        await _context.SaveChangesAsync();
                        await RecalculateInvoiceTotalsAsync(id);
                        // Refresh in-memory aggregate from the DB so the AmountPaid block below
                        // and the status-derivation see the fresh GrandTotal.
                        await _context.Entry(invoice).ReloadAsync();
                    }

                    if (dto.AmountPaid.HasValue)
                    {
                        if (dto.AmountPaid.Value < 0)
                            throw new InvalidOperationException("[INVALID_AMOUNT_PAID] AmountPaid cannot be negative");
                        // Guard: prevent overpayment when a concurrent payment already
                        // settled the invoice. The locked read above guarantees we see
                        // the latest persisted AmountPaid here.
                        if (invoice.GrandTotal > 0 && dto.AmountPaid.Value > invoice.GrandTotal)
                            throw new InvalidOperationException(
                                $"[OVERPAYMENT] AmountPaid ({dto.AmountPaid.Value}) exceeds GrandTotal ({invoice.GrandTotal}); a concurrent payment may have settled this invoice");

                        invoice.AmountPaid = dto.AmountPaid.Value;
                        if (dto.Status == null && oldStatus != "cancelled")
                        {
                            if (invoice.AmountPaid >= invoice.GrandTotal && invoice.GrandTotal > 0)
                            {
                                invoice.Status = "paid";
                                invoice.PaymentDate ??= DateTime.UtcNow;
                            }
                            else if (invoice.AmountPaid > 0)
                            {
                                invoice.Status = "partially_paid";
                            }
                            else
                            {
                                invoice.Status = "pending";
                                invoice.PaymentDate = null;
                            }
                        }
                    }
                    if (dto.PaymentDate.HasValue) invoice.PaymentDate = AsUtc(dto.PaymentDate);
                    if (dto.Notes != null) invoice.Notes = dto.Notes;
                    // RsApplicable/RsTypeCode already applied above when totalsAffected;
                    // apply here for the case where they weren't passed alongside other
                    // total-affecting fields (defensive — same branch wouldn't fire twice
                    // because totalsAffected is true iff one of these is set).
                    if (!totalsAffected)
                    {
                        if (dto.RsApplicable.HasValue) invoice.RsApplicable = dto.RsApplicable.Value;
                        if (dto.RsTypeCode != null) invoice.RsTypeCode = dto.RsTypeCode;
                    }
                    // TEJ / RiTEJ fields
                    if (dto.RsOperationCode != null) invoice.RsOperationCode = dto.RsOperationCode;
                    if (dto.Cnpc != null) invoice.Cnpc = dto.Cnpc;
                    if (dto.PriseEnCharge.HasValue) invoice.PriseEnCharge = dto.PriseEnCharge.Value;
                    if (dto.AnneeFacturation.HasValue) invoice.AnneeFacturation = dto.AnneeFacturation;
                    if (dto.RefCertifChezDeclarant != null) invoice.RefCertifChezDeclarant = dto.RefCertifChezDeclarant;
                    if (dto.RsTvaCode != null) invoice.RsTvaCode = dto.RsTvaCode;
                    if (dto.RsTvaTaux.HasValue) invoice.RsTvaTaux = dto.RsTvaTaux;
                    if (dto.TejActe.HasValue) invoice.TejActe = dto.TejActe.Value;
                    if (dto.TejSynced.HasValue) invoice.TejSynced = dto.TejSynced.Value;
                    if (dto.TejSyncDate.HasValue) invoice.TejSyncDate = AsUtc(dto.TejSyncDate);
                    if (dto.TejSyncStatus != null) invoice.TejSyncStatus = dto.TejSyncStatus;
                    if (dto.TejErrorMessage != null) invoice.TejErrorMessage = dto.TejErrorMessage;
                    if (dto.FactureEnLigneId != null) invoice.FactureEnLigneId = dto.FactureEnLigneId;
                    if (dto.FactureEnLigneStatus != null) invoice.FactureEnLigneStatus = dto.FactureEnLigneStatus;
                    if (dto.FactureEnLigneSentAt.HasValue) invoice.FactureEnLigneSentAt = AsUtc(dto.FactureEnLigneSentAt);

                    // Apply the TEJ rectification lane decided above. The certificate is
                    // no longer in sync with what was filed, so flag it for a "Modifier"
                    // (Acte = 1) re-deposit and reopen the linked RS record.
                    if (requiresTejResync)
                    {
                        invoice.TejSynced = false;
                        invoice.TejSyncStatus = "requires_resync";
                        if (invoice.TejActe == 0) invoice.TejActe = 1; // 1 = ModifierCertificats
                        invoice.TejErrorMessage =
                            "Invoice edited after TEJ export — a rectifying deposit (Acte=Modifier) must be re-generated and filed.";

                        if (invoice.RsRecordId.HasValue)
                        {
                            var rs = await _context.RSRecords
                                .FirstOrDefaultAsync(r => r.Id == invoice.RsRecordId.Value && !r.IsDeleted);
                            if (rs != null)
                            {
                                rs.Status = "pending";
                                rs.TEJExported = false;
                                rs.TEJTransmissionStatus = "pending";
                                if (rs.Acte == 0) rs.Acte = 1;
                                rs.DepotSequence += 1;   // next file for the month is a rectification
                                rs.ModifiedAt = DateTime.UtcNow;
                                rs.ModifiedBy = userId;
                            }
                        }

                        _context.PurchaseActivities.Add(new PurchaseActivity
                        {
                            EntityType = "supplier_invoice", EntityId = id, ActivityType = "tej_resync_required",
                            Description = "Fiscal fields changed after TEJ export; certificate flagged for rectifying deposit",
                            PerformedBy = userId, PerformedByName = userName, PerformedAt = DateTime.UtcNow
                        });
                    }

                    invoice.ModifiedDate = DateTime.UtcNow;
                    invoice.ModifiedBy = userId;

                    if (invoice.Status != oldStatus)
                    {
                        LogInvoiceActivity(id, "status_changed",
                            $"Status changed from {oldStatus} to {invoice.Status}",
                            userId, userName, oldStatus, invoice.Status);
                        if (invoice.PurchaseOrderId.HasValue)
                            LogPoActivity(invoice.PurchaseOrderId.Value, "invoice_status_changed",
                                $"Invoice {invoice.InvoiceNumber} status changed from {oldStatus} to {invoice.Status}",
                                userId, userName, oldStatus, invoice.Status);
                    }

                    // Payment recorded / adjusted — an explicit, first-class audit event.
                    if (dto.AmountPaid.HasValue && invoice.AmountPaid != oldAmountPaid)
                    {
                        var delta = invoice.AmountPaid - oldAmountPaid;
                        LogInvoiceActivity(id, delta > 0 ? "payment_recorded" : "payment_adjusted",
                            $"Amount paid changed from {oldAmountPaid} to {invoice.AmountPaid} ({(delta > 0 ? "+" : "")}{delta} {invoice.Currency})",
                            userId, userName, oldAmountPaid.ToString(), invoice.AmountPaid.ToString());
                        if (invoice.PurchaseOrderId.HasValue)
                            LogPoActivity(invoice.PurchaseOrderId.Value,
                                delta > 0 ? "payment_recorded" : "payment_adjusted",
                                $"Payment on invoice {invoice.InvoiceNumber}: {(delta > 0 ? "+" : "")}{delta} {invoice.Currency} (total paid {invoice.AmountPaid} of {invoice.GrandTotal})",
                                userId, userName, oldAmountPaid.ToString(), invoice.AmountPaid.ToString());
                    }

                    // Facture en Ligne (TTN) submission recorded.
                    if ((dto.FactureEnLigneId != null && invoice.FactureEnLigneId != oldFelId)
                        || (dto.FactureEnLigneStatus != null && invoice.FactureEnLigneStatus != oldFelStatus))
                    {
                        LogInvoiceActivity(id, "facture_en_ligne_recorded",
                            $"Facture en Ligne submission recorded (ref {invoice.FactureEnLigneId}, status {invoice.FactureEnLigneStatus})",
                            userId, userName, oldFelStatus, invoice.FactureEnLigneStatus);
                    }

                    // Everything else that changed on the header.
                    foreach (var (field, oldVal, newVal) in PurchaseOrderService.DiffSnapshots(beforeSnapshot, SnapshotInvoice(invoice)))
                    {
                        if (field is "Status" or "AmountPaid" or "FactureEnLigneId" or "FactureEnLigneStatus") continue;
                        LogInvoiceActivity(id, "updated",
                            $"{field} changed from '{PurchaseOrderService.Shorten(oldVal)}' to '{PurchaseOrderService.Shorten(newVal)}'",
                            userId, userName, PurchaseOrderService.Shorten(oldVal), PurchaseOrderService.Shorten(newVal));
                    }
                    await _context.SaveChangesAsync();

                    // Re-derive PO.PaymentStatus whenever AmountPaid, Status, or
                    // GrandTotal-driving fields change. Skipping this would let the PO
                    // claim "pending" while invoices for it are fully paid.
                    if (invoice.PurchaseOrderId.HasValue &&
                        (dto.AmountPaid.HasValue || dto.Status != null
                         || dto.Discount.HasValue || dto.DiscountType != null
                         || dto.FiscalStamp.HasValue
                         || dto.RsApplicable.HasValue || dto.RsTypeCode != null))
                    {
                        await SyncPurchaseOrderPaymentStatusAsync(invoice.PurchaseOrderId.Value, userId, userName);
                    }

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return (await GetInvoiceByIdAsync(id))!;
        }

        public async Task<bool> DeleteInvoiceAsync(int id, string userId, string? userName = null)
        {
            var invoice = await _context.SupplierInvoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (invoice == null) return false;

            // Financial-integrity guard: once cash has actually been recorded against
            // an invoice, soft-deleting it would silently rewrite the payment ledger.
            // SyncPurchaseOrderPaymentStatusAsync below excludes IsDeleted invoices,
            // so the PO's PaymentStatus would flip back to "pending"/"partial" while
            // the money movement stays in Payments (or the accounting export) — a
            // classic reconciliation hole. Force the caller to "cancel" (status
            // transition) instead, which keeps the row in ledger aggregations.
            if (invoice.AmountPaid > 0
                || string.Equals(invoice.Status, "paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.Status, "partially_paid", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Cannot delete invoice {invoice.InvoiceNumber}: it has recorded payments (AmountPaid={invoice.AmountPaid}, Status={invoice.Status}). Cancel the invoice or reverse the payment first.");
            }

            // Fiscal-integrity guard: a declared invoice cannot simply vanish. The DGI
            // holds a certificate for it; removing the row would leave the filed
            // declaration unbacked and untraceable. The correct move is an annulment
            // (Acte = 2) filed as a rectifying deposit.
            if (invoice.TejSynced)
            {
                throw new InvalidOperationException(
                    $"Cannot delete invoice {invoice.InvoiceNumber}: it was already declared to the DGI (TEJ export). File an annulment (Acte=Annuler) instead.");
            }

            // Wrap soft-delete + activity-log + PO sync in a single transaction so a
            // failure in the cascading PO.PaymentStatus sync rolls back the soft-delete.
            // Without this, the invoice could disappear from the ledger while the PO
            // still claims "paid" against the now-missing invoice.
            // EnableRetryOnFailure on Npgsql requires the execution-strategy wrapper.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var tracked = await _context.SupplierInvoices
                        .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
                    if (tracked == null) return; // raced with another delete

                    // Re-assert the payment guard under the transaction — a concurrent
                    // PATCH could have just recorded a payment between the pre-check
                    // and here.
                    if (tracked.AmountPaid > 0
                        || string.Equals(tracked.Status, "paid", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tracked.Status, "partially_paid", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Cannot delete invoice {tracked.InvoiceNumber}: a concurrent payment was recorded (AmountPaid={tracked.AmountPaid}, Status={tracked.Status}).");
                    }

                    tracked.IsDeleted = true;
                    tracked.DeletedAt = DateTime.UtcNow;
                    tracked.DeletedBy = userId;
                    _context.PurchaseActivities.Add(new PurchaseActivity
                    {
                        EntityType = "supplier_invoice", EntityId = id, ActivityType = "deleted",
                        Description = $"Supplier invoice {tracked.InvoiceNumber} deleted",
                        PerformedBy = userId, PerformedByName = userName, PerformedAt = DateTime.UtcNow
                    });

                    if (tracked.PurchaseOrderId.HasValue)
                        LogPoActivity(tracked.PurchaseOrderId.Value, "invoice_deleted",
                            $"Supplier invoice {tracked.InvoiceNumber} deleted",
                            userId, userName);
                    await _context.SaveChangesAsync();

                    if (tracked.PurchaseOrderId.HasValue)
                        await SyncPurchaseOrderPaymentStatusAsync(tracked.PurchaseOrderId.Value, userId, userName);

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return true;
        }

        // Recompute PurchaseOrder.PaymentStatus from all live, non-cancelled supplier
        // invoices linked to the PO. Single source of truth: the cumulative invoice
        // ledger drives the PO's payment state, not manual updates.
        //   paid     → totalDue > 0 AND totalPaid >= totalDue
        //   partial  → totalPaid > 0 (but not enough to settle)
        //   pending  → no paid amount recorded (or no live invoices)
        private async Task SyncPurchaseOrderPaymentStatusAsync(int purchaseOrderId, string? userId = null, string? userName = null)
        {
            var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == purchaseOrderId && !p.IsDeleted);
            if (po == null) return;

            var liveInvoices = await _context.SupplierInvoices.AsNoTracking()
                .Where(i => i.PurchaseOrderId == purchaseOrderId && !i.IsDeleted && i.Status != "cancelled")
                .Select(i => new { i.GrandTotal, i.AmountPaid })
                .ToListAsync();

            var totalDue = liveInvoices.Sum(i => i.GrandTotal);
            var totalPaid = liveInvoices.Sum(i => i.AmountPaid);

            string newStatus;
            if (totalDue > 0 && totalPaid >= totalDue) newStatus = "paid";
            else if (totalPaid > 0) newStatus = "partial";
            else newStatus = "pending";

            if (po.PaymentStatus != newStatus)
            {
                var oldPaymentStatus = po.PaymentStatus;
                po.PaymentStatus = newStatus;
                po.ModifiedDate = DateTime.UtcNow;
                LogPoActivity(po.Id, "payment_status_changed",
                    $"Payment status changed from {oldPaymentStatus} to {newStatus} (paid {totalPaid} of {totalDue})",
                    userId ?? "system", userName, oldPaymentStatus, newStatus);
                await _context.SaveChangesAsync();
            }
        }

        // ── Items ──
        public async Task<SupplierInvoiceItemDto> AddItemAsync(int invoiceId, CreateSupplierInvoiceItemDto dto, string? userId = null, string? userName = null)
        {
            var invoice = await _context.SupplierInvoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted)
                ?? throw new KeyNotFoundException($"SupplierInvoice {invoiceId} not found");
            if (invoice.Status != "draft")
                throw new InvalidOperationException("Items can only be modified on draft invoices");

            // If the line is linked to a PO, the PO item must belong to THIS invoice's PO.
            if (dto.PurchaseOrderItemId.HasValue)
            {
                if (!invoice.PurchaseOrderId.HasValue)
                    throw new InvalidOperationException("Cannot link a PO item to an invoice that has no PurchaseOrderId");
                var poItemExists = await _context.PurchaseOrderItems.AnyAsync(p =>
                    p.Id == dto.PurchaseOrderItemId.Value && p.PurchaseOrderId == invoice.PurchaseOrderId.Value);
                if (!poItemExists)
                    throw new InvalidOperationException($"PurchaseOrderItem {dto.PurchaseOrderItemId} does not belong to PO {invoice.PurchaseOrderId}");
            }

            // Server-side re-validation matches CreateInvoiceAsync — item mutations
            // arriving via a raw HTTP call (bypassing model binding) still can't
            // slip a negative qty / price / tax-rate past the totals recalc.
            ValidateInvoiceItem(dto);

            var lineTotal = await ResolveLineTotalAsync(dto);

            // Wrap insert + totals-recalc + PO-status-sync in a single transaction
            // (via the execution strategy so Npgsql retries still work). Without it
            // a crash between SaveChanges calls would commit the item row but leave
            // SubTotal / TaxAmount / GrandTotal and the linked PO's PaymentStatus
            // permanently inconsistent.
            SupplierInvoiceItem? created = null;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var item = new SupplierInvoiceItem
                    {
                        SupplierInvoiceId = invoiceId,
                        PurchaseOrderItemId = dto.PurchaseOrderItemId,
                        ArticleId = dto.ArticleId,
                        ArticleName = dto.ArticleName,
                        Description = dto.Description,
                        Quantity = dto.Quantity,
                        UnitPrice = dto.UnitPrice,
                        TaxRate = dto.TaxRate,
                        LineTotal = lineTotal,
                        DisplayOrder = invoice.Items?.Count ?? 0
                    };
                    _context.SupplierInvoiceItems.Add(item);
                    await _context.SaveChangesAsync();

                    LogInvoiceActivity(invoiceId, "item_added",
                        $"Line added: {DescribeInvoiceItem(item)}", userId ?? "system", userName,
                        null, PurchaseOrderService.Shorten(DescribeInvoiceItem(item)));
                    await _context.SaveChangesAsync();

                    await RecalculateInvoiceTotalsAsync(invoiceId);

                    // Draft invoices still count toward PO totalDue (Sync excludes only
                    // cancelled/deleted), so an item add can flip PO.PaymentStatus from
                    // "paid" to "partial". Mirror the header-mutation paths.
                    if (invoice.PurchaseOrderId.HasValue)
                        await SyncPurchaseOrderPaymentStatusAsync(invoice.PurchaseOrderId.Value, userId, userName);

                    await tx.CommitAsync();
                    created = item;
                }
                catch { await tx.RollbackAsync(); throw; }
            });
            return MapItemToDto(created!);
        }

        public async Task<SupplierInvoiceItemDto> UpdateItemAsync(int invoiceId, int itemId, CreateSupplierInvoiceItemDto dto, string? userId = null, string? userName = null)
        {
            var invoice = await _context.SupplierInvoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted)
                ?? throw new KeyNotFoundException($"SupplierInvoice {invoiceId} not found");
            if (invoice.Status != "draft")
                throw new InvalidOperationException("Items can only be modified on draft invoices");

            var item = invoice.Items?.FirstOrDefault(i => i.Id == itemId)
                ?? throw new KeyNotFoundException($"Item {itemId} not found");

            // Mirror AddItemAsync: a PO-linked line must reference a PO item that
            // belongs to THIS invoice's PurchaseOrderId. Without this guard,
            // ResolveLineTotalAsync would happily copy an unrelated PO line's
            // LineTotal onto our invoice (breaking reconciliation and letting a
            // caller inflate totals with another supplier's PO item).
            if (dto.PurchaseOrderItemId.HasValue)
            {
                if (!invoice.PurchaseOrderId.HasValue)
                    throw new InvalidOperationException("Cannot link a PO item to an invoice that has no PurchaseOrderId");
                var poItemExists = await _context.PurchaseOrderItems.AnyAsync(p =>
                    p.Id == dto.PurchaseOrderItemId.Value && p.PurchaseOrderId == invoice.PurchaseOrderId.Value);
                if (!poItemExists)
                    throw new InvalidOperationException($"PurchaseOrderItem {dto.PurchaseOrderItemId} does not belong to PO {invoice.PurchaseOrderId}");
            }

            ValidateInvoiceItem(dto);
            var lineTotal = await ResolveLineTotalAsync(dto);

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var itemBefore = DescribeInvoiceItem(item);
                    item.ArticleId = dto.ArticleId;
                    item.ArticleName = dto.ArticleName;
                    item.Description = dto.Description;
                    item.Quantity = dto.Quantity;
                    item.UnitPrice = dto.UnitPrice;
                    item.TaxRate = dto.TaxRate;
                    item.LineTotal = lineTotal;
                    var itemAfter = DescribeInvoiceItem(item);
                    if (itemBefore != itemAfter)
                        LogInvoiceActivity(invoiceId, "item_updated",
                            $"Line updated: {itemBefore} → {itemAfter}", userId ?? "system", userName,
                            PurchaseOrderService.Shorten(itemBefore), PurchaseOrderService.Shorten(itemAfter));
                    await _context.SaveChangesAsync();

                    await RecalculateInvoiceTotalsAsync(invoiceId);

                    if (invoice.PurchaseOrderId.HasValue)
                        await SyncPurchaseOrderPaymentStatusAsync(invoice.PurchaseOrderId.Value, userId, userName);

                    await tx.CommitAsync();
                }
                catch { await tx.RollbackAsync(); throw; }
            });
            return MapItemToDto(item);
        }

        public async Task<bool> DeleteItemAsync(int invoiceId, int itemId, string? userId = null, string? userName = null)
        {
            var invoice = await _context.SupplierInvoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted)
                ?? throw new KeyNotFoundException($"SupplierInvoice {invoiceId} not found");
            if (invoice.Status != "draft")
                throw new InvalidOperationException("Items can only be modified on draft invoices");

            var item = invoice.Items?.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var removedDesc = DescribeInvoiceItem(item);
                    _context.SupplierInvoiceItems.Remove(item);
                    await _context.SaveChangesAsync();

                    LogInvoiceActivity(invoiceId, "item_deleted",
                        $"Line removed: {removedDesc}", userId ?? "system", userName,
                        PurchaseOrderService.Shorten(removedDesc), null);
                    await _context.SaveChangesAsync();

                    await RecalculateInvoiceTotalsAsync(invoiceId);

                    if (invoice.PurchaseOrderId.HasValue)
                        await SyncPurchaseOrderPaymentStatusAsync(invoice.PurchaseOrderId.Value, userId, userName);

                    await tx.CommitAsync();
                }
                catch { await tx.RollbackAsync(); throw; }
            });
            return true;
        }

        // When a SupplierInvoiceItem is linked to a PurchaseOrderItem, use the PO
        // line's post-line-discount LineTotal (tax-exclusive) so the invoice reconciles
        // with the originating PO — even when per-line discounts were applied on the PO.
        // For standalone items (no PO link) fall back to Quantity * UnitPrice.
        private async Task<decimal> ResolveLineTotalAsync(CreateSupplierInvoiceItemDto dto)
        {
            if (dto.PurchaseOrderItemId.HasValue)
            {
                var poLine = await _context.PurchaseOrderItems
                    .Where(p => p.Id == dto.PurchaseOrderItemId.Value)
                    .Select(p => new { p.LineTotal, p.Quantity })
                    .FirstOrDefaultAsync();
                if (poLine != null)
                    return ProRatePoLineTotal(poLine.LineTotal, poLine.Quantity, dto.Quantity, dto.UnitPrice);
            }
            return dto.Quantity * dto.UnitPrice;
        }

        // A PO line's LineTotal covers the FULL ordered quantity (net of the line
        // discount). An invoice line may cover only part of it, so scale by
        // invoicedQty / orderedQty. Falls back to qty*unitPrice when the PO quantity
        // is unusable (0 / negative), which would otherwise divide by zero.
        private static decimal ProRatePoLineTotal(decimal poLineTotal, decimal poQuantity, decimal invoicedQty, decimal unitPrice)
        {
            if (poQuantity <= 0) return invoicedQty * unitPrice;
            if (invoicedQty == poQuantity) return poLineTotal;
            return Math.Round(poLineTotal * invoicedQty / poQuantity, 2);
        }

        private async Task RecalculateInvoiceTotalsAsync(int invoiceId)
        {
            var invoice = await _context.SupplierInvoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null) return;
            var items = invoice.Items?.ToList() ?? new List<SupplierInvoiceItem>();
            // SubTotal sums LineTotal (post-line-discount, tax-exclusive) instead of
            // raw Quantity*UnitPrice so PO-linked invoice lines reconcile with the
            // originating PO's afterLineDiscount base.
            invoice.SubTotal = items.Sum(i => i.LineTotal);
            var discAmt = invoice.DiscountType == "percentage" ? invoice.SubTotal * invoice.Discount / 100 : invoice.Discount;
            var afterDiscount = invoice.SubTotal - discAmt;

            // Tax must be computed on the DISCOUNTED base so the invoice reconciles
            // with the originating PO (which applies discount before tax). Previously
            // we taxed the gross sum, over-reporting VAT whenever a header discount existed.
            // We pro-rate the header discount by line subtotal so per-line tax rates are preserved.
            var subTotal = invoice.SubTotal;
            invoice.TaxAmount = subTotal > 0
                ? items.Sum(i =>
                {
                    var lineSub = i.LineTotal;
                    var lineShare = lineSub / subTotal;       // proportion of header discount that applies to this line
                    var lineAfterDisc = lineSub - (discAmt * lineShare);
                    return lineAfterDisc * i.TaxRate / 100;
                })
                : 0m;

            // RS (retenue à la source). Always reset first so toggling RsApplicable off
            // (or clearing RsTypeCode) actually removes a previously-applied retention.
            invoice.RsAmount = 0m;
            invoice.RsTvaAmount = 0m;
            if (invoice.RsApplicable && !string.IsNullOrEmpty(invoice.RsTypeCode))
            {
                // Single source of truth (RsRates); the declared TEJ operation code wins
                // over the legacy short code so RsAmount always matches what the XML declares.
                var rsRate = RsRates.GetEffectiveRate(invoice.RsOperationCode, invoice.RsTypeCode);
                invoice.RsAmount = Math.Round(afterDiscount * rsRate / 100m, 2);
                // RS-TVA (separate withholding on VAT) when configured
                if (invoice.RsTvaTaux is decimal tvaRate && tvaRate > 0)
                    invoice.RsTvaAmount = Math.Round(invoice.TaxAmount * tvaRate / 100m, 2);
            }
            // Floor non-negative: a header discount larger than SubTotal (or an RS that
            // exceeds the discounted base) would otherwise produce a negative GrandTotal,
            // breaking the AmountPaid > GrandTotal overpayment guard and the
            // PO.PaymentStatus sync (totalDue<=0 → "paid" without any cash recorded).
            invoice.GrandTotal = Math.Max(0,
                afterDiscount + invoice.TaxAmount + invoice.FiscalStamp
                - invoice.RsAmount - invoice.RsTvaAmount);
            await _context.SaveChangesAsync();
        }

        // Item validation guard. DTO attributes already reject bad payloads at
        // model binding, but any caller bypassing the pipeline still hits these
        // — negative quantities, negative prices, or tax rates outside 0-100
        // would silently corrupt SubTotal / TaxAmount / GrandTotal.
        private static void ValidateInvoiceItem(CreateSupplierInvoiceItemDto item)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException($"[INVALID_QUANTITY] Line quantity must be greater than zero (got {item.Quantity})");
            if (item.UnitPrice < 0)
                throw new InvalidOperationException($"[INVALID_UNIT_PRICE] Line unit price cannot be negative (got {item.UnitPrice})");
            if (item.TaxRate < 0 || item.TaxRate > 100)
                throw new InvalidOperationException($"[INVALID_TAX_RATE] Line tax rate must be between 0 and 100 (got {item.TaxRate})");
            if (string.IsNullOrWhiteSpace(item.Description))
                throw new InvalidOperationException("[INVALID_DESCRIPTION] Line description is required");
        }

        private static void ValidateInvoiceItems(IEnumerable<CreateSupplierInvoiceItemDto>? items)
        {
            if (items == null) return;
            foreach (var i in items) ValidateInvoiceItem(i);
        }

        private static SupplierInvoiceItemDto MapItemToDto(SupplierInvoiceItem i)

        {
            return new SupplierInvoiceItemDto
            {
                Id = i.Id,
                SupplierInvoiceId = i.SupplierInvoiceId,
                PurchaseOrderItemId = i.PurchaseOrderItemId,
                ArticleId = i.ArticleId,
                ArticleName = i.ArticleName,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxRate = i.TaxRate,
                LineTotal = i.LineTotal,
                DisplayOrder = i.DisplayOrder
            };
        }

        private static SupplierInvoiceDto MapToDto(SupplierInvoice inv, string? poNumber) => new()
        {
            Id = inv.Id, InvoiceNumber = inv.InvoiceNumber, SupplierInvoiceRef = inv.SupplierInvoiceRef,
            SupplierId = inv.SupplierId, SupplierName = inv.SupplierName,
            SupplierMatriculeFiscale = inv.SupplierMatriculeFiscale,
            PurchaseOrderId = inv.PurchaseOrderId, PurchaseOrderNumber = poNumber,
            GoodsReceiptId = inv.GoodsReceiptId, InvoiceDate = inv.InvoiceDate, DueDate = inv.DueDate,
            Status = inv.Status, Currency = inv.Currency, SubTotal = inv.SubTotal,
            Discount = inv.Discount, DiscountType = inv.DiscountType, TaxAmount = inv.TaxAmount,
            FiscalStamp = inv.FiscalStamp, GrandTotal = inv.GrandTotal, AmountPaid = inv.AmountPaid,
            PaymentMethod = inv.PaymentMethod, PaymentDate = inv.PaymentDate, Notes = inv.Notes,
            RsApplicable = inv.RsApplicable, RsTypeCode = inv.RsTypeCode, RsAmount = inv.RsAmount,
            RsRecordId = inv.RsRecordId,
            RsOperationCode = inv.RsOperationCode, Cnpc = inv.Cnpc,
            PriseEnCharge = inv.PriseEnCharge, AnneeFacturation = inv.AnneeFacturation,
            RefCertifChezDeclarant = inv.RefCertifChezDeclarant,
            RsTvaCode = inv.RsTvaCode, RsTvaTaux = inv.RsTvaTaux, RsTvaAmount = inv.RsTvaAmount,
            TejActe = inv.TejActe,
            FactureEnLigneId = inv.FactureEnLigneId, FactureEnLigneStatus = inv.FactureEnLigneStatus,
            FactureEnLigneSentAt = inv.FactureEnLigneSentAt,
            TejSynced = inv.TejSynced, TejSyncDate = inv.TejSyncDate, TejSyncStatus = inv.TejSyncStatus,
            TejErrorMessage = inv.TejErrorMessage,
            Items = inv.Items?.Select(i => MapItemToDto(i)).ToList(),
            CreatedDate = inv.CreatedDate, CreatedBy = inv.CreatedBy, ModifiedDate = inv.ModifiedDate, ModifiedBy = inv.ModifiedBy
        };

        // ── Activity audit helpers ─────────────────────────────────────────────
        // Cross-post onto the parent purchase order timeline so PO Activity shows
        // the full financial leg (invoice created / status / payment / deleted).
        private void LogPoActivity(int purchaseOrderId, string activityType, string description,
            string userId, string? userName, string? oldValue = null, string? newValue = null)
        {
            _context.PurchaseActivities.Add(new PurchaseActivity
            {
                EntityType = "purchase_order",
                EntityId = purchaseOrderId,
                ActivityType = activityType,
                Description = PurchaseOrderService.Shorten(description, 900) ?? string.Empty,
                OldValue = PurchaseOrderService.Shorten(oldValue),
                NewValue = PurchaseOrderService.Shorten(newValue),
                PerformedBy = userId,
                PerformedByName = userName,
                PerformedAt = DateTime.UtcNow
            });
        }

        private void LogInvoiceActivity(int invoiceId, string activityType, string description,
            string userId, string? userName, string? oldValue, string? newValue)
        {
            _context.PurchaseActivities.Add(new PurchaseActivity
            {
                EntityType = "supplier_invoice",
                EntityId = invoiceId,
                ActivityType = activityType,
                Description = PurchaseOrderService.Shorten(description, 900),
                OldValue = PurchaseOrderService.Shorten(oldValue),
                NewValue = PurchaseOrderService.Shorten(newValue),
                PerformedBy = userId,
                PerformedByName = userName,
                PerformedAt = DateTime.UtcNow
            });
        }

        private static string DescribeInvoiceItem(SupplierInvoiceItem i)
            => $"{(string.IsNullOrWhiteSpace(i.ArticleName) ? (string.IsNullOrWhiteSpace(i.Description) ? "item" : i.Description) : i.ArticleName)} | qty {i.Quantity} | price {i.UnitPrice} | tax {i.TaxRate}% | total {i.LineTotal}";

        private static Dictionary<string, string?> SnapshotInvoice(SupplierInvoice inv) => new()
        {
            ["SupplierInvoiceRef"] = inv.SupplierInvoiceRef,
            ["Status"] = inv.Status,
            ["DueDate"] = inv.DueDate.ToString("u"),
            ["Discount"] = inv.Discount.ToString(),
            ["DiscountType"] = inv.DiscountType,
            ["FiscalStamp"] = inv.FiscalStamp.ToString(),
            ["PaymentMethod"] = inv.PaymentMethod,
            ["AmountPaid"] = inv.AmountPaid.ToString(),
            ["PaymentDate"] = inv.PaymentDate?.ToString("u"),
            ["Notes"] = inv.Notes,
            ["GrandTotal"] = inv.GrandTotal.ToString(),
            ["RsApplicable"] = inv.RsApplicable.ToString(),
            ["RsTypeCode"] = inv.RsTypeCode,
            ["RsOperationCode"] = inv.RsOperationCode,
            ["RsTvaCode"] = inv.RsTvaCode,
            ["RsTvaTaux"] = inv.RsTvaTaux?.ToString(),
            ["Cnpc"] = inv.Cnpc,
            ["PriseEnCharge"] = inv.PriseEnCharge.ToString(),
            ["AnneeFacturation"] = inv.AnneeFacturation?.ToString(),
            ["RefCertifChezDeclarant"] = inv.RefCertifChezDeclarant,
            ["TejActe"] = inv.TejActe.ToString(),
            ["TejSynced"] = inv.TejSynced.ToString(),
            ["TejSyncStatus"] = inv.TejSyncStatus,
            ["FactureEnLigneId"] = inv.FactureEnLigneId,
            ["FactureEnLigneStatus"] = inv.FactureEnLigneStatus,
        };

        public async Task<List<PurchaseActivityDto>> GetActivitiesAsync(int invoiceId, int page = 1, int limit = 50)
        {
            // Clamp paging: a negative page yields a negative OFFSET (SQL error) and an
            // unbounded limit lets a single request pull the whole table.
            if (page < 1) page = 1;
            if (limit < 1) limit = 20;
            if (limit > 200) limit = 200;

            return await _context.PurchaseActivities.AsNoTracking()
                .Where(a => a.EntityType == "supplier_invoice" && a.EntityId == invoiceId)
                .OrderByDescending(a => a.PerformedAt)
                .Skip((page - 1) * limit).Take(limit)
                .Select(a => new PurchaseActivityDto
                {
                    Id = a.Id, EntityType = a.EntityType, EntityId = a.EntityId,
                    ActivityType = a.ActivityType, Description = a.Description,
                    OldValue = a.OldValue, NewValue = a.NewValue,
                    PerformedBy = a.PerformedBy, PerformedByName = a.PerformedByName,
                    PerformedAt = a.PerformedAt
                }).ToListAsync();
        }
    }
}
