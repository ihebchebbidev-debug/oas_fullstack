using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyApi.Data;
using MyApi.Modules.Articles.DTOs;
using MyApi.Modules.Articles.Models;

namespace MyApi.Modules.Articles.Services
{
    public class StockTransactionService : IStockTransactionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StockTransactionService> _logger;

        public StockTransactionService(ApplicationDbContext context, ILogger<StockTransactionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StockTransactionListDto> GetTransactionsAsync(StockTransactionSearchDto? searchDto = null)
        {
            searchDto ??= new StockTransactionSearchDto();

            var query = _context.Set<StockTransaction>()
                .Include(t => t.Article)
                .AsNoTracking()
                .AsQueryable();

            // Apply filters
            if (searchDto.ArticleId.HasValue)
                query = query.Where(t => t.ArticleId == searchDto.ArticleId.Value);

            if (!string.IsNullOrEmpty(searchDto.TransactionType))
                query = query.Where(t => t.TransactionType == searchDto.TransactionType);

            if (!string.IsNullOrEmpty(searchDto.ReferenceType))
                query = query.Where(t => t.ReferenceType == searchDto.ReferenceType);

            if (searchDto.DateFrom.HasValue)
                query = query.Where(t => t.CreatedAt >= searchDto.DateFrom.Value);

            if (searchDto.DateTo.HasValue)
                query = query.Where(t => t.CreatedAt <= searchDto.DateTo.Value);

            if (!string.IsNullOrEmpty(searchDto.PerformedBy))
                query = query.Where(t => t.PerformedBy == searchDto.PerformedBy || 
                    (t.PerformedByName != null && t.PerformedByName.ToLower().Contains(searchDto.PerformedBy.ToLower())));

            var total = await query.CountAsync();

            // Apply sorting based on SortBy parameter
            var isAscending = searchDto.SortOrder.ToLower() == "asc";
            query = searchDto.SortBy?.ToLower() switch
            {
                "article_id" => isAscending ? query.OrderBy(t => t.ArticleId) : query.OrderByDescending(t => t.ArticleId),
                "transaction_type" => isAscending ? query.OrderBy(t => t.TransactionType) : query.OrderByDescending(t => t.TransactionType),
                "quantity" => isAscending ? query.OrderBy(t => t.Quantity) : query.OrderByDescending(t => t.Quantity),
                "performed_by" => isAscending ? query.OrderBy(t => t.PerformedByName) : query.OrderByDescending(t => t.PerformedByName),
                _ => isAscending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt)
            };

            // Apply pagination
            var transactions = await query
                .Skip((searchDto.Page - 1) * searchDto.Limit)
                .Take(searchDto.Limit)
                .ToListAsync();

            return new StockTransactionListDto
            {
                Data = transactions.Select(MapToDto).ToList(),
                Pagination = new PaginationDto
                {
                    Total = total,
                    Page = searchDto.Page,
                    Limit = searchDto.Limit,
                    Pages = (int)Math.Ceiling(total / (double)searchDto.Limit)
                }
            };
        }

