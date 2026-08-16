using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Deals.DTOs;
using MyApi.Modules.Deals.Models;
using MyApi.Modules.Sales.DTOs;
using MyApi.Modules.Sales.Services;
using MyApi.Modules.Projects.DTOs;
using MyApi.Modules.Projects.Services;
using MyApi.Modules.Offers.DTOs;
using MyApi.Modules.Offers.Services;
using MyApi.Modules.WorkflowEngine.Services;
using MyApi.Modules.Planning.Services;

namespace MyApi.Modules.Deals.Services
{
    public class DealService : IDealService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DealService> _logger;
        private readonly ISaleService _saleService;
        private readonly IProjectService _projectService;
        private readonly IOfferService _offerService;
        private readonly ITaskService _taskService;
        private readonly IWorkflowTriggerService? _workflowTriggerService;
        private readonly MyApi.Modules.Numbering.Services.INumberingService? _numberingService;
        private readonly IPlannedLineEntryService? _plannedEntries;
        private readonly MyApi.Modules.Shared.Services.IActivityLogger? _activityLogger;

        // Stages considered "open" (still in the pipeline)
        private static readonly string[] OpenStages = { "lead", "qualified", "proposal", "negotiation" };

        // Every stage the pipeline accepts. Anything else is rejected server-side so a
        // direct API call can't park a deal in a stage no view (list/kanban/stats) knows.
        private static readonly string[] ValidStages = { "lead", "qualified", "proposal", "negotiation", "won", "lost" };

        /// <summary>Validates and canonicalises a stage value; throws for unknown stages.</summary>
        private static string NormalizeStage(string stage)
        {
            var s = (stage ?? "").Trim().ToLowerInvariant();
            if (!ValidStages.Contains(s))
                throw new InvalidOperationException(
                    $"INVALID_STAGE:{stage}: Unknown deal stage '{stage}'. Allowed: {string.Join(", ", ValidStages)}");
            return s;
        }

        // Discount modes the line-total formula understands. Anything else used to be
        // silently treated as a percentage and stored verbatim.
        private static readonly string[] ValidDiscountTypes = { "percentage", "fixed" };

        /// <summary>Activity types a user may create by hand (system events are written internally).</summary>
        private static readonly string[] UserActivityTypes =
            { "note", "call", "email", "meeting", "task", "updated", "other" };

        /// <summary>
        /// Normalises a bound query-string date into a UTC instant. Model binding yields
        /// Kind=Unspecified for "2026-08-10", which Npgsql rejects against timestamptz.
        /// </summary>
        private static DateTime? ToUtcInstant(DateTime? value)
        {
            if (!value.HasValue) return null;
            var v = value.Value;
            return v.Kind switch
            {
                DateTimeKind.Utc => v,
                DateTimeKind.Local => v.ToUniversalTime(),
                _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            };
        }


        /// <summary>Title is the only human-identifying field on a deal — it must not be blank.</summary>
        private static string ValidateTitle(string? title)
        {
            var t = (title ?? "").Trim();
            if (t.Length == 0)
                throw new InvalidOperationException("INVALID_TITLE: Deal title is required.");
            if (t.Length > 255)
                throw new InvalidOperationException("INVALID_TITLE: Deal title must be 255 characters or fewer.");
            return t;
        }

        /// <summary>Currency maps to a varchar(3) column — validate instead of letting the DB throw a 500.</summary>
        private static string ValidateCurrency(string? currency)
        {
            var c = (currency ?? "").Trim().ToUpperInvariant();
            if (c.Length == 0) return "TND";
            if (c.Length != 3 || !c.All(char.IsLetter))
                throw new InvalidOperationException(
                    $"INVALID_CURRENCY:{currency}: Currency must be a 3-letter code (e.g. TND, EUR, USD).");
            return c;
        }

        /// <summary>
        /// Postgres `timestamptz` columns reject DateTimes whose Kind is Unspecified, which is
        /// exactly what the JSON binder produces for date-only ("2026-09-30") or offset-less
        /// payloads — that used to surface as a 500 on create/update. Treat such values as UTC
        /// and convert Local values properly.
        /// </summary>
        private static DateTime? ToUtc(DateTime? value)
        {
            if (!value.HasValue) return null;
            var v = value.Value;
            return v.Kind switch
            {
                DateTimeKind.Utc => v,
                DateTimeKind.Local => v.ToUniversalTime(),
                _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            };
        }

        /// <summary>Rejects impossible line items (negative amounts, unknown discount mode, over-100% discounts).</summary>
        private static void ValidateItem(CreateDealItemDto dto, int? index = null)
        {
            var where = index.HasValue ? $" (item #{index + 1})" : "";

            if (string.IsNullOrWhiteSpace(dto.ItemName))
                throw new InvalidOperationException($"INVALID_ITEM: Item name is required{where}.");

            if (dto.Quantity < 0)
                throw new InvalidOperationException($"INVALID_ITEM: Quantity cannot be negative{where}.");

            if (dto.UnitPrice < 0)
                throw new InvalidOperationException($"INVALID_ITEM: Unit price cannot be negative{where}.");

            var discountType = string.IsNullOrWhiteSpace(dto.DiscountType) ? "percentage" : dto.DiscountType.Trim().ToLowerInvariant();
            if (!ValidDiscountTypes.Contains(discountType))
                throw new InvalidOperationException(
                    $"INVALID_ITEM: Unknown discount type '{dto.DiscountType}'{where}. Allowed: percentage, fixed.");

            if (dto.Discount < 0)
                throw new InvalidOperationException($"INVALID_ITEM: Discount cannot be negative{where}.");

            if (discountType == "percentage" && dto.Discount > 100)
                throw new InvalidOperationException($"INVALID_ITEM: Percentage discount cannot exceed 100{where}.");

            if (discountType == "fixed" && dto.Discount > dto.Quantity * dto.UnitPrice)
                throw new InvalidOperationException(
                    $"INVALID_ITEM: Fixed discount cannot exceed the line gross total{where}.");

            // Canonicalise so the stored value always matches what the formula used.
            dto.DiscountType = discountType;
        }

        /// <summary>The contact is a hard dependency of a deal — reject dangling references.</summary>
        private async Task EnsureContactExistsAsync(int contactId)
        {
            if (contactId <= 0)
                throw new InvalidOperationException("INVALID_CONTACT: A valid contact is required.");

            var exists = await _context.Contacts.AsNoTracking().AnyAsync(c => c.Id == contactId);
            if (!exists)
                throw new InvalidOperationException($"INVALID_CONTACT:{contactId}: Contact {contactId} does not exist.");
        }

