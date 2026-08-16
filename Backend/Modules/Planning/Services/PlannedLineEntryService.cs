using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Contacts.Services;
using MyApi.Modules.Planning.DTOs;
using MyApi.Modules.Planning.Models;
using MyApi.Modules.Shared.Services;

namespace MyApi.Modules.Planning.Services
{
    public class PlannedLineEntryService : IPlannedLineEntryService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PlannedLineEntryService>? _logger;
        private readonly IContactActivityService? _contactActivity;
        private readonly IActivityLogger? _activityLogger;
        private static readonly HashSet<string> ValidParents = new(StringComparer.OrdinalIgnoreCase)
            { "offer_item", "sale_item", "service_order_job", "deal_item" };
        private static readonly HashSet<string> ValidKinds = new(StringComparer.OrdinalIgnoreCase)
            { "time", "expense", "material" };
        // Expense types are now managed via the Lookups module (expense_types table).
        // Any non-empty value is accepted here; the UI supplies values from that lookup.

        public PlannedLineEntryService(
            ApplicationDbContext db,
            ILogger<PlannedLineEntryService>? logger = null,
            IContactActivityService? contactActivity = null,
            IActivityLogger? activityLogger = null)
        {
            _db = db;
            _logger = logger;
            _contactActivity = contactActivity;
            _activityLogger = activityLogger;
        }

