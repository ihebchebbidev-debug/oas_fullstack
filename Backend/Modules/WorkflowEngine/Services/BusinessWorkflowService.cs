using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.Models;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.ServiceOrders.Models;
using MyApi.Modules.Dispatches.Models;
using MyApi.Modules.Offers.Models;
using MyApi.Modules.Contacts.Models;
using System.Text.Json;
using MyApi.Modules.ServiceOrders.Services;
using MyApi.Modules.Settings.Services;


namespace MyApi.Modules.WorkflowEngine.Services
{
    public partial class BusinessWorkflowService : IBusinessWorkflowService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<BusinessWorkflowService> _logger;
        private readonly IWorkflowTriggerService _triggerService;
        private readonly IWorkflowNotificationService _notificationService;
        private readonly MyApi.Modules.Numbering.Services.INumberingService? _numberingService;
        private readonly MyApi.Modules.Planning.Services.IPlannedLineEntryService? _plannedEntries;
        private readonly IAppSettingsService? _appSettingsService;
        private readonly MyApi.Modules.Shared.Services.IEntityFormDocumentService? _formDocuments;

        public BusinessWorkflowService(
            ApplicationDbContext db,
            ILogger<BusinessWorkflowService> logger,
            IWorkflowTriggerService triggerService,
            IWorkflowNotificationService notificationService,
            MyApi.Modules.Numbering.Services.INumberingService? numberingService = null,
            MyApi.Modules.Planning.Services.IPlannedLineEntryService? plannedEntries = null,
            IAppSettingsService? appSettingsService = null,
            MyApi.Modules.Shared.Services.IEntityFormDocumentService? formDocuments = null)
        {
            _db = db;
            _logger = logger;
            _triggerService = triggerService;
            _notificationService = notificationService;
            _numberingService = numberingService;
            _plannedEntries = plannedEntries;
            _appSettingsService = appSettingsService;
            _formDocuments = formDocuments;
        }


