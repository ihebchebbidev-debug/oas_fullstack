using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Articles.Services;
using MyApi.Modules.Purchases.DTOs;
using MyApi.Modules.Purchases.Models;

namespace MyApi.Modules.Purchases.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        // Npgsql requires DateTimes destined for `timestamp with time zone`
        // columns to have Kind=Utc. JSON-bound DateTimes from the client come
        // through as Kind=Unspecified and blow up with a DbUpdateException
        // ("Cannot write DateTime with Kind=Unspecified ...") on SaveChanges.
        private static DateTime? AsUtc(DateTime? dt) => dt.HasValue ? AsUtc(dt.Value) : (DateTime?)null;
        private static DateTime AsUtc(DateTime dt) => dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<PurchaseOrderService> _logger;
        private readonly MyApi.Modules.Numbering.Services.INumberingService? _numberingService;
        private readonly IStockTransactionService? _stockService;

        public PurchaseOrderService(ApplicationDbContext context, ILogger<PurchaseOrderService> logger,
            MyApi.Modules.Numbering.Services.INumberingService? numberingService = null,
            IStockTransactionService? stockService = null)
        {
            _context = context;
            _logger = logger;
            _numberingService = numberingService;
            _stockService = stockService;
        }

        public async Task<PaginatedPurchaseOrderResponse> GetOrdersAsync(
            string? status, string? supplierId, string? paymentStatus,
            DateTime? dateFrom, DateTime? dateTo, string? search,
            int page, int limit, string sortBy, string sortOrder)
        {
            // Clamp paging: a negative page yields a negative OFFSET (SQL error) and an
            // unbounded limit lets a single request pull the whole table.
            if (page < 1) page = 1;
            if (limit < 1) limit = 20;
            if (limit > 200) limit = 200;

            var query = _context.PurchaseOrders.AsNoTracking().Where(o => !o.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
            if (!string.IsNullOrEmpty(supplierId) && int.TryParse(supplierId, out int sid))
                query = query.Where(o => o.SupplierId == sid);
            if (!string.IsNullOrEmpty(paymentStatus))
            {
                // "unpaid" is a UI pseudo-status (the list page's Unpaid stat card /
                // smart filter) meaning "anything not fully paid" — PaymentStatus itself
                // is only ever pending | partial | paid.
                query = paymentStatus == "unpaid"
                    ? query.Where(o => o.PaymentStatus != "paid")
                    : query.Where(o => o.PaymentStatus == paymentStatus);
            }
            if (dateFrom.HasValue) query = query.Where(o => o.OrderDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(o => o.OrderDate <= dateTo.Value);
            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(o =>
                    (o.OrderNumber != null && o.OrderNumber.ToLower().Contains(s)) ||
                    (o.Title != null && o.Title.ToLower().Contains(s)) ||
                    (o.SupplierName != null && o.SupplierName.ToLower().Contains(s)));
            }

            var total = await query.CountAsync();
            query = sortBy switch
            {
                "order_date" => sortOrder == "asc" ? query.OrderBy(o => o.OrderDate) : query.OrderByDescending(o => o.OrderDate),
                "grand_total" => sortOrder == "asc" ? query.OrderBy(o => o.GrandTotal) : query.OrderByDescending(o => o.GrandTotal),
                _ => sortOrder == "asc" ? query.OrderBy(o => o.CreatedDate) : query.OrderByDescending(o => o.CreatedDate)
            };

            var orders = await query.Skip((page - 1) * limit).Take(limit).Include(o => o.Items).ToListAsync();

            return new PaginatedPurchaseOrderResponse
            {
                Orders = orders.Select(MapToDto).ToList(),
                Pagination = new PurchasePaginationInfo { Page = page, Limit = limit, Total = total, TotalPages = (int)Math.Ceiling((double)total / limit) }
            };
        }

        public async Task<PurchaseOrderDto?> GetOrderByIdAsync(int id)
        {
            var order = await _context.PurchaseOrders.AsNoTracking().Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
            return order == null ? null : MapToDto(order);
        }

        public async Task<PurchaseOrderDto> CreateOrderAsync(CreatePurchaseOrderDto dto, string userId, string? userName = null, string? idempotencyKey = null)
        {
            // Idempotency short-circuit: a retried POST with the same
            // Idempotency-Key returns the existing PO instead of a duplicate.
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existingId = await _context.PurchaseOrders.AsNoTracking()
                    .Where(p => p.IdempotencyKey == idempotencyKey && !p.IsDeleted)
                    .Select(p => p.Id).FirstOrDefaultAsync();
                if (existingId > 0)
                    return (await GetOrderByIdAsync(existingId))!;
            }

            // Server-side re-validation of line-item bounds (defense in depth
            // vs. any caller that bypasses DTO model binding).
            ValidateOrderItems(dto.Items);

            // Filter out soft-deleted contacts: Contact has no global IsDeleted query
            // filter, and FindAsync would happily resurrect a tombstoned supplier onto
            // a brand-new PO (copying its stale Name/Email/Phone/Address).
            var supplier = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == dto.SupplierId && !c.IsDeleted)
                ?? throw new KeyNotFoundException($"Supplier with ID {dto.SupplierId} not found");

            string orderNumber;
            try { orderNumber = _numberingService != null ? await _numberingService.GetNextAsync("PurchaseOrder") : $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}"; }
            catch { orderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}"; }

            var order = new PurchaseOrder
            {
                OrderNumber = orderNumber,
                Title = dto.Title,
                Description = dto.Description,
                SupplierId = dto.SupplierId,
                SupplierName = supplier.Name ?? string.Empty,
                SupplierEmail = supplier.Email,
                SupplierPhone = supplier.Phone,
                SupplierAddress = supplier.Address,
                Status = "draft",
                // Respect the user-supplied OrderDate (e.g. backdated POs); fall
                // back to UtcNow only if the client didn't send one.
                OrderDate = AsUtc(dto.OrderDate ?? DateTime.UtcNow),
                ExpectedDelivery = AsUtc(dto.ExpectedDelivery),
                Currency = dto.Currency,
                Discount = dto.Discount,
                DiscountType = dto.DiscountType,
                FiscalStamp = dto.FiscalStamp,
                PaymentTerms = dto.PaymentTerms,
                Notes = dto.Notes,
                Tags = dto.Tags,
                BillingAddress = dto.BillingAddress,
                DeliveryAddress = dto.DeliveryAddress,
                ServiceOrderId = dto.ServiceOrderId,
                SaleId = dto.SaleId,
                IdempotencyKey = idempotencyKey,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            };


            // EnableRetryOnFailure is on for the Npgsql provider, so user-initiated
            // transactions MUST go through an execution strategy. Calling
            // BeginTransactionAsync directly throws InvalidOperationException
            // ("The configured execution strategy ... does not support user initiated
            // transactions") on the very first POST.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.PurchaseOrders.Add(order);
                    await _context.SaveChangesAsync();

                    if (dto.Items?.Any() == true)
                    {
                        var items = dto.Items.Select((item, idx) => new PurchaseOrderItem
                        {
                            PurchaseOrderId = order.Id,
                            ArticleId = item.ArticleId,
                            ArticleName = item.ArticleName,
                            ArticleNumber = item.ArticleNumber,
                            SupplierRef = item.SupplierRef,
                            Description = item.Description,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TaxRate = item.TaxRate,
                            Discount = item.Discount,
                            DiscountType = item.DiscountType,
                            Unit = item.Unit,
                            DisplayOrder = idx,
                            LineTotal = CalculateLineTotal(item.Quantity, item.UnitPrice, item.Discount, item.DiscountType, item.TaxRate)
                        }).ToList();
                        _context.PurchaseOrderItems.AddRange(items);
                        RecalculateTotals(order, items);
                        await _context.SaveChangesAsync();
                    }

                    LogActivity("purchase_order", order.Id, "created", $"Purchase order {orderNumber} created", userId, userName);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return (await GetOrderByIdAsync(order.Id))!;
        }

        // PO statuses where item structure (qty/price/lines) must be frozen — once
        // a PO is ordered/received, items are referenced by GoodsReceiptItem.OrderedQty
        // and stock has been moved against them.
        private static readonly HashSet<string> ItemFrozenStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "ordered", "partially_received", "received", "cancelled", "closed"
        };

        // Financial header fields (Discount / DiscountType / FiscalStamp) drive
        // GrandTotal via RecalculateTotals. Once a PO has been received or terminally
        // closed, mutating these silently rewrites historical spend numbers and
        // reconciliation baselines. Draft/validated/ordered are still open to price
        // corrections; anything past that is frozen.
        private static readonly HashSet<string> HeaderFinancialFrozenStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "partially_received", "received", "cancelled", "closed"
        };

        private static void EnsureItemsMutable(PurchaseOrder order)
        {
            if (ItemFrozenStatuses.Contains(order.Status))
                throw new InvalidOperationException($"Items cannot be modified on a PO in status '{order.Status}'");
        }

        // Allowed status transitions. Anything else is rejected so a user can't
        // e.g. revert a "received" PO to "draft" and then mutate items.
        private static readonly Dictionary<string, HashSet<string>> AllowedStatusTransitions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = new(StringComparer.OrdinalIgnoreCase) { "validated", "ordered", "cancelled" },
            ["validated"] = new(StringComparer.OrdinalIgnoreCase) { "ordered", "draft", "cancelled" },
            ["ordered"] = new(StringComparer.OrdinalIgnoreCase) { "partially_received", "received", "cancelled" },
            ["partially_received"] = new(StringComparer.OrdinalIgnoreCase) { "received", "cancelled" },
            ["received"] = new(StringComparer.OrdinalIgnoreCase) { "closed" },
            ["cancelled"] = new(StringComparer.OrdinalIgnoreCase) { },
            ["closed"] = new(StringComparer.OrdinalIgnoreCase) { },
        };

        /// <summary>
        /// Removes stock that goods receipts had added for a purchase order that is
        /// being cancelled. Traceable back to the PO in the stock ledger.
        /// </summary>
        private async Task MoveStockForCancellationAsync(
            int articleId, decimal quantity, string orderNumber, int orderId, string userId, string? userName)
        {
            if (_stockService == null || quantity <= 0) return;
            try
            {
                await _stockService.CreateTransactionAsync(new MyApi.Modules.Articles.DTOs.CreateStockTransactionDto
                {
                    ArticleId = articleId,
                    TransactionType = "remove",
                    Quantity = quantity,
                    Reason = "purchase_order_cancellation",
                    ReferenceType = "purchase_order",
                    ReferenceId = orderId.ToString(),
                    ReferenceNumber = orderNumber,
                    Notes = $"Reversal of received quantities for cancelled purchase order {orderNumber}"
                }, userId, userName);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient stock"))
            {
                throw new InvalidOperationException(
                    $"Cannot cancel purchase order {orderNumber}: article #{articleId} does not have enough stock to reverse {quantity}. " +
                    "The received goods were likely already consumed or sold. Adjust stock manually first.", ex);
            }
        }

        public async Task<PurchaseOrderDto> UpdateOrderAsync(int id, UpdatePurchaseOrderDto dto, string userId, string? userName = null)
        {
            // Wrap mutation in a transaction with execution strategy. Without this,
            // a concurrent UpdateItemAsync (which mutates Quantity/UnitPrice and
            // recomputes totals) can interleave with this update — the second
            // SaveChangesAsync overwrites the first's recomputed totals with stale
            // values. EnableRetryOnFailure also requires user-initiated transactions
            // to go through the configured execution strategy.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Serializable, like the item-level mutations (add/update/delete item):
                // a header update recalculates totals from the item set, so a concurrent
                // item change under a weaker isolation level could persist a GrandTotal
                // that never matched any consistent snapshot of the lines.
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var order = await _context.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted)
                        ?? throw new KeyNotFoundException($"PurchaseOrder {id} not found");

                    var oldStatus = order.Status;
                    var cancelling = false;
                    if (dto.Status != null && !string.Equals(dto.Status, oldStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!AllowedStatusTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(dto.Status))
                            throw new InvalidOperationException($"Status transition not allowed: '{oldStatus}' → '{dto.Status}'");

                        // Receipt integrity guard: a PO can only be marked "received" or
                        // "partially_received" when the line ReceivedQty values agree.
                        // Previously a user could flip a PO's status to "received"
                        // manually even though no GoodsReceipt existed and every
                        // line's ReceivedQty was 0 (cf. QA bug BC-XL-00001 where
                        // header=Received but Receipts tab count=0).
                        if (string.Equals(dto.Status, "received", StringComparison.OrdinalIgnoreCase))
                        {
                            var items = order.Items ?? new List<PurchaseOrderItem>();
                            if (!items.Any() || items.Any(i => i.ReceivedQty < i.Quantity))
                                throw new InvalidOperationException(
                                    "Cannot mark PO as 'received' until every line item has been fully received via a GoodsReceipt.");
                        }
                        else if (string.Equals(dto.Status, "partially_received", StringComparison.OrdinalIgnoreCase))
                        {
                            var items = order.Items ?? new List<PurchaseOrderItem>();
                            if (!items.Any(i => i.ReceivedQty > 0))
                                throw new InvalidOperationException(
                                    "Cannot mark PO as 'partially_received' without at least one line having a received quantity.");
                        }
                        else if (string.Equals(dto.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                        {
                            // Cancelling a PO that already moved goods must undo the
                            // stock impact — otherwise the warehouse keeps phantom
                            // quantities for a PO that no longer exists commercially.
                            var invoiced = await _context.SupplierInvoices
                                .AnyAsync(i => i.PurchaseOrderId == id && !i.IsDeleted);
                            if (invoiced)
                                throw new InvalidOperationException(
                                    $"Cannot cancel purchase order {order.OrderNumber}: it is referenced by one or more supplier invoices. Delete or credit those invoices first.");
                            cancelling = true;
                        }
                    }

                    // Snapshot for field-level audit: every header edit (not just the
                    // status transition) is recorded on the activity timeline.
                    var before = SnapshotOrder(order);

                    if (dto.Title != null) order.Title = dto.Title;
                    if (dto.Description != null) order.Description = dto.Description;
                    if (dto.Status != null) order.Status = dto.Status;
                    if (dto.ExpectedDelivery.HasValue) order.ExpectedDelivery = AsUtc(dto.ExpectedDelivery);

                    // Freeze financial header fields once the PO has been (partially)
                    // received / closed / cancelled. Recomputing GrandTotal after that
                    // silently rewrites historical spend numbers, breaks reconciliation
                    // against already-issued supplier invoices, and lets a "cancelled"
                    // PO be edited long after the fact.
                    var wantsFinancialChange = dto.Discount.HasValue || dto.DiscountType != null || dto.FiscalStamp.HasValue;
                    if (wantsFinancialChange && HeaderFinancialFrozenStatuses.Contains(order.Status))
                        throw new InvalidOperationException(
                            $"Financial header fields (Discount, DiscountType, FiscalStamp) cannot be modified on a PO in status '{order.Status}'");

                    if (dto.Discount.HasValue) order.Discount = dto.Discount.Value;
                    if (dto.DiscountType != null) order.DiscountType = dto.DiscountType;
                    if (dto.FiscalStamp.HasValue) order.FiscalStamp = dto.FiscalStamp.Value;
                    if (dto.PaymentTerms != null) order.PaymentTerms = dto.PaymentTerms;
                    // PaymentStatus is AUTO-DERIVED from linked SupplierInvoices (see
                    // SupplierInvoiceService.SyncPurchaseOrderPaymentStatusAsync). Manual
                    // writes are ignored to keep the invoice ledger as the source of truth.
                    if (dto.Notes != null) order.Notes = dto.Notes;
                    if (dto.Tags != null) order.Tags = dto.Tags;
                    if (dto.BillingAddress != null) order.BillingAddress = dto.BillingAddress;
                    if (dto.DeliveryAddress != null) order.DeliveryAddress = dto.DeliveryAddress;
                    order.ModifiedDate = DateTime.UtcNow;
                    order.ModifiedBy = userId;

                    if (wantsFinancialChange)
                    {
                        if (order.Items != null) RecalculateTotals(order, order.Items.ToList());
                    }

                    if (dto.Status != null && dto.Status != oldStatus)
                        LogActivity("purchase_order", id, "status_changed", $"Status changed from {oldStatus} to {dto.Status}", userId, userName, oldStatus, dto.Status);

                    // Field-level audit for every other header change.
                    foreach (var (field, oldVal, newVal) in DiffSnapshots(before, SnapshotOrder(order)))
                    {
                        if (field == "Status") continue; // already logged above
                        LogActivity("purchase_order", id, "updated",
                            $"{field} changed from '{Shorten(oldVal)}' to '{Shorten(newVal)}'",
                            userId, userName, Shorten(oldVal), Shorten(newVal));
                    }



                    // ── Cancellation: reverse received quantities + stock movements ──
                    var cancelReversals = new List<(int articleId, decimal qty)>();
                    if (cancelling)
                    {
                        foreach (var item in order.Items ?? new List<PurchaseOrderItem>())
                        {
                            if (item.ReceivedQty > 0)
                            {
                                if (item.ArticleId.HasValue)
                                    cancelReversals.Add((item.ArticleId.Value, item.ReceivedQty));
                                item.ReceivedQty = 0;
                            }
                        }
                        order.ActualDelivery = null;

                        // Linked receipts are soft-deleted so the Receipts tab stops
                        // showing deliveries against a cancelled PO, while the rows
                        // remain for audit / stock traceability.
                        var receipts = await _context.GoodsReceipts
                            .Where(r => r.PurchaseOrderId == id && !r.IsDeleted)
                            .ToListAsync();
                        foreach (var r in receipts)
                        {
                            r.IsDeleted = true;
                            r.DeletedAt = DateTime.UtcNow;
                            r.DeletedBy = userId;
                            r.ModifiedDate = DateTime.UtcNow;
                            r.ModifiedBy = userId;
                            LogActivity("goods_receipt", r.Id, "cancelled",
                                $"Receipt {r.ReceiptNumber} reversed because purchase order {order.OrderNumber} was cancelled",
                                userId, userName, "active", "cancelled");
                        }
                    }

                    await _context.SaveChangesAsync();

                    foreach (var (articleId, qty) in cancelReversals)
                        await MoveStockForCancellationAsync(articleId, qty, order.OrderNumber, id, userId, userName);

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });
            return (await GetOrderByIdAsync(id))!;
        }

        public async Task<bool> DeleteOrderAsync(int id, string userId, string? userName = null)
        {
            var order = await _context.PurchaseOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
            if (order == null) return false;

            // Cheap pre-checks (fail fast). Re-asserted under serializable isolation
            // below to close the TOCTOU window — without that, a concurrently-created
            // invoice or goods receipt could land between the check and the soft-delete
            // and end up orphaned against a hidden PO.
            var linkedInvoiceExists = await _context.SupplierInvoices
                .AnyAsync(i => i.PurchaseOrderId == id && !i.IsDeleted);
            if (linkedInvoiceExists)
                throw new InvalidOperationException($"Cannot delete purchase order {order.OrderNumber}: it is referenced by one or more supplier invoices");

            var linkedReceiptExists = await _context.GoodsReceipts
                .AnyAsync(r => r.PurchaseOrderId == id && !r.IsDeleted);
            if (linkedReceiptExists)
                throw new InvalidOperationException($"Cannot delete purchase order {order.OrderNumber}: it has goods receipts. Delete the receipts first.");

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var tracked = await _context.PurchaseOrders
                        .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
                    if (tracked == null) return;

                    // Re-assert blocks under lock.
                    if (await _context.SupplierInvoices.AnyAsync(i => i.PurchaseOrderId == id && !i.IsDeleted))
                        throw new InvalidOperationException($"Cannot delete purchase order {tracked.OrderNumber}: it is referenced by one or more supplier invoices");
                    if (await _context.GoodsReceipts.AnyAsync(r => r.PurchaseOrderId == id && !r.IsDeleted))
                        throw new InvalidOperationException($"Cannot delete purchase order {tracked.OrderNumber}: it has goods receipts. Delete the receipts first.");

                    tracked.IsDeleted = true;
                    tracked.DeletedAt = DateTime.UtcNow;
                    tracked.DeletedBy = userId;
                    LogActivity("purchase_order", id, "deleted", $"Purchase order {tracked.OrderNumber} deleted", userId, userName);
                    await _context.SaveChangesAsync();
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

        public async Task<PurchaseOrderStatsDto> GetStatsAsync(DateTime? dateFrom, DateTime? dateTo)
        {
            var query = _context.PurchaseOrders.AsNoTracking().Where(o => !o.IsDeleted);
            if (dateFrom.HasValue) query = query.Where(o => o.OrderDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(o => o.OrderDate <= dateTo.Value);

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Single SQL aggregate instead of materializing every order into memory.
            var agg = await query
                .GroupBy(o => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Draft = g.Sum(o => o.Status == "draft" ? 1 : 0),
                    Ordered = g.Sum(o => o.Status == "ordered" ? 1 : 0),
                    Received = g.Sum(o => o.Status == "received" ? 1 : 0),
                    Cancelled = g.Sum(o => o.Status == "cancelled" ? 1 : 0),
                    TotalSpend = g.Sum(o => o.Status != "cancelled" && o.Status != "draft" ? o.GrandTotal : 0m),
                    MonthlySpend = g.Sum(o => o.OrderDate >= monthStart && o.Status != "cancelled" && o.Status != "draft" ? o.GrandTotal : 0m),
                    YearSpend = g.Sum(o => o.OrderDate >= yearStart && o.Status != "cancelled" && o.Status != "draft" ? o.GrandTotal : 0m),
                    Pending = g.Sum(o => o.Status == "ordered" || o.Status == "partially_received" ? 1 : 0)
                })
                .FirstOrDefaultAsync();

            // Lead time needs date arithmetic: pull only the two date columns for the
            // (much smaller) set of delivered orders instead of whole entities.
            var deliveredDates = await query
                .Where(o => o.ActualDelivery.HasValue)
                .Select(o => new { o.OrderDate, o.ActualDelivery })
                .ToListAsync();

            var overdueInvoices = await _context.SupplierInvoices
                .CountAsync(i => !i.IsDeleted && i.DueDate < now && i.Status != "paid" && i.Status != "cancelled");

            // Open = still owed (any due date). Kept separate from overdue so the
            // dashboard "Open invoices" card is not a subset of overdue ones.
            var openInvoices = await _context.SupplierInvoices
                .CountAsync(i => !i.IsDeleted && i.Status != "paid" && i.Status != "cancelled");

            var rsTotal = await _context.SupplierInvoices
                .Where(i => !i.IsDeleted && i.Status != "cancelled")
                .SumAsync(i => (decimal?)i.RsAmount) ?? 0m;

            return new PurchaseOrderStatsDto
            {
                TotalOrders = agg?.Total ?? 0,
                DraftOrders = agg?.Draft ?? 0,
                OrderedOrders = agg?.Ordered ?? 0,
                ReceivedOrders = agg?.Received ?? 0,
                CancelledOrders = agg?.Cancelled ?? 0,
                TotalSpend = agg?.TotalSpend ?? 0m,
                MonthlySpend = agg?.MonthlySpend ?? 0m,
                TotalSpendThisYear = agg?.YearSpend ?? 0m,
                // Clamp at 0: a back-dated OrderDate later than ActualDelivery would
                // otherwise contribute a negative lead time and skew the average.
                AvgLeadTime = (decimal)deliveredDates
                    .Select(o => Math.Max(0, (o.ActualDelivery!.Value - o.OrderDate).TotalDays))
                    .DefaultIfEmpty(0)
                    .Average(),
                PendingReceipts = agg?.Pending ?? 0,
                OverdueInvoices = overdueInvoices,
                OpenInvoices = openInvoices,
                RsTotal = rsTotal
            };
        }


        public async Task<PurchaseOrderItemDto> AddItemAsync(int orderId, CreatePurchaseOrderItemDto dto, string? userId = null, string? userName = null)
        {
            // Wrap in a transaction with execution strategy. Without it, a concurrent
            // header update (which also recomputes totals) can clobber this item's
            // recomputed totals — Postgres + EnableRetryOnFailure also requires the
            // execution-strategy wrapper for any explicit BeginTransactionAsync.
            PurchaseOrderItem? created = null;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Serializable: mirror UpdateItemAsync/DeleteItemAsync. Two concurrent
                // POSTs would otherwise each load the same order.Items snapshot,
                // insert their own row, and each recompute totals from a base that
                // is missing the other request's insert — the second SaveChanges
                // silently overwrites the first's totals (PurchaseOrder has no
                // concurrency token). Serializable makes one of them retry / fail.
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var order = await _context.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted)
                        ?? throw new KeyNotFoundException($"PurchaseOrder {orderId} not found");
                    EnsureItemsMutable(order);
                    ValidateOrderItem(dto);



                    var item = new PurchaseOrderItem
                    {
                        PurchaseOrderId = orderId, ArticleId = dto.ArticleId, ArticleName = dto.ArticleName,
                        ArticleNumber = dto.ArticleNumber, SupplierRef = dto.SupplierRef, Description = dto.Description,
                        Quantity = dto.Quantity, UnitPrice = dto.UnitPrice, TaxRate = dto.TaxRate,
                        Discount = dto.Discount, DiscountType = dto.DiscountType, Unit = dto.Unit,
                        DisplayOrder = (order.Items?.Count ?? 0),
                        LineTotal = CalculateLineTotal(dto.Quantity, dto.UnitPrice, dto.Discount, dto.DiscountType, dto.TaxRate)
                    };
                    _context.PurchaseOrderItems.Add(item);
                    await _context.SaveChangesAsync();
                    RecalculateTotals(order, order.Items!.ToList());
                    LogActivity("purchase_order", orderId, "item_added",
                        $"Line added: {item.ArticleName ?? item.Description ?? "item"} × {item.Quantity} @ {item.UnitPrice}",
                        userId ?? "system", userName, null, Shorten(DescribeItem(item)));
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    created = item;
                }
                catch { await tx.RollbackAsync(); throw; }
            });
            return MapItemToDto(created!);
        }

        public async Task<PurchaseOrderItemDto> UpdateItemAsync(int orderId, int itemId, CreatePurchaseOrderItemDto dto, string? userId = null, string? userName = null)
        {
            PurchaseOrderItem? updated = null;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Serializable: a concurrent goods receipt could increment ReceivedQty
                // between our `dto.Quantity < item.ReceivedQty` check and the SaveChanges,
                // letting the new Quantity slip below the now-larger ReceivedQty.
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var order = await _context.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted)
                        ?? throw new KeyNotFoundException($"PurchaseOrder {orderId} not found");
                    EnsureItemsMutable(order);
                    ValidateOrderItem(dto);

                    var item = order.Items?.FirstOrDefault(i => i.Id == itemId)
                        ?? throw new KeyNotFoundException($"Item {itemId} not found");

                    if (dto.Quantity < item.ReceivedQty)
                        throw new InvalidOperationException($"Quantity ({dto.Quantity}) cannot be less than already-received qty ({item.ReceivedQty})");

                    var itemBefore = DescribeItem(item);

                    item.ArticleId = dto.ArticleId; item.ArticleName = dto.ArticleName; item.ArticleNumber = dto.ArticleNumber;
                    item.SupplierRef = dto.SupplierRef; item.Description = dto.Description; item.Quantity = dto.Quantity;
                    item.UnitPrice = dto.UnitPrice; item.TaxRate = dto.TaxRate; item.Discount = dto.Discount;
                    item.DiscountType = dto.DiscountType; item.Unit = dto.Unit;
                    item.LineTotal = CalculateLineTotal(dto.Quantity, dto.UnitPrice, dto.Discount, dto.DiscountType, dto.TaxRate);
                    RecalculateTotals(order, order.Items!.ToList());
                    var itemAfter = DescribeItem(item);
                    if (itemBefore != itemAfter)
                        LogActivity("purchase_order", orderId, "item_updated",
                            $"Line updated: {itemBefore} → {itemAfter}",
                            userId ?? "system", userName, Shorten(itemBefore), Shorten(itemAfter));
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    updated = item;
                }
                catch { await tx.RollbackAsync(); throw; }
            });
            return MapItemToDto(updated!);
        }

        public async Task<bool> DeleteItemAsync(int orderId, int itemId, string? userId = null, string? userName = null)
        {
            bool result = false;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Serializable: prevents a concurrent receipt from incrementing
                // ReceivedQty between the `> 0` check and the delete.
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var order = await _context.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
                    if (order == null) { result = false; await tx.CommitAsync(); return; }
                    EnsureItemsMutable(order);

                    var item = order.Items?.FirstOrDefault(i => i.Id == itemId);
                    if (item == null) { result = false; await tx.CommitAsync(); return; }
                    if (item.ReceivedQty > 0)
                        throw new InvalidOperationException("Cannot delete an item that already has received quantity");

                    var removed = DescribeItem(item);
                    _context.PurchaseOrderItems.Remove(item);
                    await _context.SaveChangesAsync();
                    RecalculateTotals(order, order.Items!.ToList());
                    LogActivity("purchase_order", orderId, "item_deleted",
                        $"Line removed: {removed}", userId ?? "system", userName, Shorten(removed), null);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    result = true;
                }
                catch { await tx.RollbackAsync(); throw; }
            });
            return result;
        }

        public async Task<List<PurchaseActivityDto>> GetActivitiesAsync(int orderId, int page, int limit)
        {
            // Clamp paging: a negative page yields a negative OFFSET (SQL error) and an
            // unbounded limit lets a single request pull the whole table.
            if (page < 1) page = 1;
            if (limit < 1) limit = 20;
            if (limit > 200) limit = 200;

            var rows = await _context.PurchaseActivities.AsNoTracking()
                .Where(a => a.EntityType == "purchase_order" && a.EntityId == orderId)
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

            await BackfillPerformedByNamesAsync(rows);
            return rows;
        }

        /// <summary>
        /// Cross-entity audit feed. Replaces the old client-side fan-out on the
        /// Audit Log page (which fetched N orders then N activity calls and only
        /// ever saw a slice of the history). Filtering, sorting and paging all
        /// happen in SQL so the log is complete and cheap.
        /// </summary>
        public async Task<PaginatedPurchaseActivityResponse> GetAllActivitiesAsync(
            string? entityType, int? entityId, string? activityType, string? search,
            DateTime? dateFrom, DateTime? dateTo, int page, int limit)
        {
            if (page < 1) page = 1;
            if (limit < 1) limit = 50;
            if (limit > 200) limit = 200;

            var query = _context.PurchaseActivities.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(a => a.EntityType == entityType);
            if (entityId.HasValue)
                query = query.Where(a => a.EntityId == entityId.Value);
            if (!string.IsNullOrWhiteSpace(activityType))
                query = query.Where(a => a.ActivityType == activityType);
            if (dateFrom.HasValue)
                query = query.Where(a => a.PerformedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(a => a.PerformedAt <= dateTo.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(a =>
                    EF.Functions.ILike(a.Description ?? string.Empty, term) ||
                    EF.Functions.ILike(a.PerformedByName ?? string.Empty, term) ||
                    EF.Functions.ILike(a.ActivityType, term));
            }

            var total = await query.CountAsync();

            var rows = await query
                .OrderByDescending(a => a.PerformedAt).ThenByDescending(a => a.Id)
                .Skip((page - 1) * limit).Take(limit)
                .Select(a => new PurchaseActivityDto
                {
                    Id = a.Id, EntityType = a.EntityType, EntityId = a.EntityId,
                    ActivityType = a.ActivityType, Description = a.Description,
                    OldValue = a.OldValue, NewValue = a.NewValue,
                    PerformedBy = a.PerformedBy, PerformedByName = a.PerformedByName,
                    PerformedAt = a.PerformedAt
                }).ToListAsync();

            await BackfillPerformedByNamesAsync(rows);

            return new PaginatedPurchaseActivityResponse
            {
                Activities = rows,
                Pagination = new PurchasePaginationInfo
                {
                    Page = page,
                    Limit = limit,
                    Total = total,
                    TotalPages = limit > 0 ? (int)Math.Ceiling(total / (double)limit) : 0
                }
            };
        }

        /// <summary>
        /// Backfill display name for legacy rows that pre-date PerformedByName
        /// persistence. Looks up each missing user id once and falls back to
        /// "First Last" / Email.
        /// </summary>
        private async Task BackfillPerformedByNamesAsync(List<PurchaseActivityDto> rows)
        {
            var missingUserIds = rows
                .Where(r => string.IsNullOrEmpty(r.PerformedByName) && !string.IsNullOrEmpty(r.PerformedBy))
                .Select(r => r.PerformedBy)
                .Distinct()
                .Select(s => int.TryParse(s, out var n) ? (int?)n : null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();
            if (missingUserIds.Count == 0) return;

            var userMap = await _context.Users.AsNoTracking()
                .Where(u => missingUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToDictionaryAsync(u => u.Id.ToString(), u =>
                    !string.IsNullOrWhiteSpace((u.FirstName + " " + u.LastName).Trim())
                        ? (u.FirstName + " " + u.LastName).Trim()
                        : u.Email);
            foreach (var r in rows)
            {
                if (!string.IsNullOrEmpty(r.PerformedByName)) continue;
                if (!string.IsNullOrEmpty(r.PerformedBy) && userMap.TryGetValue(r.PerformedBy, out var name))
                    r.PerformedByName = name;
            }
        }



        // ── Helpers ──

        // Tax-EXCLUSIVE line total so Sum(LineTotal) reconciles to SubTotal.
        // Tax is tracked separately in PurchaseOrder.TaxAmount via RecalculateTotals.
        // Item validation guard mirrors DTO [Range] attributes so a caller that
        // bypasses model binding still can't slip negatives / out-of-range values
        // into the totals recalculation.
        private static void ValidateOrderItem(CreatePurchaseOrderItemDto item)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException($"[INVALID_QUANTITY] Line quantity must be greater than zero (got {item.Quantity})");
            if (item.UnitPrice < 0)
                throw new InvalidOperationException($"[INVALID_UNIT_PRICE] Line unit price cannot be negative (got {item.UnitPrice})");
            if (item.TaxRate < 0 || item.TaxRate > 100)
                throw new InvalidOperationException($"[INVALID_TAX_RATE] Line tax rate must be between 0 and 100 (got {item.TaxRate})");
            if (item.Discount < 0)
                throw new InvalidOperationException($"[INVALID_DISCOUNT] Line discount cannot be negative (got {item.Discount})");
            if (string.IsNullOrWhiteSpace(item.Description))
                throw new InvalidOperationException("[INVALID_DESCRIPTION] Line description is required");
        }

        private static void ValidateOrderItems(IEnumerable<CreatePurchaseOrderItemDto>? items)
        {
            if (items == null) return;
            foreach (var i in items) ValidateOrderItem(i);
        }

        // All monetary results are rounded to 2 decimals (same convention as
        // SupplierInvoiceService) so in-memory totals match what is persisted and
        // PO ↔ invoice reconciliation can never drift by a cent.
        private static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        private static decimal CalculateLineTotal(decimal qty, decimal price, decimal discount, string discountType, decimal taxRate)

        {
            var subtotal = qty * price;
            var discountAmount = discountType == "percentage" ? subtotal * discount / 100 : discount;
            return Money(Math.Max(0m, subtotal - discountAmount));
        }

        private static void RecalculateTotals(PurchaseOrder order, List<PurchaseOrderItem> items)
        {
            // SubTotal is the sum of the line totals AFTER the per-line discount, which is
            // what SupplierInvoiceService persists too. Summing Quantity * UnitPrice here
            // silently dropped every per-line discount from the GrandTotal.
            var lineBases = items
                .Select(i =>
                {
                    var sub = i.Quantity * i.UnitPrice;
                    var d = i.DiscountType == "percentage" ? sub * i.Discount / 100 : i.Discount;
                    return new { Item = i, AfterLineDiscount = Math.Max(0m, sub - d) };
                })
                .ToList();

            order.SubTotal = Money(lineBases.Sum(x => x.AfterLineDiscount));
            var discAmt = Money(order.DiscountType == "percentage" ? order.SubTotal * order.Discount / 100 : order.Discount);
            var afterDiscount = order.SubTotal - discAmt;

            // Tax must be computed on the DISCOUNTED base (per-line discount + pro-rated
            // header discount) so PO totals reconcile with the originating SupplierInvoice.
            // We pro-rate the header discount by each line's post-line-discount base so
            // per-line tax rates are preserved.
            var totalAfterLineDiscount = lineBases.Sum(x => x.AfterLineDiscount);

            order.TaxAmount = totalAfterLineDiscount > 0
                ? Money(lineBases.Sum(x =>
                {
                    var lineShare = x.AfterLineDiscount / totalAfterLineDiscount;
                    var lineAfterHeaderDisc = x.AfterLineDiscount - (discAmt * lineShare);
                    return lineAfterHeaderDisc * x.Item.TaxRate / 100;
                }))
                : 0m;

            // Floor non-negative: a header discount larger than SubTotal would otherwise
            // produce a negative GrandTotal, which is meaningless for a PO and breaks the
            // PaymentStatus sync (totalDue<=0 → "paid" forever even though no payment exists).
            order.GrandTotal = Math.Max(0, Money(afterDiscount + order.TaxAmount + order.FiscalStamp));
        }

        internal static string? Shorten(string? s, int max = 480)
            => string.IsNullOrEmpty(s) ? s : (s!.Length <= max ? s : s.Substring(0, max));

        private static string DescribeItem(PurchaseOrderItem i)
            => $"{(string.IsNullOrWhiteSpace(i.ArticleName) ? (string.IsNullOrWhiteSpace(i.Description) ? "item" : i.Description) : i.ArticleName)} | qty {i.Quantity} | price {i.UnitPrice} | tax {i.TaxRate}% | total {i.LineTotal}";

        private static Dictionary<string, string?> SnapshotOrder(PurchaseOrder o) => new()
        {
            ["Title"] = o.Title,
            ["Description"] = o.Description,
            ["Status"] = o.Status,
            ["ExpectedDelivery"] = o.ExpectedDelivery?.ToString("u"),
            ["Discount"] = o.Discount.ToString(),
            ["DiscountType"] = o.DiscountType,
            ["FiscalStamp"] = o.FiscalStamp.ToString(),
            ["PaymentTerms"] = o.PaymentTerms,
            ["Notes"] = o.Notes,
            ["Tags"] = o.Tags == null ? null : string.Join(", ", o.Tags),
            ["BillingAddress"] = o.BillingAddress,
            ["DeliveryAddress"] = o.DeliveryAddress,
        };

        internal static IEnumerable<(string Field, string? Old, string? New)> DiffSnapshots(
            Dictionary<string, string?> before, Dictionary<string, string?> after)
        {
            foreach (var kv in after)
            {
                before.TryGetValue(kv.Key, out var old);
                if (!string.Equals(old ?? "", kv.Value ?? "", StringComparison.Ordinal))
                    yield return (kv.Key, old, kv.Value);
            }
        }

        private void LogActivity(string entityType, int entityId, string activityType, string desc, string userId, string? userName = null, string? oldVal = null, string? newVal = null)
        {
            _context.PurchaseActivities.Add(new PurchaseActivity
            {
                EntityType = entityType, EntityId = entityId, ActivityType = activityType,
                Description = desc, OldValue = oldVal, NewValue = newVal,
                PerformedBy = userId, PerformedByName = userName, PerformedAt = DateTime.UtcNow
            });
        }

        private static PurchaseOrderDto MapToDto(PurchaseOrder o) => new()
        {
            Id = o.Id, OrderNumber = o.OrderNumber, Title = o.Title, Description = o.Description,
            SupplierId = o.SupplierId, SupplierName = o.SupplierName, SupplierEmail = o.SupplierEmail,
            SupplierPhone = o.SupplierPhone, SupplierAddress = o.SupplierAddress,
            SupplierMatriculeFiscale = o.SupplierMatriculeFiscale,
            Status = o.Status, OrderDate = o.OrderDate, ExpectedDelivery = o.ExpectedDelivery,
            ActualDelivery = o.ActualDelivery, Currency = o.Currency, SubTotal = o.SubTotal,
            Discount = o.Discount, DiscountType = o.DiscountType, TaxAmount = o.TaxAmount,
            FiscalStamp = o.FiscalStamp, GrandTotal = o.GrandTotal, PaymentTerms = o.PaymentTerms,
            PaymentStatus = o.PaymentStatus, Notes = o.Notes, Tags = o.Tags,
            BillingAddress = o.BillingAddress, DeliveryAddress = o.DeliveryAddress,
            ServiceOrderId = o.ServiceOrderId, SaleId = o.SaleId, ApprovedBy = o.ApprovedBy,
            ApprovalDate = o.ApprovalDate, SentToSupplierAt = o.SentToSupplierAt,
            Items = o.Items?.Select(MapItemToDto).ToList(),
            CreatedDate = o.CreatedDate, CreatedBy = o.CreatedBy, CreatedByName = o.CreatedByName,
            ModifiedDate = o.ModifiedDate, ModifiedBy = o.ModifiedBy
        };

        private static PurchaseOrderItemDto MapItemToDto(PurchaseOrderItem i) => new()
        {
            Id = i.Id, PurchaseOrderId = i.PurchaseOrderId, ArticleId = i.ArticleId,
            ArticleName = i.ArticleName, ArticleNumber = i.ArticleNumber, SupplierRef = i.SupplierRef,
            Description = i.Description, Quantity = i.Quantity, ReceivedQty = i.ReceivedQty,
            UnitPrice = i.UnitPrice, TaxRate = i.TaxRate, Discount = i.Discount,
            DiscountType = i.DiscountType, LineTotal = i.LineTotal, Unit = i.Unit, DisplayOrder = i.DisplayOrder
        };
    }
}