        public async Task<List<PlannedLineEntryDto>> GetForParentAsync(string parentType, int parentId)
        {
            ValidateParent(parentType);
            return await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == parentType.ToLower() && p.ParentId == parentId)
                .OrderBy(p => p.Id)
                .Select(p => Map(p))
                .ToListAsync();
        }

        public async Task<PlannedLineEntryDto> CreateAsync(string parentType, int parentId, CreatePlannedLineEntryDto dto, string userId)
        {
            ValidateParent(parentType);
            ValidateKind(dto);

            int? origin = await ResolveOriginAsync(parentType, parentId);


            var entity = new PlannedLineEntry
            {
                ParentType = parentType.ToLower(),
                ParentId = parentId,
                OriginOfferItemId = origin,
                Kind = dto.Kind.ToLower(),
                PlannedMinutes = dto.PlannedMinutes,
                TechnicianCount = dto.TechnicianCount,
                HourlyRate = dto.HourlyRate,
                ExpenseType = dto.ExpenseType?.ToLower(),
                PlannedAmount = dto.PlannedAmount,
                Currency = dto.Currency,
                Description = dto.Description,
                ArticleId = dto.ArticleId,
                ArticleName = dto.ArticleName,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                Unit = dto.Unit,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Set<PlannedLineEntry>().Add(entity);
            await _db.SaveChangesAsync();

            var syncWarning = await SyncChainFromAsync(parentType, parentId, userId);
            await LogPlannedActivityAsync(entity, ContactActivityTypes.PlannedEntryAdded, userId);
            await LogModuleActivityAsync(entity, "item_added", userId);
            var mapped = Map(entity);
            mapped.SyncWarning = syncWarning;
            return mapped;
        }

        public async Task<PlannedLineEntryDto> UpdateAsync(int id, UpdatePlannedLineEntryDto dto, string userId)
        {
            var entity = await _db.Set<PlannedLineEntry>().FirstOrDefaultAsync(p => p.Id == id)
                ?? throw new KeyNotFoundException($"PlannedLineEntry {id} not found");
            ValidateKind(dto);
            entity.Kind = dto.Kind.ToLower();
            entity.PlannedMinutes = dto.PlannedMinutes;
            entity.TechnicianCount = dto.TechnicianCount;
            entity.HourlyRate = dto.HourlyRate;
            entity.ExpenseType = dto.ExpenseType?.ToLower();
            entity.PlannedAmount = dto.PlannedAmount;
            entity.Currency = dto.Currency;
            entity.Description = dto.Description;
            entity.ArticleId = dto.ArticleId;
            entity.ArticleName = dto.ArticleName;
            entity.Quantity = dto.Quantity;
            entity.UnitPrice = dto.UnitPrice;
            entity.Unit = dto.Unit;
            entity.ModifiedBy = userId;
            entity.ModifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var syncWarning = await SyncChainFromAsync(entity.ParentType, entity.ParentId, userId);
            await LogPlannedActivityAsync(entity, ContactActivityTypes.PlannedEntryUpdated, userId);
            await LogModuleActivityAsync(entity, "item_updated", userId);
            var mapped = Map(entity);
            mapped.SyncWarning = syncWarning;
            return mapped;
        }

        public async Task<string?> DeleteAsync(int id, string userId)
        {
            var entity = await _db.Set<PlannedLineEntry>().FirstOrDefaultAsync(p => p.Id == id);
            if (entity == null) return null;
            var parentType = entity.ParentType;
            var parentId = entity.ParentId;
            _db.Set<PlannedLineEntry>().Remove(entity);
            await _db.SaveChangesAsync();

            var syncWarning = await SyncChainFromAsync(parentType, parentId, userId);
            await LogPlannedActivityAsync(entity, ContactActivityTypes.PlannedEntryDeleted, userId);
            await LogModuleActivityAsync(entity, "item_deleted", userId);
            return syncWarning;
        }


        /// <summary>
        /// Bidirectional chain sync: after any change on one level (offer_item / sale_item /
        /// service_order_job), resync linked peers so planned time/expenses are visible from
        /// offer, sale, service-order and dispatch views.
        ///
        /// ServiceOrderJob.SaleItemId can be either "12" or an installation-grouped list
        /// like "12,13,14". Sync therefore resolves a set of related sale items and replaces
        /// only the affected OriginOfferItemId slice on grouped jobs, so changing one offer
        /// line never wipes plans belonging to sibling sale items in the same job.
        /// </summary>
        private async Task<string?> SyncChainFromAsync(string sourceParentType, int sourceParentId, string userId)
        {
            try
            {
                var sourceType = sourceParentType.ToLowerInvariant();
                var offerItemIds = new HashSet<int>();
                var saleItemIds = new HashSet<int>();

                if (sourceType == "offer_item")
                {
                    offerItemIds.Add(sourceParentId);
                    foreach (var saleItemId in await ResolveSaleItemsFromOfferItemAsync(sourceParentId))
                        saleItemIds.Add(saleItemId);
                }
                else if (sourceType == "sale_item")
                {
                    saleItemIds.Add(sourceParentId);
                    foreach (var offerItemId in await ResolveOfferItemIdsFromSaleItemIdsAsync(new[] { sourceParentId }))
                        offerItemIds.Add(offerItemId);
                }
                else if (sourceType == "service_order_job")
                {
                    var job = await _db.ServiceOrderJobs.FirstOrDefaultAsync(j => j.Id == sourceParentId);
                    foreach (var saleItemId in ParseSaleItemIds(job?.SaleItemId))
                        saleItemIds.Add(saleItemId);
                    foreach (var offerItemId in await ResolveOfferItemIdsFromSaleItemIdsAsync(saleItemIds))
                        offerItemIds.Add(offerItemId);
                    await EnsureServiceOrderJobEntryOriginsAsync(sourceParentId, offerItemIds, userId);
                }

                foreach (var offerItemId in offerItemIds.ToList())
                {
                    if (sourceType == "offer_item" && sourceParentId == offerItemId)
                        continue;
                    await OverwriteScopeAsync(
                        sourceParentType,
                        sourceParentId,
                        "offer_item",
                        offerItemId,
                        userId,
                        replaceOriginIds: new[] { offerItemId },
                        copiedOriginOverride: offerItemId,
                        sourceOriginIds: sourceType == "service_order_job" ? new[] { offerItemId } : null,
                        includeNullSourceOrigin: false);
                }

                foreach (var saleItemId in saleItemIds.ToList())
                {
                    if (sourceType == "sale_item" && sourceParentId == saleItemId)
                        continue;

                    var targetOrigins = await ResolveOfferItemIdsFromSaleItemIdsAsync(new[] { saleItemId });
                    var targetOrigin = targetOrigins.Count == 1 ? targetOrigins[0] : (int?)null;
                    await OverwriteScopeAsync(
                        sourceParentType,
                        sourceParentId,
                        "sale_item",
                        saleItemId,
                        userId,
                        replaceOriginIds: targetOrigin.HasValue ? new[] { targetOrigin.Value } : null,
                        copiedOriginOverride: sourceType == "service_order_job" ? targetOrigin : null,
                        sourceOriginIds: sourceType == "service_order_job" && targetOrigin.HasValue ? new[] { targetOrigin.Value } : null,
                        includeNullSourceOrigin: false);
                }

                // Job-level planned entries are intentionally NOT overwritten from the
                // offer/sale side any more. OverwriteScopeAsync wipes the whole target
                // scope and re-copies, which silently destroyed manual job-level plan
                // adjustments made by dispatchers whenever the originating offer/sale line
                // was later touched. Planning is now authored on the service order job
                // (JobDetail) and seeded once at service-order creation via CopyAsync, so
                // there is no upstream owner that should be allowed to overwrite it.
                await Task.CompletedTask;
                return null;
            }
            catch (Exception ex)
            {
                // Cascade is best-effort — never block the primary write, but surface
                // the failure at Error level so silent drift between offer / sale /
                // service-order-job planned views is visible in monitoring, and
                // return the reason so the UI can toast the exact cause.
                _logger?.LogError(ex,
                    "Failed to sync planned entries from {ParentType} {ParentId}. Offer/Sale/ServiceOrderJob planning views may drift until the next edit.",
                    sourceParentType,
                    sourceParentId);
                return ex.Message;
            }
        }

        private async Task<List<int>> ResolveSaleItemsFromOfferItemAsync(int offerItemId)
        {
            var direct = await _db.SaleItems
                .Where(si => si.OriginOfferItemId == offerItemId)
                .Select(si => si.Id)
                .ToListAsync();
            if (direct.Count > 0) return direct.Distinct().ToList();

            var offerItem = await _db.OfferItems.FirstOrDefaultAsync(oi => oi.Id == offerItemId);
            if (offerItem == null) return new List<int>();
            var offer = await _db.Offers.FirstOrDefaultAsync(o => o.Id == offerItem.OfferId);
            if (offer == null || string.IsNullOrWhiteSpace(offer.ConvertedToSaleId)) return new List<int>();
            if (!int.TryParse(offer.ConvertedToSaleId, out var saleId)) return new List<int>();

            await EnsureSaleItemOriginsForConvertedOfferAsync(offerItem.OfferId, saleId);

            return await _db.SaleItems
                .Where(si => si.SaleId == saleId && si.OriginOfferItemId == offerItemId)
                .Select(si => si.Id)
                .ToListAsync();
        }

        private async Task EnsureServiceOrderJobEntryOriginsAsync(int jobId, IReadOnlyCollection<int> originOfferItemIds, string userId)
        {
            var origins = originOfferItemIds.Distinct().ToList();
            if (origins.Count == 0) return;

            var unscoped = await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == "service_order_job" && p.ParentId == jobId && !p.OriginOfferItemId.HasValue)
                .ToListAsync();
            if (unscoped.Count == 0) return;

            var scoped = await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == "service_order_job" && p.ParentId == jobId && p.OriginOfferItemId.HasValue)
                .ToListAsync();
            static string Key(PlannedLineEntry p, int origin) =>
                $"{origin}|{p.Kind}|{p.ArticleId?.ToString() ?? "-"}|{p.ExpenseType ?? "-"}|{p.Description ?? p.ArticleName ?? "-"}|{p.PlannedMinutes?.ToString() ?? "-"}|{p.TechnicianCount?.ToString() ?? "-"}|{p.HourlyRate?.ToString() ?? "-"}|{p.PlannedAmount?.ToString() ?? "-"}|{p.Quantity?.ToString() ?? "-"}|{p.UnitPrice?.ToString() ?? "-"}|{p.Unit ?? "-"}";
            var existingKeys = new HashSet<string>(scoped.Select(p => Key(p, p.OriginOfferItemId!.Value)));

            foreach (var entry in unscoped)
            {
                var firstMissingOrigin = origins.FirstOrDefault(origin => !existingKeys.Contains(Key(entry, origin)));
                if (firstMissingOrigin == 0)
                {
                    _db.Set<PlannedLineEntry>().Remove(entry);
                    continue;
                }

                entry.OriginOfferItemId = firstMissingOrigin;
                entry.ModifiedBy = userId;
                entry.ModifiedAt = DateTime.UtcNow;
                existingKeys.Add(Key(entry, firstMissingOrigin));

                foreach (var origin in origins.Where(origin => origin != firstMissingOrigin))
                {
                    if (!existingKeys.Add(Key(entry, origin))) continue;
                    _db.Set<PlannedLineEntry>().Add(new PlannedLineEntry
                    {
                        ParentType = "service_order_job",
                        ParentId = jobId,
                        OriginOfferItemId = origin,
                        Kind = entry.Kind,
                        PlannedMinutes = entry.PlannedMinutes,
                        TechnicianCount = entry.TechnicianCount,
                        HourlyRate = entry.HourlyRate,
                        ExpenseType = entry.ExpenseType,
                        PlannedAmount = entry.PlannedAmount,
                        Currency = entry.Currency,
                        Description = entry.Description,
                        ArticleId = entry.ArticleId,
                        ArticleName = entry.ArticleName,
                        Quantity = entry.Quantity,
                        UnitPrice = entry.UnitPrice,
                        Unit = entry.Unit,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Fully overwrite the target scope's planned entries with a copy of the source scope.
        /// Wipes every row at (targetParentType, targetParentId) then re-copies from source so
        /// the two views end up identical. This is intentional: the user wants offer, sale, and
        /// dispatch to display the same set of planned time/expenses.
        /// </summary>
        private async Task OverwriteScopeAsync(
            string sourceParentType,
            int sourceParentId,
            string targetParentType,
            int targetParentId,
            string userId,
            IReadOnlyCollection<int>? replaceOriginIds = null,
            int? copiedOriginOverride = null,
            IReadOnlyCollection<int>? sourceOriginIds = null,
            bool includeNullSourceOrigin = false)
        {
            var targetType = targetParentType.ToLower();
            var targetQuery = _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == targetType && p.ParentId == targetParentId);

            List<PlannedLineEntry> existing;
            if (targetType == "service_order_job" && replaceOriginIds is { Count: > 0 })
            {
                var originIds = replaceOriginIds.ToList();
                existing = await targetQuery
                    .Where(p => p.OriginOfferItemId.HasValue && originIds.Contains(p.OriginOfferItemId.Value))
                    .ToListAsync();
            }
            else
            {
                existing = await targetQuery.ToListAsync();
            }

            if (existing.Count > 0)
            {
                _db.Set<PlannedLineEntry>().RemoveRange(existing);
                await _db.SaveChangesAsync();
            }
            await CopyEntriesAsync(
                sourceParentType,
                sourceParentId,
                targetParentType,
                targetParentId,
                userId,
                copiedOriginOverride,
                sourceOriginIds,
                includeNullSourceOrigin);
        }

        public async Task CopyAsync(string sourceParentType, int sourceParentId, string targetParentType, int targetParentId, string userId)
            => await CopyEntriesAsync(sourceParentType, sourceParentId, targetParentType, targetParentId, userId, copiedOriginOverride: null);

        private async Task CopyEntriesAsync(
            string sourceParentType,
            int sourceParentId,
            string targetParentType,
            int targetParentId,
            string userId,
            int? copiedOriginOverride,
            IReadOnlyCollection<int>? sourceOriginIds = null,
            bool includeNullSourceOrigin = false)
        {
            ValidateParent(sourceParentType);
            ValidateParent(targetParentType);
            var src = await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == sourceParentType.ToLower() && p.ParentId == sourceParentId)
                .ToListAsync();
            if (sourceOriginIds is { Count: > 0 })
            {
                var originIds = sourceOriginIds.ToHashSet();
                src = src
                    .Where(p => (p.OriginOfferItemId.HasValue && originIds.Contains(p.OriginOfferItemId.Value))
                        || (includeNullSourceOrigin && !p.OriginOfferItemId.HasValue))
                    .ToList();
            }
            if (src.Count == 0) return;

            // Phase A (A1): make CopyAsync idempotent. A workflow retry or a loop
            // that re-invokes copy for the same (source→target, kind, article,
            // description) must never stack duplicate planned rows on the target.
            var targetType = targetParentType.ToLower();
            var existing = await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == targetType && p.ParentId == targetParentId)
                .ToListAsync();
            static string Key(PlannedLineEntry p) =>
                $"{p.Kind}|{p.ArticleId?.ToString() ?? "-"}|{p.ExpenseType ?? "-"}|{p.Description ?? p.ArticleName ?? "-"}|{p.OriginOfferItemId?.ToString() ?? "-"}|{p.PlannedMinutes?.ToString() ?? "-"}|{p.TechnicianCount?.ToString() ?? "-"}|{p.HourlyRate?.ToString() ?? "-"}|{p.PlannedAmount?.ToString() ?? "-"}|{p.Quantity?.ToString() ?? "-"}|{p.UnitPrice?.ToString() ?? "-"}|{p.Unit ?? "-"}";
            var existingKeys = new HashSet<string>(existing.Select(Key));
            var fallbackSourceOrigin = copiedOriginOverride ?? await ResolveOriginAsync(sourceParentType, sourceParentId);

            foreach (var s in src)
            {
                var candidate = new PlannedLineEntry
                {
                    ParentType = targetType,
                    ParentId = targetParentId,
                    OriginOfferItemId = copiedOriginOverride ?? s.OriginOfferItemId ?? fallbackSourceOrigin ?? (sourceParentType.Equals("offer_item", StringComparison.OrdinalIgnoreCase) ? s.ParentId : null),
                    Kind = s.Kind,
                    PlannedMinutes = s.PlannedMinutes,
                    TechnicianCount = s.TechnicianCount,
                    HourlyRate = s.HourlyRate,
                    ExpenseType = s.ExpenseType,
                    PlannedAmount = s.PlannedAmount,
                    Currency = s.Currency,
                    Description = s.Description,
                    ArticleId = s.ArticleId,
                    ArticleName = s.ArticleName,
                    Quantity = s.Quantity,
                    UnitPrice = s.UnitPrice,
                    Unit = s.Unit,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                };
                if (existingKeys.Add(Key(candidate)))
                    _db.Set<PlannedLineEntry>().Add(candidate);
            }
            await _db.SaveChangesAsync();
        }

        private static List<int> ParseSaleItemIds(string? raw)
        {
            var ids = new List<int>();
            if (string.IsNullOrWhiteSpace(raw)) return ids;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var id))
                    ids.Add(id);
            }
            return ids.Distinct().ToList();
        }

        private async Task<List<int>> ResolveOfferItemIdsFromSaleItemIdsAsync(IEnumerable<int> saleItemIds)
        {
            var ids = saleItemIds.Distinct().ToList();
            if (ids.Count == 0) return new List<int>();

            var saleItems = await _db.SaleItems.Where(si => ids.Contains(si.Id)).ToListAsync();
            var origins = saleItems
                .Where(si => si.OriginOfferItemId.HasValue)
                .Select(si => si.OriginOfferItemId!.Value)
                .ToHashSet();

            var saleIdsToRepair = saleItems
                .Where(si => !si.OriginOfferItemId.HasValue)
                .Select(si => si.SaleId)
                .Distinct()
                .ToList();
            if (saleIdsToRepair.Count > 0)
            {
                var sales = await _db.Sales.Where(s => saleIdsToRepair.Contains(s.Id)).ToListAsync();
                foreach (var sale in sales)
                {
                    int? offerId = null;
                    if (!string.IsNullOrWhiteSpace(sale.OfferId) && int.TryParse(sale.OfferId, out var parsedOfferId))
                        offerId = parsedOfferId;
                    if (!offerId.HasValue)
                    {
                        offerId = await _db.Offers
                            .Where(o => o.ConvertedToSaleId == sale.Id.ToString())
                            .Select(o => (int?)o.Id)
                            .FirstOrDefaultAsync();
                    }
                    if (offerId.HasValue)
                        await EnsureSaleItemOriginsForConvertedOfferAsync(offerId.Value, sale.Id);
                }

                saleItems = await _db.SaleItems.Where(si => ids.Contains(si.Id)).ToListAsync();
                foreach (var origin in saleItems.Where(si => si.OriginOfferItemId.HasValue).Select(si => si.OriginOfferItemId!.Value))
                    origins.Add(origin);
            }

            var entryOrigins = await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == "sale_item" && ids.Contains(p.ParentId) && p.OriginOfferItemId.HasValue)
                .Select(p => p.OriginOfferItemId!.Value)
                .Distinct()
                .ToListAsync();
            foreach (var origin in entryOrigins)
                origins.Add(origin);

            return origins.ToList();
        }

        /// <summary>
        /// Back-fills <c>SaleItem.OriginOfferItemId</c> for a sale converted from an offer.
        ///
        /// Matching is done on content — (ArticleId, Description, Quantity, UnitPrice), then a
        /// looser (ArticleId, Description) pass — never on list position: sale lines can be
        /// reordered, inserted or deleted after conversion, and positional pairing would then
        /// silently attach the plan of one line to a completely different line.
        /// Ambiguous candidates (same signature appearing twice) are matched in document order
        /// among themselves, which is the only defensible reading of two identical lines.
        /// Lines with no confident match are left unlinked rather than mis-linked.
        /// </summary>
        private async Task EnsureSaleItemOriginsForConvertedOfferAsync(int offerId, int saleId)
        {
            var offerItems = await _db.OfferItems
                .Where(oi => oi.OfferId == offerId)
                .OrderBy(oi => oi.DisplayOrder)
                .ThenBy(oi => oi.Id)
                .ToListAsync();
            var saleItems = await _db.SaleItems
                .Where(si => si.SaleId == saleId)
                .OrderBy(si => si.DisplayOrder)
                .ThenBy(si => si.Id)
                .ToListAsync();

            if (offerItems.Count == 0 || saleItems.Count == 0) return;

            // Offer items already claimed by another sale line must not be reused.
            var taken = new HashSet<int>(saleItems
                .Where(si => si.OriginOfferItemId.HasValue)
                .Select(si => si.OriginOfferItemId!.Value));

            static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
            static string StrictKey(int? articleId, string? desc, decimal qty, decimal price)
                => $"{articleId?.ToString() ?? "-"}|{Norm(desc)}|{qty}|{price}";
            static string LooseKey(int? articleId, string? desc)
                => $"{articleId?.ToString() ?? "-"}|{Norm(desc)}";

            var strictBuckets = new Dictionary<string, Queue<int>>();
            var looseBuckets = new Dictionary<string, Queue<int>>();
            foreach (var oi in offerItems)
            {
                if (taken.Contains(oi.Id)) continue;
                var sk = StrictKey(oi.ArticleId, oi.Description, oi.Quantity, oi.UnitPrice);
                if (!strictBuckets.TryGetValue(sk, out var sq)) strictBuckets[sk] = sq = new Queue<int>();
                sq.Enqueue(oi.Id);

                var lk = LooseKey(oi.ArticleId, oi.Description);
                if (!looseBuckets.TryGetValue(lk, out var lq)) looseBuckets[lk] = lq = new Queue<int>();
                lq.Enqueue(oi.Id);
            }

            var changed = false;
            // Pass 1: exact signature.
            foreach (var si in saleItems)
            {
                if (si.OriginOfferItemId.HasValue) continue;
                var sk = StrictKey(si.ArticleId, si.Description, si.Quantity, si.UnitPrice);
                while (strictBuckets.TryGetValue(sk, out var q) && q.Count > 0)
                {
                    var candidate = q.Dequeue();
                    if (taken.Contains(candidate)) continue;
                    si.OriginOfferItemId = candidate;
                    taken.Add(candidate);
                    changed = true;
                    break;
                }
            }

            // Pass 2: article + description only (quantity/price edited after conversion).
            foreach (var si in saleItems)
            {
                if (si.OriginOfferItemId.HasValue) continue;
                var lk = LooseKey(si.ArticleId, si.Description);
                while (looseBuckets.TryGetValue(lk, out var q) && q.Count > 0)
                {
                    var candidate = q.Dequeue();
                    if (taken.Contains(candidate)) continue;
                    si.OriginOfferItemId = candidate;
                    taken.Add(candidate);
                    changed = true;
                    break;
                }
            }

            if (changed)
                await _db.SaveChangesAsync();
        }


        private async Task<List<int>> ResolveServiceOrderJobIdsForSaleItemsAsync(IEnumerable<int> saleItemIds)
        {
            var jobIds = new HashSet<int>();
            foreach (var saleItemId in saleItemIds.Distinct())
            {
                var token = saleItemId.ToString();
                var ids = await _db.ServiceOrderJobs
                    .Where(j => j.SaleItemId != null
                        && (j.SaleItemId == token
                            || j.SaleItemId.StartsWith(token + ",")
                            || j.SaleItemId.EndsWith("," + token)
                            || j.SaleItemId.Contains("," + token + ",")))
                    .Select(j => j.Id)
                    .ToListAsync();
                foreach (var id in ids)
                    jobIds.Add(id);
            }
            return jobIds.ToList();
        }

        private async Task<List<int>> ResolveSourceOriginScopeAsync(string sourceParentType, int sourceParentId)
        {
            if (sourceParentType.Equals("offer_item", StringComparison.OrdinalIgnoreCase))
                return new List<int> { sourceParentId };

            if (sourceParentType.Equals("sale_item", StringComparison.OrdinalIgnoreCase))
                return await ResolveOfferItemIdsFromSaleItemIdsAsync(new[] { sourceParentId });

            if (sourceParentType.Equals("service_order_job", StringComparison.OrdinalIgnoreCase))
            {
                var job = await _db.ServiceOrderJobs.FirstOrDefaultAsync(j => j.Id == sourceParentId);
                var saleItemIds = ParseSaleItemIds(job?.SaleItemId);
                return await ResolveOfferItemIdsFromSaleItemIdsAsync(saleItemIds);
            }

            return new List<int>();
        }

        public async Task<PlanVsActualLineDto> GetPlanVsActualAsync(int serviceOrderJobId)
        {
            var planned = await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == "service_order_job" && p.ParentId == serviceOrderJobId)
                .ToListAsync();

            int plannedMinutes = planned.Where(p => p.Kind == "time")
                .Sum(p => (p.PlannedMinutes ?? 0) * (p.TechnicianCount ?? 1));

            var plannedExpenseByType = planned
                .Where(p => p.Kind == "expense" && !string.IsNullOrEmpty(p.ExpenseType))
                .GroupBy(p => p.ExpenseType!)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedAmount ?? 0));

            // Planned materials (kind='material') are reported on their own total — NOT folded
            // into the expense buckets — so this panel agrees with the materials-exclusive
            // expense badge and the separate materials badge rendered on the same screen.
            var plannedMaterialTotal = planned
                .Where(p => p.Kind == "material")
                .Sum(p => (p.Quantity ?? 0) * (p.UnitPrice ?? 0));
            decimal actualMaterialTotal = 0m;

            // Actuals come from dispatch TimeEntries/Expenses on dispatches linked to this job.
            // Best-effort: aggregate via DispatchJobs that reference this ServiceOrderJob.
            var dispatchIds = await _db.Set<MyApi.Modules.Dispatches.Models.DispatchJob>()
                .Where(dj => dj.JobId == serviceOrderJobId && !dj.IsDeleted)
                .Select(dj => dj.DispatchId)
                .Distinct()
                .ToListAsync();

            int actualMinutes = 0;
            var actualExpenseByType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            decimal actualMinutesDecimal = 0m;
            if (dispatchIds.Count > 0)
            {
                // A dispatch can hold several jobs, and a job can belong to several dispatches
                // (re-dispatching is allowed). Attribution rules:
                //  - entries explicitly tagged with this ServiceOrderJobId  → counted in full
                //  - untagged (whole-dispatch) entries                      → shared pro-rata across
                //    the jobs of that dispatch, so nothing is lost and nothing is double-counted.
                var jobCountsByDispatch = (await _db.Set<MyApi.Modules.Dispatches.Models.DispatchJob>()
                    .Where(dj => dispatchIds.Contains(dj.DispatchId) && !dj.IsDeleted)
                    .GroupBy(dj => dj.DispatchId)
                    .Select(g => new { DispatchId = g.Key, Count = g.Count() })
                    .ToListAsync())
                    .ToDictionary(x => x.DispatchId, x => Math.Max(1, x.Count));

                decimal ShareFor(int dispatchId, int? taggedJobId, decimal amount)
                {
                    if (taggedJobId == serviceOrderJobId) return amount;
                    var count = jobCountsByDispatch.TryGetValue(dispatchId, out var c) ? c : 1;
                    return amount / count;
                }

                var timeEntries = await _db.Set<MyApi.Modules.Dispatches.Models.TimeEntry>()
                    .Where(t => dispatchIds.Contains(t.DispatchId) && t.Duration != null
                        && (t.ServiceOrderJobId == serviceOrderJobId || t.ServiceOrderJobId == null))
                    .Select(t => new { t.DispatchId, t.ServiceOrderJobId, t.Duration })
                    .ToListAsync();
                foreach (var t in timeEntries)
                    actualMinutesDecimal += ShareFor(t.DispatchId, t.ServiceOrderJobId, t.Duration ?? 0m);

                var expenses = await _db.Set<MyApi.Modules.Dispatches.Models.Expense>()
                    .Where(e => dispatchIds.Contains(e.DispatchId)
                        && (e.ServiceOrderJobId == serviceOrderJobId || e.ServiceOrderJobId == null))
                    .ToListAsync();
                foreach (var e in expenses)
                {
                    var key = (e.ExpenseType ?? "other").ToLower();
                    actualExpenseByType.TryGetValue(key, out var cur);
                    actualExpenseByType[key] = cur + ShareFor(e.DispatchId, e.ServiceOrderJobId, e.Amount);
                }

                // Actual material cost counts against the planned "materials" expense bucket.
                var materials = await _db.Set<MyApi.Modules.Dispatches.Models.MaterialUsage>()
                    .Where(m => dispatchIds.Contains(m.DispatchId)
                        && (m.ServiceOrderJobId == serviceOrderJobId || m.ServiceOrderJobId == null))
                    .Select(m => new { m.DispatchId, m.ServiceOrderJobId, m.TotalPrice })
                    .ToListAsync();
                actualMaterialTotal += materials.Sum(m => ShareFor(m.DispatchId, m.ServiceOrderJobId, m.TotalPrice));

                // Round once, at the end — never truncate (that under-reports every partial minute).
                actualMinutes = (int)Math.Round(actualMinutesDecimal, MidpointRounding.AwayFromZero);
            }


            // G6: also include SO-direct time/expenses (logged against the parent ServiceOrder, not a dispatch).
            var serviceOrderId = await _db.Set<MyApi.Modules.ServiceOrders.Models.ServiceOrderJob>()
                .Where(j => j.Id == serviceOrderJobId)
                .Select(j => (int?)j.ServiceOrderId)
                .FirstOrDefaultAsync();
            if (serviceOrderId.HasValue)
            {
                // SO-direct actuals belong to the whole service order: share them pro-rata across
                // its jobs so they are neither dropped nor double-counted on multi-job orders.
                var jobCount = Math.Max(1, await _db.Set<MyApi.Modules.ServiceOrders.Models.ServiceOrderJob>()
                    .CountAsync(j => j.ServiceOrderId == serviceOrderId.Value));

                var soMinutes = await _db.Set<MyApi.Modules.ServiceOrders.Models.ServiceOrderTimeEntry>()
                    .Where(t => t.ServiceOrderId == serviceOrderId.Value)
                    .SumAsync(t => (int?)t.Duration ?? 0);
                actualMinutes += (int)Math.Round((decimal)soMinutes / jobCount, MidpointRounding.AwayFromZero);

                var soExpenses = await _db.Set<MyApi.Modules.ServiceOrders.Models.ServiceOrderExpense>()
                    .Where(e => e.ServiceOrderId == serviceOrderId.Value)
                    .ToListAsync();
                foreach (var e in soExpenses)
                {
                    var key = (e.Type ?? "other").ToLower();
                    actualExpenseByType.TryGetValue(key, out var cur);
                    actualExpenseByType[key] = cur + (e.Amount / jobCount);
                }

                // SO-direct materials count against the planned "materials" bucket too.
                var soMaterialTotal = await _db.Set<MyApi.Modules.ServiceOrders.Models.ServiceOrderMaterial>()
                    .Where(m => m.ServiceOrderId == serviceOrderId.Value)
                    .SumAsync(m => (decimal?)m.TotalPrice ?? 0m);
                if (soMaterialTotal > 0)
                    actualMaterialTotal += soMaterialTotal / jobCount;
            }


            var allTypes = plannedExpenseByType.Keys.Union(actualExpenseByType.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            var buckets = allTypes.Select(t => new PlanVsActualExpenseBucket
            {
                ExpenseType = t,
                Planned = plannedExpenseByType.TryGetValue(t, out var p) ? p : 0,
                Actual = actualExpenseByType.TryGetValue(t, out var a) ? a : 0,
            }).ToList();

            return new PlanVsActualLineDto
            {
                JobId = serviceOrderJobId,
                PlannedMinutes = plannedMinutes,
                ActualMinutes = actualMinutes,
                PlannedExpenseTotal = buckets.Sum(b => b.Planned),
                ActualExpenseTotal = buckets.Sum(b => b.Actual),
                PlannedMaterialTotal = plannedMaterialTotal,
                ActualMaterialTotal = actualMaterialTotal,
                ExpenseBuckets = buckets,
            };
        }

        private static void ValidateParent(string parentType)
        {
            if (!ValidParents.Contains(parentType))
                throw new ArgumentException($"Invalid parentType '{parentType}'. Allowed: offer_item, sale_item, service_order_job");
        }

        /// <summary>
        /// Resolve OriginOfferItemId for a new direct-create entry by walking the lineage:
        /// offer_item → self; otherwise inherit from any sibling entry already linked to its origin,
        /// or for service_order_job fall back to siblings of its source sale_item.
        /// </summary>
        private async Task<int?> ResolveOriginAsync(string parentType, int parentId)
        {
            if (parentType.Equals("offer_item", StringComparison.OrdinalIgnoreCase))
                return parentId;

            var pt = parentType.ToLower();
            var siblingOrigin = await _db.Set<PlannedLineEntry>()
                .Where(p => p.ParentType == pt && p.ParentId == parentId && p.OriginOfferItemId != null)
                .Select(p => p.OriginOfferItemId)
                .FirstOrDefaultAsync();
            if (siblingOrigin != null) return siblingOrigin;

            if (parentType.Equals("sale_item", StringComparison.OrdinalIgnoreCase))
            {
                var modelOrigin = await _db.SaleItems
                    .Where(si => si.Id == parentId && si.OriginOfferItemId.HasValue)
                    .Select(si => si.OriginOfferItemId)
                    .FirstOrDefaultAsync();
                if (modelOrigin != null) return modelOrigin;
            }

            if (parentType.Equals("service_order_job", StringComparison.OrdinalIgnoreCase))
            {
                var job = await _db.ServiceOrderJobs.FirstOrDefaultAsync(j => j.Id == parentId);
                var saleItemIds = ParseSaleItemIds(job?.SaleItemId);
                if (saleItemIds.Count > 0)
                {
                    var modelOrigins = await ResolveOfferItemIdsFromSaleItemIdsAsync(saleItemIds);
                    if (modelOrigins.Count == 1) return modelOrigins[0];

                    var saleOrigin = await _db.Set<PlannedLineEntry>()
                        .Where(p => p.ParentType == "sale_item" && saleItemIds.Contains(p.ParentId) && p.OriginOfferItemId != null)
                        .Select(p => p.OriginOfferItemId)
                        .FirstOrDefaultAsync();
                    if (saleOrigin != null) return saleOrigin;
                }
            }

            return null;
        }

        private static void ValidateKind(CreatePlannedLineEntryDto dto)
        {
            if (!ValidKinds.Contains(dto.Kind))
                throw new ArgumentException($"Invalid kind '{dto.Kind}'. Allowed: time, expense, material");
            if (string.Equals(dto.Kind, "time", StringComparison.OrdinalIgnoreCase))
            {
                if ((dto.PlannedMinutes ?? 0) <= 0)
                    throw new ArgumentException("PlannedMinutes is required and must be > 0 for time entries");
            }
            else if (string.Equals(dto.Kind, "expense", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(dto.ExpenseType))
                    throw new ArgumentException("ExpenseType is required for expense entries");
                if ((dto.PlannedAmount ?? 0) <= 0)
                    throw new ArgumentException("PlannedAmount is required and must be > 0 for expense entries");
            }
            else // material
            {
                if ((dto.ArticleId ?? 0) <= 0 && string.IsNullOrWhiteSpace(dto.ArticleName))
                    throw new ArgumentException("ArticleId or ArticleName is required for material entries");
                if ((dto.Quantity ?? 0) <= 0)
                    throw new ArgumentException("Quantity is required and must be > 0 for material entries");
            }
        }

        private static PlannedLineEntryDto Map(PlannedLineEntry p) => new()
        {
            Id = p.Id,
            ParentType = p.ParentType,
            ParentId = p.ParentId,
            OriginOfferItemId = p.OriginOfferItemId,
            Kind = p.Kind,
            PlannedMinutes = p.PlannedMinutes,
            TechnicianCount = p.TechnicianCount,
            HourlyRate = p.HourlyRate,
            ExpenseType = p.ExpenseType,
            PlannedAmount = p.PlannedAmount,
            Currency = p.Currency,
            Description = p.Description,
            ArticleId = p.ArticleId,
            ArticleName = p.ArticleName,
            Quantity = p.Quantity,
            UnitPrice = p.UnitPrice,
            Unit = p.Unit,
        };

        /// <summary>
        /// Resolve the Contact that owns the parent (offer/sale/service order) of a planned entry
        /// so the activity feed can surface plan changes on the same contact timeline as the
        /// offer, sale, service order, and dispatch events.
        /// </summary>
        private async Task<(int contactId, string relatedEntityType, int relatedEntityId)?> ResolveContactContextAsync(string parentType, int parentId)
        {
            try
            {
                var pt = (parentType ?? "").ToLowerInvariant();
                if (pt == "offer_item")
                {
                    var row = await _db.OfferItems
                        .Where(oi => oi.Id == parentId)
                        .Join(_db.Offers, oi => oi.OfferId, o => o.Id, (oi, o) => new { o.ContactId, o.Id })
                        .FirstOrDefaultAsync();
                    if (row != null && row.ContactId > 0)
                        return (row.ContactId, ContactActivityEntityTypes.Offer, row.Id);
                }
                else if (pt == "sale_item")
                {
                    var row = await _db.SaleItems
                        .Where(si => si.Id == parentId)
                        .Join(_db.Sales, si => si.SaleId, s => s.Id, (si, s) => new { s.ContactId, s.Id })
                        .FirstOrDefaultAsync();
                    if (row != null && row.ContactId > 0)
                        return (row.ContactId, ContactActivityEntityTypes.Sale, row.Id);
                }
                else if (pt == "service_order_job")
                {
                    var row = await _db.ServiceOrderJobs
                        .Where(j => j.Id == parentId)
                        .Join(_db.ServiceOrders, j => j.ServiceOrderId, so => so.Id, (j, so) => new { so.ContactId, so.Id })
                        .FirstOrDefaultAsync();
                    if (row != null && row.ContactId > 0)
                        return (row.ContactId, ContactActivityEntityTypes.ServiceOrder, row.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to resolve contact context for {ParentType} {ParentId}", parentType, parentId);
            }
            return null;
        }

        private async Task LogPlannedActivityAsync(PlannedLineEntry entry, string activityType, string userId)
        {
            if (_contactActivity == null) return;
            try
            {
                var ctx = await ResolveContactContextAsync(entry.ParentType, entry.ParentId);
                if (ctx == null) return;

                string summary = entry.Kind?.ToLowerInvariant() switch
                {
                    "time" => $"{(entry.PlannedMinutes ?? 0)} min" +
                              ((entry.TechnicianCount ?? 1) > 1 ? $" x {entry.TechnicianCount} tech" : ""),
                    "expense" => $"{entry.ExpenseType} {entry.PlannedAmount:0.##} {entry.Currency}".Trim(),
                    "material" => $"{entry.ArticleName ?? ("article #" + entry.ArticleId)} x {entry.Quantity}",
                    _ => entry.Description ?? ""
                };

                string verb = activityType switch
                {
                    ContactActivityTypes.PlannedEntryAdded => "planned",
                    ContactActivityTypes.PlannedEntryUpdated => "updated planned",
                    ContactActivityTypes.PlannedEntryDeleted => "removed planned",
                    _ => "changed planned"
                };

                await _contactActivity.LogAsync(
                    contactId: ctx.Value.contactId,
                    type: activityType,
                    relatedEntityType: ctx.Value.relatedEntityType,
                    relatedEntityId: ctx.Value.relatedEntityId,
                    description: $"{verb} {entry.Kind}: {summary}".Trim(),
                    metadata: new
                    {
                        plannedEntryId = entry.Id,
                        parentType = entry.ParentType,
                        parentId = entry.ParentId,
                        originOfferItemId = entry.OriginOfferItemId,
                        kind = entry.Kind,
                        plannedMinutes = entry.PlannedMinutes,
                        technicianCount = entry.TechnicianCount,
                        hourlyRate = entry.HourlyRate,
                        expenseType = entry.ExpenseType,
                        plannedAmount = entry.PlannedAmount,
                        currency = entry.Currency,
                        articleId = entry.ArticleId,
                        articleName = entry.ArticleName,
                        quantity = entry.Quantity,
                        unitPrice = entry.UnitPrice,
                        unit = entry.Unit,
                    },
                    createdBy: userId);
            }
            catch (Exception ex)
            {
                // Activity logging is best-effort — never block the primary write.
                _logger?.LogWarning(ex, "Failed to log planned entry activity for entry {EntryId}", entry.Id);
            }
        }

        /// <summary>
        /// Resolve the module-level parent (Offer / Sale / Deal / Project) that owns
        /// this planned entry so it surfaces on that entity's Activity tab.
        /// </summary>
        private async Task<(string entityType, int entityId, string module)?> ResolveModuleParentAsync(string parentType, int parentId)
        {
            try
            {
                var pt = (parentType ?? "").ToLowerInvariant();
                if (pt == "offer_item")
                {
                    var offerId = await _db.OfferItems.Where(oi => oi.Id == parentId).Select(oi => (int?)oi.OfferId).FirstOrDefaultAsync();
                    if (offerId.HasValue) return ("Offer", offerId.Value, "Offers");
                }
                else if (pt == "sale_item")
                {
                    var saleId = await _db.SaleItems.Where(si => si.Id == parentId).Select(si => (int?)si.SaleId).FirstOrDefaultAsync();
                    if (saleId.HasValue) return ("Sale", saleId.Value, "Sales");
                }
                else if (pt == "deal_item")
                {
                    var dealId = await _db.Set<MyApi.Modules.Deals.Models.DealItem>().Where(di => di.Id == parentId).Select(di => (int?)di.DealId).FirstOrDefaultAsync();
                    if (dealId.HasValue) return ("Deal", dealId.Value, "Deals");
                }
                else if (pt == "service_order_job")
                {
                    var so = await _db.ServiceOrderJobs
                        .Where(j => j.Id == parentId)
                        .Join(_db.ServiceOrders, j => j.ServiceOrderId, s => s.Id, (j, s) => s)
                        .FirstOrDefaultAsync();
                    if (so != null)
                    {
                        if (so.ProjectId.HasValue) return ("Project", so.ProjectId.Value, "ServiceOrders");
                        if (int.TryParse(so.SaleId, out var sid)) return ("Sale", sid, "ServiceOrders");
                        if (so.AutoGeneratedSaleId.HasValue) return ("Sale", so.AutoGeneratedSaleId.Value, "ServiceOrders");
                        if (int.TryParse(so.OfferId, out var oid)) return ("Offer", oid, "ServiceOrders");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to resolve module parent for {ParentType} {ParentId}", parentType, parentId);
            }
            return null;
        }

        private async Task LogModuleActivityAsync(PlannedLineEntry entry, string action, string userId)
        {
            if (_activityLogger == null) return;
            try
            {
                var parent = await ResolveModuleParentAsync(entry.ParentType, entry.ParentId);
                if (parent == null) return;

                string summary = entry.Kind?.ToLowerInvariant() switch
                {
                    "time" => $"{(entry.PlannedMinutes ?? 0)} min" +
                              ((entry.TechnicianCount ?? 1) > 1 ? $" x {entry.TechnicianCount} tech" : ""),
                    "expense" => $"{entry.ExpenseType} {entry.PlannedAmount:0.##} {entry.Currency}".Trim(),
                    "material" => $"{entry.ArticleName ?? ("article #" + entry.ArticleId)} x {entry.Quantity}",
                    _ => entry.Description ?? ""
                };

                string verb = action switch
                {
                    "item_added" => "Planned",
                    "item_updated" => "Updated planned",
                    "item_deleted" => "Removed planned",
                    _ => "Changed planned"
                };

                await _activityLogger.LogAsync(new ActivityLogEntry
                {
                    Module = parent.Value.module,
                    Action = $"planned_{action}",
                    EntityType = "PlannedLineEntry",
                    EntityId = entry.Id.ToString(),
                    Message = $"{verb} {entry.Kind}: {summary}".Trim(),
                    UserId = userId,
                    ParentEntityType = parent.Value.entityType,
                    ParentEntityId = parent.Value.entityId,
                    Details = $"{{\"parentType\":\"{entry.ParentType}\",\"parentId\":{entry.ParentId},\"kind\":\"{entry.Kind}\"}}",
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to log module activity for planned entry {EntryId}", entry.Id);
            }
        }
    }
}
