using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApi.Data;
using MyApi.Modules.Sales.DTOs;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Articles.Services;
using MyApi.Modules.WorkflowEngine.Services;
using MyApi.Modules.Projects.Services;


namespace MyApi.Modules.Sales.Services
{
    public class SaleService : ISaleService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SaleService> _logger;
        private readonly IStockTransactionService? _stockTransactionService;
        private readonly IWorkflowTriggerService? _workflowTriggerService;
        private readonly MyApi.Modules.Numbering.Services.INumberingService? _numberingService;
        private readonly MyApi.Modules.Planning.Services.IPlannedLineEntryService? _plannedEntries;
        private readonly MyApi.Modules.Shared.Services.IEntityFormDocumentService? _formDocuments;
        private readonly MyApi.Modules.Shared.Services.IActivityLogger? _activityLogger;
        private readonly MyApi.Modules.Contacts.Services.IContactActivityService? _contactActivity;
        // Resolved lazily to avoid a DI cycle (ServiceOrderService already depends on IInvoiceService).
        private readonly IServiceProvider? _serviceProvider;

        public SaleService(
            ApplicationDbContext context,
            ILogger<SaleService> logger,
            IStockTransactionService? stockTransactionService = null,
            IWorkflowTriggerService? workflowTriggerService = null,
            MyApi.Modules.Numbering.Services.INumberingService? numberingService = null,
            MyApi.Modules.Planning.Services.IPlannedLineEntryService? plannedEntries = null,
            MyApi.Modules.Shared.Services.IEntityFormDocumentService? formDocuments = null,
            MyApi.Modules.Shared.Services.IActivityLogger? activityLogger = null,
            MyApi.Modules.Contacts.Services.IContactActivityService? contactActivity = null,
            IServiceProvider? serviceProvider = null)
        {
            _context = context;
            _logger = logger;
            _stockTransactionService = stockTransactionService;
            _workflowTriggerService = workflowTriggerService;
            _numberingService = numberingService;
            _plannedEntries = plannedEntries;
            _formDocuments = formDocuments;
            _activityLogger = activityLogger;
            _contactActivity = contactActivity;
            _serviceProvider = serviceProvider;
        }



