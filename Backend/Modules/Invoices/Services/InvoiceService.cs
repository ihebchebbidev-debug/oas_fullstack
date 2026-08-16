using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Invoices.DTOs;
using MyApi.Modules.Invoices.Models;
using MyApi.Modules.Numbering.Services;

namespace MyApi.Modules.Invoices.Services
{
    /// <summary>
    /// Phase B customer invoice ledger. Single-entry: header + lines.
    /// Numbering is assigned only on Post — drafts have no number, which lets
    /// them be edited/deleted without leaving gaps in the invoice sequence.
    /// </summary>
    public class InvoiceService : IInvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceService> _logger;
        private readonly INumberingService? _numbering;
        // Resolved lazily to avoid a DI cycle (ServiceOrderService depends on IInvoiceService).
        private readonly IServiceProvider? _serviceProvider;

        public InvoiceService(
            ApplicationDbContext context,
            ILogger<InvoiceService> logger,
            INumberingService? numbering = null,
            IServiceProvider? serviceProvider = null)
        {
            _context = context;
            _logger = logger;
            _numbering = numbering;
            _serviceProvider = serviceProvider;
        }


        public async Task<PagedInvoiceResponse> GetInvoicesAsync(InvoiceQueryParams q)
        {
            var query = _context.Set<Invoice>()
                .Where(i => !i.IsDeleted)
                .Include(i => i.Lines)
                .AsQueryable();

            if (!string.IsNullOrEmpty(q.Status))
            {
                if (string.Equals(q.Status, "overdue", StringComparison.OrdinalIgnoreCase))
                {
                    var nowUtc = DateTime.UtcNow;
                    query = query.Where(i =>
                        i.Status == "posted" &&
                        i.DueDate.HasValue &&
                        i.DueDate.Value < nowUtc &&
                        (i.GrandTotal - i.AmountPaid) > 0m);
                }
                else if (!string.Equals(q.Status, "all", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(i => i.Status == q.Status);
                }
            }
            if (q.ContactId.HasValue) query = query.Where(i => i.ContactId == q.ContactId.Value);
            if (q.SaleId.HasValue) query = query.Where(i => i.SaleId == q.SaleId.Value);
            if (q.ServiceOrderId.HasValue) query = query.Where(i => i.ServiceOrderId == q.ServiceOrderId.Value);
            // Filter by IssueDate when available (falls back to CreatedAt for drafts without an issue date).
            if (q.DateFrom.HasValue) query = query.Where(i => (i.IssueDate ?? i.CreatedAt) >= q.DateFrom.Value);
            if (q.DateTo.HasValue) query = query.Where(i => (i.IssueDate ?? i.CreatedAt) <= q.DateTo.Value);
            if (!string.IsNullOrEmpty(q.Search))
            {
                // Fix §5.2: use Postgres ILIKE (via EF.Functions.ILike) instead of
                // .ToLower().Contains(). ILike is index-friendly against the trigram
                // indexes added in add_invoice_search_indexes.sql, whereas
                // lower(col).Contains forces a Seq Scan on every request.
                // Escape user-supplied LIKE metacharacters so a search for "50%" or
                // "foo_bar" doesn't accidentally become a wildcard.
                var raw = q.Search;
                var escaped = raw.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                var pattern = $"%{escaped}%";
                query = query.Where(i =>
                    (i.InvoiceNumber != null && EF.Functions.ILike(i.InvoiceNumber, pattern)) ||
                    (i.Title != null && EF.Functions.ILike(i.Title, pattern)) ||
                    (i.Notes != null && EF.Functions.ILike(i.Notes, pattern)));
            }


            var total = await query.CountAsync();
            var desc = string.Equals(q.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);
            query = q.SortBy.ToLower() switch
            {
                "invoice_number" => desc ? query.OrderByDescending(i => i.InvoiceNumber) : query.OrderBy(i => i.InvoiceNumber),
                "grand_total"    => desc ? query.OrderByDescending(i => i.GrandTotal)    : query.OrderBy(i => i.GrandTotal),
                "issue_date"     => desc ? query.OrderByDescending(i => i.IssueDate)     : query.OrderBy(i => i.IssueDate),
                _                => desc ? query.OrderByDescending(i => i.CreatedAt)     : query.OrderBy(i => i.CreatedAt),
            };

            var page = Math.Max(1, q.Page);
            var limit = Math.Clamp(q.Limit, 1, 200);
            var rows = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

            return new PagedInvoiceResponse
            {
                Data = await EnrichListAsync(rows),
                PageNumber = page,
                PageSize = limit,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit),
            };
        }