        /// <summary>A deal may live inside a project container — reject links to projects that don't exist.</summary>
        private async Task EnsureProjectExistsAsync(int projectId)
        {
            if (projectId <= 0)
                throw new InvalidOperationException("INVALID_PROJECT: A valid project is required.");

            var exists = await _context.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId);
            if (!exists)
                throw new InvalidOperationException($"INVALID_PROJECT:{projectId}: Project {projectId} does not exist.");
        }

        /// <summary>
        /// EstimatedValue feeds the pipeline forecast, the project budget and the synthesized
        /// conversion line — a negative number would poison all three (and render as a negative
        /// offer total), so reject it at the edge instead of storing it.
        /// </summary>
        private static decimal ValidateEstimatedValue(decimal value)
        {
            if (value < 0)
                throw new InvalidOperationException("INVALID_VALUE: Estimated value cannot be negative.");
            return Math.Round(value, 2);
        }



        public DealService(
            ApplicationDbContext context,
            ILogger<DealService> logger,
            ISaleService saleService,
            IProjectService projectService,
            IOfferService offerService,
            ITaskService taskService,
            IWorkflowTriggerService? workflowTriggerService = null,
            MyApi.Modules.Numbering.Services.INumberingService? numberingService = null,
            IPlannedLineEntryService? plannedEntries = null,
            MyApi.Modules.Shared.Services.IActivityLogger? activityLogger = null)
        {
            _context = context;
            _logger = logger;
            _saleService = saleService;
            _projectService = projectService;
            _offerService = offerService;
            _taskService = taskService;
            _workflowTriggerService = workflowTriggerService;
            _numberingService = numberingService;
            _plannedEntries = plannedEntries;
            _activityLogger = activityLogger;
        }

        private Task LogActivityAsync(string action, string entityType, string entityId, int dealId,
            string message, string userId, string? userName = null, string? details = null,
            string? oldValue = null, string? newValue = null)
        {
            if (_activityLogger == null) return Task.CompletedTask;
            return _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
            {
                Module = "Deals",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                ParentEntityType = "Deal",
                ParentEntityId = dealId,
                UserId = userId,
                UserName = userName,
                Message = message,
                Details = details,
                OldValue = oldValue,
                NewValue = newValue,
            });
        }

        // ── Queries ──

        public async Task<PaginatedDealResponse> GetDealsAsync(
            string? stage = null, string? category = null, string? source = null,
            string? contactId = null, string? projectId = null, string? search = null,
            int page = 1, int limit = 20, string sortBy = "updated_at", string sortOrder = "desc")
        {
            // Guard against bad input: page 0/negative → negative Skip (EF throws),
            // limit 0 → divide-by-zero when computing TotalPages.
            if (page < 1) page = 1;
            if (limit < 1) limit = 20;
            if (limit > 200) limit = 200;

            var query = _context.Deals.AsNoTracking().Where(d => !d.IsDeleted).AsQueryable();

            // Filters arrive from query strings, so they carry whatever casing/spacing the
            // caller used ("Won", " won "). Stored stages are canonical lower-case, so a
            // raw equality test silently returned an empty pipeline. Normalise first.
            if (!string.IsNullOrWhiteSpace(stage))
            {
                var s = NormalizeStage(stage);
                query = query.Where(d => d.Stage == s);
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                var c = category.Trim();
                query = query.Where(d => d.Category == c);
            }
            if (!string.IsNullOrWhiteSpace(source))
            {
                var src = source.Trim();
                query = query.Where(d => d.Source == src);
            }
            if (!string.IsNullOrEmpty(contactId) && int.TryParse(contactId, out int cId))
                query = query.Where(d => d.ContactId == cId);
            if (!string.IsNullOrEmpty(projectId) && int.TryParse(projectId, out int pId))
                query = query.Where(d => d.ProjectId == pId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Users search by the number printed on the card ("DEAL-00074") and by
                // the customer name just as often as by title, so cover all of them.
                var s = search.Trim().ToLower();
                query = query.Where(d =>
                    (d.Title != null && d.Title.ToLower().Contains(s)) ||
                    (d.Description != null && d.Description.ToLower().Contains(s)) ||
                    (d.DealNumber != null && d.DealNumber.ToLower().Contains(s)) ||
                    (d.NextAction != null && d.NextAction.ToLower().Contains(s)));
            }


            var total = await query.CountAsync();

            bool asc = sortOrder.ToLower() == "asc";
            query = sortBy.ToLower() switch
            {
                "created_at" => asc ? query.OrderBy(d => d.CreatedDate) : query.OrderByDescending(d => d.CreatedDate),
                "title" => asc ? query.OrderBy(d => d.Title) : query.OrderByDescending(d => d.Title),
                "value" => asc ? query.OrderBy(d => d.EstimatedValue) : query.OrderByDescending(d => d.EstimatedValue),
                "stage" => asc ? query.OrderBy(d => d.Stage) : query.OrderByDescending(d => d.Stage),
                _ => asc ? query.OrderBy(d => d.ModifiedDate ?? d.CreatedDate) : query.OrderByDescending(d => d.ModifiedDate ?? d.CreatedDate)
            };

            var deals = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Include(d => d.Items)
                .ToListAsync();

            var contacts = await LoadContactsAsync(deals.Select(d => d.ContactId));

            return new PaginatedDealResponse
            {
                Deals = deals.Select(d => MapToDto(d, contacts)).ToList(),
                Pagination = new PaginationInfoDto
                {
                    Page = page,
                    Limit = limit,
                    Total = total,
                    TotalPages = (int)Math.Ceiling(total / (double)limit)
                }
            };
        }

        public async Task<DealDto?> GetDealByIdAsync(int id)
        {
            var deal = await _context.Deals.AsNoTracking()
                .Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
            if (deal == null) return null;
            var contacts = await LoadContactsAsync(new[] { deal.ContactId });
            return MapToDto(deal, contacts);
        }

        // ── Mutations ──

        public async Task<DealDto> CreateDealAsync(CreateDealDto dto, string userId, string? userName = null)
        {
            // Reject unknown stages before touching the DB or burning a document number.
            var stage = string.IsNullOrWhiteSpace(dto.Stage) ? "lead" : NormalizeStage(dto.Stage);

            // Validate the rest of the payload up-front, for the same reason: a rejected
            // deal must not consume a document number or leave a partial row behind.
            var title = ValidateTitle(dto.Title);
            var currency = ValidateCurrency(dto.Currency);
            await EnsureContactExistsAsync(dto.ContactId);
            if (dto.ProjectId.HasValue) await EnsureProjectExistsAsync(dto.ProjectId.Value);
            var estimatedValue = ValidateEstimatedValue(dto.EstimatedValue);
            if (dto.Items != null)
                for (var i = 0; i < dto.Items.Count; i++) ValidateItem(dto.Items[i], i);


            // Resolve the document number from the configurable numbering service
            // (admin can customise the Deal template in Settings → Numbering). Falls
            // back to a GUID, and ultimately to the legacy Id-derived number below if
            // generation yields nothing — so a deal can never persist without a number.
            string dealNumber;

            try
            {
                dealNumber = _numberingService != null
                    ? await _numberingService.GetNextAsync("Deal")
                    : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Deal");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Numbering service failed for Deal, using GUID fallback");
                dealNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Deal");
            }

            // Persist the deal (and its number) atomically. Wrapping both saves in one
            // transaction guarantees a deal can never persist without a number.
            // Uses the execution strategy (retry-on-failure is enabled on the connection).
            Deal deal = null!;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Retry-safe: drop any entities a previous (rolled-back) attempt added.
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync();

                deal = new Deal
                {
                    DealNumber = string.IsNullOrWhiteSpace(dealNumber) ? null : dealNumber,
                    Title = title,
                    Description = dto.Description,
                    ContactId = dto.ContactId,
                    ProjectId = dto.ProjectId,
                    Stage = stage,
                    // Probability is a percentage — clamp so charts/forecasts can trust it.
                    // Terminal stages are certain by definition, so a deal created straight
                    // into won/lost is pinned the same way UpdateDealAsync pins it.
                    Probability = stage == "won" ? 100
                        : stage == "lost" ? 0
                        : Math.Clamp(dto.Probability, 0, 100),

                    // A deal created straight into a terminal stage still needs a close date.
                    ActualCloseDate = (stage == "won" || stage == "lost") ? DateTime.UtcNow : null,

                    EstimatedValue = estimatedValue,
                    Currency = currency,
                    ExpectedCloseDate = ToUtc(dto.ExpectedCloseDate),
                    NextActionDate = ToUtc(dto.NextActionDate),
                    NextAction = dto.NextAction,
                    Category = dto.Category,
                    Source = dto.Source,
                    Notes = dto.Notes,
                    Tags = dto.Tags,
                    AssignedTo = dto.AssignedTo,
                    AssignedToName = dto.AssignedToName,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId,
                    CreatedByName = userName,
                    LastActivity = DateTime.UtcNow,
                };

                if (dto.Items != null && dto.Items.Any())
                {
                    deal.Items = dto.Items.Select((it, idx) => BuildItem(it, idx)).ToList();
                }

                _context.Deals.Add(deal);
                await _context.SaveChangesAsync();          // assign identity Id

                deal.DealNumber ??= $"DEAL-{deal.Id:D5}";
                deal.EstimatedValue = RecomputeValue(deal);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
            });

            await AddActivityInternalAsync(deal.Id, "created", "Deal created", null, userId, userName);

            var contacts = await LoadContactsAsync(new[] { deal.ContactId });
            return MapToDto(deal, contacts);
        }

        public async Task<DealDto?> UpdateDealAsync(int id, UpdateDealDto dto, string userId, string? userName = null)
        {
            var deal = await _context.Deals.Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
            if (deal == null) return null;

            var oldStage = deal.Stage;
            
            // Validate before mutating anything so a bad stage can't partially apply.
            var newStage = dto.Stage != null ? NormalizeStage(dto.Stage) : null;
            var newTitle = dto.Title != null ? ValidateTitle(dto.Title) : null;
            var newCurrency = dto.Currency != null ? ValidateCurrency(dto.Currency) : null;
            if (dto.ContactId.HasValue) await EnsureContactExistsAsync(dto.ContactId.Value);
            if (dto.ProjectId.HasValue) await EnsureProjectExistsAsync(dto.ProjectId.Value);
            if (dto.EstimatedValue.HasValue) ValidateEstimatedValue(dto.EstimatedValue.Value);
            if (dto.Items != null)
                for (var i = 0; i < dto.Items.Count; i++) ValidateItem(dto.Items[i], i);


            if (newTitle != null) deal.Title = newTitle;
            if (dto.Description != null) deal.Description = dto.Description;
            if (dto.ContactId.HasValue) deal.ContactId = dto.ContactId.Value;
            if (dto.ProjectId.HasValue) deal.ProjectId = dto.ProjectId.Value;
            if (newStage != null) deal.Stage = newStage;
            if (dto.Probability.HasValue) deal.Probability = Math.Clamp(dto.Probability.Value, 0, 100);
            // Line items are the source of truth for the value whenever the deal has any.
            // A header-only PATCH (kanban drag, inline stage change) used to overwrite the
            // item-derived total with a stale number, leaving the deal's value out of sync
            // with the lines it converts into. Only honour the header value for itemless
            // deals; the items branch below owns the rest.
            if (dto.EstimatedValue.HasValue && dto.Items == null && (deal.Items == null || !deal.Items.Any()))
                deal.EstimatedValue = ValidateEstimatedValue(dto.EstimatedValue.Value);

            if (newCurrency != null) deal.Currency = newCurrency;
            if (dto.ExpectedCloseDate.HasValue) deal.ExpectedCloseDate = ToUtc(dto.ExpectedCloseDate);
            if (dto.ActualCloseDate.HasValue) deal.ActualCloseDate = ToUtc(dto.ActualCloseDate);
            if (dto.NextActionDate.HasValue) deal.NextActionDate = ToUtc(dto.NextActionDate);
            if (dto.NextAction != null) deal.NextAction = dto.NextAction;
            if (dto.LostReason != null) deal.LostReason = dto.LostReason;
            if (dto.Category != null) deal.Category = dto.Category;
            if (dto.Source != null) deal.Source = dto.Source;
            if (dto.Notes != null) deal.Notes = dto.Notes;
            if (dto.Tags != null) deal.Tags = dto.Tags;
            if (dto.AssignedTo != null) deal.AssignedTo = dto.AssignedTo;
            if (dto.AssignedToName != null) deal.AssignedToName = dto.AssignedToName;

            if (newStage != null && newStage != oldStage)
            {
                var enteringTerminal = newStage == "won" || newStage == "lost";
                var leavingTerminal = oldStage == "won" || oldStage == "lost";

                // Auto-stamp close date when entering a terminal stage
                if (enteringTerminal && deal.ActualCloseDate == null)
                    deal.ActualCloseDate = DateTime.UtcNow;

                // Reopening a closed deal must clear the closure metadata, otherwise the
                // deal shows up in the pipeline still carrying a close date / lost reason.
                if (leavingTerminal && !enteringTerminal)
                {
                    if (!dto.ActualCloseDate.HasValue) deal.ActualCloseDate = null;
                    if (dto.LostReason == null) deal.LostReason = null;
                }

                // A won deal never keeps a lost reason (and vice-versa).
                if (newStage == "won" && dto.LostReason == null) deal.LostReason = null;

                // Terminal stages pin the probability unconditionally: a won deal is 100%
                // certain and a lost deal 0%, whatever the caller sent. Previously a client
                // that changed the stage AND the probability in the same request could store
                // e.g. "won @ 20%", which silently corrupted weighted-pipeline and win-rate
                // analytics.
                if (newStage == "won") deal.Probability = 100;
                else if (newStage == "lost") deal.Probability = 0;
                // Re-opening a closed deal restored no forecast: the deal stayed pinned at
                // 0% (from "lost") or 100% (from "won"), which skewed the weighted pipeline
                // until someone retyped a value. When the caller does not send an explicit
                // probability, fall back to the stage default.
                else if (leavingTerminal && !dto.Probability.HasValue)
                {
                    deal.Probability = newStage switch
                    {
                        "lead" => 20,
                        "qualified" => 40,
                        "proposal" => 60,
                        "negotiation" => 80,
                        _ => deal.Probability,
                    };
                }

            }



            // Replace line items atomically when the caller manages them. Doing it here
            // (rather than via a chatty delete-then-add loop from the client) keeps the
            // whole edit in one transaction and recomputes the value consistently.
            if (dto.Items != null)
            {
                if (deal.Items != null && deal.Items.Any())
                    _context.DealItems.RemoveRange(deal.Items);
                deal.Items = dto.Items.Select((it, idx) => BuildItem(it, idx)).ToList();
                // An explicit item set drives the value; an empty set means 0 (no stale total).
                deal.EstimatedValue = deal.Items.Any()
                    ? Math.Round(deal.Items.Sum(i => i.LineTotal), 2)
                    : (dto.EstimatedValue ?? 0m);
            }

            deal.ModifiedDate = DateTime.UtcNow;
            deal.ModifiedBy = userId;
            deal.LastActivity = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (newStage != null && newStage != oldStage)
                await AddActivityInternalAsync(deal.Id, "status_change", $"Stage changed to {newStage}", oldStage, userId, userName, newStage);

            // Fire workflow automation on stage change (deals trigger on "Stage").
            if (newStage != null && newStage != oldStage && _workflowTriggerService != null)
            {
                try
                {
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "deal",
                        deal.Id,
                        oldStage ?? "",
                        newStage,

                        userId,
                        new { dealId = deal.Id, dealNumber = deal.DealNumber, title = deal.Title, contactId = deal.ContactId }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to trigger workflow for deal {DealId} stage change", deal.Id);
                }
            }

            var contacts = await LoadContactsAsync(new[] { deal.ContactId });
            return MapToDto(deal, contacts);
        }

        public async Task<bool> DeleteDealAsync(int id, string userId = "system")
        {
            var deal = await _context.Deals.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
            if (deal == null) return false;
            deal.IsDeleted = true;
            deal.DeletedAt = DateTime.UtcNow;
            deal.DeletedBy = userId;
            // Keep the audit trail coherent: a soft delete is still a mutation, so the
            // modified stamps must move with it (lists sort on ModifiedDate).
            deal.ModifiedDate = DateTime.UtcNow;
            deal.ModifiedBy = userId;
            deal.LastActivity = DateTime.UtcNow;
            await _context.SaveChangesAsync();


            await LogActivityAsync("delete", "Deal", id.ToString(), id,
                $"Deal deleted: {deal.Title}", userId);
            return true;
        }

        // ── Stats ──

        public async Task<DealStatsDto> GetDealStatsAsync(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            // CreatedDate is a timestamptz column: Npgsql refuses DateTime values with
            // Kind=Unspecified (what model binding produces for "2026-08-10"), which used
            // to make ?date_from=/?date_to= return a 500 instead of filtered stats.
            var from = ToUtcInstant(dateFrom);
            var to = ToUtcInstant(dateTo);

            var query = _context.Deals.AsNoTracking().Where(d => !d.IsDeleted);
            if (from.HasValue) query = query.Where(d => d.CreatedDate >= from.Value);
            if (to.HasValue) query = query.Where(d => d.CreatedDate <= to.Value);


            var deals = await query.Select(d => new { d.Stage, d.EstimatedValue }).ToListAsync();

            long total = deals.Count;
            var open = deals.Where(d => OpenStages.Contains(d.Stage)).ToList();
            var won = deals.Where(d => d.Stage == "won").ToList();
            var lost = deals.Where(d => d.Stage == "lost").ToList();
            long decided = won.Count + lost.Count;

            return new DealStatsDto
            {
                TotalDeals = total,
                OpenDeals = open.Count,
                WonDeals = won.Count,
                LostDeals = lost.Count,
                TotalValue = deals.Sum(d => d.EstimatedValue),
                OpenValue = open.Sum(d => d.EstimatedValue),
                WonValue = won.Sum(d => d.EstimatedValue),
                // Rounded like every other monetary figure the UI renders.
                AverageValue = total > 0 ? Math.Round(deals.Sum(d => d.EstimatedValue) / total, 2) : 0,
                WinRate = decided > 0 ? Math.Round(won.Count * 100m / decided, 1) : 0
            };
        }

        // ── Conversion ──

        public async Task<ConvertDealResultDto> ConvertDealAsync(int id, ConvertDealDto dto, string userId, string? userName = null)
        {
            if (!dto.ConvertToSale && !dto.ConvertToProject && !dto.ConvertToOffer)
                throw new InvalidOperationException("Select at least one target (sale, project or offer) to convert into.");

            var result = new ConvertDealResultDto();
            // Captured for the (non-fatal) back-link pass that runs after the commit.
            int? saleBackId = null, offerBackId = null, projectBackId = null;

            // The whole conversion is one unit of work: a Sale/Project/Offer must never
            // be committed without the deal's matching ConvertedTo* flag, otherwise a
            // partial failure would orphan records AND defeat the idempotency guards
            // (allowing duplicate conversions). EnableRetryOnFailure forces us to open
            // the transaction inside an execution strategy.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // On a strategy retry the previous (rolled-back) attempt may have left
                // entities tracked as Added — clear them so we never double-insert.
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync();

                // Reset accumulators so a strategy retry starts from a clean slate.
                result.SaleId = null; result.ProjectId = null; result.OfferId = null;
                saleBackId = offerBackId = projectBackId = null;

                // Serialise concurrent conversions of the same deal: take a row-level
                // lock BEFORE reading the ConvertedTo* guards, so a double-submit can't
                // have both requests see "not converted yet" and each spawn a Sale/
                // Offer/Project. The second request blocks here, then reads the flags
                // the first one committed and fails with ALREADY_CONVERTED_*.
                await _context.Database.ExecuteSqlRawAsync(
                    @"SELECT 1 FROM ""Deals"" WHERE ""Id"" = {0} FOR UPDATE", id);

                var deal = await _context.Deals.Include(d => d.Items)
                    .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
                if (deal == null) throw new KeyNotFoundException($"Deal {id} not found");

                // A lost deal must be reopened before it can spawn commercial documents:
                // converting it silently flipped the stage to won and resurrected a deal
                // the team had already written off (and its lost reason with it).
                if (deal.Stage == "lost")
                    throw new InvalidOperationException($"CANNOT_CONVERT_LOST: Deal {id} is marked lost — reopen it before converting.");

                // ── Deal → Project (FIRST, so a Sale/Offer in the same call links to it) ──
                if (dto.ConvertToProject)
                {

                    if (!string.IsNullOrEmpty(deal.ConvertedToProjectId))
                        throw new InvalidOperationException($"ALREADY_CONVERTED_PROJECT:{deal.ConvertedToProjectId}: Deal {id} has already been converted to Project {deal.ConvertedToProjectId}");

                    var projectDto = new CreateProjectRequestDto
                    {
                        Name = deal.Title,
                        Description = deal.Description,
                        ContactId = deal.ContactId,
                        Status = "active",
                        ProjectKind = "client",
                        Priority = "medium",
                        // Seed the project budget from the deal's value (delivery vs forecast).
                        Budget = deal.EstimatedValue,
                        CreateDefaultColumns = true,
                    };
                    var project = await _projectService.CreateProjectAsync(projectDto, userId);
                    result.ProjectId = project.Id;
                    projectBackId = project.Id;
                    deal.ConvertedToProjectId = project.Id.ToString();
                    // The deal now belongs to the project it spawned.
                    deal.ProjectId = project.Id;

                    // Incremental conversions stay consistent: if this deal was already
                    // turned into a Sale/Offer in an earlier call, retro-link those
                    // records to the project just created (only when they aren't already
                    // linked elsewhere). The Sale/Offer created later in THIS call use
                    // effectiveProjectId, so they're handled separately below.
                    if (int.TryParse(deal.ConvertedToSaleId, out var priorSaleId))
                    {
                        var priorSale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == priorSaleId);
                        if (priorSale != null && priorSale.ProjectId == null) priorSale.ProjectId = project.Id;
                    }
                    if (int.TryParse(deal.ConvertedToOfferId, out var priorOfferId))
                    {
                        var priorOffer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == priorOfferId);
                        if (priorOffer != null && priorOffer.ProjectId == null) priorOffer.ProjectId = project.Id;
                    }

                    // Seed a starter task per deal line item so the new project opens with
                    // an actionable to-do list derived from exactly what was sold. The task
                    // service stamps TenantId from the parent project (tenant-safe), and
                    // these inserts join the same transaction.
                    foreach (var it in deal.Items ?? new List<DealItem>())
                    {
                        await _taskService.CreateProjectTaskAsync(new CreateProjectTaskRequestDto
                        {
                            Title = string.IsNullOrWhiteSpace(it.ItemName) ? $"Deliver item #{it.Id}" : it.ItemName,
                            Description = $"Qty {it.Quantity:0.##} · {it.UnitPrice:0.00} {deal.Currency}"
                                + (string.IsNullOrEmpty(it.InstallationName) ? "" : $" · {it.InstallationName}"),
                            TaskType = it.Type == "service" ? "visit" : "follow-up",
                            Status = "open",
                            RelatedEntityType = "project",
                            RelatedEntityId = project.Id,
                        }, userId);
                    }

                    // An itemless deal produced a project with a budget but an empty board,
                    // so the work had no visible entry point. Seed one task from the deal
                    // header, mirroring the synthetic line the Sale/Offer paths create.
                    if ((deal.Items == null || !deal.Items.Any()))
                    {
                        await _taskService.CreateProjectTaskAsync(new CreateProjectTaskRequestDto
                        {
                            Title = string.IsNullOrWhiteSpace(deal.Title) ? $"Deliver deal #{deal.Id}" : deal.Title,
                            Description = $"Converted from deal {deal.DealNumber ?? $"#{deal.Id}"}"
                                + (deal.EstimatedValue > 0 ? $" · {deal.EstimatedValue:0.00} {deal.Currency}" : ""),
                            TaskType = "follow-up",
                            Status = "open",
                            RelatedEntityType = "project",
                            RelatedEntityId = project.Id,
                        }, userId);
                    }
                }


                // New project (if just created) wins; otherwise keep any existing link.
                var effectiveProjectId = deal.ProjectId;

                // An itemless deal still carries a forecast in EstimatedValue. Sale and
                // Offer totals are derived exclusively from line items, so converting
                // without seeding a line silently dropped the value (offer showed only
                // the fiscal stamp). Synthesize one line from the deal header so the
                // Sale/Offer/Project paths all agree on the same number.
                var sourceItems = (deal.Items ?? new List<DealItem>())
                    .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToList();
                var needsSyntheticLine = sourceItems.Count == 0 && deal.EstimatedValue > 0;
                var syntheticLineName = string.IsNullOrWhiteSpace(deal.Title)
                    ? $"Deal #{deal.Id}"
                    : deal.Title;

                // ── Deal → Sale ──
                if (dto.ConvertToSale)
                {
                    if (!string.IsNullOrEmpty(deal.ConvertedToSaleId))
                        throw new InvalidOperationException($"ALREADY_CONVERTED_SALE:{deal.ConvertedToSaleId}: Deal {id} has already been converted to Sale {deal.ConvertedToSaleId}");

                    var saleItemDtos = sourceItems.Select(it => new CreateSaleItemDto
                    {
                        Type = it.Type,
                        ArticleId = it.ArticleId,
                        ItemName = it.ItemName,
                        ItemCode = it.ItemCode,
                        Description = it.Description,
                        Quantity = it.Quantity,
                        UnitPrice = it.UnitPrice,
                        Discount = it.Discount,
                        DiscountType = it.DiscountType,
                        InstallationId = it.InstallationId,
                        InstallationName = it.InstallationName,
                    }).ToList();

                    if (needsSyntheticLine)
                    {
                        saleItemDtos.Add(new CreateSaleItemDto
                        {
                            Type = "article",
                            ItemName = syntheticLineName,
                            Description = deal.Description,
                            Quantity = 1,
                            UnitPrice = deal.EstimatedValue,
                            Discount = 0,
                            DiscountType = "percentage",
                            DisplayOrder = 0,
                        });
                    }

                    var saleDto = new CreateSaleDto
                    {
                        Title = deal.Title,
                        Description = deal.Description,
                        ContactId = deal.ContactId,
                        ProjectId = effectiveProjectId,
                        Status = "created",
                        Currency = deal.Currency,
                        Category = deal.Category,
                        Source = deal.Source,
                        Items = saleItemDtos
                    };

                    var sale = await _saleService.CreateSaleAsync(saleDto, userId);
                    result.SaleId = sale.Id;
                    saleBackId = sale.Id;
                    deal.ConvertedToSaleId = sale.Id.ToString();

                    // Phase A (A5): carry planned time/expenses/materials from
                    // deal items → new sale items. Pairing here is index-based
                    // because deal→sale creation is a single, fresh insert with
                    // no concurrent writer, so the order matches the input DTO.
                    if (_plannedEntries != null && sale.Items != null)
                    {
                        var dealItems = (deal.Items ?? new List<DealItem>())
                            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToList();
                        var saleItemsOrdered = sale.Items
                            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToList();
                        for (int i = 0; i < dealItems.Count && i < saleItemsOrdered.Count; i++)
                        {
                            await _plannedEntries.CopyAsync("deal_item", dealItems[i].Id, "sale_item", saleItemsOrdered[i].Id, userId);
                        }
                    }
                }

                // ── Deal → Offer ──
                if (dto.ConvertToOffer)
                {
                    if (!string.IsNullOrEmpty(deal.ConvertedToOfferId))
                        throw new InvalidOperationException($"ALREADY_CONVERTED_OFFER:{deal.ConvertedToOfferId}: Deal {id} has already been converted to Offer {deal.ConvertedToOfferId}");

                    var offerItemDtos = sourceItems.Select(it => new CreateOfferItemDto
                    {
                        Type = it.Type,
                        ArticleId = it.ArticleId,
                        ItemName = it.ItemName,
                        ItemCode = it.ItemCode,
                        Description = it.Description,
                        Quantity = it.Quantity,
                        UnitPrice = it.UnitPrice,
                        Discount = it.Discount,
                        DiscountType = it.DiscountType,
                        InstallationId = it.InstallationId,
                        InstallationName = it.InstallationName,
                    }).ToList();

                    if (needsSyntheticLine)
                    {
                        offerItemDtos.Add(new CreateOfferItemDto
                        {
                            Type = "article",
                            ItemName = syntheticLineName,
                            Description = deal.Description,
                            Quantity = 1,
                            UnitPrice = deal.EstimatedValue,
                            Discount = 0,
                            DiscountType = "percentage",
                            DisplayOrder = 0,
                        });
                    }

                    var offerDto = new CreateOfferDto
                    {
                        Title = deal.Title,
                        Description = deal.Description,
                        ContactId = deal.ContactId,
                        ProjectId = effectiveProjectId,
                        Status = "draft",
                        Currency = deal.Currency,
                        Category = deal.Category,
                        Source = deal.Source,
                        Notes = deal.Notes,
                        Items = offerItemDtos
                    };
                    var offer = await _offerService.CreateOfferAsync(offerDto, userId);
                    result.OfferId = offer.Id;
                    offerBackId = offer.Id;
                    deal.ConvertedToOfferId = offer.Id.ToString();

                    // OfferService.CreateOfferAsync persists the header with TotalAmount = 0
                    // and never recomputes it from the items it just inserted (the UI normally
                    // PATCHes totals afterwards). A converted offer has no such follow-up call,
                    // so stamp the totals here — otherwise the offer renders as the fiscal
                    // stamp alone (the "1 TND" bug).
                    var offerEntity = await _context.Offers.FirstOrDefaultAsync(o => o.Id == offer.Id);
                    if (offerEntity != null)
                    {
                        var subtotal = offerItemDtos.Sum(i =>
                        {
                            var gross = i.Quantity * i.UnitPrice;
                            var off = string.Equals(i.DiscountType, "fixed", StringComparison.OrdinalIgnoreCase)
                                ? i.Discount
                                : gross * i.Discount / 100m;
                            return Math.Max(0, gross - off);
                        });
                        subtotal = Math.Round(subtotal, 2);
                        offerEntity.TotalAmount = subtotal;
                        offerEntity.TaxAmount = 0;
                        offerEntity.GrandTotal = Math.Round(subtotal + (offerEntity.FiscalStamp ?? 0), 2);
                    }


                    // Phase A (A5): copy planned entries from deal items → new offer items.
                    if (_plannedEntries != null && offer.Items != null)
                    {
                        var dealItems = (deal.Items ?? new List<DealItem>())
                            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToList();
                        var offerItemsOrdered = offer.Items
                            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToList();
                        for (int i = 0; i < dealItems.Count && i < offerItemsOrdered.Count; i++)
                        {
                            await _plannedEntries.CopyAsync("deal_item", dealItems[i].Id, "offer_item", offerItemsOrdered[i].Id, userId);
                        }
                    }
                }

                deal.ConvertedAt = DateTime.UtcNow;
                // Phase A (A4): a deal is "won" as soon as it's converted to ANY
                // downstream artifact — sale, project, OR offer. Leaving an
                // offer-only conversion open re-listed the same deal in the
                // pipeline and let it be reconverted.
                if (dto.ConvertToSale || dto.ConvertToProject || dto.ConvertToOffer)
                {
                    deal.Stage = "won";
                    deal.ActualCloseDate ??= DateTime.UtcNow;
                    // A won deal is 100% certain — leaving the old forecast probability
                    // skews weighted-pipeline and win-rate analytics.
                    deal.Probability = 100;
                    deal.LostReason = null;
                }

                deal.ModifiedDate = DateTime.UtcNow;
                deal.ModifiedBy = userId;
                deal.LastActivity = DateTime.UtcNow;

                var targets = new List<string>();
                if (result.SaleId.HasValue) targets.Add($"Sale #{result.SaleId}");
                if (result.ProjectId.HasValue) targets.Add($"Project #{result.ProjectId}");
                if (result.OfferId.HasValue) targets.Add($"Offer #{result.OfferId}");
                _context.DealActivities.Add(new DealActivity
                {
                    DealId = deal.Id,
                    Type = "converted",
                    Description = $"Converted to {string.Join(", ", targets)}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    CreatedByName = userName,
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            });

            // ── Forward back-links (best-effort, outside the transaction) ──────────
            // These columns trace a Sale/Offer/Project back to the deal it came from.
            // They're supplementary metadata and may predate the deals migration on a
            // given DB, so a failure here must NOT roll back a successful conversion.
            await TrySetBackLinkAsync("Sales", "DealId", id, saleBackId);
            await TrySetBackLinkAsync("Offers", "DealId", id, offerBackId);
            await TrySetBackLinkAsync("Projects", "ConvertedFromDealId", id, projectBackId);

            // NOTE: the conversion timeline entry is written inside the transaction above
            // (DealActivities "converted"). Logging it a second time here duplicated every
            // conversion in the Activity tab, so this path only handles the back-links.


            return result;
        }

        /// <summary>Stamps a forward back-link column on a spawned record. Non-fatal: logs and
        /// continues if the column doesn't exist yet (table/column names are trusted literals).</summary>
        private async Task TrySetBackLinkAsync(string table, string column, int dealId, int? targetId)
        {
            if (targetId == null) return;
            try
            {
                switch (table, column)
                {
                    case ("Sales", "DealId"):
                        await _context.Database.ExecuteSqlRawAsync(
                            @"UPDATE ""Sales"" SET ""DealId"" = {0} WHERE ""Id"" = {1}",
                            dealId, targetId.Value);
                        break;
                    case ("Offers", "DealId"):
                        await _context.Database.ExecuteSqlRawAsync(
                            @"UPDATE ""Offers"" SET ""DealId"" = {0} WHERE ""Id"" = {1}",
                            dealId, targetId.Value);
                        break;
                    case ("Projects", "ConvertedFromDealId"):
                        await _context.Database.ExecuteSqlRawAsync(
                            @"UPDATE ""Projects"" SET ""ConvertedFromDealId"" = {0} WHERE ""Id"" = {1}",
                            dealId, targetId.Value);
                        break;
                    default:
                        _logger.LogWarning(
                            "Refusing back-link update for unknown table/column {Table}.{Column}",
                            table, column);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not set {Table}.{Column} back-link for deal {DealId} (run the deals migration to add it)",
                    table, column, dealId);
            }
        }

        // ── Items ──

        public async Task<DealItemDto?> AddDealItemAsync(int dealId, CreateDealItemDto dto, string userId = "system", string? userName = null)
        {
            var deal = await _context.Deals.Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == dealId && !d.IsDeleted);
            if (deal == null) return null;
            ValidateItem(dto);
            var order = (deal.Items?.Count ?? 0);
            var item = BuildItem(dto, order);
            item.DealId = dealId;
            _context.DealItems.Add(item);
            await _context.SaveChangesAsync();
            deal.EstimatedValue = RecomputeValue(await ReloadWithItems(dealId));
            deal.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await LogActivityAsync("item_added", "DealItem", item.Id.ToString(), dealId,
                $"Item added: {item.ItemName} (qty {item.Quantity})", userId, userName);
            return MapItem(item);
        }

        public async Task<DealItemDto?> UpdateDealItemAsync(int dealId, int itemId, CreateDealItemDto dto, string userId = "system", string? userName = null)
        {
            // Guard the parent: items of a soft-deleted deal must not be mutable.
            var parentAlive = await _context.Deals.AsNoTracking().AnyAsync(d => d.Id == dealId && !d.IsDeleted);
            if (!parentAlive) return null;
            var item = await _context.DealItems.FirstOrDefaultAsync(i => i.Id == itemId && i.DealId == dealId);
            if (item == null) return null;
            ValidateItem(dto);

            var oldName = item.ItemName;
            var oldQty = item.Quantity;
            var oldPrice = item.UnitPrice;

            item.ArticleId = dto.ArticleId;
            item.Type = dto.Type;
            item.ItemName = dto.ItemName;
            item.ItemCode = dto.ItemCode;
            item.Description = dto.Description;
            item.Quantity = dto.Quantity;
            item.UnitPrice = dto.UnitPrice;
            item.Discount = dto.Discount;
            item.DiscountType = dto.DiscountType;
            item.InstallationId = dto.InstallationId;
            item.InstallationName = dto.InstallationName;
            item.LineTotal = ComputeLineTotal(dto.Quantity, dto.UnitPrice, dto.Discount, dto.DiscountType);
            await _context.SaveChangesAsync();
            var deal = await ReloadWithItems(dealId);
            if (deal != null) { deal.EstimatedValue = RecomputeValue(deal); deal.ModifiedDate = DateTime.UtcNow; await _context.SaveChangesAsync(); }

            await LogActivityAsync("item_updated", "DealItem", itemId.ToString(), dealId,
                $"Item updated: {item.ItemName} (qty {oldQty}→{item.Quantity}, price {oldPrice}→{item.UnitPrice})",
                userId, userName,
                details: oldName != item.ItemName ? $"renamed from {oldName}" : null);
            return MapItem(item);
        }

        public async Task<bool> DeleteDealItemAsync(int dealId, int itemId, string userId = "system", string? userName = null)
        {
            // Guard the parent: items of a soft-deleted deal must not be mutable.
            var parentAlive = await _context.Deals.AsNoTracking().AnyAsync(d => d.Id == dealId && !d.IsDeleted);
            if (!parentAlive) return false;
            var item = await _context.DealItems.FirstOrDefaultAsync(i => i.Id == itemId && i.DealId == dealId);
            if (item == null) return false;

            var snapshotName = item.ItemName;
            var snapshotQty = item.Quantity;

            _context.DealItems.Remove(item);
            await _context.SaveChangesAsync();
            var deal = await ReloadWithItems(dealId);
            if (deal != null)
            {
                // Removing the last item zeroes the value rather than keeping the stale total.
                deal.EstimatedValue = (deal.Items != null && deal.Items.Any())
                    ? Math.Round(deal.Items.Sum(i => i.LineTotal), 2)
                    : 0m;
                deal.ModifiedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            await LogActivityAsync("item_deleted", "DealItem", itemId.ToString(), dealId,
                $"Item removed: {snapshotName} (qty {snapshotQty})", userId, userName);
            return true;
        }

        // ── Activities ──

        public async Task<(List<DealActivityDto> Items, int Total)> GetDealActivitiesAsync(int dealId, string? type = null, int page = 1, int limit = 20)
        {
            if (page < 1) page = 1;
            if (limit < 1) limit = 20;
            if (limit > 200) limit = 200;

            // A soft-deleted deal is invisible everywhere else — its timeline must be too,
            // otherwise the activity endpoint keeps serving the history of deleted deals.
            var dealExists = await _context.Deals.AsNoTracking()
                .AnyAsync(d => d.Id == dealId && !d.IsDeleted);
            if (!dealExists) return (new List<DealActivityDto>(), 0);

            var query = _context.DealActivities.AsNoTracking().Where(a => a.DealId == dealId);

            if (!string.IsNullOrEmpty(type)) query = query.Where(a => a.Type == type);

            var total = await query.CountAsync();
            var list = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * limit).Take(limit)
                .ToListAsync();
            return (list.Select(MapActivity).ToList(), total);
        }

        public async Task<DealActivityDto?> AddDealActivityAsync(int dealId, CreateDealActivityDto dto, string userId, string? userName = null)
        {
            var deal = await _context.Deals.FirstOrDefaultAsync(d => d.Id == dealId && !d.IsDeleted);
            if (deal == null) return null;
            // A timeline entry with no text renders as an empty row in the UI, and an
            // unknown type has no icon/label mapping — reject both instead of storing junk.
            var description = (dto.Description ?? "").Trim();
            if (description.Length == 0)
                throw new InvalidOperationException("INVALID_ACTIVITY: Activity description is required.");
            if (description.Length > 1000)
                throw new InvalidOperationException("INVALID_ACTIVITY: Activity description must be 1000 characters or fewer.");

            var type = string.IsNullOrWhiteSpace(dto.Type) ? "note" : dto.Type.Trim().ToLowerInvariant();
            if (!UserActivityTypes.Contains(type))
                throw new InvalidOperationException(
                    $"INVALID_ACTIVITY_TYPE:{dto.Type}: Unknown activity type '{dto.Type}'. Allowed: {string.Join(", ", UserActivityTypes)}");

            var activity = await AddActivityInternalAsync(dealId, type, description, null, userId, userName, null, dto.Details);
            deal.LastActivity = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return MapActivity(activity);
        }

        public async Task<bool> DeleteDealActivityAsync(int dealId, int activityId, string userId = "system", string? userName = null)
        {
            // Guard the parent: the timeline of a soft-deleted deal is not mutable.
            var parentAlive = await _context.Deals.AsNoTracking().AnyAsync(d => d.Id == dealId && !d.IsDeleted);
            if (!parentAlive) return false;
            var activity = await _context.DealActivities.FirstOrDefaultAsync(a => a.Id == activityId && a.DealId == dealId);
            if (activity == null) return false;

            var snapshotType = activity.Type;
            var snapshotDesc = activity.Description;
            _context.DealActivities.Remove(activity);
            await _context.SaveChangesAsync();

            await LogActivityAsync("activity_deleted", "DealActivity", activityId.ToString(), dealId,
                $"Activity removed ({snapshotType}): {snapshotDesc}", userId, userName);
            return true;
        }

        // ── Helpers ──

        private async Task<DealActivity> AddActivityInternalAsync(int dealId, string type, string description, string? oldValue, string userId, string? userName, string? newValue = null, string? details = null)
        {
            var activity = new DealActivity
            {
                DealId = dealId,
                Type = type,
                Description = description,
                Details = details,
                OldValue = oldValue,
                NewValue = newValue,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                CreatedByName = userName,
            };
            _context.DealActivities.Add(activity);
            await _context.SaveChangesAsync();
            return activity;
        }

        private async Task<Deal?> ReloadWithItems(int dealId)
            => await _context.Deals.Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == dealId);

        private static DealItem BuildItem(CreateDealItemDto dto, int order) => new DealItem
        {
            ArticleId = dto.ArticleId,
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "article" : dto.Type,
            ItemName = (dto.ItemName ?? "").Trim(),
            ItemCode = dto.ItemCode,
            Description = dto.Description,
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            Discount = dto.Discount,
            DiscountType = dto.DiscountType,
            LineTotal = ComputeLineTotal(dto.Quantity, dto.UnitPrice, dto.Discount, dto.DiscountType),
            DisplayOrder = order,
            InstallationId = dto.InstallationId,
            InstallationName = dto.InstallationName,
        };

        private static decimal ComputeLineTotal(decimal qty, decimal unitPrice, decimal discount, string discountType)
        {
            var gross = qty * unitPrice;
            // Round to 2 dp to match the decimal(18,2) columns (no compute-vs-stored drift)
            // and never emit a negative total, on either branch.
            if (discount <= 0) return Math.Round(Math.Max(gross, 0), 2);
            var net = discountType == "fixed" ? gross - discount : gross * (1 - discount / 100m);
            return Math.Round(Math.Max(net, 0), 2);
        }

        /// <summary>If a deal has line items, its value is the sum of them; otherwise keep the manual estimate.</summary>
        private static decimal RecomputeValue(Deal? deal)
        {
            if (deal == null) return 0;
            if (deal.Items != null && deal.Items.Any())
                return Math.Round(deal.Items.Sum(i => i.LineTotal), 2);
            return deal.EstimatedValue;
        }

        private async Task<Dictionary<int, MyApi.Modules.Contacts.Models.Contact>> LoadContactsAsync(IEnumerable<int> ids)
        {
            var idList = ids.Distinct().ToList();
            if (!idList.Any()) return new();
            return await _context.Contacts.AsNoTracking()
                .Where(c => idList.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);
        }

        private static DealDto MapToDto(Deal d, Dictionary<int, MyApi.Modules.Contacts.Models.Contact> contacts)
        {
            contacts.TryGetValue(d.ContactId, out var contact);
            var contactName = contact != null ? $"{contact.FirstName} {contact.LastName}".Trim() : null;
            return new DealDto
            {
                Id = d.Id,
                DealNumber = d.DealNumber,
                Title = d.Title,
                Description = d.Description,
                ContactId = d.ContactId,
                ContactName = contactName,
                Contact = contact == null ? null : new DealContactSummaryDto
                {
                    Id = contact.Id,
                    Name = contactName,
                    Company = contact.Company,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Address = contact.Address,
                    City = contact.City,
                },
                ProjectId = d.ProjectId,
                Stage = d.Stage,
                Probability = d.Probability,
                EstimatedValue = d.EstimatedValue,
                Currency = d.Currency,
                ExpectedCloseDate = d.ExpectedCloseDate,
                ActualCloseDate = d.ActualCloseDate,
                NextActionDate = d.NextActionDate,
                NextAction = d.NextAction,
                LostReason = d.LostReason,
                Category = d.Category,
                Source = d.Source,
                Notes = d.Notes,
                Tags = d.Tags,
                AssignedTo = d.AssignedTo,
                AssignedToName = d.AssignedToName,
                ConvertedToOfferId = d.ConvertedToOfferId,
                ConvertedToSaleId = d.ConvertedToSaleId,
                ConvertedToProjectId = d.ConvertedToProjectId,
                ConvertedAt = d.ConvertedAt,
                CreatedAt = d.CreatedDate,
                UpdatedAt = d.ModifiedDate,
                CreatedBy = d.CreatedBy,
                CreatedByName = d.CreatedByName,
                LastActivity = d.LastActivity,
                Items = (d.Items ?? new List<DealItem>()).OrderBy(i => i.DisplayOrder).Select(MapItem).ToList(),
            };
        }

        private static DealItemDto MapItem(DealItem i) => new DealItemDto
        {
            Id = i.Id,
            DealId = i.DealId,
            ArticleId = i.ArticleId,
            Type = i.Type,
            ItemName = i.ItemName,
            ItemCode = i.ItemCode,
            Description = i.Description,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Discount = i.Discount,
            DiscountType = i.DiscountType,
            LineTotal = i.LineTotal,
            DisplayOrder = i.DisplayOrder,
            InstallationId = i.InstallationId,
            InstallationName = i.InstallationName,
        };

        private static DealActivityDto MapActivity(DealActivity a) => new DealActivityDto
        {
            Id = a.Id,
            DealId = a.DealId,
            Type = a.Type,
            Description = a.Description,
            Details = a.Details,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            CreatedAt = a.CreatedAt,
            CreatedBy = a.CreatedBy,
            CreatedByName = a.CreatedByName,
        };
    }
}