        public async Task<PaginatedSaleResponse> GetSalesAsync(
            string? status = null,
            string? stage = null,
            string? priority = null,
            string? contactId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? search = null,
            int page = 1,
            int limit = 20,
            string sortBy = "updated_at",
            string sortOrder = "desc"
        )
        {
            var query = _context.Sales.AsNoTracking().Where(s => !s.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status);

            if (!string.IsNullOrEmpty(stage))
                query = query.Where(s => s.Stage == stage);

            if (!string.IsNullOrEmpty(priority))
                query = query.Where(s => s.Priority == priority);

            if (!string.IsNullOrEmpty(contactId) && int.TryParse(contactId, out int contactIdInt))
                query = query.Where(s => s.ContactId == contactIdInt);

            if (dateFrom.HasValue)
                query = query.Where(s => s.CreatedDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(s => s.CreatedDate <= dateTo.Value);

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(s =>
                    (s.Title != null && s.Title.ToLower().Contains(searchLower)) ||
                    (s.Description != null && s.Description.ToLower().Contains(searchLower))
                );
            }

            var total = await query.CountAsync();

            query = sortBy.ToLower() switch
            {
                "created_at" => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.CreatedDate) : query.OrderByDescending(s => s.CreatedDate),
                "title" => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.Title) : query.OrderByDescending(s => s.Title),
                "amount" => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.TotalAmount) : query.OrderByDescending(s => s.TotalAmount),
                _ => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.UpdatedAt) : query.OrderByDescending(s => s.UpdatedAt)
            };

            var sales = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Include(s => s.Items)
                .ToListAsync();
                
            var contactIds = sales.Select(s => s.ContactId).Distinct().ToList();
            var contacts = await _context.Contacts
                .AsNoTracking()
                .Where(c => contactIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var saleDtos = sales.Select(s => MapToDto(s, contacts.GetValueOrDefault(s.ContactId))).ToList();

            return new PaginatedSaleResponse
            {
                Sales = saleDtos,
                Pagination = new PaginationInfo
                {
                    Page = page,
                    Limit = limit,
                    Total = total,
                    TotalPages = (int)Math.Ceiling((double)total / limit)
                }
            };
        }

        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (sale == null) return null;
            
            var contact = await _context.Contacts.FindAsync(sale.ContactId);
            return MapToDto(sale, contact);
        }

        public async Task<SaleDto> CreateSaleAsync(CreateSaleDto createDto, string userId)
        {
            // Verify contact exists
            var contact = await _context.Contacts.FindAsync(createDto.ContactId);
            if (contact == null)
                throw new KeyNotFoundException($"Contact with ID {createDto.ContactId} not found");

            string saleNumber;
            try
            {
                saleNumber = _numberingService != null
                    ? await _numberingService.GetNextAsync("Sale")
                    : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Sale");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Numbering service failed for Sale, using GUID fallback");
                saleNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Sale");
            }

            var sale = new Sale
            {
                SaleNumber = saleNumber,
                Title = createDto.Title,
                Description = createDto.Description,
                ContactId = createDto.ContactId,
                ProjectId = createDto.ProjectId,
                // Fix #8: default to the documented workflow start ('created'). Defaulting
                // to 'won' silently short-circuited the pipeline for integration callers
                // that don't send a status, AND bypassed stock deduction which only runs
                // in UpdateSaleAsync's closing branch.
                Status = createDto.Status ?? "created",
                Stage = createDto.Stage ?? "new",
                Priority = createDto.Priority,
                // Currency always comes from the caller (tenant preference on the FE).
                // No hardcoded literal here; if truly missing we fail fast so the FE
                // is fixed rather than silently persisting an incorrect currency.
                Currency = !string.IsNullOrWhiteSpace(createDto.Currency)
                    ? createDto.Currency!
                    : throw new ArgumentException("Currency is required (comes from the user's preferences)."),
                EstimatedCloseDate = createDto.EstimatedCloseDate,
                ActualCloseDate = createDto.ActualCloseDate,
                BillingAddress = createDto.BillingAddress,
                BillingPostalCode = createDto.BillingPostalCode,
                BillingCountry = createDto.BillingCountry,
                DeliveryAddress = createDto.DeliveryAddress,
                DeliveryPostalCode = createDto.DeliveryPostalCode,
                DeliveryCountry = createDto.DeliveryCountry,
                Taxes = createDto.Taxes ?? 0,
                TaxType = createDto.TaxType ?? "percentage",
                Discount = createDto.Discount ?? 0,
                // The header discount type has no column of its own: a non-null
                // DiscountPercent means "read Discount as a percentage".
                DiscountPercent = string.Equals(createDto.DiscountType, "percentage", StringComparison.OrdinalIgnoreCase)
                    ? createDto.Discount ?? 0
                    : (decimal?)null,
                FiscalStamp = createDto.FiscalStamp ?? 1.000m,
                TotalAmount = 0,
                OfferId = createDto.OfferId,
                CreatedBy = userId,
                CreatedByName = await ResolveUserNameAsync(userId),
                CreatedDate = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Tags = new string[] { },
                // Copy contact geolocation
                ContactLatitude = contact.Latitude,
                ContactLongitude = contact.Longitude,
                ContactHasLocation = contact.HasLocation
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            // Add items if provided
            if (createDto.Items != null && createDto.Items.Any())
            {
                var items = createDto.Items.Select((itemDto, index) => new SaleItem
                {
                    // Id is auto-generated
                    SaleId = sale.Id,
                    Type = itemDto.Type ?? "article",
                    ArticleId = itemDto.ArticleId,
                    ItemName = itemDto.ItemName,
                    ItemCode = itemDto.ItemCode,
                    Description = itemDto.Description ?? itemDto.ItemName ?? string.Empty,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.UnitPrice,
                    Discount = itemDto.Discount,
                    DiscountType = itemDto.DiscountType ?? "percentage",
                    InstallationId = itemDto.InstallationId,
                    InstallationName = itemDto.InstallationName,
                    RequiresServiceOrder = itemDto.RequiresServiceOrder,
                    // Stamp the sale's currency so each historical line remembers its own currency.
                    Currency = sale.Currency,
                    // Preserve the exact order items were selected/sent in.
                    DisplayOrder = itemDto.DisplayOrder ?? index
                }).ToList();

                _context.SaleItems.AddRange(items);
                await _context.SaveChangesAsync();
            }

            // Persist line totals + header totals (subtotal, tax, grand total).
            // Without this the sale would be stored with 0 amounts.
            await RecalculateSaleTotalsAsync(sale.Id);


            if (_contactActivity != null && sale.ContactId > 0)
            {
                await _contactActivity.LogAsync(
                    contactId: sale.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.SaleCreated,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Sale,
                    relatedEntityId: sale.Id,
                    description: $"Sale {sale.SaleNumber} '{sale.Title}' was created",
                    metadata: new { number = sale.SaleNumber, title = sale.Title, status = sale.Status, currency = sale.Currency },
                    createdBy: userId);
            }

            var createdSale = await GetSaleByIdAsync(sale.Id);
            return createdSale!;
        }

        public async Task<SaleDto> CreateSaleFromOfferAsync(int offerId, string userId)
        {
            var offer = await _context.Offers
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
                throw new KeyNotFoundException($"Offer with ID {offerId} not found");

            // Fix #1: idempotency. This method is reachable from a public endpoint
            // (POST /api/sales/from-offer/{id}) that a double-click or a client retry can
            // fire twice. The workflow path (BusinessWorkflowService.HandleOfferAcceptedAsync)
            // and OfferService.ConvertOfferAsync both guard already; this one did not, and
            // sales.offer_id carries only a non-unique index. Return the existing sale
            // instead of minting a duplicate.
            var offerKey = offerId.ToString();
            var preExisting = await _context.Sales
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OfferId == offerKey);
            if (preExisting != null)
            {
                _logger.LogInformation(
                    "CreateSaleFromOffer: sale {SaleId} already exists for offer {OfferId}; returning it instead of creating a duplicate",
                    preExisting.Id, offerId);
                return (await GetSaleByIdAsync(preExisting.Id))!;
            }


            // Get user name for sale and activity
            string createdByName = userId;
            var adminUser = await _context.MainAdminUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
            if (adminUser != null)
            {
                createdByName = $"{adminUser.FirstName} {adminUser.LastName}".Trim();
            }
            else
            {
                var regularUser = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                if (regularUser != null)
                {
                    createdByName = $"{regularUser.FirstName} {regularUser.LastName}".Trim();
                }
            }

            // Get contact for geolocation data
            var contact = await _context.Contacts.FindAsync(offer.ContactId);

            string saleNumber2;
            try
            {
                saleNumber2 = _numberingService != null
                    ? await _numberingService.GetNextAsync("Sale")
                    : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Sale");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Numbering service failed for Sale (from offer), using GUID fallback");
                saleNumber2 = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Sale");
            }

            // Wrap the whole conversion in a transaction so a partial copy
            // (e.g. planned-entry CopyAsync failure) rolls everything back.
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure.
            int createdSaleId = 0;
            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
            await strategy.ExecuteAsync(async () =>

            {
                // Serializable so two concurrent conversions of the same offer cannot both
                // pass the existence re-check below before either commits. The unique index
                // on sales(offer_id) is the final backstop.
                using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                var raced = await _context.Sales
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OfferId == offerKey);
                if (raced != null)
                {
                    _logger.LogInformation(
                        "CreateSaleFromOffer: concurrent conversion detected for offer {OfferId}; reusing sale {SaleId}",
                        offerId, raced.Id);
                    createdSaleId = raced.Id;
                    return;
                }



                var sale = new Sale
                {
                    SaleNumber = saleNumber2,
                    Title = offer.Title,
                    Description = offer.Description,
                    ContactId = offer.ContactId,
                    ProjectId = offer.ProjectId,
                    IsDeal = offer.ProjectId.HasValue,
                    Status = "created",  // Start with 'created' status instead of 'won'
                    Stage = "offer",     // Start at 'offer' stage
                    Priority = "medium",
                    Currency = offer.Currency ?? "TND",
                    BillingAddress = offer.BillingAddress,
                    BillingPostalCode = offer.BillingPostalCode,
                    BillingCountry = offer.BillingCountry,
                    DeliveryAddress = offer.DeliveryAddress,
                    DeliveryPostalCode = offer.DeliveryPostalCode,
                    DeliveryCountry = offer.DeliveryCountry,
                    Taxes = offer.Taxes ?? 0,
                    TaxType = offer.TaxType ?? "percentage",
                    Discount = offer.Discount ?? 0,
                    DiscountPercent = string.Equals(offer.DiscountType, "percentage", StringComparison.OrdinalIgnoreCase)
                        ? offer.Discount ?? 0
                        : (decimal?)null,
                    FiscalStamp = offer.FiscalStamp ?? 1.000m,
                    TotalAmount = offer.TotalAmount,
                    AssignedTo = offer.AssignedTo,
                    AssignedToName = offer.AssignedToName,
                    Tags = offer.Tags != null ? offer.Tags.Concat(new[] { "Converted" }).ToArray() : new[] { "Converted" },
                    OfferId = offerId.ToString(),
                    ConvertedFromOfferAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    CreatedByName = createdByName,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    // Copy contact geolocation
                    ContactLatitude = contact?.Latitude ?? offer.ContactLatitude,
                    ContactLongitude = contact?.Longitude ?? offer.ContactLongitude,
                    ContactHasLocation = contact?.HasLocation ?? offer.ContactHasLocation
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Auto-note on the project (deal won)
                ProjectAutoNote.Add(_context, offer.ProjectId,
                    $"Offer #{offer.OfferNumber} won → Sale #{sale.SaleNumber} created (deal, total {sale.TotalAmount} {sale.Currency}).",
                    userId);

                // Copy items — track (offerItem, saleItem) pairs so we can copy
                // PlannedLineEntries with stable OriginOfferItemId lineage.
                var itemPairs = new List<(MyApi.Modules.Offers.Models.OfferItem Src, SaleItem Dst)>();
                if (offer.Items != null && offer.Items.Any())
                {
                    foreach (var offerItem in offer.Items.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id))
                    {
                        var saleItem = new SaleItem
                        {
                            SaleId = sale.Id,
                            Type = offerItem.Type,
                            ArticleId = offerItem.ArticleId,
                            ItemName = offerItem.ItemName,
                            ItemCode = offerItem.ItemCode,
                            Description = offerItem.Description ?? offerItem.ItemName ?? "Item",
                            Quantity = offerItem.Quantity,
                            UnitPrice = offerItem.UnitPrice,
                            Discount = offerItem.Discount,
                            DiscountType = offerItem.DiscountType ?? "percentage",
                            InstallationId = offerItem.InstallationId,
                            InstallationName = offerItem.InstallationName,
                            RequiresServiceOrder = offerItem.Type == "service",
                            FulfillmentStatus = "pending",
                            TaxRate = offerItem.TaxRate,
                            // Inherit the parent sale's currency so offer→sale copy is currency-safe.
                            Currency = sale.Currency,
                            DisplayOrder = offerItem.DisplayOrder,
                            // Stable lineage anchor used by planned-entry propagation.
                            // Without this, plans added on an offer after conversion cannot
                            // find the related sale item or downstream service-order jobs.
                            OriginOfferItemId = offerItem.Id
                        };
                        SaleTotalsCalculator.ApplyLineTotal(saleItem);
                        _context.SaleItems.Add(saleItem);
                        itemPairs.Add((offerItem, saleItem));
                    }
                    await _context.SaveChangesAsync();
                }

                // Recompute the header money from the copied lines so the sale
                // never inherits a stale/zero total from the offer. Offers with no
                // lines keep their manually entered total as-is.
                if (itemPairs.Count > 0)
                    SaleTotalsCalculator.Apply(sale, itemPairs.Select(p => p.Dst).ToList());
                else
                    sale.GrandTotal = sale.TotalAmount;



                // Propagate planned time/expenses from offer_item → sale_item.
                // Inside the transaction: any failure rolls the whole conversion back
                // so we never end up with a Sale that lost its plan.
                if (itemPairs.Count > 0)
                {
                    foreach (var (src, dst) in itemPairs)
                    {
                        if (_plannedEntries != null)
                            await _plannedEntries.CopyAsync("offer_item", src.Id, "sale_item", dst.Id, userId);
                        // Carry item-level checklists (offer line → sale line) too.
                        if (_formDocuments != null)
                            await _formDocuments.CopyItemDocumentsAsync("offer_item", src.Id, "sale_item", dst.Id, userId);
                    }
                }

                // Update offer status
                offer.Status = "accepted";
                offer.ConvertedToSaleId = sale.Id.ToString();
                offer.ConvertedAt = DateTime.UtcNow;
                offer.UpdatedAt = DateTime.UtcNow;

                // Log sale creation activity
                var creationActivity = new SaleActivity
                {
                    SaleId = sale.Id,
                    Type = "created",
                    Description = $"Sale order created from Offer #{offer.OfferNumber}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = createdByName
                };
                _context.SaleActivities.Add(creationActivity);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                createdSaleId = sale.Id;
            });
            }
            catch (DbUpdateException ex) when (createdSaleId == 0)
            {
                // Final backstop: the unique index on sales(offer_id) rejected a racing
                // insert. Surface the winning sale rather than a 500.
                var winner = await _context.Sales.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OfferId == offerKey);
                if (winner == null) throw;
                _logger.LogWarning(ex,
                    "CreateSaleFromOffer: unique-index race on offer {OfferId}; returning existing sale {SaleId}",
                    offerId, winner.Id);
                createdSaleId = winner.Id;
            }

            var createdSale = await GetSaleByIdAsync(createdSaleId);


            if (_contactActivity != null && createdSale != null && createdSale.ContactId > 0)
            {
                await _contactActivity.LogAsync(
                    contactId: createdSale.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.SaleCreated,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Sale,
                    relatedEntityId: createdSale.Id,
                    description: $"Sale {createdSale.SaleNumber} was created from offer",
                    metadata: new { number = createdSale.SaleNumber, fromOffer = offerId, status = createdSale.Status },
                    createdBy: userId);
            }

            return createdSale!;
        }


        public async Task<SaleDto> UpdateSaleAsync(int id, UpdateSaleDto updateDto, string userId)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null)
                throw new KeyNotFoundException($"Sale with ID {id} not found");

            // Track if status is changing to closed/won for stock deduction
            var previousStatus = sale.Status;
            var isClosing = updateDto.Status != null && 
                           (updateDto.Status == "closed" || updateDto.Status == "won" || updateDto.Status == "completed") &&
                           previousStatus != "closed" && previousStatus != "won" && previousStatus != "completed";

            // Track if status is changing from closed to something else (reopening)
            var isReopening = updateDto.Status != null &&
                             updateDto.Status != "closed" && updateDto.Status != "won" && updateDto.Status != "completed" &&
                             (previousStatus == "closed" || previousStatus == "won" || previousStatus == "completed");

            // Status guard: once a sale is finalized (closed/won/completed/cancelled/lost),
            // financial & scope fields are locked. Tags, addresses, description, fulfillment
            // tracking, status transitions, and close dates can still be edited.
            var finalizedSaleStatuses = new[] { "closed", "won", "completed", "cancelled", "lost" };
            var isSaleFinalized = !string.IsNullOrEmpty(previousStatus) &&
                                  finalizedSaleStatuses.Contains(previousStatus);

            bool TriedToEditSaleFinancials() =>
                updateDto.Amount.HasValue ||
                updateDto.Taxes.HasValue ||
                updateDto.TaxType != null ||
                updateDto.Discount.HasValue ||
                updateDto.FiscalStamp.HasValue;

            if (isSaleFinalized && TriedToEditSaleFinancials())
            {
                throw new InvalidOperationException(
                    $"Cannot modify financial fields on a {previousStatus} sale. " +
                    "Reopen the sale first (change status) or create a new sale.");
            }

            if (updateDto.Title != null) sale.Title = updateDto.Title;
            if (updateDto.ProjectId.HasValue) sale.ProjectId = updateDto.ProjectId.Value;
            if (updateDto.Description != null) sale.Description = updateDto.Description;
            if (updateDto.Status != null) sale.Status = updateDto.Status;
            if (updateDto.Stage != null) sale.Stage = updateDto.Stage;
            if (updateDto.Priority != null) sale.Priority = updateDto.Priority;
            if (!isSaleFinalized && updateDto.Amount.HasValue) sale.TotalAmount = updateDto.Amount.Value;
            if (!isSaleFinalized && updateDto.Taxes.HasValue) sale.Taxes = updateDto.Taxes.Value;
            if (!isSaleFinalized && updateDto.TaxType != null) sale.TaxType = updateDto.TaxType;
            if (!isSaleFinalized && updateDto.Discount.HasValue) sale.Discount = updateDto.Discount.Value;
            if (!isSaleFinalized && updateDto.DiscountType != null)
            {
                sale.DiscountPercent = string.Equals(updateDto.DiscountType, "percentage", StringComparison.OrdinalIgnoreCase)
                    ? (updateDto.Discount ?? sale.Discount ?? 0)
                    : (decimal?)null;
            }
            else if (!isSaleFinalized && updateDto.Discount.HasValue && sale.DiscountPercent.HasValue)
            {
                // Keep the percentage mirror in sync when only the value changes.
                sale.DiscountPercent = updateDto.Discount.Value;
            }
            if (!isSaleFinalized && updateDto.FiscalStamp.HasValue) sale.FiscalStamp = updateDto.FiscalStamp.Value;
            if (updateDto.EstimatedCloseDate.HasValue) sale.EstimatedCloseDate = updateDto.EstimatedCloseDate;
            if (updateDto.ActualCloseDate.HasValue) sale.ActualCloseDate = updateDto.ActualCloseDate;
            if (updateDto.BillingAddress != null) sale.BillingAddress = updateDto.BillingAddress;
            if (updateDto.BillingPostalCode != null) sale.BillingPostalCode = updateDto.BillingPostalCode;
            if (updateDto.BillingCountry != null) sale.BillingCountry = updateDto.BillingCountry;
            if (updateDto.DeliveryAddress != null) sale.DeliveryAddress = updateDto.DeliveryAddress;
            if (updateDto.DeliveryPostalCode != null) sale.DeliveryPostalCode = updateDto.DeliveryPostalCode;
            if (updateDto.DeliveryCountry != null) sale.DeliveryCountry = updateDto.DeliveryCountry;
            if (updateDto.LostReason != null) sale.LostReason = updateDto.LostReason;
            if (updateDto.MaterialsFulfillment != null) sale.MaterialsFulfillment = updateDto.MaterialsFulfillment;
            if (updateDto.ServiceOrdersStatus != null) sale.ServiceOrdersStatus = updateDto.ServiceOrdersStatus;
            if (updateDto.Tags != null) sale.Tags = updateDto.Tags;

            // Auto-set ActualCloseDate when closing, clear when reopening
            if (isClosing && !sale.ActualCloseDate.HasValue)
            {
                sale.ActualCloseDate = DateTime.UtcNow;
            }
            if (isReopening)
            {
                sale.ActualCloseDate = null;
            }

            sale.UpdatedAt = DateTime.UtcNow;
            sale.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            // Get user name for stock transaction logging
            string userName = await ResolveUserNameAsync(userId);

            // Deduct stock from materials when sale is closed.
            // If deduction reports failures (e.g., insufficient stock) or throws,
            // revert the close so the sale state and inventory stay consistent —
            // previously we swallowed errors and the sale closed "successfully"
            // with wrong inventory numbers.
            if (isClosing && _stockTransactionService != null)
            {
                async Task RevertCloseAsync()
                {
                    sale.Status = previousStatus;
                    sale.ActualCloseDate = null;
                    sale.UpdatedAt = DateTime.UtcNow;
                    try { await _context.SaveChangesAsync(); }
                    catch (Exception revertEx)
                    {
                        _logger.LogError(revertEx, "Failed to revert sale {SaleId} close after stock deduction failure", id);
                    }
                }

                try
                {
                    _logger.LogInformation("Sale {SaleId} closed, deducting stock for material items", id);
                    var result = await _stockTransactionService.DeductStockFromSaleAsync(id, userId, userName);

                    if (!result.Success)
                    {
                        var errors = string.Join("; ", result.Errors);
                        _logger.LogWarning("Stock deduction failed for sale {SaleId}: {Errors}", id, errors);
                        await RevertCloseAsync();
                        throw new InvalidOperationException(
                            $"Cannot close sale: stock deduction failed. {errors}");
                    }

                    _logger.LogInformation("Successfully deducted stock for {Count} items from sale {SaleId}",
                        result.ItemsDeducted, id);
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deducting stock for sale {SaleId}", id);
                    await RevertCloseAsync();
                    throw new InvalidOperationException(
                        $"Cannot close sale: stock deduction error — {ex.Message}", ex);
                }
            }

            // Restore stock when sale is reopened/cancelled
            if (isReopening && _stockTransactionService != null)
            {
                try
                {
                    _logger.LogInformation("Sale {SaleId} reopened, restoring stock for material items", id);
                    var result = await _stockTransactionService.RestoreStockFromSaleAsync(id, userId, userName);
                    
                    if (!result.Success)
                    {
                        _logger.LogWarning("Some stock restorations failed for sale {SaleId}: {Errors}", 
                            id, string.Join(", ", result.Errors));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error restoring stock for sale {SaleId}", id);
                }
            }

            // Trigger workflow automation for status change
            if (updateDto.Status != null && previousStatus != updateDto.Status && _workflowTriggerService != null)
            {
                try
                {
                    // Include service order config in the context if provided
                    var contextData = new Dictionary<string, object>
                    {
                        { "saleId", id },
                        { "saleNumber", sale.SaleNumber ?? "" },
                        { "title", sale.Title ?? "" }
                    };
                    
                    if (updateDto.ServiceOrderConfig != null)
                    {
                        contextData["serviceOrderConfig"] = updateDto.ServiceOrderConfig;
                    }
                    
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "sale",
                        id,
                        previousStatus ?? "",
                        updateDto.Status,
                        userId,
                        contextData
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to trigger workflow for sale {SaleId} status change", id);
                }
            }

            // Log status change to the contact activity feed
            if (_contactActivity != null && sale.ContactId > 0 && updateDto.Status != null && previousStatus != updateDto.Status)
            {
                await _contactActivity.LogAsync(
                    contactId: sale.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.SaleStatusChanged,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Sale,
                    relatedEntityId: sale.Id,
                    description: $"Sale {sale.SaleNumber} status: {previousStatus} → {updateDto.Status}",
                    metadata: new { number = sale.SaleNumber, title = sale.Title, oldStatus = previousStatus, status = updateDto.Status },
                    createdBy: userId);
            }

            // Cascade the sale's new status onto every linked Service Order so the
            // user doesn't have to reopen each SO after items were transferred.
            // Runs after the sale row is persisted; failures are swallowed inside the cascade.
            if (updateDto.Status != null && previousStatus != updateDto.Status && _serviceProvider != null)
            {
                try
                {
                    var soService = _serviceProvider.GetService<MyApi.Modules.ServiceOrders.Services.IServiceOrderService>();
                    if (soService != null)
                    {
                        await soService.CascadeSaleStatusToServiceOrdersAsync(id, updateDto.Status, userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SaleService: cascade to service orders failed for sale {SaleId}", id);
                }
            }

            // Keep persisted money in sync with items / tax / discount changes.
            await RecalculateSaleTotalsAsync(id);


            var updatedSale = await GetSaleByIdAsync(id);
            return updatedSale!;

        }

        public async Task<bool> DeleteSaleAsync(int id, string userId = "system")
        {
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var sale = await _context.Sales
                        .Include(s => s.Items)
                        .FirstOrDefaultAsync(s => s.Id == id);

                    if (sale == null || sale.IsDeleted)
                        return false;

                    // Data integrity: never orphan an invoice. A sale that has any
                    // non-void invoice (draft included) must have those invoices
                    // removed/voided first, otherwise the invoice would point at a
                    // deleted order and the customer ledger loses its audit chain.
                    var linkedInvoices = await GetActiveInvoicesForSaleAsync(id);
                    if (linkedInvoices.Count > 0)
                    {
                        var numbers = string.Join(", ", linkedInvoices.Select(i => i.InvoiceNumber ?? $"draft #{i.Id}"));
                        throw new InvalidOperationException(
                            $"Cannot delete this sale: {linkedInvoices.Count} invoice(s) are linked to it ({numbers}). " +
                            "Delete the drafts and void the posted invoices first.");
                    }



                    // Get user name for activity logging
                    string deletedByName = userId;
                    var adminUser = await _context.MainAdminUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                    if (adminUser != null)
                    {
                        deletedByName = $"{adminUser.FirstName} {adminUser.LastName}".Trim();
                    }
                    else
                    {
                        var regularUser = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                        if (regularUser != null)
                        {
                            deletedByName = $"{regularUser.FirstName} {regularUser.LastName}".Trim();
                        }
                    }

                    // Get sale item IDs before deletion
                    var saleItemIds = sale.Items?.Select(i => i.Id).ToList() ?? new List<int>();

                    // Remove deleted sale item IDs from ServiceOrderJobs. Jobs can store either a
                    // single ID ("12") or an installation-grouped comma list ("12,13,14").
                    if (saleItemIds.Any())
                    {
                        var saleItemTokens = saleItemIds.Select(x => x.ToString()).ToHashSet();
                        var linkedJobs = await _context.ServiceOrderJobs
                            .Where(j => j.SaleItemId != null)
                            .ToListAsync();
                        foreach (var job in linkedJobs)
                        {
                            var remaining = job.SaleItemId!
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .Where(p => !saleItemTokens.Contains(p))
                                .Distinct()
                                .ToList();
                            if (remaining.Count == 0)
                                job.SaleItemId = null;
                            else
                                job.SaleItemId = string.Join(",", remaining);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Nullify SaleId reference in ServiceOrders (cast int to string for VARCHAR column)
                    await _context.Database.ExecuteSqlRawAsync(
                        @"UPDATE ""ServiceOrders"" SET ""SaleId"" = NULL WHERE ""SaleId"" = @p0 AND ""TenantId"" = @p1",
                        id.ToString(), _context.GetTenantId());

                    // If sale was converted from an offer, reset the offer and log activity
                    if (!string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                    {
                        var offer = await _context.Offers.FindAsync(offerId);
                        if (offer != null)
                        {
                            // Reset offer so it can be converted again
                            offer.ConvertedToSaleId = null;
                            offer.ConvertedAt = null;
                            offer.Status = "sent"; // Reset to sent status
                            offer.UpdatedAt = DateTime.UtcNow;

                            // Create activity on the offer
                            var offerActivity = new MyApi.Modules.Offers.Models.OfferActivity
                            {
                                OfferId = offerId,
                                Type = "sale_deleted",
                                Description = $"Sale #{sale.SaleNumber} was deleted by {deletedByName}. The offer can now be converted to a new sale.",
                                CreatedAt = DateTime.UtcNow,
                                CreatedByName = deletedByName
                            };
                            _context.OfferActivities.Add(offerActivity);
                        }
                    }

                    sale.IsDeleted = true;
                    sale.DeletedAt = DateTime.UtcNow;
                    sale.DeletedBy = userId;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error deleting sale {SaleId}", id);
                    throw;
                }
            });
        }

        public async Task<SaleStatsDto> GetSaleStatsAsync(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var query = _context.Sales.AsNoTracking().Where(s => !s.IsDeleted).AsQueryable();

            if (dateFrom.HasValue)
                query = query.Where(s => s.CreatedDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(s => s.CreatedDate <= dateTo.Value);

            var sales = await query.ToListAsync();

            var totalSales = sales.Count;
            var activeSales = sales.Count(s => new[] { "new_offer", "draft", "sent", "accepted" }.Contains(s.Status));
            var wonSales = sales.Count(s => new[] { "won", "completed" }.Contains(s.Status));
            var lostSales = sales.Count(s => new[] { "lost", "cancelled" }.Contains(s.Status));
            var totalValue = sales.Sum(s => s.GrandTotal > 0 ? s.GrandTotal : s.TotalAmount);
            var averageValue = totalSales > 0 ? totalValue / totalSales : 0;
            var conversionRate = totalSales > 0 ? (decimal)wonSales / totalSales * 100 : 0;

            return new SaleStatsDto
            {
                TotalSales = totalSales,
                ActiveSales = activeSales,
                WonSales = wonSales,
                LostSales = lostSales,
                TotalValue = totalValue,
                AverageValue = averageValue,
                WinRate = Math.Round(conversionRate, 2),
                ConversionRate = Math.Round(conversionRate, 2),
                MonthlyGrowth = 15.2m
            };
        }

        public async Task<SaleItemDto> AddSaleItemAsync(int saleId, CreateSaleItemDto itemDto)
        {
            var sale = await _context.Sales.FindAsync(saleId);
            if (sale == null)
                throw new KeyNotFoundException($"Sale with ID {saleId} not found");

            // Adding scope to an already-invoiced sale would make the invoice
            // under-charge without anyone noticing.
            await GuardSaleNotInvoicedAsync(saleId, "add an item");

            var nextOrder = await _context.SaleItems
                .Where(si => si.SaleId == saleId)
                .Select(si => (int?)si.DisplayOrder)
                .MaxAsync() ?? -1;

            var item = new SaleItem
            {
                SaleId = saleId,
                Type = itemDto.Type,
                ArticleId = itemDto.ArticleId,
                ItemName = itemDto.ItemName,
                ItemCode = itemDto.ItemCode,
                Description = itemDto.Description ?? string.Empty,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                Discount = itemDto.Discount,
                DiscountType = itemDto.DiscountType,
                InstallationId = itemDto.InstallationId,
                InstallationName = itemDto.InstallationName,
                RequiresServiceOrder = itemDto.RequiresServiceOrder,
                // New lines always inherit the parent sale's currency.
                Currency = sale.Currency,
                DisplayOrder = itemDto.DisplayOrder ?? (nextOrder + 1)
            };
            SaleTotalsCalculator.ApplyLineTotal(item);

            _context.SaleItems.Add(item);
            await _context.SaveChangesAsync();
            await RecalculateSaleTotalsAsync(saleId);

            var addedItem = await _context.SaleItems.FindAsync(item.Id);


            if (_activityLogger != null)
            {
                await _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
                {
                    Module = "Sales",
                    Action = "item_added",
                    EntityType = "SaleItem",
                    EntityId = item.Id.ToString(),
                    ParentEntityType = "Sale",
                    ParentEntityId = saleId,
                    UserId = sale.CreatedBy,
                    UserName = sale.CreatedByName,
                    Message = $"Item added: {item.ItemName} (qty {item.Quantity})",
                });
            }

            return MapItemToDto(addedItem!);
        }

        public async Task<SaleItemDto> UpdateSaleItemAsync(int saleId, int itemId, CreateSaleItemDto itemDto)
        {
            // Changing a line after it has been invoiced would silently desync the
            // invoice (invoice lines are value copies taken at generation time).
            await GuardSaleNotInvoicedAsync(saleId, "edit this item");

            var item = await _context.SaleItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.SaleId == saleId);

            if (item == null)
                throw new KeyNotFoundException($"Item with ID {itemId} not found in sale {saleId}");


            var oldName = item.ItemName;
            var oldQty = item.Quantity;
            var oldPrice = item.UnitPrice;

            item.Type = itemDto.Type;
            item.ArticleId = itemDto.ArticleId;
            item.ItemName = itemDto.ItemName;
            item.ItemCode = itemDto.ItemCode;
            item.Description = itemDto.Description ?? string.Empty;
            item.Quantity = itemDto.Quantity;
            item.UnitPrice = itemDto.UnitPrice;
            item.Discount = itemDto.Discount;
            item.DiscountType = itemDto.DiscountType;
            item.InstallationId = itemDto.InstallationId;
            item.InstallationName = itemDto.InstallationName;
            item.RequiresServiceOrder = itemDto.RequiresServiceOrder;
            SaleTotalsCalculator.ApplyLineTotal(item);

            await _context.SaveChangesAsync();
            await RecalculateSaleTotalsAsync(saleId);


            if (_activityLogger != null)
            {
                await _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
                {
                    Module = "Sales",
                    Action = "item_updated",
                    EntityType = "SaleItem",
                    EntityId = itemId.ToString(),
                    ParentEntityType = "Sale",
                    ParentEntityId = saleId,
                    Message = $"Item updated: {item.ItemName} (qty {oldQty}→{item.Quantity}, price {oldPrice}→{item.UnitPrice})",
                    Details = oldName != item.ItemName ? $"renamed from {oldName}" : null,
                });
            }

            var updatedItem = await _context.SaleItems.FindAsync(itemId);
            return MapItemToDto(updatedItem!);
        }

        public async Task<bool> DeleteSaleItemAsync(int saleId, int itemId)
        {
            // Removing a line after invoicing would leave the invoice charging for
            // something the order no longer contains.
            await GuardSaleNotInvoicedAsync(saleId, "remove this item");

            var item = await _context.SaleItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.SaleId == saleId);

            if (item == null)
                return false;

            var snapshotName = item.ItemName;
            var snapshotQty = item.Quantity;

            _context.SaleItems.Remove(item);
            await _context.SaveChangesAsync();
            await RecalculateSaleTotalsAsync(saleId);


            if (_activityLogger != null)
            {
                await _activityLogger.LogAsync(new MyApi.Modules.Shared.Services.ActivityLogEntry
                {
                    Module = "Sales",
                    Action = "item_deleted",
                    EntityType = "SaleItem",
                    EntityId = itemId.ToString(),
                    ParentEntityType = "Sale",
                    ParentEntityId = saleId,
                    Message = $"Item removed: {snapshotName} (qty {snapshotQty})",
                });
            }

            return true;
        }

        public async Task<List<SaleActivityDto>> GetSaleActivitiesAsync(int saleId, string? type = null, int page = 1, int limit = 20)
        {
            var query = _context.SaleActivities.Where(a => a.SaleId == saleId);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(a => a.Type == type);

            var activities = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return activities.Select(a => new SaleActivityDto
            {
                Id = a.Id,
                SaleId = a.SaleId,
                Type = a.Type ?? "",
                Description = a.Description ?? "",
                OldValue = null,
                NewValue = null,
                CreatedAt = a.CreatedAt,
                CreatedBy = a.CreatedByName ?? ""
            }).ToList();
        }

        private SaleDto MapToDto(Sale sale, Contact? contact)
        {
            return new SaleDto
            {
                Id = sale.Id,
                SaleNumber = sale.SaleNumber,
                Title = sale.Title ?? "",
                Description = sale.Description,
                ContactId = sale.ContactId,
                ProjectId = sale.ProjectId,
                IsDeal = sale.IsDeal,
                Contact = contact != null ? new ContactSummaryDto
                {
                    Id = contact.Id,
                    Name = $"{contact.FirstName} {contact.LastName}".Trim(),
                    Company = contact.Company,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Address = contact.Address,
                    City = contact.City,
                    Latitude = contact.Latitude,
                    Longitude = contact.Longitude,
                    HasLocation = contact.HasLocation
                } : null,
                Amount = sale.TotalAmount,
                Currency = sale.Currency ?? "TND",
                Taxes = sale.Taxes,
                TaxType = sale.TaxType,
                Discount = sale.Discount,
                DiscountType = sale.DiscountPercent.HasValue ? "percentage" : "fixed",
                FiscalStamp = sale.FiscalStamp,
                TotalAmount = sale.GrandTotal > 0 ? sale.GrandTotal : sale.TotalAmount,
                Status = sale.Status,
                Stage = sale.Stage,
                Priority = sale.Priority,
                BillingAddress = sale.BillingAddress,
                BillingPostalCode = sale.BillingPostalCode,
                BillingCountry = sale.BillingCountry,
                DeliveryAddress = sale.DeliveryAddress,
                DeliveryPostalCode = sale.DeliveryPostalCode,
                DeliveryCountry = sale.DeliveryCountry,
                EstimatedCloseDate = sale.EstimatedCloseDate,
                ActualCloseDate = sale.ActualCloseDate,
                ValidUntil = sale.ValidUntil,
                AssignedTo = sale.AssignedTo,
                AssignedToName = sale.AssignedToName,
                Tags = sale.Tags,
                CreatedAt = sale.CreatedDate,
                UpdatedAt = sale.UpdatedAt ?? sale.CreatedDate,
                CreatedBy = sale.CreatedBy,
                CreatedByName = sale.CreatedByName,
                LastActivity = sale.LastActivity,
                OfferId = sale.OfferId,
                ConvertedFromOfferAt = sale.ConvertedFromOfferAt,
                LostReason = sale.LostReason,
                MaterialsFulfillment = sale.MaterialsFulfillment,
                ServiceOrdersStatus = sale.ServiceOrdersStatus,
                Notes = sale.Notes,
                // Get the first service order ID from items that have been converted
                ConvertedToServiceOrderId = sale.Items?.FirstOrDefault(i => !string.IsNullOrEmpty(i.ServiceOrderId))?.ServiceOrderId,
                Items = sale.Items?.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(MapItemToDto).ToList() ?? new List<SaleItemDto>()
            };
        }

        private SaleItemDto MapItemToDto(SaleItem item)
        {
            return new SaleItemDto
            {
                Id = item.Id,
                SaleId = item.SaleId,
                Type = item.Type ?? "",
                ArticleId = item.ArticleId,
                ItemName = item.ItemName ?? "",
                ItemCode = item.ItemCode,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount ?? 0,
                DiscountType = item.DiscountType ?? "percentage",
                TotalPrice = item.LineTotal,
                InstallationId = item.InstallationId,
                InstallationName = item.InstallationName,
                RequiresServiceOrder = item.RequiresServiceOrder,
                ServiceOrderGenerated = item.ServiceOrderGenerated,
                ServiceOrderId = item.ServiceOrderId,
                FulfillmentStatus = item.FulfillmentStatus,
                DisplayOrder = item.DisplayOrder,
                Currency = item.Currency
            };
        }

        /// <summary>
        /// Recomputes and persists every line total plus the sale header totals
        /// (subtotal / tax / grand total). Called after any write that can change
        /// the money on a sale, so the API never returns zeroed amounts.
        /// </summary>
        private async Task<SaleTotalsCalculator.SaleTotals> RecalculateSaleTotalsAsync(int saleId)
        {
            var sale = await _context.Sales
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale == null)
                return SaleTotalsCalculator.Compute(0, 0, "fixed", 0, "fixed", 0);

            var totals = SaleTotalsCalculator.Apply(sale, sale.Items);
            sale.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return totals;
        }

        /// <summary>
        /// Non-void, non-deleted invoices attached to a sale. Used to lock the
        /// sale's scope once it has been (partially) invoiced, so an invoice can
        /// never silently desync from the order it was generated from.
        /// </summary>
        private async Task<List<MyApi.Modules.Invoices.Models.Invoice>> GetActiveInvoicesForSaleAsync(int saleId)
        {
            return await _context.Set<MyApi.Modules.Invoices.Models.Invoice>()
                .Where(i => !i.IsDeleted && i.SaleId == saleId && i.Status != "void")
                .ToListAsync();
        }

        private async Task GuardSaleNotInvoicedAsync(int saleId, string action)
        {
            // Fix #3: draft invoices must block scope changes too. Invoice lines are
            // value copies taken at generation time and PostAsync never re-derives them
            // from the sale, while CreateDraftFromSaleAsync already treats a drafted
            // item as consumed. Allowing edits while only a draft existed meant the
            // posted invoice could carry stale quantities/prices with no resync path.
            var blocking = await GetActiveInvoicesForSaleAsync(saleId);
            if (blocking.Count > 0)
            {
                var numbers = string.Join(", ", blocking.Select(i =>
                    (i.InvoiceNumber ?? $"#{i.Id}") + (i.Status == "draft" ? " (draft)" : "")));
                var hasDraftOnly = blocking.All(i => i.Status == "draft");
                throw new InvalidOperationException(
                    $"Cannot {action}: this sale is already invoiced ({numbers}). " +
                    (hasDraftOnly
                        ? "Delete or void the draft invoice first, then adjust the sale and regenerate it."
                        : "Void the invoice first, then adjust the sale."));
            }
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
    }
}