        public async Task<List<StockTransactionDto>> GetArticleTransactionsAsync(int articleId, int limit = 50)
        {
            var transactions = await _context.Set<StockTransaction>()
                .Include(t => t.Article)
                .Where(t => t.ArticleId == articleId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();

            return transactions.Select(MapToDto).ToList();
        }

        public async Task<StockTransactionDto?> GetTransactionByIdAsync(int id)
        {
            var transaction = await _context.Set<StockTransaction>()
                .Include(t => t.Article)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            return transaction != null ? MapToDto(transaction) : null;
        }

        public async Task<StockTransactionDto> CreateTransactionAsync(
            CreateStockTransactionDto dto, 
            string userId, 
            string? userName = null, 
            string? ipAddress = null)
        {
            // Validate quantity
            if (dto.Quantity <= 0 && dto.TransactionType != "adjustment")
            {
                throw new ArgumentException("Quantity must be greater than zero");
            }

            // AMBIENT TRANSACTION SUPPORT.
            // Callers such as GoodsReceiptService (create/update/delete receipt) already
            // hold an open serializable transaction on this SAME scoped DbContext when
            // they call AddStockAsync/RemoveStockAsync. Npgsql has no nested transactions:
            // calling BeginTransactionAsync again throws "A transaction is already
            // started", and opening a nested retrying ExecutionStrategy rejects
            // user-initiated transactions. Either one 500s the whole request and rolls
            // back the receipt. So when a transaction is already open we JOIN it and let
            // the caller own commit/rollback; only when there is none do we open our own
            // under the execution strategy (required by EnableRetryOnFailure).
            var ambientTx = _context.Database.CurrentTransaction;
            if (ambientTx != null)
                return await CoreAsync(ambientTx, ownsTx: false);

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
                await CoreAsync(await _context.Database.BeginTransactionAsync(), ownsTx: true));

            // Uses a transaction with row-level locking to prevent race conditions.
            async Task<StockTransactionDto> CoreAsync(IDbContextTransaction dbTransaction, bool ownsTx)
            {
                try
                {
                    // ── Idempotency guard ─────────────────────────────────────────
                    // Physical stock movements are keyed by (article, reference_type,
                    // reference_id, transaction_type). If a matching row already
                    // exists we've already deducted these goods once — return the
                    // existing transaction and do NOT touch stock again. Prevents
                    // double-deduction from retries, sale close→reopen→close cycles,
                    // and cross-service overlap between SaleService and DispatchService.
                    //
                    // Only enforced when both reference fields are populated AND the
                    // type represents a real stock movement (skip "adjustment", "add",
                    // "offer_added", manual returns, etc.).
                    // Keep this set in sync with the partial unique index
                    // "UX_stock_transactions_idempotency" (see migration
                    // 20260724_StockTransaction_Idempotency.sql). The C# guard
                    // and DB index MUST agree on which (transaction_type,
                    // reference_type) pairs are treated as idempotent, otherwise
                    // one layer collapses duplicates while the other allows them.
                    var isIdempotentRefType = dto.ReferenceType == "sale"
                        || dto.ReferenceType == "dispatch_material";
                    var isIdempotentTxnType = dto.TransactionType == "sale_deduction"
                        || dto.TransactionType == "remove"
                        || dto.TransactionType == "return";
                    if (isIdempotentTxnType
                        && isIdempotentRefType
                        && !string.IsNullOrEmpty(dto.ReferenceId))
                    {
                        var existing = await _context.Set<StockTransaction>()
                            .Include(t => t.Article)
                            .FirstOrDefaultAsync(t =>
                                t.ArticleId == dto.ArticleId
                                && t.TransactionType == dto.TransactionType
                                && t.ReferenceType == dto.ReferenceType
                                && t.ReferenceId == dto.ReferenceId);
                        if (existing != null)
                        {
                            _logger.LogInformation(
                                "Stock transaction is idempotent no-op: article {ArticleId} already has {Type} for {RefType}:{RefId} (txn {ExistingId})",
                                dto.ArticleId, dto.TransactionType, dto.ReferenceType, dto.ReferenceId, existing.Id);
                            if (ownsTx) await dbTransaction.CommitAsync();
                            return MapToDto(existing);
                        }
                    }

                    // Lock the article row for update (prevents concurrent modifications)
                    var article = await _context.Set<Article>()
                        .FromSqlRaw("SELECT * FROM \"Articles\" WHERE \"Id\" = {0} AND \"TenantId\" = {1} AND \"IsDeleted\" = false FOR UPDATE", dto.ArticleId, _context.GetTenantId())
                        .FirstOrDefaultAsync();

                    if (article == null)
                        throw new KeyNotFoundException($"Article with ID {dto.ArticleId} not found");

                    var previousStock = article.StockQuantity;
                    decimal newStock;

                    // Calculate new stock based on transaction type
                    if (dto.TransactionType == "add" || dto.TransactionType == "transfer_in" || dto.TransactionType == "return")
                    {
                        newStock = previousStock + dto.Quantity;
                    }
                    else if (dto.TransactionType == "remove" || dto.TransactionType == "sale_deduction" ||
                             dto.TransactionType == "transfer_out" || dto.TransactionType == "damaged" || dto.TransactionType == "lost")
                    {
                        newStock = previousStock - dto.Quantity;

                        // Validate stock won't go negative
                        if (newStock < 0)
                        {
                            throw new InvalidOperationException(
                                $"Insufficient stock. Current: {previousStock}, Requested: {dto.Quantity}, Would result in: {newStock}");
                        }
                    }
                    else if (dto.TransactionType == "adjustment")
                    {
                        // Adjustment sets to absolute value - allow any value including negative for corrections
                        if (dto.Quantity < 0)
                        {
                            throw new ArgumentException("Adjustment quantity cannot be negative. Use the actual stock count.");
                        }
                        newStock = dto.Quantity;
                    }
                    else if (dto.TransactionType == "offer_added")
                    {
                        // Offer tracking only - no stock change
                        newStock = previousStock;
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown transaction type: {dto.TransactionType}");
                    }

                    // Resolve user name if not provided
                    if (string.IsNullOrEmpty(userName))
                    {
                        userName = await ResolveUserNameAsync(userId);
                    }

                    var transaction = new StockTransaction
                    {
                        ArticleId = dto.ArticleId,
                        TransactionType = dto.TransactionType,
                        Quantity = dto.Quantity,
                        PreviousStock = previousStock,
                        NewStock = newStock,
                        Reason = dto.Reason,
                        ReferenceType = dto.ReferenceType,
                        ReferenceId = dto.ReferenceId,
                        ReferenceNumber = dto.ReferenceNumber,
                        Notes = dto.Notes,
                        PerformedBy = userId,
                        PerformedByName = userName,
                        IpAddress = ipAddress,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Update article stock (except for offer tracking) - use decimal precision
                    if (dto.TransactionType != "offer_added")
                    {
                        article.StockQuantity = newStock;  // Fixed: no longer casting to int
                        article.ModifiedDate = DateTime.UtcNow;
                        article.ModifiedBy = userId;
                    }

                    _context.Set<StockTransaction>().Add(transaction);
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException dux) when (IsIdempotencyViolation(dux))
                    {
                        // Concurrent writer inserted the same idempotent transaction
                        // between our SELECT and INSERT. Roll back the stock update
                        // we just applied in-memory and return the winning row so
                        // the API stays idempotent instead of failing the caller.
                        //
                        // When we're inside a caller's transaction we must NOT roll it
                        // back (that would silently discard the caller's own writes).
                        // Postgres has already aborted the enclosing transaction on the
                        // unique violation anyway, so rethrow and let the owner unwind.
                        if (!ownsTx) throw;
                        await dbTransaction.RollbackAsync();

                        // CRITICAL: the Article was loaded with FOR UPDATE and is
                        // tracked as Modified (StockQuantity was set to newStock in
                        // memory). The DB tx rollback does NOT revert the in-memory
                        // change, and this scoped DbContext is shared with callers
                        // like DispatchService — the next SaveChangesAsync would
                        // silently re-persist the stale deduction, defeating the
                        // whole idempotency guard. Reload from the DB to sync.
                        try
                        {
                            await _context.Entry(article).ReloadAsync();
                        }
                        catch (Exception reloadEx)
                        {
                            _logger.LogWarning(reloadEx, "Failed to reload Article {ArticleId} after idempotency race; detaching to avoid stale write", article.Id);
                            _context.Entry(article).State = EntityState.Detached;
                        }

                        var winner = await _context.Set<StockTransaction>()
                            .Include(t => t.Article)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t =>
                                t.ArticleId == dto.ArticleId
                                && t.TransactionType == dto.TransactionType
                                && t.ReferenceType == dto.ReferenceType
                                && t.ReferenceId == dto.ReferenceId);
                        if (winner != null)
                        {
                            _logger.LogInformation(
                                "Stock idempotency race resolved for article {ArticleId} {Type} {RefType}:{RefId}; returning existing txn {Id}",
                                dto.ArticleId, dto.TransactionType, dto.ReferenceType, dto.ReferenceId, winner.Id);
                            return MapToDto(winner);
                        }
                        throw;
                    }

                    // Commit only if we opened the transaction; otherwise the caller
                    // commits once its own unit of work completes.
                    if (ownsTx) await dbTransaction.CommitAsync();

                    transaction.Article = article;
                    return MapToDto(transaction);
                }
                catch
                {
                    if (ownsTx) await dbTransaction.RollbackAsync();
                    throw;
                }
                finally
                {
                    if (ownsTx) await dbTransaction.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// Recognises the "UX_stock_transactions_idempotency" partial unique index
        /// violation raised by Postgres when two concurrent writers try to record
        /// the same physical stock movement.
        /// </summary>
        private static bool IsIdempotencyViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                var msg = e.Message ?? string.Empty;
                if (msg.Contains("UX_stock_transactions_idempotency", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("stock_transactions_idempotency", StringComparison.OrdinalIgnoreCase))
                    return true;
                // Postgres error code 23505 = unique_violation.
                if (msg.Contains("23505") && msg.Contains("stock_transactions", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public async Task<StockTransactionDto> AddStockAsync(
            int articleId, 
            decimal quantity, 
            string reason, 
            string userId, 
            string? userName = null, 
            string? notes = null,
            string? ipAddress = null)
        {
            return await CreateTransactionAsync(new CreateStockTransactionDto
            {
                ArticleId = articleId,
                TransactionType = "add",
                Quantity = quantity,
                Reason = reason,
                ReferenceType = "manual",
                Notes = notes
            }, userId, userName, ipAddress);
        }

        public async Task<StockTransactionDto> RemoveStockAsync(
            int articleId, 
            decimal quantity, 
            string reason, 
            string userId, 
            string? userName = null, 
            string? notes = null,
            string? ipAddress = null)
        {
            return await CreateTransactionAsync(new CreateStockTransactionDto
            {
                ArticleId = articleId,
                TransactionType = "remove",
                Quantity = quantity,
                Reason = reason,
                ReferenceType = "manual",
                Notes = notes
            }, userId, userName, ipAddress);
        }

        public async Task<StockDeductionResultDto> DeductStockFromSaleAsync(int saleId, string userId, string? userName = null, string? ipAddress = null)
        {
            var result = new StockDeductionResultDto { Success = true };

            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale == null)
            {
                result.Success = false;
                result.Errors.Add($"Sale with ID {saleId} not found");
                return result;
            }

            // Resolve user name
            if (string.IsNullOrEmpty(userName))
            {
                userName = await ResolveUserNameAsync(userId);
            }

            // Process only material items (not services)
            var materialItems = sale.Items?.Where(i => i.Type == "material" || i.Type == "article").ToList() ?? new List<MyApi.Modules.Sales.Models.SaleItem>();

            foreach (var item in materialItems)
            {
                result.ItemsProcessed++;

                if (!item.ArticleId.HasValue)
                {
                    result.Errors.Add($"Item '{item.ItemName}' has no linked article");
                    result.ItemsFailed++;
                    continue;
                }

                try
                {
                    // ReferenceId is composite (saleId:itemId) so each SaleItem
                    // line gets its own idempotency key. Keying on saleId alone
                    // caused sales with two lines for the same article to be
                    // silently deduplicated (only the first line deducted).
                    var transaction = await CreateTransactionAsync(new CreateStockTransactionDto
                    {
                        ArticleId = item.ArticleId.Value,
                        TransactionType = "sale_deduction",
                        Quantity = item.Quantity,
                        Reason = $"Stock deducted for closed sale",
                        ReferenceType = "sale",
                        ReferenceId = $"{saleId}:{item.Id}",
                        ReferenceNumber = sale.SaleNumber,
                        Notes = $"Item: {item.ItemName}, Qty: {item.Quantity}"
                    }, userId, userName, ipAddress);

                    result.Transactions.Add(transaction);
                    result.ItemsDeducted++;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient stock"))
                {
                    _logger.LogWarning("Insufficient stock for article {ArticleId} in sale {SaleId}: {Message}", 
                        item.ArticleId, saleId, ex.Message);
                    result.Errors.Add($"Insufficient stock for '{item.ItemName}': {ex.Message}");
                    result.ItemsFailed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deducting stock for article {ArticleId} from sale {SaleId}", item.ArticleId, saleId);
                    result.Errors.Add($"Failed to deduct stock for '{item.ItemName}': {ex.Message}");
                    result.ItemsFailed++;
                }
            }

            result.Success = result.ItemsFailed == 0;
            return result;
        }

        public async Task<StockDeductionResultDto> RestoreStockFromSaleAsync(int saleId, string userId, string? userName = null, string? ipAddress = null)
        {
            var result = new StockDeductionResultDto { Success = true };

            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale == null)
            {
                result.Success = false;
                result.Errors.Add($"Sale with ID {saleId} not found");
                return result;
            }

            // Resolve user name
            if (string.IsNullOrEmpty(userName))
            {
                userName = await ResolveUserNameAsync(userId);
            }

            // Process only material items
            var materialItems = sale.Items?.Where(i => i.Type == "material" || i.Type == "article").ToList() ?? new List<MyApi.Modules.Sales.Models.SaleItem>();

            foreach (var item in materialItems)
            {
                result.ItemsProcessed++;

                if (!item.ArticleId.HasValue)
                {
                    result.ItemsFailed++;
                    continue;
                }

                try
                {
                    // Composite ReferenceId — mirrors DeductStockFromSaleAsync so
                    // per-line returns remain distinct under the idempotency guard.
                    var transaction = await CreateTransactionAsync(new CreateStockTransactionDto
                    {
                        ArticleId = item.ArticleId.Value,
                        TransactionType = "return",
                        Quantity = item.Quantity,
                        Reason = $"Stock restored from cancelled/reopened sale",
                        ReferenceType = "sale",
                        ReferenceId = $"{saleId}:{item.Id}",
                        ReferenceNumber = sale.SaleNumber,
                        Notes = $"Item: {item.ItemName}, Qty: {item.Quantity}"
                    }, userId, userName, ipAddress);

                    // Fix #4: after the return, "reverse" the matching prior sale_deduction
                    // rows by mutating their ReferenceId to a one-off suffix. Otherwise the
                    // idempotency guard (partial unique index on
                    // (tenant, article, ref_type, ref_id, txn_type)) would treat the next
                    // close as a no-op — silently skipping stock deduction after a
                    // reopen-and-edit-quantity cycle, while the sale reports success.
                    var reversedTag = $"{saleId}:{item.Id}:reversed-{DateTime.UtcNow.Ticks}";
                    var priorDeductions = await _context.Set<StockTransaction>()
                        .Where(t => t.ArticleId == item.ArticleId.Value
                                    && t.TransactionType == "sale_deduction"
                                    && t.ReferenceType == "sale"
                                    && t.ReferenceId == $"{saleId}:{item.Id}")
                        .ToListAsync();
                    foreach (var prior in priorDeductions)
                    {
                        prior.ReferenceId = reversedTag;
                        prior.Notes = string.IsNullOrEmpty(prior.Notes)
                            ? "Reversed by sale restore"
                            : prior.Notes + " | Reversed by sale restore";
                    }
                    if (priorDeductions.Count > 0)
                        await _context.SaveChangesAsync();

                    result.Transactions.Add(transaction);
                    result.ItemsRestored++;  // Fixed: was incorrectly named ItemsDeducted
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error restoring stock for article {ArticleId} from sale {SaleId}", item.ArticleId, saleId);
                    result.Errors.Add($"Failed to restore stock for '{item.ItemName}': {ex.Message}");
                    result.ItemsFailed++;
                }
            }

            result.Success = result.ItemsFailed == 0;
            return result;
        }

        public async Task<StockTransactionDto> LogOfferItemAddedAsync(
            int articleId, 
            decimal quantity, 
            int offerId, 
            string offerNumber, 
            string userId, 
            string? userName = null)
        {
            return await CreateTransactionAsync(new CreateStockTransactionDto
            {
                ArticleId = articleId,
                TransactionType = "offer_added",
                Quantity = quantity,
                Reason = "Item added to offer",
                ReferenceType = "offer",
                ReferenceId = offerId.ToString(),
                ReferenceNumber = offerNumber
            }, userId, userName);
        }

        private async Task<string> ResolveUserNameAsync(string userId)
        {
            // Try admin users first
            var adminUser = await _context.MainAdminUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
            if (adminUser != null)
                return $"{adminUser.FirstName} {adminUser.LastName}".Trim();

            // Try regular users
            var regularUser = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
            if (regularUser != null)
                return $"{regularUser.FirstName} {regularUser.LastName}".Trim();

            return userId;
        }

        private StockTransactionDto MapToDto(StockTransaction t)
        {
            return new StockTransactionDto
            {
                Id = t.Id,
                ArticleId = t.ArticleId,
                ArticleName = t.Article?.Name,
                ArticleNumber = t.Article?.ArticleNumber,
                TransactionType = t.TransactionType,
                Quantity = t.Quantity,
                PreviousStock = t.PreviousStock,
                NewStock = t.NewStock,
                Reason = t.Reason,
                ReferenceType = t.ReferenceType,
                ReferenceId = t.ReferenceId,
                ReferenceNumber = t.ReferenceNumber,
                Notes = t.Notes,
                PerformedBy = t.PerformedBy,
                PerformedByName = t.PerformedByName,
                CreatedAt = t.CreatedAt
            };
        }
    }
}