        /// <summary>
        /// Offer accepted → Create Sale automatically
        /// </summary>
        public async Task<int?> HandleOfferAcceptedAsync(int offerId, string? userId = null)
        {
            _logger.LogInformation("HandleOfferAccepted: Offer {OfferId} accepted, creating sale...", offerId);

            try
            {
                // Get the offer with items
                var offer = await _db.Offers
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == offerId);

                if (offer == null)
                {
                    _logger.LogWarning("HandleOfferAccepted: Offer {OfferId} not found", offerId);
                    return null;
                }

                // Check if a sale already exists for this offer (idempotency)
                var existingSale = await _db.Sales.FirstOrDefaultAsync(s => s.OfferId == offerId.ToString());
                if (existingSale != null)
                {
                    _logger.LogInformation("HandleOfferAccepted: Sale already exists for offer {OfferId}", offerId);
                    return existingSale.Id;
                }

                // Get contact for geolocation data
                var contact = await _db.Contacts.FindAsync(offer.ContactId);

                // Resolve user name for CreatedByName
                string createdByName = userId ?? "system";
                if (!string.IsNullOrEmpty(userId) && userId != "system")
                {
                    var adminUser = await _db.MainAdminUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                    if (adminUser != null)
                    {
                        createdByName = $"{adminUser.FirstName} {adminUser.LastName}".Trim();
                    }
                    else
                    {
                        var regularUser = await _db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                        if (regularUser != null)
                        {
                            createdByName = $"{regularUser.FirstName} {regularUser.LastName}".Trim();
                        }
                    }
                }

                // Create the sale with ALL offer fields (matching ConvertOfferAsync/CreateSaleFromOfferAsync)
                string saleNum;
                try
                {
                    saleNum = _numberingService != null ? await _numberingService.GetNextAsync("Sale") : $"SALE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                }
                catch
                {
                    saleNum = $"SALE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                }

                var sale = new Sale
                {
                    SaleNumber = saleNum,
                    Title = offer.Title,
                    Description = offer.Description,
                    ContactId = offer.ContactId,
                    OfferId = offerId.ToString(),
                    Status = "created",
                    Stage = "offer",
                    Priority = "medium",
                    Currency = offer.Currency ?? "TND",
                    TotalAmount = offer.TotalAmount,
                    TaxAmount = offer.TaxAmount,
                    GrandTotal = offer.GrandTotal,
                    Taxes = offer.Taxes ?? 0,
                    TaxType = offer.TaxType ?? "percentage",
                    Discount = offer.Discount ?? 0,
                    FiscalStamp = offer.FiscalStamp ?? 1.000m,
                    BillingAddress = offer.BillingAddress,
                    BillingPostalCode = offer.BillingPostalCode,
                    BillingCountry = offer.BillingCountry,
                    DeliveryAddress = offer.DeliveryAddress,
                    DeliveryPostalCode = offer.DeliveryPostalCode,
                    DeliveryCountry = offer.DeliveryCountry,
                    AssignedTo = offer.AssignedTo,
                    AssignedToName = offer.AssignedToName,
                    Tags = offer.Tags != null ? offer.Tags.Concat(new[] { "Converted" }).ToArray() : new[] { "Converted" },
                    ConvertedFromOfferAt = DateTime.UtcNow,
                    SaleDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId ?? "system",
                    CreatedByName = createdByName,
                    UpdatedAt = DateTime.UtcNow,
                    // Copy contact geolocation
                    ContactLatitude = contact?.Latitude ?? offer.ContactLatitude,
                    ContactLongitude = contact?.Longitude ?? offer.ContactLongitude,
                    ContactHasLocation = contact?.HasLocation ?? offer.ContactHasLocation
                };

                // Wrap in execution strategy to be compatible with EnableRetryOnFailure
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync();
                    try
                    {
                    _db.Sales.Add(sale);
                    await _db.SaveChangesAsync();

                    // Copy offer items to sale items with all fields
                    var offerItemToSaleItem = new List<(int OfferItemId, SaleItem SaleItem)>();
                    if (offer.Items != null && offer.Items.Any())
                    {
                        foreach (var offerItem in offer.Items)
                        {
                            var saleItem = new SaleItem
                            {
                                SaleId = sale.Id,
                                // Phase A (A2): FK lineage back to the OfferItem so planning
                                // copy / invoicing rollups never need positional pairing.
                                OriginOfferItemId = offerItem.Id,
                                ArticleId = offerItem.ArticleId,
                                Description = offerItem.Description ?? offerItem.ItemName ?? "Item",
                                Quantity = offerItem.Quantity,
                                UnitPrice = offerItem.UnitPrice,
                                Discount = offerItem.Discount,
                                TaxRate = offerItem.TaxRate,
                                LineTotal = offerItem.LineTotal,
                                DisplayOrder = offerItem.DisplayOrder,
                                Type = offerItem.Type,
                                ItemName = offerItem.ItemName,
                                ItemCode = offerItem.ItemCode,
                                DiscountType = offerItem.DiscountType ?? "percentage",
                                InstallationId = offerItem.InstallationId,
                                InstallationName = offerItem.InstallationName,
                                RequiresServiceOrder = offerItem.Type == "service",
                                FulfillmentStatus = "pending",
                                Currency = sale.Currency
                            };
                            _db.SaleItems.Add(saleItem);
                            offerItemToSaleItem.Add((offerItem.Id, saleItem));
                        }
                        await _db.SaveChangesAsync();

                        // Carry planned time/expenses from offer items → sale items (Stage 2).
                        // Propagate failures: a missing plan is a data-integrity bug, not a warning.
                        // The outer catch will mark the workflow step failed and notify the user.
                        if (_plannedEntries != null || _formDocuments != null)
                        {
                            foreach (var (offerItemId, saleItem) in offerItemToSaleItem)
                            {
                                if (_plannedEntries != null)
                                    await _plannedEntries.CopyAsync("offer_item", offerItemId, "sale_item", saleItem.Id, userId ?? "system");
                                if (_formDocuments != null)
                                    await _formDocuments.CopyItemDocumentsAsync("offer_item", offerItemId, "sale_item", saleItem.Id, userId ?? "system");
                            }
                        }
                    }

                    // Update offer status
                    offer.Status = "accepted";
                    offer.ConvertedToSaleId = sale.Id.ToString();
                    offer.ConvertedAt = DateTime.UtcNow;
                    offer.UpdatedAt = DateTime.UtcNow;

                    // Stage activities alongside the final state update — one atomic commit
                    _db.SaleActivities.Add(new SaleActivity
                    {
                        SaleId = sale.Id,
                        Type = "created",
                        Description = $"Sale created from accepted offer #{offerId}",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    });
                    _db.OfferActivities.Add(new OfferActivity
                    {
                        OfferId = offerId,
                        Type = "converted",
                        Description = $"Offer converted to sale #{sale.Id}",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    });

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                    }
                    catch
                    {
                        await tx.RollbackAsync();
                        throw;
                    }
                });

                // Log workflow note
                await LogWorkflowNoteAsync("sale", sale.Id, null, "created", userId, 
                    $"Auto-created from accepted offer #{offerId}");

                _logger.LogInformation("HandleOfferAccepted: Created sale {SaleId} from offer {OfferId}", 
                    sale.Id, offerId);

                return sale.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleOfferAccepted: Error creating sale from offer {OfferId}", offerId);
                throw;
            }
        }

        /// <summary>
        /// Sale in progress → Create Service Order if sale has service items
        /// </summary>
        public async Task<int?> HandleSaleInProgressAsync(int saleId, string? userId = null, object? serviceOrderConfig = null)
        {
            _logger.LogInformation("HandleSaleInProgress: Sale {SaleId} in progress, checking for services...", saleId);

            try
            {
                var sale = await _db.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == saleId);

                if (sale == null)
                {
                    _logger.LogWarning("HandleSaleInProgress: Sale {SaleId} not found", saleId);
                    return null;
                }

                // Check if sale has service items
                var serviceItems = sale.Items?.Where(i => i.Type == "service").ToList() ?? new List<SaleItem>();
                if (!serviceItems.Any())
                {
                    _logger.LogInformation("HandleSaleInProgress: Sale {SaleId} has no service items, skipping", saleId);
                    return null;
                }

                // Check if service order already exists
                var existingServiceOrder = await _db.ServiceOrders.FirstOrDefaultAsync(so => so.SaleId == saleId.ToString());
                if (existingServiceOrder != null)
                {
                    _logger.LogInformation("HandleSaleInProgress: Service order already exists for sale {SaleId}", saleId);
                    return existingServiceOrder.Id;
                }

                // Parse service order configuration if provided
                string priority = "medium";
                string? notes = null;
                DateTime? startDate = null;
                DateTime? targetDate = null;
                
                if (serviceOrderConfig != null)
                {
                    try
                    {
                        // Try to extract config values from the object (could be Dictionary or JSON element)
                        var configJson = System.Text.Json.JsonSerializer.Serialize(serviceOrderConfig);
                        var configDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(configJson);
                        
                        if (configDict != null)
                        {
                            if (configDict.TryGetValue("priority", out var priorityVal) || configDict.TryGetValue("Priority", out priorityVal))
                                priority = priorityVal.GetString() ?? "medium";
                            if (configDict.TryGetValue("notes", out var notesVal) || configDict.TryGetValue("Notes", out notesVal))
                                notes = notesVal.GetString();
                            if (configDict.TryGetValue("startDate", out var startVal) || configDict.TryGetValue("StartDate", out startVal))
                            {
                                var dateStr = startVal.GetString();
                                if (DateTime.TryParse(dateStr, out var parsed))
                                    startDate = DateTime.SpecifyKind(parsed.Kind == DateTimeKind.Unspecified ? parsed : parsed.ToUniversalTime(), DateTimeKind.Utc);
                            }
                            if (configDict.TryGetValue("targetCompletionDate", out var targetVal) || configDict.TryGetValue("TargetCompletionDate", out targetVal))
                            {
                                var dateStr = targetVal.GetString();
                                if (DateTime.TryParse(dateStr, out var parsed))
                                    targetDate = DateTime.SpecifyKind(parsed.Kind == DateTimeKind.Unspecified ? parsed : parsed.ToUniversalTime(), DateTimeKind.Utc);
                            }
                        }
                        _logger.LogInformation("HandleSaleInProgress: Using config - Priority: {Priority}, Notes: {HasNotes}, StartDate: {StartDate}, TargetDate: {TargetDate}", 
                            priority, notes != null, startDate, targetDate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "HandleSaleInProgress: Failed to parse service order config, using defaults");
                    }
                }

                // Everything below writes the service order, its jobs, its materials and the
                // activity trail. It used to run as a series of independent SaveChanges calls:
                // a failure half-way left a header with no jobs behind, and the next retry
                // (workflow re-trigger) created a SECOND service order for the same sale.
                // One transaction + a re-check inside it makes the whole conversion atomic
                // and safe to retry.
                int createdServiceOrderId = 0;
                var soStrategy = _db.Database.CreateExecutionStrategy();
                await soStrategy.ExecuteAsync(async () =>
                {
                createdServiceOrderId = 0;
                var ownsTx = _db.Database.CurrentTransaction == null;
                var soTx = ownsTx ? await _db.Database.BeginTransactionAsync() : null;
                try
                {
                // Re-check inside the transaction: a concurrent trigger may have created it.
                var raceServiceOrder = await _db.ServiceOrders.FirstOrDefaultAsync(so => so.SaleId == saleId.ToString());
                if (raceServiceOrder != null)
                {
                    createdServiceOrderId = raceServiceOrder.Id;
                    if (soTx != null) await soTx.CommitAsync();
                    return;
                }

                // Get contact for geolocation data
                var contact = await _db.Contacts.FindAsync(sale.ContactId);


                // Determine ServiceType from service items
                var serviceType = string.Join(", ", serviceItems
                    .Select(si => si.ItemName ?? si.Description ?? "")
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .Take(3));
                if (string.IsNullOrEmpty(serviceType)) serviceType = "maintenance";

                // Create service order with config values + helper columns + geolocation
                string soNumber;
                try
                {
                    soNumber = _numberingService != null ? await _numberingService.GetNextAsync("ServiceOrder") : $"SO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                }
                catch
                {
                    soNumber = $"SO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                }

                var serviceOrder = new ServiceOrder
                {
                    SaleId = saleId.ToString(),
                    OfferId = sale.OfferId,  // Propagate OfferId from Sale
                    ContactId = sale.ContactId,
                    Status = "pending",  // Initial status after creation - workflow: pending → ready_for_planning → scheduled → in_progress...
                    OrderNumber = soNumber,
                    OrderDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId ?? "system",
                    Priority = priority,
                    ServiceType = serviceType,  // Set ServiceType from sale service items
                    Description = notes ?? $"Service order created from sale #{saleId}",
                    Notes = notes,
                    ScheduledDate = startDate,
                    TargetCompletionDate = targetDate,
                    TotalAmount = serviceItems.Sum(si => si.LineTotal),
                    EstimatedCost = sale.GrandTotal > 0 ? sale.GrandTotal : sale.TotalAmount,
                    // Helper columns
                    ServiceCount = serviceItems.Count,
                    CompletedDispatchCount = 0,
                    // Contact geolocation
                    ContactLatitude = contact?.Latitude ?? sale.ContactLatitude,
                    ContactLongitude = contact?.Longitude ?? sale.ContactLongitude,
                    ContactHasLocation = contact?.HasLocation ?? sale.ContactHasLocation
                };

                _db.ServiceOrders.Add(serviceOrder);
                await _db.SaveChangesAsync();

                // Determine job conversion mode: AppSettings > default "installation".
                // Mirrors ServiceOrderService.CreateFromSaleAsync so manual + workflow
                // paths produce identical SO structures from the same sale.
                string? jobConversionMode = null;
                if (_appSettingsService != null)
                {
                    jobConversionMode = await _appSettingsService.GetSettingAsync("JobConversionMode");
                }
                jobConversionMode ??= "installation";

                // Create service order jobs. In installation mode, sale items sharing
                // the same InstallationId collapse into a single job whose SaleItemId
                // is the CSV of constituent sale-item IDs (matches manual path).
                var jobs = new List<ServiceOrderJob>();
                // (saleItemId, job) pairs used to propagate planned entries — only
                // the primary (first) sale item of each job seeds planned entries
                // to avoid stacking duplicates on installation-grouped jobs.
                var planSeeds = new List<(int SaleItemId, ServiceOrderJob Job)>();
                // Phase A (A1): every sale item that ends up in an (installation-grouped) job
                // must contribute its planned entries. Keep the full list per job, not just the
                // first item, so multi-line installations don't silently lose planned budget.
                var planSeedsAll = new List<(int SaleItemId, ServiceOrderJob Job)>();

                if (jobConversionMode == "installation")
                {
                    var groupedByInstallation = serviceItems
                        .Where(i => !string.IsNullOrEmpty(i.InstallationId))
                        .GroupBy(i => i.InstallationId!)
                        .ToList();
                    var orphanItems = serviceItems
                        .Where(i => string.IsNullOrEmpty(i.InstallationId))
                        .ToList();

                    foreach (var group in groupedByInstallation)
                    {
                        var items = group.ToList();
                        var installationName = items.First().InstallationName ?? $"Installation #{group.Key}";
                        var serviceNames = items.Select(i => i.ItemName ?? "Service").ToList();
                        var totalCost = items.Sum(i => i.LineTotal > 0 ? i.LineTotal : (i.UnitPrice * i.Quantity));
                        var job = new ServiceOrderJob
                        {
                            ServiceOrderId = serviceOrder.Id,
                            SaleItemId = string.Join(",", items.Select(i => i.Id)),
                            Title = installationName,
                            JobDescription = "Services: " + string.Join(", ", serviceNames),
                            Description = string.Join("\n", items.Select(i =>
                                $"- {i.ItemName}: {i.Quantity} x {i.UnitPrice:F2} = {(i.LineTotal > 0 ? i.LineTotal : i.UnitPrice * i.Quantity):F2}")),
                            InstallationId = int.TryParse(group.Key, out var _gkId) ? _gkId : (int?)null,
                            InstallationName = installationName,
                            Status = "unscheduled",
                            EstimatedCost = totalCost,
                            Priority = priority ?? "medium",
                            WorkType = "maintenance",
                            CompletionPercentage = 0
                        };
                        jobs.Add(job);
                        planSeeds.Add((items.First().Id, job));
                        foreach (var it in items) planSeedsAll.Add((it.Id, job));
                        foreach (var item in items)
                        {
                            item.ServiceOrderGenerated = true;
                            item.ServiceOrderId = serviceOrder.Id.ToString();
                        }
                    }

                    foreach (var item in orphanItems)
                    {
                        var job = new ServiceOrderJob
                        {
                            ServiceOrderId = serviceOrder.Id,
                            SaleItemId = item.Id.ToString(),
                            Title = item.ItemName ?? "Service Job",
                            JobDescription = item.Description ?? item.ItemName ?? "Service job from sale",
                            Description = item.Description ?? "",
                            InstallationId = null,
                            InstallationName = null,
                            Status = "unscheduled",
                            EstimatedCost = item.LineTotal,
                            Priority = priority ?? "medium",
                            WorkType = "maintenance",
                            CompletionPercentage = 0
                        };
                        jobs.Add(job);
                        planSeeds.Add((item.Id, job));
                        planSeedsAll.Add((item.Id, job));
                        item.ServiceOrderGenerated = true;
                        item.ServiceOrderId = serviceOrder.Id.ToString();
                    }
                }
                else
                {
                    // SERVICE-BASED: one job per service item (legacy behavior)
                    foreach (var serviceItem in serviceItems)
                    {
                        var job = new ServiceOrderJob
                        {
                            ServiceOrderId = serviceOrder.Id,
                            SaleItemId = serviceItem.Id.ToString(),
                            Title = serviceItem.ItemName ?? serviceItem.Description ?? "Service Job",
                            Description = serviceItem.Description ?? "",
                            JobDescription = serviceItem.Description ?? serviceItem.ItemName ?? "Service job from sale",
                            InstallationId = int.TryParse(serviceItem.InstallationId, out var _siId) ? _siId : (int?)null,
                            InstallationName = serviceItem.InstallationName,
                            Status = "unscheduled",
                            EstimatedCost = serviceItem.LineTotal,
                            Priority = priority ?? "medium",
                            WorkType = "maintenance",
                            CompletionPercentage = 0
                        };
                        jobs.Add(job);
                        planSeeds.Add((serviceItem.Id, job));
                        planSeedsAll.Add((serviceItem.Id, job));
                        serviceItem.ServiceOrderGenerated = true;
                        serviceItem.ServiceOrderId = serviceOrder.Id.ToString();
                    }
                }

                _db.ServiceOrderJobs.AddRange(jobs);
                await _db.SaveChangesAsync();

                // Carry planned time/expenses from primary sale item → service order job.
                // Propagate failures so the workflow step fails visibly rather than
                // silently producing jobs without their planned budget.
                if (_plannedEntries != null || _formDocuments != null)
                {
                    foreach (var (saleItemId, job) in planSeedsAll)
                    {
                        if (_plannedEntries != null)
                            await _plannedEntries.CopyAsync("sale_item", saleItemId, "service_order_job", job.Id, userId ?? "system");
                        // Checklists copy from every sale item; dedup-by-form keeps one per job.
                        if (_formDocuments != null)
                            await _formDocuments.CopyItemDocumentsAsync("sale_item", saleItemId, "service_order_job", job.Id, userId ?? "system");
                    }
                }


                // Copy every NON-service sale item (article / material / product / untyped)
                // to service order materials — mirrors ServiceOrderService.CreateFromSaleAsync.
                var materialItems = sale.Items?
                    .Where(i => !string.Equals(i.Type?.Trim(), "service", StringComparison.OrdinalIgnoreCase))
                    .ToList() ?? new List<SaleItem>();
                if (materialItems.Any())
                {
                    foreach (var materialItem in materialItems)
                    {
                        var soMaterial = new ServiceOrderMaterial
                        {
                            ServiceOrderId = serviceOrder.Id,
                            SaleItemId = materialItem.Id,
                            ArticleId = materialItem.ArticleId,
                            Name = materialItem.ItemName ?? "Material",
                            Sku = materialItem.ItemCode,
                            Description = materialItem.Description,
                            Quantity = materialItem.Quantity,
                            EstimatedQuantity = materialItem.Quantity,
                            UnitPrice = materialItem.UnitPrice,
                            TotalPrice = materialItem.LineTotal > 0
                                ? materialItem.LineTotal
                                : (materialItem.UnitPrice * materialItem.Quantity),
                            Status = "pending",
                            Source = "sale_conversion",
                            InstallationId = int.TryParse(materialItem.InstallationId, out var _miId) ? _miId : (int?)null,
                            InstallationName = materialItem.InstallationName,
                            CreatedBy = userId ?? "system",
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.ServiceOrderMaterials.Add(soMaterial);
                    }
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("HandleSaleInProgress: Copied {Count} material(s) to service order {ServiceOrderId}", 
                        materialItems.Count, serviceOrder.Id);
                }

                var saleActivity = new SaleActivity
                {
                    SaleId = saleId,
                    Type = "service_order_created",
                    Description = $"Service order #{serviceOrder.OrderNumber} created with {serviceItems.Count} job(s)",
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = sale.AssignedToName ?? "System"
                };
                _db.SaleActivities.Add(saleActivity);

                // Propagate to Offer if sale came from an offer
                if (!string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                {
                    var offerActivity = new OfferActivity
                    {
                        OfferId = offerId,
                        Type = "service_order_created",
                        Description = $"Service order #{serviceOrder.OrderNumber} created from sale #{saleId}",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    };
                    _db.OfferActivities.Add(offerActivity);
                }
                
                await _db.SaveChangesAsync();

                // Log workflow note
                await LogWorkflowNoteAsync("service_order", serviceOrder.Id, null, "pending", userId,
                    $"Auto-created from sale #{saleId} with {serviceItems.Count} service(s)");

                if (soTx != null) await soTx.CommitAsync();
                createdServiceOrderId = serviceOrder.Id;

                _logger.LogInformation("HandleSaleInProgress: Created service order {ServiceOrderId} from sale {SaleId}", 
                    serviceOrder.Id, saleId);
                }
                catch
                {
                    if (soTx != null) await soTx.RollbackAsync();
                    throw;
                }
                finally
                {
                    if (soTx != null) await soTx.DisposeAsync();
                }
                });

                return createdServiceOrderId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleSaleInProgress: Error creating service order from sale {SaleId}", saleId);
                throw;
            }
        }

        /// <summary>
        /// Service Order scheduled → Create Dispatches for each service
        /// </summary>
        public async Task<IEnumerable<int>> HandleServiceOrderScheduledAsync(int serviceOrderId, string? userId = null)
        {
            _logger.LogInformation("HandleServiceOrderScheduled: Service order {ServiceOrderId} scheduled, creating dispatches...", 
                serviceOrderId);

            var dispatchIds = new List<int>();

            try
            {
                var serviceOrder = await _db.ServiceOrders
                    .Include(so => so.Jobs)
                    .FirstOrDefaultAsync(so => so.Id == serviceOrderId);

                if (serviceOrder == null)
                {
                    _logger.LogWarning("HandleServiceOrderScheduled: Service order {ServiceOrderId} not found", serviceOrderId);
                    return dispatchIds;
                }

                // Check for existing dispatches
                var existingDispatches = await _db.Dispatches
                    .Where(d => d.ServiceOrderId == serviceOrderId)
                    .ToListAsync();

                if (existingDispatches.Any())
                {
                    _logger.LogInformation("HandleServiceOrderScheduled: Dispatches already exist for service order {ServiceOrderId}", 
                        serviceOrderId);
                    return existingDispatches.Select(d => d.Id);
                }

                var jobs = serviceOrder.Jobs?.ToList() ?? new List<ServiceOrderJob>();
                if (!jobs.Any())
                {
                    // Create at least one dispatch for the service order
                    jobs = new List<ServiceOrderJob> { new ServiceOrderJob { Title = "Default Service" } };
                }

                foreach (var job in jobs)
                {
                    var dispatch = new Dispatch
                    {
                        ServiceOrderId = serviceOrderId,
                        ContactId = serviceOrder.ContactId,
                        JobId = job.Id > 0 ? job.Id.ToString() : null, // Link dispatch to its job for CompletedDispatchCount tracking
                        Status = "assigned",
                        ScheduledDate = serviceOrder.ScheduledDate ?? DateTime.UtcNow.AddDays(1),
                        DispatchNumber = $"DSP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                        Description = job.Title ?? job.Description,
                        SiteAddress = "To be confirmed",
                        Priority = serviceOrder.Priority,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = userId ?? "system"
                    };

                    _db.Dispatches.Add(dispatch);
                    await _db.SaveChangesAsync();

                    dispatchIds.Add(dispatch.Id);

                    // Log workflow note
                    await LogWorkflowNoteAsync("dispatch", dispatch.Id, null, "assigned", userId,
                        $"Auto-created from service order #{serviceOrderId}, job: {job.Title}");
                }

                // Upward propagation: Create SaleActivity for dispatches creation
                if (!string.IsNullOrEmpty(serviceOrder.SaleId) && int.TryParse(serviceOrder.SaleId, out int saleId))
                {
                    var sale = await _db.Sales.FindAsync(saleId);
                    var saleActivity = new SaleActivity
                    {
                        SaleId = saleId,
                        Type = "dispatches_created",
                        Description = $"{dispatchIds.Count} dispatch(es) created for service order #{serviceOrder.OrderNumber}",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale?.AssignedToName ?? "System"
                    };
                    _db.SaleActivities.Add(saleActivity);

                    // Propagate to Offer if sale came from an offer
                    if (sale != null && !string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                    {
                        var offerActivity = new OfferActivity
                        {
                            OfferId = offerId,
                            Type = "dispatches_created",
                            Description = $"{dispatchIds.Count} dispatch(es) created for service order #{serviceOrder.OrderNumber}",
                            CreatedAt = DateTime.UtcNow,
                            CreatedByName = sale.AssignedToName ?? "System"
                        };
                        _db.OfferActivities.Add(offerActivity);
                    }
                    await _db.SaveChangesAsync();
                }

                _logger.LogInformation("HandleServiceOrderScheduled: Created {Count} dispatches for service order {ServiceOrderId}", 
                    dispatchIds.Count, serviceOrderId);

                return dispatchIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleServiceOrderScheduled: Error creating dispatches for service order {ServiceOrderId}", 
                    serviceOrderId);
                throw;
            }
        }

        /// <summary>
        /// Dispatch in progress → Service Order in progress (if not already)
        /// </summary>
        public async Task HandleDispatchInProgressAsync(int dispatchId, string? userId = null)
        {
            _logger.LogInformation("HandleDispatchInProgress: Dispatch {DispatchId} in progress...", dispatchId);

            try
            {
                var dispatch = await _db.Dispatches
                    .FirstOrDefaultAsync(d => d.Id == dispatchId);

                if (dispatch == null || dispatch.ServiceOrderId == null)
                {
                    _logger.LogWarning("HandleDispatchInProgress: Dispatch {DispatchId} or its service order not found", dispatchId);
                    return;
                }

                var serviceOrder = await _db.ServiceOrders
                    .FirstOrDefaultAsync(so => so.Id == dispatch.ServiceOrderId);

                if (serviceOrder == null)
                {
                    _logger.LogWarning("HandleDispatchInProgress: Service order for dispatch {DispatchId} not found", dispatchId);
                    return;
                }

                // Only update if not already in progress or beyond
                if (serviceOrder.Status == "pending" || serviceOrder.Status == "planned" || serviceOrder.Status == "scheduled")
                {
                    var oldStatus = serviceOrder.Status ?? string.Empty;
                    serviceOrder.Status = "in_progress";
                    serviceOrder.ActualStartDate = DateTime.UtcNow;
                    serviceOrder.ModifiedBy = userId;
                    serviceOrder.ModifiedDate = DateTime.UtcNow;

                    await _db.SaveChangesAsync();

                    // Upward propagation: Create SaleActivity for dispatch in progress
                    if (!string.IsNullOrEmpty(serviceOrder.SaleId) && int.TryParse(serviceOrder.SaleId, out int saleId))
                    {
                        var sale = await _db.Sales.FindAsync(saleId);
                        var saleActivity = new SaleActivity
                        {
                            SaleId = saleId,
                            Type = "dispatch_in_progress",
                            Description = $"Dispatch #{dispatch.DispatchNumber} started - service order now in progress",
                            CreatedAt = DateTime.UtcNow,
                            CreatedByName = sale?.AssignedToName ?? "System"
                        };
                        _db.SaleActivities.Add(saleActivity);

                        // Propagate to Offer if sale came from an offer
                        if (sale != null && !string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                        {
                            var offerActivity = new OfferActivity
                            {
                                OfferId = offerId,
                                Type = "dispatch_in_progress",
                                Description = $"Dispatch #{dispatch.DispatchNumber} started for sale #{saleId}",
                                CreatedAt = DateTime.UtcNow,
                                CreatedByName = sale.AssignedToName ?? "System"
                            };
                            _db.OfferActivities.Add(offerActivity);
                        }
                        await _db.SaveChangesAsync();
                    }

                    // Log workflow note
                    await LogWorkflowNoteAsync("service_order", serviceOrder.Id, oldStatus, "in_progress", userId,
                        $"Status updated when dispatch #{dispatchId} started");

                    // Trigger any registered workflows
                    await _triggerService.TriggerStatusChangeAsync("service_order", serviceOrder.Id, 
                        oldStatus, "in_progress", userId);

                    _logger.LogInformation("HandleDispatchInProgress: Service order {ServiceOrderId} updated to in_progress", 
                        serviceOrder.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleDispatchInProgress: Error updating service order for dispatch {DispatchId}", dispatchId);
                throw;
            }
        }

        /// <summary>
        /// Dispatch technically completed → Evaluate service order status
        /// All dispatches completed → Service Order technically_completed
        /// Some dispatches completed → Service Order partially_completed
        /// </summary>
        public async Task HandleDispatchTechnicallyCompletedAsync(int dispatchId, string? userId = null)
        {
            _logger.LogInformation("HandleDispatchTechnicallyCompleted: Dispatch {DispatchId} technically completed...", dispatchId);

            try
            {
                var dispatch = await _db.Dispatches
                    .FirstOrDefaultAsync(d => d.Id == dispatchId);

                if (dispatch == null || dispatch.ServiceOrderId == null)
                {
                    _logger.LogWarning("HandleDispatchTechnicallyCompleted: Dispatch {DispatchId} or its service order not found", 
                        dispatchId);
                    return;
                }

                var serviceOrderId = dispatch.ServiceOrderId.Value;

                // Get all non-deleted dispatches for this service order and hand the decision to
                // ServiceOrderStatusCalculator — the single source of truth shared with
                // DispatchService and ServiceOrderService, so the three code paths can no longer
                // reach different conclusions about the same dispatch set.
                var dispatchStatuses = await _db.Dispatches
                    .Where(d => d.ServiceOrderId == serviceOrderId && !d.IsDeleted)
                    .Select(d => d.Status)
                    .ToListAsync();

                var serviceOrder = await _db.ServiceOrders
                    .FirstOrDefaultAsync(so => so.Id == serviceOrderId);

                if (serviceOrder == null)
                {
                    _logger.LogWarning("HandleDispatchTechnicallyCompleted: Service order {ServiceOrderId} not found", serviceOrderId);
                    return;
                }

                var oldStatus = serviceOrder.Status ?? string.Empty;
                var evaluation = ServiceOrderStatusCalculator.Compute(oldStatus, dispatchStatuses);
                var completedCount = evaluation.CompletedDispatchCount;
                var totalCount = evaluation.ActiveDispatchCount;
                var newStatus = evaluation.Status;

                // Once the order is invoiced/closed/cancelled a late dispatch completion may only
                // refresh the counter, never reopen the order.
                if (evaluation.IsTerminal)
                {
                    if (serviceOrder.CompletedDispatchCount != completedCount)
                    {
                        serviceOrder.CompletedDispatchCount = completedCount;
                        await _db.SaveChangesAsync();
                    }
                    _logger.LogInformation("HandleDispatchTechnicallyCompleted: Service order {ServiceOrderId} is terminal ('{Status}'), only counter refreshed",
                        serviceOrderId, oldStatus);
                    return;
                }

                _logger.LogInformation(
                    "HandleDispatchTechnicallyCompleted: {Completed}/{Total} live dispatches completed → service order status '{Old}' → '{New}'",
                    completedCount, totalCount, oldStatus, newStatus);


                // Keep the counter accurate even when the status itself does not move
                // (e.g. the 3rd of 5 dispatches completes while already "partially_completed").
                if (serviceOrder.CompletedDispatchCount != completedCount)
                {
                    serviceOrder.CompletedDispatchCount = completedCount;
                    await _db.SaveChangesAsync();
                }

                if (oldStatus != newStatus)
                {
                    serviceOrder.Status = newStatus;
                    serviceOrder.ModifiedBy = userId;
                    serviceOrder.ModifiedDate = DateTime.UtcNow;
                    serviceOrder.CompletedDispatchCount = completedCount;


                    if (newStatus == ServiceOrderStatusCalculator.FieldWorkCompleteStatus)
                    {
                        serviceOrder.ActualCompletionDate ??= DateTime.UtcNow;
                        serviceOrder.TechnicallyCompletedAt ??= DateTime.UtcNow;
                    }

                    await _db.SaveChangesAsync();

                    // Upward propagation: Create SaleActivity for dispatch completion
                    if (!string.IsNullOrEmpty(serviceOrder.SaleId) && int.TryParse(serviceOrder.SaleId, out int saleId))
                    {
                        var sale = await _db.Sales.FindAsync(saleId);
                        var isFullyCompleted = newStatus == ServiceOrderStatusCalculator.FieldWorkCompleteStatus;
                        var statusText = isFullyCompleted
                            ? "All dispatches completed - service order technically completed, awaiting invoice review"
                            : $"Dispatch #{dispatch.DispatchNumber} completed ({completedCount}/{totalCount})";

                        
                        var saleActivity = new SaleActivity
                        {
                            SaleId = saleId,
                            Type = isFullyCompleted ? "service_order_completed" : "dispatch_completed",
                            Description = statusText,
                            CreatedAt = DateTime.UtcNow,
                            CreatedByName = sale?.AssignedToName ?? "System"
                        };
                        _db.SaleActivities.Add(saleActivity);

                        // Propagate to Offer if sale came from an offer
                        if (sale != null && !string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                        {
                            var offerActivity = new OfferActivity
                            {
                                OfferId = offerId,
                                Type = isFullyCompleted ? "service_order_completed" : "dispatch_completed",
                                Description = statusText,
                                CreatedAt = DateTime.UtcNow,
                                CreatedByName = sale.AssignedToName ?? "System"
                            };
                            _db.OfferActivities.Add(offerActivity);
                        }
                        await _db.SaveChangesAsync();
                    }

                    // Log workflow note
                    await LogWorkflowNoteAsync("service_order", serviceOrder.Id, oldStatus, newStatus, userId,
                        $"Status updated: {completedCount}/{totalCount} dispatches completed");

                    // Trigger any registered workflows
                    await _triggerService.TriggerStatusChangeAsync("service_order", serviceOrder.Id, 
                        oldStatus, newStatus, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleDispatchTechnicallyCompleted: Error evaluating service order for dispatch {DispatchId}", 
                    dispatchId);
                throw;
            }
        }

        /// <summary>
        /// Get workflow status summary for an entity
        /// </summary>
        public async Task<WorkflowStatusSummary> GetWorkflowStatusSummaryAsync(string entityType, int entityId)
        {
            var summary = new WorkflowStatusSummary
            {
                EntityType = entityType,
                EntityId = entityId
            };

            try
            {
                // Get notes from workflow execution logs
                var executionLogs = await _db.WorkflowExecutionLogs
                    .Include(l => l.Execution)
                    .Where(l => l.Execution != null && l.Execution.TriggerEntityType == entityType && l.Execution.TriggerEntityId == entityId)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(20)
                    .ToListAsync();

                foreach (var log in executionLogs)
                {
                    try
                    {
                        var input = JsonSerializer.Deserialize<Dictionary<string, object>>(log.Input ?? "{}");
                        var output = JsonSerializer.Deserialize<Dictionary<string, object>>(log.Output ?? "{}");

                        summary.Notes.Add(new WorkflowStatusNote
                        {
                            Timestamp = log.Timestamp,
                            FromStatus = input?.GetValueOrDefault("oldStatus")?.ToString() ?? "",
                            ToStatus = input?.GetValueOrDefault("newStatus")?.ToString() ?? "",
                            ChangedBy = log.Execution?.TriggeredBy,
                            Reason = output?.GetValueOrDefault("reason")?.ToString()
                        });
                    }
                    catch
                    {
                        // Skip malformed logs
                    }
                }

                // Get related entities based on entity type
                switch (entityType)
                {
                    case "service_order":
                        var dispatches = await _db.Dispatches
                            .Where(d => d.ServiceOrderId == entityId)
                            .Select(d => new RelatedEntityStatus
                            {
                                EntityType = "dispatch",
                                EntityId = d.Id,
                                Status = d.Status,
                                Relationship = "child"
                            })
                            .ToListAsync();
                        summary.RelatedEntities.AddRange(dispatches);
                        break;

                    case "dispatch":
                        var dispatch = await _db.Dispatches.FirstOrDefaultAsync(d => d.Id == entityId);
                        if (dispatch != null && dispatch.ServiceOrderId.HasValue)
                        {
                            var parentSO = await _db.ServiceOrders.FirstOrDefaultAsync(so => so.Id == dispatch.ServiceOrderId);
                            if (parentSO != null)
                            {
                                summary.RelatedEntities.Add(new RelatedEntityStatus
                                {
                                    EntityType = "service_order",
                                    EntityId = parentSO.Id,
                                    Status = parentSO.Status,
                                    Relationship = "parent"
                                });

                                // Add sibling dispatches
                                var siblings = await _db.Dispatches
                                    .Where(d => d.ServiceOrderId == dispatch.ServiceOrderId && d.Id != entityId)
                                    .Select(d => new RelatedEntityStatus
                                    {
                                        EntityType = "dispatch",
                                        EntityId = d.Id,
                                        Status = d.Status,
                                        Relationship = "sibling"
                                    })
                                    .ToListAsync();
                                summary.RelatedEntities.AddRange(siblings);
                            }
                        }
                        break;

                    case "sale":
                        var saleServiceOrders = await _db.ServiceOrders
                            .Where(so => so.SaleId == entityId.ToString())
                            .Select(so => new RelatedEntityStatus
                            {
                                EntityType = "service_order",
                                EntityId = so.Id,
                                Status = so.Status,
                                Relationship = "child"
                            })
                            .ToListAsync();
                        summary.RelatedEntities.AddRange(saleServiceOrders);
                        break;

                    case "offer":
                        var offerSales = await _db.Sales
                            .Where(s => s.OfferId == entityId.ToString())
                            .Select(s => new RelatedEntityStatus
                            {
                                EntityType = "sale",
                                EntityId = s.Id,
                                Status = s.Status,
                                Relationship = "child"
                            })
                            .ToListAsync();
                        summary.RelatedEntities.AddRange(offerSales);
                        break;
                }

                // Get current status
                summary.CurrentStatus = await GetEntityStatusAsync(entityType, entityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetWorkflowStatusSummary: Error getting summary for {EntityType} {EntityId}", 
                    entityType, entityId);
            }

            return summary;
        }

        private async Task<string> GetEntityStatusAsync(string entityType, int entityId)
        {
            return entityType switch
            {
                "offer" => (await _db.Offers.FindAsync(entityId))?.Status ?? "unknown",
                "sale" => (await _db.Sales.FindAsync(entityId))?.Status ?? "unknown",
                "service_order" => (await _db.ServiceOrders.FindAsync(entityId))?.Status ?? "unknown",
                "dispatch" => (await _db.Dispatches.FindAsync(entityId))?.Status ?? "unknown",
                _ => "unknown"
            };
        }

        private Task LogWorkflowNoteAsync(string entityType, int entityId, string? fromStatus, string toStatus, 
            string? userId, string reason)
        {
            try
            {
                // This can be expanded to write to a dedicated workflow notes table
                _logger.LogInformation(
                    "WorkflowNote: {EntityType}#{EntityId} status changed from '{FromStatus}' to '{ToStatus}' by {UserId}. Reason: {Reason}",
                    entityType, entityId, fromStatus ?? "(none)", toStatus, userId ?? "system", reason);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log workflow note for {EntityType}#{EntityId}", entityType, entityId);
            }
            return Task.CompletedTask;
        }
    }
}