        public async Task<InvoiceDto?> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _context.Set<Invoice>()
                .Where(i => !i.IsDeleted)
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            return invoice == null ? null : await EnrichAsync(invoice);
        }


        public async Task<InvoiceDto> CreateDraftAsync(CreateInvoiceDto dto, string userId)
        {
            if (dto.ContactId <= 0) throw new ArgumentException("ContactId is required");
            // Business rule: every customer invoice must originate from a sale (order),
            // guaranteeing full traceability between orders and invoicing.
            if (!dto.SaleId.HasValue || dto.SaleId.Value <= 0)
                throw new ArgumentException("SaleId is required — invoices must be linked to a sale.");

            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == dto.SaleId.Value && !s.IsDeleted);
            if (sale == null) throw new KeyNotFoundException($"Sale {dto.SaleId.Value} not found");
            if (string.Equals(sale.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Sale {sale.Id} is cancelled and cannot be invoiced.");
            if (sale.ContactId != dto.ContactId)
                throw new ArgumentException("ContactId does not match the sale's contact.");

            // Fix #7: wrap the over-invoicing guard + insert in a serializable transaction
            // (same pattern as CreateDraftFromSaleAsync). Without this, two concurrent manual
            // invoice creations against the same sale can both pass the guard before either
            // commits, silently double-invoicing past the sale total.
            Invoice invoice = null!;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    // Fix #14: block re-billing a fiscal stamp already invoiced for this sale
                    // on any live invoice (mirrors CreateDraftFromSaleAsync).
                    var fiscalStampAlreadyBilled = await _context.Set<InvoiceLine>()
                        .AnyAsync(l => l.SourceType == "fiscal_stamp"
                                       && l.Invoice != null
                                       && l.Invoice.SaleId == sale.Id
                                       && l.Invoice.Status != "void"
                                       && !l.Invoice.IsDeleted);
                    if (fiscalStampAlreadyBilled &&
                        dto.Lines.Any(l => string.Equals(l.SourceType, "fiscal_stamp", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException(
                            $"Fiscal stamp is already billed on a prior invoice for sale {sale.Id}; remove the fiscal-stamp line.");
                    }

                    // Fix #4: item-level double-billing guard for the manual invoice path.
                    // The money-total ceiling below is not enough — a caller could re-bill a
                    // sale item that is already fully invoiced as long as the sale still had
                    // headroom from some other un-invoiced item. CreateDraftFromSaleAsync
                    // already de-duplicates by SourceId; this mirrors that here.
                    var requestedSaleItemIds = dto.Lines
                        .Where(l => string.Equals(l.SourceType, "sale_item", StringComparison.OrdinalIgnoreCase)
                                    && !string.IsNullOrWhiteSpace(l.SourceId))
                        .Select(l => l.SourceId!)
                        .ToList();
                    if (requestedSaleItemIds.Count > 0)
                    {
                        var billedSaleItemIds = await _context.Set<InvoiceLine>()
                            .Where(l => l.SourceType == "sale_item"
                                        && l.Invoice != null
                                        && l.Invoice.SaleId == sale.Id
                                        && l.Invoice.Status != "void"
                                        && !l.Invoice.IsDeleted
                                        && l.SourceId != null
                                        && requestedSaleItemIds.Contains(l.SourceId))
                            .Select(l => l.SourceId!)
                            .Distinct()
                            .ToListAsync();

                        if (billedSaleItemIds.Count > 0)
                        {
                            var names = await _context.Set<Sales.Models.SaleItem>()
                                .Where(si => si.SaleId == sale.Id)
                                .Select(si => new { si.Id, si.ItemName })
                                .ToListAsync();
                            var labels = billedSaleItemIds
                                .Select(id => names.FirstOrDefault(n => n.Id.ToString() == id)?.ItemName ?? $"#{id}")
                                .ToList();
                            throw new InvalidOperationException(
                                $"These sale items are already billed on a live invoice for sale {sale.Id}: " +
                                $"{string.Join(", ", labels)}. Remove them from this invoice, or void the invoice that carries them.");
                        }
                    }



                    invoice = new Invoice
                    {
                        ContactId = dto.ContactId,
                        SaleId = dto.SaleId,
                        ServiceOrderId = dto.ServiceOrderId,
                        Title = dto.Title,
                        Notes = dto.Notes,
                        // Currency always follows the parent sale (which itself was set from
                        // the user's tenant preference at sale creation). Never fall back to
                        // a hardcoded currency literal.
                        Currency = !string.IsNullOrEmpty(dto.Currency) ? dto.Currency! : sale.Currency,
                        Status = "draft",
                        IssueDate = dto.IssueDate,
                        DueDate = dto.DueDate,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        Lines = dto.Lines.Select((l, idx) => BuildLine(l, idx)).ToList(),
                    };
                    RecalculateTotals(invoice);

                    // Over-invoicing guard, re-checked inside the serializable tx so a
                    // concurrent draft cannot slip past before we commit.
                    // The ceiling is recomputed from the live sale items + header
                    // discount/tax/fiscal stamp rather than read from sale.GrandTotal:
                    // ServiceOrderService.PrepareForInvoiceAsync briefly leaves the stored
                    // header totals as a raw sum of line totals, which would make this
                    // ceiling wrong in both directions (too high with a big header discount,
                    // too low once tax/stamp apply).
                    var saleItemsForCeiling = await _context.SaleItems
                        .Where(i => i.SaleId == sale.Id)
                        .ToListAsync();
                    var saleTotal = saleItemsForCeiling.Count > 0
                        ? Sales.Services.SaleTotalsCalculator.Compute(
                            saleItemsForCeiling.Sum(i => Sales.Services.SaleTotalsCalculator.ComputeLineTotal(
                                i.Quantity, i.UnitPrice, i.Discount, i.DiscountType)),
                            sale.Discount, Sales.Services.SaleTotalsCalculator.HeaderDiscountType(sale),
                            sale.Taxes, sale.TaxType, sale.FiscalStamp).GrandTotal
                        : (sale.GrandTotal > 0m ? sale.GrandTotal : sale.TotalAmount);
                    if (saleTotal > 0m)
                    {
                        var alreadyInvoiced = await GetInvoicedTotalForSaleAsync(sale.Id);
                        if (alreadyInvoiced + invoice.GrandTotal > saleTotal + 0.009m)
                        {
                            throw new InvalidOperationException(
                                $"This invoice ({invoice.GrandTotal:0.##}) would take sale {sale.Id} to " +
                                $"{alreadyInvoiced + invoice.GrandTotal:0.##} {invoice.Currency}, over its total of {saleTotal:0.##}. " +
                                $"Remaining to invoice: {Math.Max(0m, saleTotal - alreadyInvoiced):0.##}.");
                        }
                    }

                    _context.Add(invoice);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Invoice draft {Id} created for sale {SaleId} / contact {ContactId}",
                invoice.Id, invoice.SaleId, invoice.ContactId);
            await LogActivityAsync(invoice.Id, "created", userId,
                description: $"Draft invoice created for sale #{invoice.SaleId} with {invoice.Lines.Count} line(s).",
                newValue: invoice.SaleId?.ToString());
            await LogSaleActivityAsync(invoice.SaleId, "invoice_created", userId,
                $"Invoice {InvoiceLabel(invoice)} drafted for this sale ({invoice.Lines.Count} line(s), {invoice.GrandTotal:0.##} {invoice.Currency}).");
            await SyncSaleInvoiceStateAsync(sale.Id);
            return await EnrichAsync(invoice);
        }

        public async Task<InvoiceDto> CreateDraftFromSaleAsync(int saleId, string userId, int? serviceOrderId = null)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == saleId && !s.IsDeleted);
            if (sale == null) throw new KeyNotFoundException($"Sale {saleId} not found");
            if (string.Equals(sale.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Sale {saleId} is cancelled and cannot be invoiced.");

            // #3 fix: skip sale items that are already on a non-void invoice.
            // Previously CreateDraftFromSaleAsync re-billed every sale item on every call,
            // gated only by a money-total guard — so adding a new item to a partially-invoiced
            // sale and re-drafting would duplicate the already-billed lines. Schema already
            // supports this: InvoiceLine.SourceType/SourceId are stamped at line 234.
            var alreadyInvoicedItemIds = await _context.Set<InvoiceLine>()
                .Where(l => l.SourceType == "sale_item"
                            && l.Invoice != null
                            && l.Invoice.SaleId == saleId
                            && l.Invoice.Status != "void"
                            && !l.Invoice.IsDeleted
                            && l.SourceId != null)
                .Select(l => l.SourceId!)
                .ToListAsync();
            var alreadyInvoicedItemIdSet = new HashSet<string>(alreadyInvoicedItemIds);

            var allSaleItems = (sale.Items ?? new List<Sales.Models.SaleItem>()).ToList();
            var saleItems = allSaleItems
                .Where(i => !alreadyInvoicedItemIdSet.Contains(i.Id.ToString()))
                .ToList();
            if (saleItems.Count == 0)
                throw new InvalidOperationException(
                    allSaleItems.Count == 0
                        ? $"Sale {saleId} has no items to invoice."
                        : $"Sale {saleId} has no un-invoiced items left. Add new items or void an existing invoice first.");

            // Has the fiscal stamp already been billed on a prior (non-void) invoice for this sale?
            var fiscalStampAlreadyBilled = await _context.Set<InvoiceLine>()
                .AnyAsync(l => l.SourceType == "fiscal_stamp"
                               && l.Invoice != null
                               && l.Invoice.SaleId == saleId
                               && l.Invoice.Status != "void"
                               && !l.Invoice.IsDeleted);

            // Authoritative money for THIS draft: recomputed from the subset of un-invoiced
            // items only. Header discount and tax are applied to that subset's subtotal so
            // pro-rata math still balances; fiscal stamp is billed once per sale.
            var saleTotals = Sales.Services.SaleTotalsCalculator.Compute(
                saleItems.Sum(i => Sales.Services.SaleTotalsCalculator.ComputeLineTotal(
                    i.Quantity, i.UnitPrice, i.Discount, i.DiscountType)),
                sale.Discount, Sales.Services.SaleTotalsCalculator.HeaderDiscountType(sale),
                sale.Taxes, sale.TaxType,
                fiscalStampAlreadyBilled ? 0m : sale.FiscalStamp);

            // Guard reference: the FULL sale grand total, computed from ALL sale items
            // (not just the un-invoiced subset). Used only for the over-invoicing guards.
            var fullSaleTotals = Sales.Services.SaleTotalsCalculator.Compute(
                allSaleItems.Sum(i => Sales.Services.SaleTotalsCalculator.ComputeLineTotal(
                    i.Quantity, i.UnitPrice, i.Discount, i.DiscountType)),
                sale.Discount, Sales.Services.SaleTotalsCalculator.HeaderDiscountType(sale),
                sale.Taxes, sale.TaxType, sale.FiscalStamp);



            // #4 fix: wrap the over-invoicing guard + insert in a serializable transaction
            // via the execution strategy. Without this, two concurrent CreateDraftFromSale
            // calls can both pass the guard before either commits, silently double-invoicing
            // past the sale total. The offer→sale conversion path already uses this pattern.
            Invoice invoice = null!;
            List<InvoiceLine> lines = null!;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    // Re-check the guard inside the serializable transaction so a
                    // concurrent draft cannot slip past before we commit. Uses the FULL
                    // sale total as the ceiling — saleTotals here is the un-invoiced subset.
                    var alreadyInvoiced = await GetInvoicedTotalForSaleAsync(saleId);
                    var remaining = fullSaleTotals.GrandTotal - alreadyInvoiced;
                    if (fullSaleTotals.GrandTotal > 0m && remaining <= 0.009m)
                    {
                        throw new InvalidOperationException(
                            $"Sale {saleId} is already fully invoiced ({alreadyInvoiced:0.##} of {fullSaleTotals.GrandTotal:0.##} {sale.Currency}). " +
                            "Void an existing invoice before creating a new one.");
                    }


                    // An invoice has no header-discount or fiscal-stamp field: it is
                    // just lines + per-line tax. So the sale's header discount is spread
                    // pro-rata over the lines and the fiscal stamp becomes its own line.
                    var scale = saleTotals.Subtotal > 0m ? saleTotals.AfterDiscount / saleTotals.Subtotal : 1m;
                    var taxRate = saleTotals.EffectiveTaxRate;
                    var discounted = saleTotals.DiscountAmount > 0m;

                    var ordered = saleItems.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToList();

                    // Fix #5: per-line VAT fidelity. SaleItem.TaxRate is carried over from
                    // the offer but was previously ignored — every invoice line got one
                    // blended EffectiveTaxRate, which is wrong for fiscal reporting as soon
                    // as a sale mixes VAT rates. Only switch to per-item rates when the
                    // items genuinely carry more than one distinct non-zero rate; otherwise
                    // keep the header-derived rate so single-rate sales behave exactly as before.
                    var distinctItemRates = ordered
                        .Select(i => i.TaxRate)
                        .Where(r => r > 0m)
                        .Distinct()
                        .ToList();
                    var usePerItemRates = distinctItemRates.Count > 1;

                    lines = new List<InvoiceLine>();
                    var displayOrder = 0;
                    foreach (var i in ordered)
                    {
                        var gross = Sales.Services.SaleTotalsCalculator.ComputeLineTotal(
                            i.Quantity, i.UnitPrice, i.Discount, i.DiscountType);
                        var net = Round2(gross * scale);
                        var qty = i.Quantity != 0m ? i.Quantity : 1m;
                        var lineRate = usePerItemRates
                            ? (i.TaxRate > 0m ? i.TaxRate : taxRate)
                            : taxRate;
                        lines.Add(new InvoiceLine
                        {
                            SourceType = "sale_item",
                            SourceId = i.Id.ToString(),
                            ItemName = i.ItemName ?? "Item",
                            Description = discounted
                                ? string.Join(" — ", new[] { i.Description, "order discount applied" }.Where(x => !string.IsNullOrWhiteSpace(x)))
                                : i.Description,
                            Quantity = i.Quantity,
                            Unit = null,
                            UnitPrice = Round2(net / qty),
                            TaxRate = lineRate,
                            LineTotal = net,
                            TaxAmount = Round2(net * (lineRate / 100m)),
                            DisplayOrder = displayOrder++,
                            CreatedAt = DateTime.UtcNow,
                        });
                    }

                    // Push rounding remainders onto the last taxable line so the invoice
                    // matches the sale exactly instead of drifting by a cent.
                    if (lines.Count > 0)
                    {
                        var last = lines[^1];
                        last.LineTotal = Round2(last.LineTotal + (saleTotals.AfterDiscount - lines.Sum(l => l.LineTotal)));
                        if (last.Quantity != 0m) last.UnitPrice = Round2(last.LineTotal / last.Quantity);

                        if (usePerItemRates)
                        {
                            // With mixed rates the sale header's single tax figure is not the
                            // correct target — each line's own rate is authoritative. Only
                            // re-derive the adjusted line's tax; do not force the header total.
                            last.TaxAmount = Round2(last.LineTotal * (last.TaxRate / 100m));
                        }
                        else
                        {
                            last.TaxAmount = Round2(last.TaxAmount + (saleTotals.TaxAmount - lines.Sum(l => l.TaxAmount)));
                        }
                    }


                    // Fiscal stamp: a flat, untaxed line.
                    if (saleTotals.FiscalStamp > 0m)
                    {
                        lines.Add(new InvoiceLine
                        {
                            SourceType = "fiscal_stamp",
                            SourceId = null,
                            ItemName = "Fiscal stamp",
                            Quantity = 1m,
                            Unit = null,
                            UnitPrice = saleTotals.FiscalStamp,
                            TaxRate = 0m,
                            LineTotal = saleTotals.FiscalStamp,
                            TaxAmount = 0m,
                            DisplayOrder = displayOrder++,
                            CreatedAt = DateTime.UtcNow,
                        });
                    }

                    invoice = new Invoice
                    {
                        ContactId = sale.ContactId,
                        SaleId = sale.Id,
                        ServiceOrderId = serviceOrderId,
                        Title = sale.Title,
                        // Currency always inherited from the parent sale (which itself came
                        // from the user's tenant preference at creation). No hardcoded fallback.
                        Currency = sale.Currency,
                        Status = "draft",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        Lines = lines,
                    };
                    RecalculateTotals(invoice);

                    // Final in-tx guard uses the FULL sale total as the ceiling.
                    if (fullSaleTotals.GrandTotal > 0m && alreadyInvoiced + invoice.GrandTotal > fullSaleTotals.GrandTotal + 0.009m)
                    {
                        throw new InvalidOperationException(
                            $"This invoice ({invoice.GrandTotal:0.##}) would take sale {saleId} to " +
                            $"{alreadyInvoiced + invoice.GrandTotal:0.##} {invoice.Currency}, over its total of {fullSaleTotals.GrandTotal:0.##}.");
                    }


                    _context.Add(invoice);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("Invoice draft {Id} created from sale {SaleId} ({LineCount} lines, total {Total})",
                invoice.Id, sale.Id, lines.Count, invoice.GrandTotal);
            await LogActivityAsync(invoice.Id, "created_from_sale", userId,
                description: $"Draft invoice created from sale #{sale.Id} ({lines.Count} line(s), {invoice.GrandTotal:0.##} {invoice.Currency}).",
                newValue: sale.Id.ToString());
            await LogSaleActivityAsync(sale.Id, "invoice_created", userId,
                $"Invoice {InvoiceLabel(invoice)} drafted for this sale ({lines.Count} line(s), {invoice.GrandTotal:0.##} {invoice.Currency}).");
            await SyncSaleInvoiceStateAsync(sale.Id);
            return await EnrichAsync(invoice);
        }



        public async Task<InvoiceDto> UpdateDraftAsync(int id, UpdateInvoiceDto dto, string userId)
        {
            var invoice = await _context.Set<Invoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (invoice == null) throw new KeyNotFoundException($"Invoice {id} not found");
            if (invoice.Status != "draft")
                throw new InvalidOperationException($"Invoice {id} is '{invoice.Status}'; only drafts are editable.");

            // Capture pre-change values for the audit trail.
            var prevTitle = invoice.Title;
            var prevNotes = invoice.Notes;
            var prevCurrency = invoice.Currency;
            var prevIssue = invoice.IssueDate;
            var prevDue = invoice.DueDate;
            var prevLineCount = invoice.Lines?.Count ?? 0;
            var prevGrand = invoice.GrandTotal;

            invoice.Title = dto.Title ?? invoice.Title;
            invoice.Notes = dto.Notes ?? invoice.Notes;
            // Fix #15: currency is stamped at creation from the parent sale (which
            // follows the tenant preference). It is intentionally immutable on the
            // invoice so over-invoicing / payment math stays consistent.
            invoice.IssueDate = dto.IssueDate ?? invoice.IssueDate;
            invoice.DueDate = dto.DueDate ?? invoice.DueDate;

            if (dto.Lines != null)
            {
                _context.RemoveRange(invoice.Lines ?? new List<MyApi.Modules.Invoices.Models.InvoiceLine>());
                invoice.Lines = dto.Lines.Select((l, idx) => BuildLine(l, idx)).ToList();
            }

            RecalculateTotals(invoice);

            // The over-invoicing ceiling was only enforced when the draft was created, so
            // editing its lines afterwards could push the sale past its own total unchecked.
            if (invoice.SaleId.HasValue)
            {
                var parentSale = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == invoice.SaleId.Value && !s.IsDeleted);
                if (parentSale != null)
                {
                    var items = parentSale.Items ?? new List<Sales.Models.SaleItem>();
                    var ceiling = items.Count > 0
                        ? Sales.Services.SaleTotalsCalculator.Compute(
                            items.Sum(i => Sales.Services.SaleTotalsCalculator.ComputeLineTotal(
                                i.Quantity, i.UnitPrice, i.Discount, i.DiscountType)),
                            parentSale.Discount, Sales.Services.SaleTotalsCalculator.HeaderDiscountType(parentSale),
                            parentSale.Taxes, parentSale.TaxType, parentSale.FiscalStamp).GrandTotal
                        : (parentSale.GrandTotal > 0m ? parentSale.GrandTotal : parentSale.TotalAmount);

                    if (ceiling > 0m)
                    {
                        var otherInvoiced = await GetInvoicedTotalForSaleAsync(parentSale.Id, invoice.Id);
                        if (otherInvoiced + invoice.GrandTotal > ceiling + 0.009m)
                            throw new InvalidOperationException(
                                $"These changes take invoice {invoice.InvoiceNumber ?? $"#{invoice.Id}"} to {invoice.GrandTotal:0.##} and sale " +
                                $"{parentSale.Id} to {otherInvoiced + invoice.GrandTotal:0.##} {invoice.Currency}, over its total of {ceiling:0.##}. " +
                                $"Remaining to invoice: {Math.Max(0m, ceiling - otherInvoiced):0.##}.");
                    }
                }
            }
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Build a compact summary of what changed so the activity feed is meaningful.
            var changes = new List<string>();
            if (!string.Equals(prevTitle, invoice.Title)) changes.Add("title");
            if (!string.Equals(prevNotes, invoice.Notes)) changes.Add("notes");
            if (!string.Equals(prevCurrency, invoice.Currency)) changes.Add("currency");
            if (prevIssue != invoice.IssueDate) changes.Add("issue date");
            if (prevDue != invoice.DueDate) changes.Add("due date");
            if (dto.Lines != null) changes.Add($"lines ({prevLineCount}→{(invoice.Lines?.Count ?? 0)})");
            if (prevGrand != invoice.GrandTotal) changes.Add($"total ({prevGrand:0.##}→{invoice.GrandTotal:0.##})");
            if (changes.Count > 0)
            {
                await LogActivityAsync(invoice.Id, "updated", userId,
                    description: $"Draft edited: {string.Join(", ", changes)}.");
            }
            if (invoice.SaleId.HasValue && prevGrand != invoice.GrandTotal)
                await SyncSaleInvoiceStateAsync(invoice.SaleId.Value);
            return await EnrichAsync(invoice);
        }

        public async Task<InvoiceDto> PostAsync(int id, PostInvoiceDto dto, string userId, string? trigger = null)
        {
            trigger = string.IsNullOrWhiteSpace(trigger) ? "manual" : trigger!;
            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["Operation"] = "InvoicePost",
                ["InvoiceId"] = id,
                ["Trigger"] = trigger,
                ["UserId"] = userId,
            });

            var invoice = await _context.Set<Invoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (invoice == null)
            {
                _logger.LogWarning("Invoice post skipped: invoice {InvoiceId} not found (trigger {Trigger})", id, trigger);
                throw new KeyNotFoundException($"Invoice {id} not found");
            }
            if (invoice.Status != "draft")
            {
                _logger.LogWarning("Invoice post skipped: invoice {InvoiceId} is '{Status}', not a draft (trigger {Trigger})",
                    id, invoice.Status, trigger);
                throw new InvalidOperationException($"Invoice {id} is '{invoice.Status}', cannot post.");
            }
            if (!invoice.Lines.Any())
            {
                _logger.LogWarning("Invoice post skipped: invoice {InvoiceId} has no lines (trigger {Trigger})", id, trigger);
                throw new InvalidOperationException("Cannot post an invoice with no lines.");
            }

            _logger.LogInformation(
                "Posting invoice {InvoiceId} (sale {SaleId}, {LineCount} line(s)) — trigger {Trigger}, requested by {UserId}",
                id, invoice.SaleId, invoice.Lines.Count, trigger, userId);

            RecalculateTotals(invoice);

            if (string.IsNullOrEmpty(invoice.InvoiceNumber))
            {
                // Fix #13: posting an invoice with the wrong number breaks fiscal reporting.
                // If the numbering service is unavailable, refuse to post — the user should
                // retry rather than get a non-sequential fallback number silently persisted.
                if (_numbering == null)
                {
                    _logger.LogError("Invoice post aborted for {InvoiceId}: numbering service unavailable (trigger {Trigger})", id, trigger);
                    throw new InvalidOperationException("Invoice numbering service is unavailable — cannot post right now. Please retry.");
                }
                string number;
                try { number = await _numbering.GetNextAsync("Invoice"); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Invoice numbering failed for {InvoiceId} (trigger {Trigger}); refusing to post with a fallback number", id, trigger);
                    throw new InvalidOperationException("Invoice numbering failed. Please retry — no fallback number was assigned.", ex);
                }
                if (string.IsNullOrWhiteSpace(number))
                {
                    _logger.LogError("Invoice numbering returned empty for {InvoiceId} (trigger {Trigger})", id, trigger);
                    throw new InvalidOperationException("Invoice numbering returned an empty value. Please retry.");
                }
                invoice.InvoiceNumber = number;
            }

            invoice.Status = "posted";
            invoice.IssueDate = dto.IssueDate ?? invoice.IssueDate ?? DateTime.UtcNow.Date;
            invoice.DueDate = dto.DueDate ?? invoice.DueDate;
            invoice.PostedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Invoice {InvoiceId} posted as {Number} — total {Total} {Currency}, sale {SaleId}, trigger {Trigger}",
                invoice.Id, invoice.InvoiceNumber, invoice.GrandTotal, invoice.Currency, invoice.SaleId, trigger);

            var triggerNote = trigger == "manual" ? "" : $" (auto-posted — {DescribeTrigger(trigger)})";
            await LogActivityAsync(invoice.Id, "posted", userId,
                description: $"Invoice posted as {invoice.InvoiceNumber} — total {invoice.GrandTotal:0.##} {invoice.Currency}.{triggerNote}",
                oldValue: "draft",
                newValue: invoice.InvoiceNumber);
            await LogSaleActivityAsync(invoice.SaleId, "invoice_posted", userId,
                $"Invoice {InvoiceLabel(invoice)} posted — total {invoice.GrandTotal:0.##} {invoice.Currency}.{triggerNote}");
            await RecalculatePaymentStateAsync(invoice.Id);
            if (invoice.SaleId.HasValue) await SyncSaleInvoiceStateAsync(invoice.SaleId.Value);
            return await EnrichAsync(invoice);
        }

        /// <summary>Human-readable label for an auto-post trigger, used in activity feeds.</summary>
        private static string DescribeTrigger(string trigger) => trigger switch
        {
            "auto:create_from_sale" => "created from the sale's Invoices tab",
            "auto:payment_recording" => "a payment was recorded against it",
            _ => trigger,
        };

        public async Task LogAutoPostSkippedAsync(int invoiceId, string userId, string trigger, string reason)
        {
            var invoice = await _context.Set<Invoice>()
                .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted);

            _logger.LogWarning(
                "Auto-post did not complete for invoice {InvoiceId} (sale {SaleId}, status {Status}) — trigger {Trigger}, reason: {Reason}",
                invoiceId, invoice?.SaleId, invoice?.Status, trigger, reason);

            await LogActivityAsync(invoiceId, "auto_post_skipped", userId,
                description: $"Auto-post skipped ({DescribeTrigger(trigger)}) — left as draft. Reason: {reason}",
                oldValue: invoice?.Status,
                newValue: invoice?.Status);
            if (invoice != null)
            {
                await LogSaleActivityAsync(invoice.SaleId, "invoice_auto_post_skipped", userId,
                    $"Invoice {InvoiceLabel(invoice)} stayed a draft ({DescribeTrigger(trigger)}) — {reason}");
            }
        }


        public async Task<InvoiceDto> VoidAsync(int id, VoidInvoiceDto dto, string userId)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                throw new ArgumentException("A reason is required to void an invoice.");

            var invoice = await _context.Set<Invoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (invoice == null) throw new KeyNotFoundException($"Invoice {id} not found");
            if (invoice.Status == "void") return await EnrichAsync(invoice);
            if (invoice.Status == "draft")
                throw new InvalidOperationException("Delete drafts instead of voiding them.");

            // Fix #2: never void an invoice that still has money attached. Either the
            // manual "mark paid" flag or real completed Payment rows would leave the
            // ledger showing a paid, voided invoice. Force the operator to reverse
            // payments first (delete the Payment records) then void.
            var idStr = invoice.Id.ToString();
            var completedPayments = await _context.Set<MyApi.Modules.Payments.Models.Payment>()
                .Where(p => p.EntityType == "invoice" && p.EntityId == idStr && p.Status == "completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            if (completedPayments > 0m || invoice.AmountPaid > 0m)
            {
                throw new InvalidOperationException(
                    $"Invoice {InvoiceLabel(invoice)} has {Math.Max(completedPayments, invoice.AmountPaid):0.##} {invoice.Currency} in recorded payments. " +
                    "Delete/refund those payments before voiding.");
            }

            var prevStatus = invoice.Status;
            invoice.Status = "void";
            invoice.AmountPaid = 0m;
            invoice.VoidedAt = DateTime.UtcNow;
            invoice.VoidReason = dto.Reason!.Trim();
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Invoice {Id} voided by {User}", invoice.Id, userId);
            await LogActivityAsync(invoice.Id, "voided", userId,
                description: $"Invoice voided. Reason: {invoice.VoidReason}",
                oldValue: prevStatus,
                newValue: "void");
            await LogSaleActivityAsync(invoice.SaleId, "invoice_voided", userId,
                $"Invoice {InvoiceLabel(invoice)} voided. Reason: {invoice.VoidReason}");
            // A voided invoice no longer covers the sale — reopen it for invoicing.
            if (invoice.SaleId.HasValue) await SyncSaleInvoiceStateAsync(invoice.SaleId.Value);
            return await EnrichAsync(invoice);
        }

        public async Task<InvoiceDto> MarkPaidAsync(int id, MarkPaidInvoiceDto dto, string userId)
        {
            if (string.IsNullOrWhiteSpace(dto?.Memo))
                throw new ArgumentException("A memo is required to mark an invoice as paid.");

            var invoice = await _context.Set<Invoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (invoice == null) throw new KeyNotFoundException($"Invoice {id} not found");
            if (invoice.Status != "posted")
                throw new InvalidOperationException($"Invoice {id} is '{invoice.Status}'; only posted invoices can be marked as paid.");

            var prevStatus = invoice.Status;
            var memo = dto.Memo!.Trim();

            // Fix #1: without a backing Payment row the next call to
            // RecalculatePaymentStateAsync (triggered by any other payment event)
            // would recompute AmountPaid = SUM(Payments) = 0 and silently revert
            // the invoice back to "posted". Insert a Payment row for the exact
            // outstanding balance so the two views of the world stay consistent.
            var idStr = invoice.Id.ToString();
            var alreadyPaid = await _context.Set<MyApi.Modules.Payments.Models.Payment>()
                .Where(p => p.EntityType == "invoice" && p.EntityId == idStr && p.Status == "completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var outstanding = Round2(invoice.GrandTotal - alreadyPaid);
            if (outstanding > 0m)
            {
                var count = await _context.Set<MyApi.Modules.Payments.Models.Payment>()
                    .CountAsync(p => p.EntityType == "invoice" && p.EntityId == idStr);
                var receiptNumber = $"REC-INV-{invoice.Id}-{(count + 1):D3}";
                _context.Add(new MyApi.Modules.Payments.Models.Payment
                {
                    Id = Guid.NewGuid().ToString(),
                    EntityType = "invoice",
                    EntityId = idStr,
                    Amount = outstanding,
                    Currency = invoice.Currency,
                    PaymentMethod = "manual",
                    PaymentDate = DateTime.UtcNow,
                    Status = "completed",
                    Notes = $"Manual mark-paid: {memo}",
                    ReceiptNumber = receiptNumber,
                    CreatedBy = userId,
                    CreatedByName = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            invoice.Status = "paid";
            invoice.AmountPaid = invoice.GrandTotal;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Invoice {Id} manually marked paid by {User}", invoice.Id, userId);
            await LogActivityAsync(invoice.Id, "manual_marked_paid", userId,
                description: $"Invoice manually marked as paid ({outstanding:0.##} {invoice.Currency} recorded as a manual payment). Memo: {memo}",
                oldValue: prevStatus,
                newValue: "paid");
            await LogSaleActivityAsync(invoice.SaleId, "invoice_marked_paid", userId,
                $"Invoice {InvoiceLabel(invoice)} marked as paid. Memo: {memo}");
            return await EnrichAsync(invoice);
        }

        public async Task<InvoiceDto> ReopenAsync(int id, ReopenInvoiceDto dto, string userId)
        {
            if (string.IsNullOrWhiteSpace(dto?.Memo))
                throw new ArgumentException("A memo is required to reopen an invoice.");

            var invoice = await _context.Set<Invoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (invoice == null) throw new KeyNotFoundException($"Invoice {id} not found");
            if (invoice.Status != "paid" && invoice.Status != "void")
                throw new InvalidOperationException($"Invoice {id} is '{invoice.Status}'; only paid or voided invoices can be reopened.");

            // While this invoice sat voided, the sale was free to be re-billed — including a
            // fresh fiscal-stamp line, which the stamp guard only blocks against LIVE invoices.
            // Reopening would then leave two live invoices both charging the stamp.
            if (invoice.Status == "void" && invoice.SaleId.HasValue
                && (invoice.Lines?.Any(l => string.Equals(l.SourceType, "fiscal_stamp", StringComparison.OrdinalIgnoreCase)) ?? false))
            {
                var stampBilledElsewhere = await _context.Set<InvoiceLine>()
                    .AnyAsync(l => l.SourceType == "fiscal_stamp"
                                   && l.Invoice != null
                                   && l.Invoice.SaleId == invoice.SaleId
                                   && l.Invoice.Id != invoice.Id
                                   && l.Invoice.Status != "void"
                                   && !l.Invoice.IsDeleted);
                if (stampBilledElsewhere)
                    throw new InvalidOperationException(
                        $"Cannot reopen invoice {InvoiceLabel(invoice)}: another live invoice for sale {invoice.SaleId} already bills the fiscal stamp. " +
                        "Void that invoice first, or remove the stamp line before reopening.");
            }

            var prevStatus = invoice.Status;
            invoice.Status = "posted";
            invoice.VoidedAt = null;
            invoice.VoidReason = null;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Invoice {Id} reopened from {Prev} by {User}", invoice.Id, prevStatus, userId);
            await LogActivityAsync(invoice.Id, "manual_reopened", userId,
                description: $"Invoice reopened from '{prevStatus}'. Memo: {dto.Memo!.Trim()}",
                oldValue: prevStatus,
                newValue: "posted");
            await LogSaleActivityAsync(invoice.SaleId, "invoice_reopened", userId,
                $"Invoice {InvoiceLabel(invoice)} reopened from '{prevStatus}'. Memo: {dto.Memo!.Trim()}");
            // Re-sync payment state in case payments changed while it was paid/void.
            await RecalculatePaymentStateAsync(invoice.Id);
            if (invoice.SaleId.HasValue) await SyncSaleInvoiceStateAsync(invoice.SaleId.Value);
            return await EnrichAsync(invoice);
        }

        public async Task<bool> DeleteDraftAsync(int id, string userId)
        {
            var invoice = await _context.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (invoice == null) return false;
            if (invoice.Status != "draft")
                throw new InvalidOperationException($"Invoice {id} is '{invoice.Status}'; only drafts can be deleted.");
            invoice.IsDeleted = true;
            invoice.DeletedAt = DateTime.UtcNow;
            invoice.DeletedBy = userId;
            await _context.SaveChangesAsync();
            await LogActivityAsync(invoice.Id, "deleted", userId,
                description: "Draft invoice deleted.");
            await LogSaleActivityAsync(invoice.SaleId, "invoice_deleted", userId,
                $"Draft invoice {InvoiceLabel(invoice)} deleted.");
            if (invoice.SaleId.HasValue) await SyncSaleInvoiceStateAsync(invoice.SaleId.Value);
            return true;
        }

        public async Task RecalculatePaymentStateAsync(int invoiceId)
        {
            // Fix #5: wrap the read-then-write in a transaction with row-level lock on
            // the invoice so two concurrent payment events (e.g. webhook + manual entry)
            // can't interleave and clobber each other's AmountPaid / Status writes.
            string prevStatus = string.Empty;
            string newStatus = string.Empty;
            decimal paid = 0m;
            decimal grandTotal = 0m;
            string currency = string.Empty;
            int? saleId = null;
            string label = string.Empty;

            // This method is also called from inside an outer transaction (e.g.
            // PaymentService.CreatePaymentAsync auto-posts a draft invoice while holding a
            // serializable transaction). Npgsql does not support nested transactions, so
            // when a transaction is already open we join it instead of starting a new one
            // (and skip the execution strategy, which rejects user-initiated transactions).
            async Task RecalcCoreAsync()
            {
                var ambient = _context.Database.CurrentTransaction;
                var tx = ambient == null ? await _context.Database.BeginTransactionAsync() : null;
                try
                {
                    // FOR UPDATE lock on the invoice row serialises concurrent recalcs.
                    var tenantId = _context.GetTenantId();
                    var invoice = await _context.Set<Invoice>()
                        .FromSqlRaw("SELECT * FROM \"Invoices\" WHERE \"Id\" = {0} AND \"TenantId\" = {1} FOR UPDATE", invoiceId, tenantId)
                        .FirstOrDefaultAsync();
                    if (invoice == null)
                    {
                        if (tx != null) await tx.RollbackAsync();
                        return;
                    }

                    var idStr = invoice.Id.ToString();
                    paid = await _context.Set<MyApi.Modules.Payments.Models.Payment>()
                        .Where(p => p.EntityType == "invoice" && p.EntityId == idStr && p.Status == "completed")
                        .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                    prevStatus = invoice.Status;
                    invoice.AmountPaid = paid;

                    // Fix #12: drop the "> 0m" guard so a legitimately zero-total posted
                    // invoice (all lines discounted to nothing) can auto-transition to paid.
                    if (invoice.Status == "posted" && paid >= invoice.GrandTotal)
                        invoice.Status = "paid";
                    else if (invoice.Status == "paid" && paid < invoice.GrandTotal)
                        invoice.Status = "posted";

                    invoice.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    if (tx != null) await tx.CommitAsync();

                    newStatus = invoice.Status;
                    grandTotal = invoice.GrandTotal;
                    currency = invoice.Currency;
                    saleId = invoice.SaleId;
                    label = InvoiceLabel(invoice);
                }
                catch
                {
                    if (tx != null) await tx.RollbackAsync();
                    throw;
                }
                finally
                {
                    if (tx != null) await tx.DisposeAsync();
                }
            }

            if (_context.Database.CurrentTransaction != null)
            {
                await RecalcCoreAsync();
            }
            else
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(RecalcCoreAsync);
            }

            if (!string.IsNullOrEmpty(prevStatus) && prevStatus != newStatus)
            {
                var type = newStatus == "paid" ? "auto_marked_paid" : "auto_reopened";
                await LogActivityAsync(invoiceId, type, "system",
                    description: newStatus == "paid"
                        ? $"Fully paid ({paid:0.##} {currency}) — status advanced from posted to paid."
                        : $"Payment reversed — status returned to posted (paid {paid:0.##} of {grandTotal:0.##} {currency}).",
                    oldValue: prevStatus,
                    newValue: newStatus);
                await LogSaleActivityAsync(saleId,
                    newStatus == "paid" ? "invoice_auto_marked_paid" : "invoice_auto_reopened",
                    "system",
                    newStatus == "paid"
                        ? $"Invoice {label} fully paid ({paid:0.##} {currency})."
                        : $"Invoice {label} payment reversed — back to posted ({paid:0.##} / {grandTotal:0.##} {currency}).");
            }

            // Payment state drives the sale's invoicing state: a sale only becomes
            // "invoiced" once its invoices are fully paid, so every payment /
            // reversal must re-evaluate the sale. Best-effort (never throws).
            if (saleId.HasValue)
            {
                await SyncSaleInvoiceStateAsync(saleId.Value);
            }
        }

        public async Task<IReadOnlyList<InvoiceActivityDto>> GetActivitiesAsync(int invoiceId)
        {
            var rows = await _context.Set<InvoiceActivity>()
                .Where(a => a.InvoiceId == invoiceId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
            return rows.Select(a => new InvoiceActivityDto
            {
                Id = a.Id,
                InvoiceId = a.InvoiceId,
                Type = a.Type,
                Description = a.Description,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                CreatedAt = a.CreatedAt,
                CreatedBy = a.CreatedBy,
            }).ToList();
        }

        /// <summary>Stable human label for an invoice: its number once posted, otherwise #id.</summary>
        private static string InvoiceLabel(Invoice invoice) =>
            string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ? $"#{invoice.Id}" : invoice.InvoiceNumber!;

        /// <summary>
        /// Mirrors an invoice lifecycle event onto the related sale's activity feed so the
        /// order timeline (and the traceability module, which aggregates it) shows who
        /// posted / voided / paid / reopened / deleted an invoice and when.
        /// Best-effort: an audit failure never breaks the invoice action.
        /// </summary>
        private async Task LogSaleActivityAsync(int? saleId, string type, string userId, string description)
        {
            if (!saleId.HasValue) return;
            try
            {
                _context.Add(new MyApi.Modules.Sales.Models.SaleActivity
                {
                    SaleId = saleId.Value,
                    Type = type,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = string.IsNullOrEmpty(userId) ? "system" : userId,
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mirror {Type} onto sale {SaleId} activity", type, saleId);
            }
        }

        private async Task LogActivityAsync(int invoiceId, string type, string userId,
            string? description = null, string? oldValue = null, string? newValue = null)
        {
            try
            {
                _context.Add(new InvoiceActivity
                {
                    InvoiceId = invoiceId,
                    Type = type,
                    Description = description,
                    OldValue = oldValue,
                    NewValue = newValue,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = string.IsNullOrEmpty(userId) ? "system" : userId,
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Never let audit-trail writes break the primary business action.
                _logger.LogWarning(ex, "Failed to write InvoiceActivity {Type} for invoice {Id}", type, invoiceId);
            }
        }


        // ── helpers ───────────────────────────────────────────
        private static InvoiceLine BuildLine(CreateInvoiceLineDto dto, int idx)
        {
            // Without these guards a negative quantity / price / tax rate produced a
            // negative line that dragged GrandTotal down, letting a caller slip past the
            // over-invoicing ceiling while still writing a nonsensical invoice.
            if (dto.Quantity < 0m)
                throw new ArgumentException($"Invoice line '{dto.ItemName}' has a negative quantity ({dto.Quantity}).");
            if (dto.UnitPrice < 0m)
                throw new ArgumentException($"Invoice line '{dto.ItemName}' has a negative unit price ({dto.UnitPrice}).");
            if (dto.TaxRate < 0m || dto.TaxRate > 100m)
                throw new ArgumentException($"Invoice line '{dto.ItemName}' has an invalid tax rate ({dto.TaxRate}). Expected 0-100.");

            var lineTotal = Round2(dto.Quantity * dto.UnitPrice);
            var tax = Round2(lineTotal * (dto.TaxRate / 100m));
            return new InvoiceLine
            {
                SourceType = dto.SourceType,
                SourceId = dto.SourceId,
                ItemName = dto.ItemName,
                Description = dto.Description,
                Quantity = dto.Quantity,
                Unit = dto.Unit,
                UnitPrice = dto.UnitPrice,
                TaxRate = dto.TaxRate,
                LineTotal = lineTotal,
                TaxAmount = tax,
                DisplayOrder = dto.DisplayOrder == 0 ? idx : dto.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
            };
        }

        private static void RecalculateTotals(Invoice invoice)
        {
            var lines = invoice.Lines ?? new List<InvoiceLine>();
            invoice.Subtotal = Round2(lines.Sum(l => l.LineTotal));
            invoice.TaxAmount = Round2(lines.Sum(l => l.TaxAmount));
            invoice.GrandTotal = Round2(invoice.Subtotal + invoice.TaxAmount);
        }

        private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Total already committed to invoices for a sale (drafts included, voided
        /// excluded). Basis for the over-invoicing guard and the sale's
        /// invoiced / partially_invoiced state.
        /// </summary>
        private async Task<decimal> GetInvoicedTotalForSaleAsync(int saleId, int? excludeInvoiceId = null)
        {
            return await _context.Set<Invoice>()
                .Where(i => !i.IsDeleted
                            && i.SaleId == saleId
                            && i.Status != "void"
                            && (!excludeInvoiceId.HasValue || i.Id != excludeInvoiceId.Value))
                .SumAsync(i => (decimal?)i.GrandTotal) ?? 0m;
        }

        /// <summary>
        /// Total actually collected against this sale's invoices (voided excluded).
        /// A sale only reaches the terminal "invoiced" state once this covers the
        /// sale total — being billed is not the same as being paid.
        /// </summary>
        private async Task<decimal> GetPaidTotalForSaleAsync(int saleId)
        {
            return await _context.Set<Invoice>()
                .Where(i => !i.IsDeleted && i.SaleId == saleId && i.Status != "void")
                .SumAsync(i => (decimal?)i.AmountPaid) ?? 0m;
        }

        /// <summary>
        /// Reflects invoicing progress back onto the sale so the orders list /
        /// kanban shows whether an order is partially or fully invoiced.
        /// Never overrides a terminal sale status (closed, cancelled, lost, ...).
        /// </summary>
        private async Task SyncSaleInvoiceStateAsync(int saleId)
        {
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == saleId && !s.IsDeleted);
                if (sale == null) return;

                // Only these statuses participate in the invoicing sub-flow.
                var mutable = new[] { "created", "in_progress", "ready_to_invoice", "partially_invoiced", "invoiced" };

                if (!mutable.Contains((sale.Status ?? string.Empty).ToLowerInvariant())) return;

                // Recompute (and heal) the sale's totals: legacy sales were saved
                // with zeros, which would otherwise make every sale look
                // "partially invoiced" forever.
                var saleTotal = sale.GrandTotal;
                if ((sale.Items?.Count ?? 0) > 0)
                    saleTotal = Sales.Services.SaleTotalsCalculator.Apply(sale, sale.Items).GrandTotal;
                else if (saleTotal <= 0m)
                    saleTotal = sale.TotalAmount;

                var invoiced = await GetInvoicedTotalForSaleAsync(saleId);
                var paid = await GetPaidTotalForSaleAsync(saleId);

                // "invoiced" is reserved for a sale that is fully billed AND fully
                // paid. Anything billed but not yet collected stays
                // "partially_invoiced" so the pipeline never shows money as done
                // before it has actually come in.
                var fullyBilled = saleTotal > 0m && invoiced + 0.009m >= saleTotal;
                var fullyPaid = saleTotal > 0m && paid + 0.009m >= saleTotal;

                string next;
                if (invoiced <= 0m) next = sale.Status == "invoiced" || sale.Status == "partially_invoiced" ? "in_progress" : sale.Status!;
                else if (fullyBilled && fullyPaid) next = "invoiced";
                else next = "partially_invoiced";

                var statusChanged = !string.Equals(next, sale.Status, StringComparison.OrdinalIgnoreCase);
                var prevSaleStatus = sale.Status;
                if (statusChanged)
                {
                    sale.Status = next;
                }
                sale.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Fix #9: log the automatic sale-status transition so the sale's
                // activity feed explains why it flipped in/out of invoiced states,
                // instead of the change appearing out of nowhere.
                if (statusChanged)
                {
                    try
                    {
                        _context.Add(new MyApi.Modules.Sales.Models.SaleActivity
                        {
                            SaleId = sale.Id,
                            Type = "invoice_sync_status_changed",
                            Description = $"Sale status auto-updated from '{prevSaleStatus}' to '{next}' (invoiced {invoiced:0.##}, paid {paid:0.##} of {saleTotal:0.##}).",
                            CreatedAt = DateTime.UtcNow,
                            CreatedByName = "system",
                        });
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogWarning(logEx, "Failed to log invoice_sync_status_changed on sale {SaleId}", sale.Id);
                    }
                }

                // Cascade the sale's new status onto the Service Orders this sale
                // actually bills: once the sale is fully invoiced the SO is closed
                // automatically (its work is done and billed). The cascade applies
                // all SO updates inside its own transaction.
                // Re-run it whenever the sale is fully invoiced — not only when the
                // status just changed — so an SO left behind by an earlier failed
                // cascade heals on the next invoice action. Best-effort: never throws.
                var shouldCascade = statusChanged ||
                                    string.Equals(next, "invoiced", StringComparison.OrdinalIgnoreCase);

                if (shouldCascade && _serviceProvider != null)
                {
                    try
                    {
                        var soService = _serviceProvider.GetService<MyApi.Modules.ServiceOrders.Services.IServiceOrderService>();
                        if (soService != null)
                        {
                            await soService.CascadeSaleStatusToServiceOrdersAsync(saleId, next, "system");
                        }
                    }
                    catch (Exception cascadeEx)
                    {
                        _logger.LogWarning(cascadeEx, "InvoiceService: cascade to service orders failed for sale {SaleId}", saleId);
                    }
                }
            }

            catch (Exception ex)
            {
                // Never let the sale-state mirror break the invoice action itself.
                _logger.LogWarning(ex, "Failed to sync invoice state onto sale {SaleId}", saleId);
            }
        }


        // Enrichment ensures the DTO carries the sale number and contact display name
        // so the frontend can render the "invoice → order → customer" chain without extra fetches.
        private async Task<InvoiceDto> EnrichAsync(Invoice invoice)
        {
            var dto = MapToDto(invoice);
            if (invoice.SaleId.HasValue)
            {
                dto.SaleNumber = await _context.Sales
                    .Where(s => s.Id == invoice.SaleId.Value)
                    .Select(s => s.SaleNumber)
                    .FirstOrDefaultAsync();
            }
            dto.ContactName = await _context.Contacts
                .Where(c => c.Id == invoice.ContactId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();
            return dto;
        }

        private async Task<List<InvoiceDto>> EnrichListAsync(List<Invoice> invoices)
        {
            if (invoices.Count == 0) return new List<InvoiceDto>();
            var saleIds = invoices.Where(i => i.SaleId.HasValue).Select(i => i.SaleId!.Value).Distinct().ToList();
            var contactIds = invoices.Select(i => i.ContactId).Distinct().ToList();
            var saleMap = await _context.Sales
                .Where(s => saleIds.Contains(s.Id))
                .Select(s => new { s.Id, s.SaleNumber })
                .ToDictionaryAsync(s => s.Id, s => s.SaleNumber);
            var contactMap = await _context.Contacts
                .Where(c => contactIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name);
            return invoices.Select(i =>
            {
                var d = MapToDto(i);
                if (i.SaleId.HasValue && saleMap.TryGetValue(i.SaleId.Value, out var sn)) d.SaleNumber = sn;
                if (contactMap.TryGetValue(i.ContactId, out var cn)) d.ContactName = cn;
                return d;
            }).ToList();
        }

        private static InvoiceDto MapToDto(Invoice i) => new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            Status = i.Status,
            ContactId = i.ContactId,
            SaleId = i.SaleId,
            ServiceOrderId = i.ServiceOrderId,
            Title = i.Title,
            Notes = i.Notes,
            Currency = i.Currency,
            Subtotal = i.Subtotal,
            TaxAmount = i.TaxAmount,
            GrandTotal = i.GrandTotal,
            AmountPaid = i.AmountPaid,
            IssueDate = i.IssueDate,
            DueDate = i.DueDate,
            PostedAt = i.PostedAt,
            VoidedAt = i.VoidedAt,
            VoidReason = i.VoidReason,
            CreatedBy = i.CreatedBy,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            Lines = (i.Lines ?? new List<InvoiceLine>())
                .OrderBy(l => l.DisplayOrder)
                .Select(l => new InvoiceLineDto
                {
                    Id = l.Id,
                    InvoiceId = l.InvoiceId,
                    SourceType = l.SourceType,
                    SourceId = l.SourceId,
                    ItemName = l.ItemName,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    UnitPrice = l.UnitPrice,
                    TaxRate = l.TaxRate,
                    LineTotal = l.LineTotal,
                    TaxAmount = l.TaxAmount,
                    DisplayOrder = l.DisplayOrder,
                }).ToList(),
        };
    }
}
