using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.ServiceOrders.DTOs;
using MyApi.Modules.ServiceOrders.Models;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Dispatches.DTOs;
using MyApi.Modules.Dispatches.Models;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.WorkflowEngine.Services;
using MyApi.Modules.Settings.Services;

namespace MyApi.Modules.ServiceOrders.Services
{
    public class ServiceOrderService : IServiceOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServiceOrderService> _logger;
        private readonly IWorkflowTriggerService? _workflowTriggerService;
        private readonly MyApi.Modules.Numbering.Services.INumberingService? _numberingService;
        private readonly IAppSettingsService? _appSettingsService;
        private readonly MyApi.Modules.Planning.Services.IPlannedLineEntryService? _plannedEntries;
        private readonly MyApi.Modules.Shared.Services.IEntityFormDocumentService? _formDocuments;
        private readonly MyApi.Modules.Invoices.Services.IInvoiceService? _invoiceService;
        private readonly MyApi.Modules.Contacts.Services.IContactActivityService? _contactActivity;

        public ServiceOrderService(
            ApplicationDbContext context,
            ILogger<ServiceOrderService> logger,
            IWorkflowTriggerService? workflowTriggerService = null,
            MyApi.Modules.Numbering.Services.INumberingService? numberingService = null,
            IAppSettingsService? appSettingsService = null,
            MyApi.Modules.Planning.Services.IPlannedLineEntryService? plannedEntries = null,
            MyApi.Modules.Shared.Services.IEntityFormDocumentService? formDocuments = null,
            MyApi.Modules.Invoices.Services.IInvoiceService? invoiceService = null,
            MyApi.Modules.Contacts.Services.IContactActivityService? contactActivity = null)
        {
            _context = context;
            _logger = logger;
            _workflowTriggerService = workflowTriggerService;
            _numberingService = numberingService;
            _appSettingsService = appSettingsService;
            _plannedEntries = plannedEntries;
            _formDocuments = formDocuments;
            _invoiceService = invoiceService;
            _contactActivity = contactActivity;
        }

        // Phase A (A6): single formula for per-job estimated duration.
        // Denominator = number of jobs actually created (never a mix of items + orphans).
        private static int? AverageDurationPerJob(DateTime? start, DateTime? end, int jobCount)
        {
            if (!start.HasValue || !end.HasValue || jobCount <= 0) return null;
            return (int)(end.Value - start.Value).TotalHours / jobCount;
        }

        // =====================================================================
        // DIRECT CREATION (no Offer / Sale parent)
        // =====================================================================

        public async Task<ServiceOrderDto> CreateDirectAsync(CreateDirectServiceOrderDto createDto, string userId)
        {
            // --- validation -------------------------------------------------
            if (createDto == null)
                throw new ArgumentNullException(nameof(createDto));
            if (createDto.ContactId <= 0)
                throw new ArgumentException("ContactId is required", nameof(createDto));

            var contact = await _context.Contacts.FindAsync(createDto.ContactId);
            if (contact == null)
                throw new KeyNotFoundException($"Contact with ID {createDto.ContactId} not found");

            // --- number -----------------------------------------------------
            string orderNumber;
            try
            {
                orderNumber = _numberingService != null
                    ? await _numberingService.GetNextAsync("ServiceOrder")
                    : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("ServiceOrder");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Numbering service failed for direct ServiceOrder, using GUID fallback");
                orderNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("ServiceOrder");
            }

            // --- materials (optional) --------------------------------------
            var materials = createDto.Materials ?? new List<CreateDirectServiceOrderLineDto>();
            var materialTotal = materials.Sum(m => m.Quantity * m.UnitPrice);

            // --- status decision -------------------------------------------
            // Customer-only       -> draft
            // Any work detail set -> ready_for_planning (so it can be dispatched)
            var hasWorkDetail = createDto.StartDate.HasValue
                                || createDto.TargetCompletionDate.HasValue
                                || (createDto.AssignedTechnicianIds?.Length ?? 0) > 0
                                || (createDto.InstallationIds?.Length ?? 0) > 0
                                || materials.Any();
            var status = hasWorkDetail ? "ready_for_planning" : "draft";

            // --- order ------------------------------------------------------
            var serviceOrder = new ServiceOrder
            {
                OrderNumber = orderNumber,
                Origin = "direct",
                SaleId = null,
                OfferId = null,
                AutoGeneratedSaleId = null,
                ProjectId = createDto.ProjectId,
                ContactId = createDto.ContactId,
                ServiceType = string.IsNullOrWhiteSpace(createDto.ServiceType) ? "maintenance" : createDto.ServiceType,
                Status = status,
                Priority = createDto.Priority ?? "medium",
                Description = createDto.Description,
                Notes = createDto.Notes,
                StartDate = createDto.StartDate.HasValue
                    ? DateTime.SpecifyKind(createDto.StartDate.Value, DateTimeKind.Utc) : null,
                TargetCompletionDate = createDto.TargetCompletionDate.HasValue
                    ? DateTime.SpecifyKind(createDto.TargetCompletionDate.Value, DateTimeKind.Utc) : null,
                EstimatedDuration = createDto.EstimatedDuration
                    ?? (createDto.StartDate.HasValue && createDto.TargetCompletionDate.HasValue
                        ? (int)(createDto.TargetCompletionDate.Value - createDto.StartDate.Value).TotalHours
                        : (int?)null),
                EstimatedCost = createDto.EstimatedCost ?? materialTotal,
                ActualCost = 0,
                Discount = 0,
                DiscountPercentage = 0,
                Tax = 0,
                TotalAmount = createDto.EstimatedCost ?? materialTotal,
                PaymentStatus = "pending",
                PaymentTerms = "net30",
                CompletionPercentage = 0,
                RequiresApproval = createDto.RequiresApproval,
                Tags = createDto.Tags,
                PreferredSkills = createDto.PreferredSkills != null && createDto.PreferredSkills.Length > 0
                    ? createDto.PreferredSkills
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : null,
                CustomFields = createDto.CustomFields != null
                    ? System.Text.Json.JsonSerializer.Serialize(createDto.CustomFields) : null,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                ContactLatitude = contact.Latitude,
                ContactLongitude = contact.Longitude,
                ContactHasLocation = contact.HasLocation,
                ServiceCount = materials.Count
            };

            _context.ServiceOrders.Add(serviceOrder);
            await _context.SaveChangesAsync();

            // --- optional default job (if work detail provided) ------------
            if (hasWorkDetail)
            {
                var defaultJob = new ServiceOrderJob
                {
                    ServiceOrderId = serviceOrder.Id,
                    Title = string.IsNullOrWhiteSpace(createDto.Description) ? "Service work" : createDto.Description!,
                    JobDescription = createDto.Description,
                    Description = createDto.Notes,
                    Status = "unscheduled",
                    Priority = createDto.Priority ?? "medium",
                    EstimatedDuration = createDto.EstimatedDuration,
                    EstimatedCost = materialTotal > 0 ? materialTotal : (createDto.EstimatedCost ?? 0),
                    AssignedTechnicianIds = createDto.AssignedTechnicianIds,
                    InstallationId = (createDto.InstallationIds != null && createDto.InstallationIds.Length > 0
                                        && int.TryParse(createDto.InstallationIds[0], out var _defIid))
                        ? _defIid : (int?)null
                };
                _context.ServiceOrderJobs.Add(defaultJob);
                await _context.SaveChangesAsync();
            }

            // --- optional materials ----------------------------------------
            if (materials.Any())
            {
                var rows = materials.Select(m => new ServiceOrderMaterial
                {
                    ServiceOrderId = serviceOrder.Id,
                    ArticleId = m.ArticleId,
                    Name = m.Name,
                    Sku = m.Sku,
                    Description = m.Description,
                    Quantity = m.Quantity,
                    EstimatedQuantity = m.EstimatedQuantity ?? m.Quantity,
                    UnitPrice = m.UnitPrice,
                    TotalPrice = m.Quantity * m.UnitPrice,
                    Status = "pending",
                    Source = "direct",
                    InternalComment = m.InternalComment,
                    ExternalComment = m.ExternalComment,
                    Unit = string.IsNullOrWhiteSpace(m.Unit) ? "piece" : m.Unit!,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
                _context.ServiceOrderMaterials.AddRange(rows);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Direct service order {OrderNumber} (Id {Id}) created for contact {ContactId} by {UserId}",
                serviceOrder.OrderNumber, serviceOrder.Id, serviceOrder.ContactId, userId);

            if (_contactActivity != null && serviceOrder.ContactId > 0)
            {
                await _contactActivity.LogAsync(
                    contactId: serviceOrder.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.ServiceOrderCreated,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.ServiceOrder,
                    relatedEntityId: serviceOrder.Id,
                    description: $"Service order {serviceOrder.OrderNumber} was created",
                    metadata: new { number = serviceOrder.OrderNumber, status = serviceOrder.Status, serviceType = serviceOrder.ServiceType, priority = serviceOrder.Priority },
                    createdBy: userId);
            }

            var result = await GetServiceOrderByIdAsync(serviceOrder.Id);
            return result!;
        }

        // =====================================================================
        // SHADOW SALE GENERATOR
        // Produces a Sale from a completed direct ServiceOrder so that all
        // downstream invoicing / accounting / reporting flows continue to work.
        // Idempotent: returns the existing Sale Id if AutoGeneratedSaleId is
        // already set on the order.
        // =====================================================================
        private async Task<int> EnsureShadowSaleAsync(ServiceOrder order, string userId)
        {
            if (order.AutoGeneratedSaleId.HasValue)
                return order.AutoGeneratedSaleId.Value;

            var contact = await _context.Contacts.FindAsync(order.ContactId);

            // Load materials to mirror as SaleItems
            var materials = await _context.ServiceOrderMaterials
                .Where(m => m.ServiceOrderId == order.Id)
                .ToListAsync();

            decimal totalAmount = materials.Sum(m => m.TotalPrice);
            if (totalAmount == 0)
                totalAmount = order.ActualCost ?? order.TotalAmount ?? order.EstimatedCost ?? 0;

            string saleNumber;
            try
            {
                saleNumber = _numberingService != null
                    ? await _numberingService.GetNextAsync("Sale")
                    : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Sale");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Numbering service failed for shadow Sale, using GUID fallback");
                saleNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Sale");
            }

            var sale = new Sale
            {
                TenantId = order.TenantId,
                SaleNumber = saleNumber,
                Title = $"Service Order {order.OrderNumber}",
                Description = order.Description,
                ContactId = order.ContactId,
                ProjectId = order.ProjectId,
                Status = "won",
                Stage = "closed",
                Priority = order.Priority,
                Currency = "TND",
                TotalAmount = totalAmount,
                GrandTotal = totalAmount,
                ActualCloseDate = DateTime.UtcNow,
                IsAutoGenerated = true,
                SourceServiceOrderId = order.Id,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Tags = new string[] { "auto-generated", "from-service-order" },
                ContactLatitude = contact?.Latitude ?? order.ContactLatitude,
                ContactLongitude = contact?.Longitude ?? order.ContactLongitude,
                ContactHasLocation = contact?.HasLocation ?? order.ContactHasLocation
            };

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            // Mirror materials as SaleItems
            if (materials.Any())
            {
                var items = materials.Select(m => new SaleItem
                {
                    TenantId = order.TenantId,
                    SaleId = sale.Id,
                    Type = m.ArticleId.HasValue ? "article" : "service",
                    ArticleId = m.ArticleId,
                    ItemName = m.Name,
                    ItemCode = m.Sku,
                    Description = m.Description ?? m.Name,
                    Quantity = m.Quantity,
                    UnitPrice = m.UnitPrice,
                    LineTotal = m.TotalPrice,
                    Discount = 0,
                    DiscountType = "percentage",
                    ServiceOrderGenerated = true,
                    ServiceOrderId = order.Id.ToString(),
                    FulfillmentStatus = "fulfilled",
                    Currency = sale.Currency
                }).ToList();
                _context.SaleItems.AddRange(items);
                await _context.SaveChangesAsync();
            }

            // Back-link the order to the new sale
            order.AutoGeneratedSaleId = sale.Id;
            order.SaleId = sale.Id.ToString();
            await _context.SaveChangesAsync();

            _logger.LogInformation("Shadow Sale {SaleNumber} (Id {SaleId}) generated for direct ServiceOrder {OrderId}",
                sale.SaleNumber, sale.Id, order.Id);

            return sale.Id;
        }

        public async Task<ServiceOrderDto> CreateFromSaleAsync(int saleId, CreateServiceOrderDto createDto, string userId)
        {
            // --- Task 3: idempotent creation. Fast pre-check (outside the retry loop) so
            // repeated clicks return the existing SO instead of throwing. A concurrent race
            // is caught below via DbUpdateException on the unique index.
            var preCheckSaleIdStr = saleId.ToString();
            var preExisting = await _context.ServiceOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SaleId == preCheckSaleIdStr && !s.IsDeleted);
            if (preExisting != null)
            {
                _logger.LogInformation("CreateFromSaleAsync: returning existing ServiceOrder {Id} for Sale {SaleId} (idempotent)", preExisting.Id, saleId);
                var existingDto = await GetServiceOrderByIdAsync(preExisting.Id);
                return existingDto!;
            }

            try
            {
            // Atomic creation: ServiceOrder + jobs + planned entries + materials + sale flags
            // must all succeed together. A mid-flow failure would otherwise leave a SO
            // with jobs but no planned budget, or sale items wrongly marked as converted.
            int createdServiceOrderId = 0;
            // Wrap in execution strategy to be compatible with EnableRetryOnFailure
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Phase A (A7): retry-safe. On a strategy retry, EF may still hold
                // tracked entities from the failed attempt — clear them so we never
                // double-insert or update detached rows.
                _context.ChangeTracker.Clear();
                createdServiceOrderId = 0;
                await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            // Verify sale exists with its items
                var sale = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == saleId);
                if (sale == null)
                    throw new KeyNotFoundException($"Sale with ID {saleId} not found");

                // Re-check inside the serializable transaction (race guard). The DB-level
                // partial unique index (ux_serviceorders_tenant_saleid) is the hard fence.
                var saleIdStr = saleId.ToString();
                var existingOrder = await _context.ServiceOrders.FirstOrDefaultAsync(s => s.SaleId == saleIdStr && !s.IsDeleted);
                if (existingOrder != null)
                {
                    createdServiceOrderId = existingOrder.Id;
                    await tx.CommitAsync();
                    return;
                }

                // Get service-type items from the sale (these become jobs)
                var serviceItems = sale.Items?.Where(i => i.Type?.ToLower() == "service").ToList() ?? new List<Sales.Models.SaleItem>();

                // Get contact for geolocation data
                var contact = await _context.Contacts.FindAsync(sale.ContactId);

                string orderNumber;
                try
                {
                    orderNumber = _numberingService != null
                        ? await _numberingService.GetNextAsync("ServiceOrder")
                        : MyApi.Modules.Numbering.Services.NumberingFallback.Generate("ServiceOrder");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Numbering service failed for ServiceOrder, using GUID fallback");
                    orderNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("ServiceOrder");
                }

                var serviceOrder = new ServiceOrder
                {
                    OrderNumber = orderNumber,
                    SaleId = saleId.ToString(),
                    OfferId = sale.OfferId,
                    ProjectId = createDto.ProjectId ?? sale.ProjectId,
                    ContactId = sale.ContactId,
                    ServiceType = serviceItems.FirstOrDefault()?.ItemName ?? "maintenance",
                    Status = "pending",  // Initial status after creation - workflow: pending → ready_for_planning → scheduled → in_progress...
                    Priority = createDto.Priority ?? "medium",
                    Description = sale.Description,
                    Notes = createDto.Notes ?? sale.Description,
                    StartDate = createDto.StartDate.HasValue ? DateTime.SpecifyKind(createDto.StartDate.Value, DateTimeKind.Utc) : null,
                    TargetCompletionDate = createDto.TargetCompletionDate.HasValue ? DateTime.SpecifyKind(createDto.TargetCompletionDate.Value, DateTimeKind.Utc) : null,
                    EstimatedDuration = createDto.StartDate.HasValue && createDto.TargetCompletionDate.HasValue
                        ? (createDto.TargetCompletionDate.Value < createDto.StartDate.Value
                            ? throw new ArgumentException("TargetCompletionDate must be on or after StartDate.")
                            : (int)(createDto.TargetCompletionDate.Value - createDto.StartDate.Value).TotalHours)
                        : null,
                    // Sale.TotalAmount is the PRE-discount, PRE-tax subtotal; GrandTotal is
                    // what the customer actually owes. Seeding the SO from TotalAmount made
                    // the order value disagree with its own sale whenever a discount, tax or
                    // fiscal stamp existed. Same fallback as line ~968.
                    EstimatedCost = sale.GrandTotal > 0m ? sale.GrandTotal : sale.TotalAmount,
                    ActualCost = 0,
                    Discount = 0,
                    DiscountPercentage = 0,
                    Tax = 0,
                    TotalAmount = sale.GrandTotal > 0m ? sale.GrandTotal : sale.TotalAmount,
                    PaymentStatus = "pending",
                    PaymentTerms = "net30",
                    CompletionPercentage = 0,
                    RequiresApproval = createDto.RequiresApproval,
                    Tags = createDto.Tags,
                    CustomFields = createDto.CustomFields != null ? System.Text.Json.JsonSerializer.Serialize(createDto.CustomFields) : null,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow,
                    // Copy contact geolocation
                    ContactLatitude = contact?.Latitude ?? sale.ContactLatitude,
                    ContactLongitude = contact?.Longitude ?? sale.ContactLongitude,
                    ContactHasLocation = contact?.HasLocation ?? sale.ContactHasLocation
                };

                // Set ServiceCount from the number of service-type sale items
                serviceOrder.ServiceCount = serviceItems.Count;

                _context.ServiceOrders.Add(serviceOrder);
                await _context.SaveChangesAsync();

                // Determine job conversion mode: DTO override > AppSettings > default "installation"
                var jobConversionMode = createDto.JobConversionMode;
                if (string.IsNullOrEmpty(jobConversionMode) && _appSettingsService != null)
                {
                    jobConversionMode = await _appSettingsService.GetSettingAsync("JobConversionMode");
                }
                jobConversionMode ??= "installation";

                // Create jobs from service-type sale items
                if (serviceItems.Any())
                {
                    var jobs = new List<ServiceOrderJob>();

                    // Pre-fetch required skills from each service article so they propagate
                    // to the jobs — the dispatcher uses this for technician matching.
                    var serviceArticleIds = serviceItems
                        .Where(i => i.ArticleId.HasValue)
                        .Select(i => i.ArticleId!.Value)
                        .Distinct()
                        .ToList();
                    var articleSkillsById = new Dictionary<int, string[]>();
                    if (serviceArticleIds.Count > 0)
                    {
                        var articleRows = await _context.Articles
                            .Where(a => serviceArticleIds.Contains(a.Id) && !a.IsDeleted)
                            .Select(a => new { a.Id, a.SkillsRequired })
                            .ToListAsync();
                        foreach (var row in articleRows)
                        {
                            if (string.IsNullOrEmpty(row.SkillsRequired)) continue;
                            try
                            {
                                var parsed = System.Text.Json.JsonSerializer.Deserialize<string[]>(row.SkillsRequired);
                                if (parsed?.Length > 0) articleSkillsById[row.Id] = parsed;
                            }
                            catch { /* skip malformed JSON */ }
                        }
                    }

                    // Seed PreferredSkills on the ServiceOrder from the union of every
                    // service-article's SkillsRequired. Explicit DTO value wins if provided.
                    var seededSkills = articleSkillsById.Values
                        .SelectMany(s => s)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var explicitSkills = createDto.PreferredSkills?
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var finalSkills = (explicitSkills?.Length > 0 ? explicitSkills : seededSkills);
                    if (finalSkills.Length > 0)
                    {
                        serviceOrder.PreferredSkills = finalSkills;
                        await _context.SaveChangesAsync();
                    }

                    if (jobConversionMode == "installation")
                    {
                        // INSTALLATION-BASED: Group service items by InstallationId
                        var groupedByInstallation = serviceItems
                            .Where(i => !string.IsNullOrEmpty(i.InstallationId))
                            .GroupBy(i => i.InstallationId!)
                            .ToList();

                        // Items without installation fall back to service-based
                        var orphanItems = serviceItems
                            .Where(i => string.IsNullOrEmpty(i.InstallationId))
                            .ToList();

                        foreach (var group in groupedByInstallation)
                        {
                            var items = group.ToList();
                            var installationName = items.First().InstallationName ?? $"Installation #{group.Key}";
                            var serviceNames = items.Select(i => i.ItemName ?? "Service").ToList();
                            var totalCost = items.Sum(i => i.LineTotal > 0 ? i.LineTotal : (i.UnitPrice * i.Quantity));

                            jobs.Add(new ServiceOrderJob
                            {
                                ServiceOrderId = serviceOrder.Id,
                                SaleItemId = string.Join(",", items.Select(i => i.Id)),
                                Title = installationName,
                                JobDescription = "Services: " + string.Join(", ", serviceNames),
                                Description = string.Join("\n", items.Select(i => 
                                    $"- {i.ItemName}: {i.Quantity} x {i.UnitPrice:F2} = {(i.LineTotal > 0 ? i.LineTotal : i.UnitPrice * i.Quantity):F2}")),
                                Status = "unscheduled",
                                Priority = createDto.Priority ?? "medium",
                                InstallationId = int.TryParse(group.Key, out var _grpIid) ? _grpIid : (int?)null,
                                InstallationName = installationName,
                                WorkType = DetermineWorkType(items.First().ItemName),
                                EstimatedDuration = AverageDurationPerJob(
                                    createDto.StartDate, createDto.TargetCompletionDate,
                                    groupedByInstallation.Count() + orphanItems.Count()),
                                EstimatedCost = totalCost,
                                CompletionPercentage = 0,
                                AssignedTechnicianIds = createDto.AssignedTechnicianIds?.Select(id => id.ToString()).ToArray(),
                                RequiredSkills = items
                                    .Where(i => i.ArticleId.HasValue && articleSkillsById.ContainsKey(i.ArticleId.Value))
                                    .SelectMany(i => articleSkillsById[i.ArticleId!.Value])
                                    .Distinct()
                                    .ToArray() is { Length: > 0 } gs ? gs : null,
                                Notes = System.Text.Json.JsonSerializer.Serialize(items.Select(i => new {
                                    itemName = i.ItemName,
                                    quantity = i.Quantity,
                                    unitPrice = i.UnitPrice,
                                    lineTotal = i.LineTotal > 0 ? i.LineTotal : i.UnitPrice * i.Quantity
                                })),
                                UpdatedAt = DateTime.UtcNow
                            });
                        }

                        // Orphan items: each becomes its own job (service-based fallback)
                        foreach (var item in orphanItems)
                        {
                            jobs.Add(new ServiceOrderJob
                            {
                                ServiceOrderId = serviceOrder.Id,
                                SaleItemId = item.Id.ToString(),
                                Title = item.ItemName ?? "Service Job",
                                JobDescription = item.Description ?? item.ItemName ?? "Service job",
                                Description = item.Description,
                                Status = "unscheduled",
                                Priority = createDto.Priority ?? "medium",
                                InstallationId = null,
                                InstallationName = null,
                                WorkType = DetermineWorkType(item.ItemName),
                                EstimatedDuration = AverageDurationPerJob(
                                    createDto.StartDate, createDto.TargetCompletionDate,
                                    groupedByInstallation.Count() + orphanItems.Count()),
                                EstimatedCost = item.LineTotal > 0 ? item.LineTotal : (item.UnitPrice * item.Quantity),
                                CompletionPercentage = 0,
                                AssignedTechnicianIds = createDto.AssignedTechnicianIds?.Select(id => id.ToString()).ToArray(),
                                RequiredSkills = item.ArticleId.HasValue && articleSkillsById.TryGetValue(item.ArticleId.Value, out var ors) ? ors : null,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    else
                    {
                        // SERVICE-BASED (current/legacy behavior): Each service item becomes its own job
                        jobs = serviceItems.Select(item => new ServiceOrderJob
                        {
                            ServiceOrderId = serviceOrder.Id,
                            SaleItemId = item.Id.ToString(),
                            Title = item.ItemName ?? "Service Job",
                            JobDescription = item.Description ?? item.ItemName ?? "Service job",
                            Description = item.Description,
                            Status = "unscheduled",
                            Priority = createDto.Priority ?? "medium",
                            InstallationId = int.TryParse(item.InstallationId, out var _sbJobIid) ? _sbJobIid : (int?)null,
                            InstallationName = item.InstallationName,
                            WorkType = DetermineWorkType(item.ItemName),
                            EstimatedDuration = AverageDurationPerJob(
                                createDto.StartDate, createDto.TargetCompletionDate, serviceItems.Count),
                            EstimatedCost = item.LineTotal > 0 ? item.LineTotal : (item.UnitPrice * item.Quantity),
                            CompletionPercentage = 0,
                            AssignedTechnicianIds = createDto.AssignedTechnicianIds?.Select(id => id.ToString()).ToArray(),
                            RequiredSkills = item.ArticleId.HasValue && articleSkillsById.TryGetValue(item.ArticleId.Value, out var sbs) ? sbs : null,
                            UpdatedAt = DateTime.UtcNow
                        }).ToList();
                    }

                    _context.ServiceOrderJobs.AddRange(jobs);
                    await _context.SaveChangesAsync();

                    // Carry planned time/expenses from sale items → service order jobs (Stage 2).
                    // A job may aggregate multiple sale items (installation-grouped); SaleItemId stores "1,2,3".
                    // Phase A (A1): copy planned entries from EVERY source sale item in the group.
                    // Previously only the first item's plans were carried through, which silently
                    // dropped 30–80% of planned budget on installation-grouped sales.
                    // CopyAsync is idempotent (see PlannedLineEntryService), so a strategy retry
                    // does not stack duplicates.
                    // Fail-fast: planning propagation is a hard requirement of the ServiceOrder
                    // creation flow. If DI is misconfigured we must NOT quietly create jobs
                    // without their planned time/expenses — that silently loses budget data.
                    if (_plannedEntries == null)
                    {
                        _logger.LogError("PlannedLineEntryService is not registered — aborting service-order creation to avoid dropping planned time/expenses. Fix DI registration.");
                        throw new InvalidOperationException(
                            "IPlannedLineEntryService is not registered. Register it in Program.cs so planned time/expenses propagate from sale items to service order jobs.");
                    }
                    if (_plannedEntries != null || _formDocuments != null)
                    {
                        foreach (var j in jobs)
                        {
                            if (string.IsNullOrWhiteSpace(j.SaleItemId)) continue;
                            var saleItemIds = j.SaleItemId
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .Select(p => int.TryParse(p, out var n) ? (int?)n : null)
                                .Where(n => n.HasValue)
                                .Select(n => n!.Value)
                                .ToList();
                            if (saleItemIds.Count == 0) continue;

                            if (_plannedEntries != null)
                            {
                                foreach (var sid in saleItemIds)
                                    await _plannedEntries.CopyAsync("sale_item", sid, "service_order_job", j.Id, userId);
                            }

                            // Checklists: copy from EVERY sale item the job aggregates, so each service
                            // article's checklist lands on the job (idempotent — only seeds an empty job).
                            if (_formDocuments != null)
                            {
                                foreach (var saleItemId in saleItemIds)
                                {
                                    await _formDocuments.CopyItemDocumentsAsync("sale_item", saleItemId, "service_order_job", j.Id, userId);
                                }
                            }
                        }
                    }

                    // Phase A: derive per-job EstimatedDuration from planned time budget
                    // (PlannedLineEntry.PlannedMinutes × TechnicianCount) instead of the
                    // start/end span ÷ job count. Fall back to AverageDurationPerJob only
                    // when the job has no planned time entries at all.
                    if (_plannedEntries != null && jobs.Count > 0)
                    {
                        var jobIds = jobs.Select(j => j.Id).ToList();
                        var plannedByJob = await _context.Set<MyApi.Modules.Planning.Models.PlannedLineEntry>()
                            .Where(p => p.ParentType == "service_order_job"
                                     && jobIds.Contains(p.ParentId)
                                     && p.Kind == "time")
                            .GroupBy(p => p.ParentId)
                            .Select(g => new { JobId = g.Key, Minutes = g.Sum(x => (x.PlannedMinutes ?? 0) * (x.TechnicianCount ?? 1)) })
                            .ToListAsync();
                        var minutesByJob = plannedByJob.ToDictionary(x => x.JobId, x => x.Minutes);
                        foreach (var j in jobs)
                        {
                            if (minutesByJob.TryGetValue(j.Id, out var m) && m > 0)
                            {
                                // EstimatedDuration is stored in hours (see AverageDurationPerJob).
                                j.EstimatedDuration = Math.Max(1, m / 60);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }




                    // Update sale items with service order information
                    foreach (var item in serviceItems)
                    {
                        item.ServiceOrderGenerated = true;
                        item.ServiceOrderId = serviceOrder.Id.ToString();
                    }
                    await _context.SaveChangesAsync();
                }

                // Create materials from every NON-service sale item.
                // Frontend uses "article", legacy/imported rows may say "material",
                // "product", "goods" or carry no type at all — all of those are
                // materials and must land in the service order Materials tab.
                var materialItems = sale.Items?
                    .Where(i => !string.Equals(i.Type?.Trim(), "service", StringComparison.OrdinalIgnoreCase))
                    .ToList() ?? new List<Sales.Models.SaleItem>();

                if (materialItems.Any())
                {
                    var materials = materialItems.Select(item => new ServiceOrderMaterial
                    {
                        ServiceOrderId = serviceOrder.Id,
                        SaleItemId = item.Id,
                        ArticleId = item.ArticleId,
                        Name = item.ItemName ?? "Material",
                        Sku = item.ItemCode,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        EstimatedQuantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.LineTotal > 0 ? item.LineTotal : (item.UnitPrice * item.Quantity),
                        Status = "pending",
                        Source = "sale_conversion",
                        InstallationId = int.TryParse(item.InstallationId, out var _matIid) ? _matIid : (int?)null,
                        InstallationName = item.InstallationName,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    _context.ServiceOrderMaterials.AddRange(materials);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Created {Count} materials for service order {ServiceOrderId}", materials.Count, serviceOrder.Id);
                }

                // Update the sale's ServiceOrdersStatus to track the conversion
                sale.ServiceOrdersStatus = "created";
                sale.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
                createdServiceOrderId = serviceOrder.Id;
            });

                var result = await GetServiceOrderByIdAsync(createdServiceOrderId);

                // Log to contact activity feed (best-effort; won't throw)
                if (_contactActivity != null && result != null && result.ContactId > 0)
                {
                    await _contactActivity.LogAsync(
                        contactId: result.ContactId,
                        type: MyApi.Modules.Contacts.Models.ContactActivityTypes.ServiceOrderCreated,
                        relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.ServiceOrder,
                        relatedEntityId: result.Id,
                        description: $"Service order {result.OrderNumber} was created from sale #{saleId}",
                        metadata: new { number = result.OrderNumber, status = result.Status, fromSale = saleId },
                        createdBy: userId);
                }

                return result!;
            }
            catch (DbUpdateException dupEx) when (IsUniqueSaleIdViolation(dupEx))
            {
                // Concurrent race: another request already created the SO. Return it.
                _logger.LogWarning(dupEx, "Duplicate ServiceOrder creation for Sale {SaleId} caught by unique index; returning existing", saleId);
                var saleIdStr = saleId.ToString();
                var existing = await _context.ServiceOrders.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SaleId == saleIdStr && !s.IsDeleted);
                if (existing != null)
                    return (await GetServiceOrderByIdAsync(existing.Id))!;
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service order from sale {SaleId}: {Message}", saleId, ex.Message);
                throw;
            }
        }

        private static bool IsUniqueSaleIdViolation(DbUpdateException ex)
        {
            // Npgsql throws PostgresException with SqlState 23505 for unique_violation.
            var inner = ex.InnerException;
            while (inner != null)
            {
                var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
                if (sqlState == "23505")
                {
                    var msg = inner.Message ?? string.Empty;
                    if (msg.Contains("ux_serviceorders_tenant_saleid", StringComparison.OrdinalIgnoreCase)
                        || msg.Contains("SaleId", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                inner = inner.InnerException;
            }
            return false;
        }

        public async Task<PaginatedServiceOrderResponse> GetServiceOrdersAsync(
            string? status = null,
            string? priority = null,
            int? contactId = null,
            int? saleId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? paymentStatus = null,
            string? search = null,
            int page = 1,
            int limit = 20,
            string sortBy = "created_at",
            string sortOrder = "desc"
        )
        {
            var query = _context.ServiceOrders.AsNoTracking().Where(s => !s.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status);

            if (!string.IsNullOrEmpty(priority))
                query = query.Where(s => s.Priority == priority);

            if (contactId.HasValue)
                query = query.Where(s => s.ContactId == contactId.Value);

            if (saleId.HasValue)
            {
                var saleIdStr = saleId.Value.ToString();
                query = query.Where(s => s.SaleId == saleIdStr);
            }

            if (startDate.HasValue)
                query = query.Where(s => s.StartDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.StartDate <= endDate.Value);

            if (!string.IsNullOrEmpty(paymentStatus))
                query = query.Where(s => s.PaymentStatus == paymentStatus);

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(s =>
                    s.OrderNumber.ToLower().Contains(searchLower) ||
                    (s.Description != null && s.Description.ToLower().Contains(searchLower)) ||
                    (s.Notes != null && s.Notes.ToLower().Contains(searchLower))
                );
            }

            var total = await query.CountAsync();

            query = sortBy.ToLower() switch
            {
                "order_number" => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.OrderNumber) : query.OrderByDescending(s => s.OrderNumber),
                "start_date" => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.StartDate) : query.OrderByDescending(s => s.StartDate),
                "priority" => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.Priority) : query.OrderByDescending(s => s.Priority),
                "status" => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.Status) : query.OrderByDescending(s => s.Status),
                _ => sortOrder.ToLower() == "asc" ? query.OrderBy(s => s.CreatedDate) : query.OrderByDescending(s => s.CreatedDate)
            };

            var serviceOrders = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Include(s => s.Jobs)
                .Include(s => s.Materials)
                .ToListAsync();

            var contactIds = serviceOrders.Select(s => s.ContactId).Distinct().ToList();
            var contacts = await _context.Contacts
                .Where(c => contactIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            // Fetch sale numbers for service orders that have a saleId
            var saleIds = serviceOrders
                .Where(s => !string.IsNullOrEmpty(s.SaleId) && int.TryParse(s.SaleId, out _))
                .Select(s => int.Parse(s.SaleId!))
                .Distinct()
                .ToList();
            var saleNumbers = saleIds.Any() 
                ? await _context.Sales
                    .Where(s => saleIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id.ToString(), s => s.SaleNumber)
                : new Dictionary<string, string>();

            // Fetch user names for createdBy - resolve from MainAdminUsers (ID 1) or Users table
            var creatorUserIds = serviceOrders
                .Where(s => !string.IsNullOrEmpty(s.CreatedBy) && int.TryParse(s.CreatedBy, out _))
                .Select(s => int.Parse(s.CreatedBy!))
                .Distinct()
                .ToList();
            
            var userNames = new Dictionary<string, string>();
            
            // Check MainAdminUsers for ID 1
            if (creatorUserIds.Contains(1))
            {
                var adminUser = await _context.MainAdminUsers.FirstOrDefaultAsync();
                if (adminUser != null)
                    userNames["1"] = $"{adminUser.FirstName} {adminUser.LastName}".Trim();
            }
            
            // Check Users table for other IDs
            var regularUserIds = creatorUserIds.Where(id => id != 1).ToList();
            if (regularUserIds.Any())
            {
                var users = await _context.Users
                    .Where(u => regularUserIds.Contains(u.Id))
                    .ToListAsync();
                foreach (var user in users)
                {
                    userNames[user.Id.ToString()] = $"{user.FirstName} {user.LastName}".Trim();
                }
            }

            // Fetch user names for technicians assigned to jobs
            var jobTechnicianIds = serviceOrders
                .SelectMany(s => s.Jobs ?? Enumerable.Empty<MyApi.Modules.ServiceOrders.Models.ServiceOrderJob>())
                .Where(j => j.AssignedTechnicianIds != null)
                .SelectMany(j => j.AssignedTechnicianIds!)
                .Where(id => int.TryParse(id, out _))
                .Select(id => int.Parse(id))
                .Distinct()
                .ToList();

            if (jobTechnicianIds.Any())
            {
                var techUsers = await _context.Users
                    .Where(u => jobTechnicianIds.Contains(u.Id))
                    .ToListAsync();
                foreach (var user in techUsers)
                {
                    userNames[user.Id.ToString()] = $"{user.FirstName} {user.LastName}".Trim();
                }
            }

            var dtos = serviceOrders.Select(s => MapToDto(
                s, 
                contacts.GetValueOrDefault(s.ContactId), 
                saleNumbers.GetValueOrDefault(s.SaleId ?? ""),
                userNames.GetValueOrDefault(s.CreatedBy ?? ""),
                userNames
            )).ToList();

            return new PaginatedServiceOrderResponse
            {
                ServiceOrders = dtos,
                Pagination = new PaginationInfo
                {
                    Page = page,
                    Limit = limit,
                    Total = total,
                    TotalPages = (int)Math.Ceiling((double)total / limit)
                }
            };
        }

        public async Task<ServiceOrderDto?> GetServiceOrderByIdAsync(int id, bool includeJobs = true)
        {
            var query = _context.ServiceOrders.AsNoTracking().Where(s => !s.IsDeleted).AsQueryable();
            if (includeJobs)
                query = query.Include(s => s.Jobs).Include(s => s.Materials);

            var serviceOrder = await query.FirstOrDefaultAsync(s => s.Id == id);
            if (serviceOrder == null)
                return null;

            var contact = await _context.Contacts.FindAsync(serviceOrder.ContactId);
            
            // Fetch sale number and backfill estimated cost if needed
            string? saleNumber = null;
            var mappedSaleId = await ResolveSaleIdAsync(serviceOrder.SaleId);
            if (mappedSaleId is not null)
            {
                var sale = await _context.Sales.FindAsync(mappedSaleId.Value);
                saleNumber = sale?.SaleNumber;

                
                // Backfill estimated cost from sale if it's 0 (legacy data)
                if ((serviceOrder.EstimatedCost == null || serviceOrder.EstimatedCost == 0) && sale != null)
                {
                    var saleCost = sale.GrandTotal > 0 ? sale.GrandTotal : sale.TotalAmount;
                    if (saleCost > 0)
                    {
                        serviceOrder.EstimatedCost = saleCost;
                        // Also persist the fix so it doesn't need to be recalculated
                        var tracked = await _context.ServiceOrders.FindAsync(serviceOrder.Id);
                        if (tracked != null)
                        {
                            tracked.EstimatedCost = saleCost;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }
            
            // Resolve createdByName - check MainAdminUsers first (ID 1), then Users table
            string? createdByName = null;
            if (!string.IsNullOrEmpty(serviceOrder.CreatedBy) && int.TryParse(serviceOrder.CreatedBy, out int createdByUserId))
            {
                if (createdByUserId == 1)
                {
                    var adminUser = await _context.MainAdminUsers.FirstOrDefaultAsync();
                    createdByName = adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null;
                }
                else
                {
                    var user = await _context.Users.FindAsync(createdByUserId);
                    createdByName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;
                }
            }
            
            // Resolving technician names for jobs
            var userNames = new Dictionary<string, string>();
            if (createdByName != null && serviceOrder.CreatedBy != null)
            {
                userNames[serviceOrder.CreatedBy] = createdByName;
            }

            var jobTechnicianIds = serviceOrder.Jobs?
                .Where(j => j.AssignedTechnicianIds != null)
                .SelectMany(j => j.AssignedTechnicianIds!)
                .Where(id => int.TryParse(id, out _))
                .Select(id => int.Parse(id))
                .Distinct()
                .ToList() ?? new List<int>();

            if (jobTechnicianIds.Any())
            {
                var techUsers = await _context.Users
                    .Where(u => jobTechnicianIds.Contains(u.Id))
                    .ToListAsync();
                foreach (var user in techUsers)
                {
                    userNames[user.Id.ToString()] = $"{user.FirstName} {user.LastName}".Trim();
                }
            }

            return MapToDto(serviceOrder, contact, saleNumber, createdByName, userNames);
        }

        public async Task<ServiceOrderDto> UpdateServiceOrderAsync(int id, UpdateServiceOrderDto updateDto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            // Fix #7: the generic PUT/PATCH path used to assign Status directly, bypassing
            // the transition whitelist that UpdateStatusAsync enforces — so any caller could
            // set an arbitrary or out-of-order status string. Validate here too. A no-op
            // (same status resent as part of a wider update) stays allowed.
            if (updateDto.Status != null && updateDto.Status != serviceOrder.Status)
            {
                var allowed = GetValidStatusTransitions(serviceOrder.Status);
                if (!allowed.Contains(updateDto.Status))
                    throw new InvalidOperationException(
                        $"Cannot transition from '{serviceOrder.Status}' to '{updateDto.Status}'. " +
                        (allowed.Count > 0
                            ? $"Allowed next: {string.Join(", ", allowed)}."
                            : $"'{serviceOrder.Status}' is a terminal status."));

                serviceOrder.Status = updateDto.Status;
                if (updateDto.Status == "in_progress" && !serviceOrder.ActualStartDate.HasValue)
                    serviceOrder.ActualStartDate = DateTime.UtcNow;
            }

            if (updateDto.ProjectId.HasValue) serviceOrder.ProjectId = updateDto.ProjectId.Value;
            if (updateDto.Priority != null) serviceOrder.Priority = updateDto.Priority;
            if (updateDto.Description != null) serviceOrder.Description = updateDto.Description;
            if (updateDto.Notes != null) serviceOrder.Notes = updateDto.Notes;
            if (updateDto.StartDate.HasValue) serviceOrder.StartDate = DateTime.SpecifyKind(updateDto.StartDate.Value, DateTimeKind.Utc);
            if (updateDto.TargetCompletionDate.HasValue) serviceOrder.TargetCompletionDate = DateTime.SpecifyKind(updateDto.TargetCompletionDate.Value, DateTimeKind.Utc);
            if (updateDto.EstimatedDuration.HasValue) serviceOrder.EstimatedDuration = updateDto.EstimatedDuration;
            if (updateDto.Discount.HasValue) serviceOrder.Discount = updateDto.Discount;
            if (updateDto.DiscountPercentage.HasValue) serviceOrder.DiscountPercentage = updateDto.DiscountPercentage;
            if (updateDto.PaymentTerms != null) serviceOrder.PaymentTerms = updateDto.PaymentTerms;
            if (updateDto.RequiresApproval.HasValue) serviceOrder.RequiresApproval = updateDto.RequiresApproval.Value;
            if (updateDto.Tags != null) serviceOrder.Tags = updateDto.Tags;
            if (updateDto.PreferredSkills != null) serviceOrder.PreferredSkills = updateDto.PreferredSkills;
            if (updateDto.CustomFields != null) serviceOrder.CustomFields = System.Text.Json.JsonSerializer.Serialize(updateDto.CustomFields);

            serviceOrder.ModifiedBy = userId;
            serviceOrder.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var result = await GetServiceOrderByIdAsync(id);
            return result!;
        }

        public async Task<ServiceOrderDto> PatchServiceOrderAsync(int id, UpdateServiceOrderDto updateDto, string userId)
        {
            return await UpdateServiceOrderAsync(id, updateDto, userId);
        }

        /// <summary>
        /// Server-side reconciliation of a service order's status from its dispatches' statuses
        /// (replaces the old client-side cascade). System-driven: it sets the status directly (no
        /// user-transition validation) and delegates the decision to
        /// <see cref="ServiceOrderStatusCalculator"/>, which is also used by DispatchService and
        /// BusinessWorkflowService. Because all three share one implementation, the frontend
        /// calling updateStatus and recalculateStatus back to back on the same click can no longer
        /// produce two different statuses for one dispatch set.
        /// </summary>
        public async Task<ServiceOrderDto> RecalculateStatusFromDispatchesAsync(int id, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            var dispatchStatuses = await _context.Dispatches
                .Where(d => d.ServiceOrderId == id && !d.IsDeleted)
                .Select(d => d.Status)
                .ToListAsync();

            var evaluation = ServiceOrderStatusCalculator.Compute(serviceOrder.Status, dispatchStatuses);
            var dirty = false;

            if (serviceOrder.CompletedDispatchCount != evaluation.CompletedDispatchCount)
            {
                serviceOrder.CompletedDispatchCount = evaluation.CompletedDispatchCount;
                dirty = true;
            }

            if (!evaluation.IsTerminal && evaluation.StatusChanged)
            {
                var newStatus = evaluation.Status;
                serviceOrder.Status = newStatus;
                serviceOrder.ModifiedBy = userId;
                serviceOrder.ModifiedDate = DateTime.UtcNow;
                if (newStatus == "in_progress" && !serviceOrder.ActualStartDate.HasValue)
                    serviceOrder.ActualStartDate = DateTime.UtcNow;
                if (newStatus == ServiceOrderStatusCalculator.FieldWorkCompleteStatus)
                {
                    serviceOrder.TechnicallyCompletedAt ??= DateTime.UtcNow;
                    serviceOrder.ActualCompletionDate ??= DateTime.UtcNow;
                }
                dirty = true;
            }

            if (dirty) await _context.SaveChangesAsync();


            var result = await GetServiceOrderByIdAsync(id);
            return result!;
        }

        public async Task<ServiceOrderDto> UpdateStatusAsync(int id, UpdateServiceOrderStatusDto statusDto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            // Validate status transition
            var validTransitions = GetValidStatusTransitions(serviceOrder.Status);
            if (!validTransitions.Contains(statusDto.Status))
                throw new InvalidOperationException($"Cannot transition from '{serviceOrder.Status}' to '{statusDto.Status}'");

            var oldStatus = serviceOrder.Status;
            serviceOrder.Status = statusDto.Status;
            if (statusDto.Status == "in_progress" && !serviceOrder.ActualStartDate.HasValue)
                serviceOrder.ActualStartDate = DateTime.UtcNow;

            serviceOrder.ModifiedBy = userId;
            serviceOrder.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Upward propagation: Log activity to parent Sale (and Offer)
            if (oldStatus != statusDto.Status)
            {
                await PropagateServiceOrderStatusToSaleAsync(serviceOrder, oldStatus, statusDto.Status, userId);
            }

            // Trigger workflow automation for status change
            if (oldStatus != statusDto.Status && _workflowTriggerService != null)
            {
                try
                {
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "service_order",
                        id,
                        oldStatus ?? "",
                        statusDto.Status,
                        userId,
                        new { serviceOrderId = id, orderNumber = serviceOrder.OrderNumber }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to trigger workflow for service order {ServiceOrderId} status change", id);
                }
            }

            // Log status change to the contact activity feed
            if (_contactActivity != null && serviceOrder.ContactId > 0 && oldStatus != statusDto.Status)
            {
                await _contactActivity.LogAsync(
                    contactId: serviceOrder.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.ServiceOrderStatusChanged,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.ServiceOrder,
                    relatedEntityId: serviceOrder.Id,
                    description: $"Service order {serviceOrder.OrderNumber} status: {oldStatus} → {statusDto.Status}",
                    metadata: new { number = serviceOrder.OrderNumber, oldStatus, status = statusDto.Status },
                    createdBy: userId);
            }

            var result = await GetServiceOrderByIdAsync(id);
            return result!;
        }

        /// <summary>
        /// Propagate service order status changes to parent Sale and Offer activities
        /// </summary>
        private async Task PropagateServiceOrderStatusToSaleAsync(ServiceOrder serviceOrder, string? oldStatus, string newStatus, string userId)
        {
            try
            {
                var resolvedSaleId = await ResolveSaleIdAsync(serviceOrder.SaleId);
                if (resolvedSaleId is null) return;
                var saleId = resolvedSaleId.Value;


                var sale = await _context.Sales.FindAsync(saleId);
                if (sale == null) return;

                // Create SaleActivity for service order status change
                var saleActivity = new SaleActivity
                {
                    SaleId = saleId,
                    Type = "service_order_status_changed",
                    Description = $"Service order #{serviceOrder.OrderNumber} status: {oldStatus} → {newStatus}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedByName = sale.AssignedToName ?? "System"
                };
                _context.SaleActivities.Add(saleActivity);

                // Propagate to Offer if sale came from an offer
                if (!string.IsNullOrEmpty(sale.OfferId) && int.TryParse(sale.OfferId, out int offerId))
                {
                    var offerActivity = new MyApi.Modules.Offers.Models.OfferActivity
                    {
                        OfferId = offerId,
                        Type = "service_order_status_changed",
                        Description = $"Service order #{serviceOrder.OrderNumber} status: {oldStatus} → {newStatus} (Sale #{saleId})",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = sale.AssignedToName ?? "System"
                    };
                    _context.OfferActivities.Add(offerActivity);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to propagate service order status to sale activities for service order {ServiceOrderId}", serviceOrder.Id);
            }
        }

        public async Task<ServiceOrderDto> ApproveAsync(int id, ApproveServiceOrderDto approveDto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            if (!serviceOrder.RequiresApproval)
                throw new InvalidOperationException("Service order does not require approval");

            serviceOrder.ApprovedBy = userId;
            serviceOrder.ApprovalDate = approveDto.ApprovalDate ?? DateTime.UtcNow;
            serviceOrder.Status = "completed";
            serviceOrder.ActualCompletionDate = DateTime.UtcNow;
            serviceOrder.CompletionPercentage = 100;
            serviceOrder.ModifiedBy = userId;
            serviceOrder.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var result = await GetServiceOrderByIdAsync(id);
            return result!;
        }

        public async Task<ServiceOrderDto> CompleteAsync(int id, CompleteServiceOrderDto completeDto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.Include(s => s.Jobs).FirstOrDefaultAsync(s => s.Id == id);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            // Fix §4.1: treat "cancelled" jobs as terminal too. Previously any
            // cancelled job permanently blocked SO completion, forcing operators
            // to route around the state machine.
            if (serviceOrder.Jobs != null && serviceOrder.Jobs.Any(j => j.Status != "completed" && j.Status != "cancelled"))
                throw new InvalidOperationException("Not all jobs are completed or cancelled.");

            serviceOrder.Status = "completed";
            serviceOrder.ActualCompletionDate = DateTime.UtcNow;
            serviceOrder.TechnicallyCompletedAt = DateTime.UtcNow;
            serviceOrder.CompletionPercentage = 100;
            serviceOrder.PaymentStatus = "pending";

            // Update CompletedDispatchCount from actual dispatch data
            // Canonical counter definition (shared with ServiceOrderStatusCalculator): every live
            // dispatch on this service order whose status counts as done. It used to be scoped by
            // JobId and to the literal status "completed" only, so orders whose dispatches were
            // linked by ServiceOrderId, or which reached "technically_completed", counted as 0.
            serviceOrder.CompletedDispatchCount = await _context.Dispatches
                .CountAsync(d => d.ServiceOrderId == serviceOrder.Id && !d.IsDeleted
                    && (d.Status == "completed" || d.Status == "technically_completed"));


            if (completeDto.GenerateInvoice)
            {
                serviceOrder.InvoiceNumber = MyApi.Modules.Numbering.Services.NumberingFallback.Generate("Invoice");
                serviceOrder.InvoiceDate = DateTime.UtcNow;
            }

            serviceOrder.ModifiedBy = userId;
            serviceOrder.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Shadow Sale: when a "direct" order completes and has no parent
            // Sale yet, generate one so downstream invoicing / reporting work
            // unchanged. Idempotent — no-op if already linked.
            string? shadowSaleWarning = null;
            if (string.Equals(serviceOrder.Origin, "direct", StringComparison.OrdinalIgnoreCase)
                && !serviceOrder.AutoGeneratedSaleId.HasValue)
            {
                try
                {
                    await EnsureShadowSaleAsync(serviceOrder, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate shadow Sale for direct ServiceOrder {Id}; order is marked completed but no Sale exists yet", serviceOrder.Id);
                    // Fix §4.2: do NOT swallow the failure silently. The order is still
                    // completed (correct), but we surface a warning so the UI can prompt
                    // the operator to retry via POST /{id}/retry-shadow-sale, instead of
                    // the order becoming permanently stranded outside the invoicing chain.
                    shadowSaleWarning = ex.Message;
                }
            }

            var result = await GetServiceOrderByIdAsync(id);
            if (result != null && shadowSaleWarning != null)
            {
                result.Warnings ??= new List<ServiceOrderCompletionWarningDto>();
                result.Warnings.Add(new ServiceOrderCompletionWarningDto
                {
                    Code = "shadow_sale_failed",
                    Message = $"Order completed but shadow Sale could not be generated: {shadowSaleWarning}. Use retry-shadow-sale to recover."
                });
            }
            return result!;
        }

        /// <summary>
        /// Fix §4.2: retry endpoint for the shadow-Sale generation that CompleteAsync
        /// warned about. Idempotent — no-op if the order already has an
        /// AutoGeneratedSaleId. Only valid on completed, direct-origin orders.
        /// </summary>
        public async Task<ServiceOrderDto> RetryShadowSaleAsync(int id, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder == null || serviceOrder.IsDeleted)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            if (!string.Equals(serviceOrder.Origin, "direct", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Shadow Sale is only generated for direct-origin service orders.");

            if (serviceOrder.Status != "completed" && serviceOrder.Status != "invoiced" && serviceOrder.Status != "closed")
                throw new InvalidOperationException("Order must be completed before generating a shadow Sale.");

            if (serviceOrder.AutoGeneratedSaleId.HasValue)
            {
                // Nothing to do; return current state.
                return (await GetServiceOrderByIdAsync(id))!;
            }

            await EnsureShadowSaleAsync(serviceOrder, userId);
            return (await GetServiceOrderByIdAsync(id))!;
        }

        public async Task<ServiceOrderDto> CancelAsync(int id, CancelServiceOrderDto cancelDto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder == null || serviceOrder.IsDeleted)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            // Fix §4.3: block cancellation from terminal / billed states. Previously
            // CancelAsync bypassed the transition table entirely, silently orphaning
            // invoices raised against the SO.
            var current = (serviceOrder.Status ?? "").ToLowerInvariant();
            var terminalOrBilled = new HashSet<string> { "cancelled", "invoiced", "closed", "completed" };
            if (terminalOrBilled.Contains(current))
                throw new InvalidOperationException(
                    $"Service order in status '{serviceOrder.Status}' cannot be cancelled. Void the invoice / reopen the order first.");

            serviceOrder.Status = "cancelled";
            serviceOrder.CancellationReason = cancelDto.CancellationReason;
            serviceOrder.CancellationNotes = cancelDto.CancellationNotes;
            serviceOrder.ModifiedBy = userId;
            serviceOrder.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var result = await GetServiceOrderByIdAsync(id);
            return result!;
        }

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder == null || serviceOrder.IsDeleted)
                return false;

            // Fix §4.6: block soft-delete of a SO that still has live (non-cancelled,
            // non-completed) dispatches or that has already been invoiced. Preserves
            // ledger integrity — a hidden parent must never orphan live child rows.
            var linkedIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);
            if (linkedIds.Count > 0)
            {
                var activeCount = await _context.Dispatches
                    .Where(d => linkedIds.Contains(d.Id) && d.Status != "completed" && d.Status != "cancelled")
                    .CountAsync();
                if (activeCount > 0)
                    throw new InvalidOperationException($"Cannot delete: {activeCount} active dispatch(es) still linked. Cancel or complete them first.");
            }
            if (!string.IsNullOrEmpty(serviceOrder.InvoiceNumber) ||
                string.Equals(serviceOrder.Status, "invoiced", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(serviceOrder.Status, "closed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot delete an invoiced or closed service order. Void the invoice first.");
            }

            // Store sale ID before deletion for resetting
            var saleId = serviceOrder.SaleId;

            serviceOrder.IsDeleted = true;
            serviceOrder.DeletedAt = DateTime.UtcNow;
            serviceOrder.DeletedBy = userId;
            await _context.SaveChangesAsync();

            // Reset the sale's serviceOrdersStatus if linked
            var deleteSaleId = await ResolveSaleIdAsync(saleId);
            if (deleteSaleId is not null)
            {
                var parsedSaleId = deleteSaleId.Value;
                var sale = await _context.Sales.FindAsync(parsedSaleId);

                if (sale != null)
                {
                    sale.ServiceOrdersStatus = null;
                    sale.ModifiedDate = DateTime.UtcNow;
                    
                    // Also reset service items that were marked as converted
                    var saleItems = await _context.SaleItems
                        .Where(si => si.SaleId == parsedSaleId && si.ServiceOrderId == id.ToString())
                        .ToListAsync();
                    
                    foreach (var item in saleItems)
                    {
                        item.ServiceOrderGenerated = false;
                        item.ServiceOrderId = null;
                    }
                    
                    await _context.SaveChangesAsync();

                    // Add activity to the sale
                    var saleActivity = new SaleActivity
                    {
                        SaleId = parsedSaleId,
                        Type = "service_order_deleted",
                        Description = $"Service Order #{serviceOrder.OrderNumber} was deleted. The sale can now be converted to a new service order.",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByName = "System"
                    };
                    _context.SaleActivities.Add(saleActivity);
                    await _context.SaveChangesAsync();
                }
            }

            return true;
        }

        public async Task<ServiceOrderStatsDto> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, string? status = null, int? contactId = null)
        {
            var query = _context.ServiceOrders.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.CreatedDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.CreatedDate <= endDate.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status);

            if (contactId.HasValue)
                query = query.Where(s => s.ContactId == contactId.Value);

            var serviceOrders = await query.ToListAsync();

            var stats = new ServiceOrderStatsDto
            {
                TotalServiceOrders = serviceOrders.Count,
                ByStatus = new Dictionary<string, int>
                {
                    { "draft", serviceOrders.Count(s => s.Status == "draft") },
                    { "planned", serviceOrders.Count(s => s.Status == "planned") },
                    { "in_progress", serviceOrders.Count(s => s.Status == "in_progress") },
                    { "on_hold", serviceOrders.Count(s => s.Status == "on_hold") },
                    { "completed", serviceOrders.Count(s => s.Status == "completed") },
                    { "cancelled", serviceOrders.Count(s => s.Status == "cancelled") }
                },
                ByPriority = new Dictionary<string, int>
                {
                    { "low", serviceOrders.Count(s => s.Priority == "low") },
                    { "medium", serviceOrders.Count(s => s.Priority == "medium") },
                    { "high", serviceOrders.Count(s => s.Priority == "high") },
                    { "urgent", serviceOrders.Count(s => s.Priority == "urgent") }
                },
                Financials = new FinancialStatsDto
                {
                    TotalEstimatedCost = serviceOrders.Sum(s => s.EstimatedCost ?? 0),
                    TotalActualCost = serviceOrders.Sum(s => s.ActualCost ?? 0),
                    TotalDiscount = serviceOrders.Sum(s => s.Discount ?? 0),
                    TotalTax = serviceOrders.Sum(s => s.Tax ?? 0),
                    TotalBilled = serviceOrders.Sum(s => s.TotalAmount ?? 0),
                    TotalPaid = serviceOrders.Where(s => s.PaymentStatus == "paid").Sum(s => s.TotalAmount ?? 0),
                    TotalPending = serviceOrders.Where(s => s.PaymentStatus == "pending").Sum(s => s.TotalAmount ?? 0)
                }
            };

            // Calculate completion metrics
            var completedOrders = serviceOrders.Where(s => s.Status == "completed").ToList();
            if (completedOrders.Any())
            {
                var completionTimes = completedOrders
                    .Where(s => s.ActualStartDate.HasValue && s.ActualCompletionDate.HasValue)
                    .Select(s => (s.ActualCompletionDate!.Value - s.ActualStartDate!.Value).TotalHours)
                    .ToList();

                stats.AverageCompletionTime = completionTimes.Any() ? completionTimes.Average() : 0;
                stats.CompletionRate = (double)completedOrders.Count / serviceOrders.Count * 100;

                var onTimeCompleted = completedOrders.Count(s =>
                    s.ActualCompletionDate.HasValue &&
                    s.TargetCompletionDate.HasValue &&
                    s.ActualCompletionDate <= s.TargetCompletionDate);
                stats.OnTimeCompletionRate = (double)onTimeCompleted / completedOrders.Count * 100;
            }

            return stats;
        }

        private ServiceOrderDto MapToDto(ServiceOrder serviceOrder, Contact? contact, string? saleNumber = null, string? createdByName = null, Dictionary<string, string>? userNames = null)
        {
            return new ServiceOrderDto
            {
                Id = serviceOrder.Id,
                OrderNumber = serviceOrder.OrderNumber,
                SaleId = serviceOrder.SaleId,
                SaleNumber = saleNumber,
                OfferId = serviceOrder.OfferId,
                ProjectId = serviceOrder.ProjectId,
                ContactId = serviceOrder.ContactId,
                Status = serviceOrder.Status,
                Priority = serviceOrder.Priority,
                Description = serviceOrder.Description,
                Notes = serviceOrder.Notes,
                StartDate = serviceOrder.StartDate,
                TargetCompletionDate = serviceOrder.TargetCompletionDate,
                ActualStartDate = serviceOrder.ActualStartDate,
                ActualCompletionDate = serviceOrder.ActualCompletionDate,
                EstimatedDuration = serviceOrder.EstimatedDuration,
                ActualDuration = serviceOrder.ActualDuration,
                EstimatedCost = serviceOrder.EstimatedCost,
                ActualCost = serviceOrder.ActualCost,
                Discount = serviceOrder.Discount,
                DiscountPercentage = serviceOrder.DiscountPercentage,
                Tax = serviceOrder.Tax,
                TotalAmount = serviceOrder.TotalAmount,
                PaymentStatus = serviceOrder.PaymentStatus,
                PaymentTerms = serviceOrder.PaymentTerms,
                InvoiceNumber = serviceOrder.InvoiceNumber,
                InvoiceDate = serviceOrder.InvoiceDate,
                CompletionPercentage = serviceOrder.CompletionPercentage,
                RequiresApproval = serviceOrder.RequiresApproval,
                ApprovedBy = serviceOrder.ApprovedBy,
                ApprovalDate = serviceOrder.ApprovalDate,
                Tags = serviceOrder.Tags,
                PreferredSkills = serviceOrder.PreferredSkills,
                CustomFields = serviceOrder.CustomFields != null
                    ? System.Text.Json.JsonSerializer.Deserialize<object>(serviceOrder.CustomFields)
                    : null,
                CreatedBy = serviceOrder.CreatedBy,
                CreatedByName = createdByName,
                CreatedAt = serviceOrder.CreatedDate,
                UpdatedBy = serviceOrder.ModifiedBy,
                UpdatedAt = serviceOrder.ModifiedDate ?? serviceOrder.CreatedDate,
                Jobs = serviceOrder.Jobs?.Select(j => new ServiceOrderJobDto
                {
                    Id = j.Id,
                    ServiceOrderId = j.ServiceOrderId,
                    Title = j.Title ?? string.Empty,
                    Description = j.Description,
                    Status = j.Status,
                    InstallationId = j.InstallationId?.ToString(),
                    WorkType = j.WorkType,
                    EstimatedDuration = j.EstimatedDuration,
                    EstimatedCost = j.EstimatedCost,
                    CompletionPercentage = j.CompletionPercentage,
                    AssignedTechnicianIds = j.AssignedTechnicianIds,
                    AssignedTechnicians = j.AssignedTechnicianIds?.Select(id => {
                        return new UserLightDto 
                        {
                            Id = int.TryParse(id, out var parsedId) ? parsedId : 0,
                            Name = userNames?.GetValueOrDefault(id) ?? id,
                            Email = null
                        };
                    }).ToList()
                }).ToList(),
                Materials = serviceOrder.Materials?.Select(m => new ServiceOrderMaterialDto
                {
                    Id = m.Id,
                    ServiceOrderId = m.ServiceOrderId,
                    SaleItemId = m.SaleItemId,
                    ArticleId = m.ArticleId,
                    Name = m.Name,
                    Sku = m.Sku,
                    Description = m.Description,
                    Quantity = m.Quantity,
                    EstimatedQuantity = m.EstimatedQuantity ?? m.Quantity,
                    UnitPrice = m.UnitPrice,
                    TotalPrice = m.TotalPrice,
                    Status = m.Status,
                    Source = m.Source,
                    InternalComment = m.InternalComment,
                    ExternalComment = m.ExternalComment,
                    Replacing = m.Replacing,
                    OldArticleModel = m.OldArticleModel,
                    OldArticleStatus = m.OldArticleStatus,
                    InstallationId = m.InstallationId?.ToString(),
                    InstallationName = m.InstallationName,
                    CreatedBy = m.CreatedBy,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                TechnicallyCompletedAt = serviceOrder.TechnicallyCompletedAt,
                ServiceCount = serviceOrder.ServiceCount,
                CompletedDispatchCount = serviceOrder.CompletedDispatchCount,
                Origin = string.IsNullOrEmpty(serviceOrder.Origin) ? "from_sale" : serviceOrder.Origin,
                AutoGeneratedSaleId = serviceOrder.AutoGeneratedSaleId,
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
                } : null
            };
        }

        private List<string> GetValidStatusTransitions(string currentStatus)
        {
            return currentStatus switch
            {
                "draft" => new List<string> { "pending", "planned", "ready_for_planning", "cancelled" },
                "pending" => new List<string> { "planned", "ready_for_planning", "in_progress", "on_hold", "cancelled" },
                "planned" => new List<string> { "pending", "ready_for_planning", "in_progress", "on_hold", "cancelled" },
                "ready_for_planning" => new List<string> { "pending", "planned", "in_progress", "on_hold", "cancelled" },
                // NOTE: legacy 'scheduled' rows are migrated to 'planned'; kept here so any
                // straggler row is not a dead end.
                "scheduled" => new List<string> { "pending", "planned", "ready_for_planning", "in_progress", "on_hold", "cancelled" },
                // Fix #6: 'partially_completed' exists in the frontend SO config and type
                // union but had no case here, so it was unreachable and, once set, a dead
                // end. Wired into the natural in_progress → partially_completed → completion path.
                "in_progress" => new List<string> { "on_hold", "partially_completed", "technically_completed", "completed", "cancelled" },
                "on_hold" => new List<string> { "pending", "planned", "ready_for_planning", "in_progress", "cancelled" },
                "partially_completed" => new List<string> { "in_progress", "on_hold", "technically_completed", "completed", "cancelled" },
                "technically_completed" => new List<string> { "in_progress", "ready_for_invoice", "completed", "cancelled" },

                "ready_for_invoice" => new List<string> { "technically_completed", "invoiced", "cancelled" },
                "completed" => new List<string> { "ready_for_invoice", "invoiced", "closed" },
                "invoiced" => new List<string> { "closed" },
                "closed" => new List<string>(),
                "cancelled" => new List<string> { "pending", "planned", "ready_for_planning" },
                _ => new List<string>()
            };
        }

        private string DetermineWorkType(string? itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return "maintenance";
            
            var name = itemName.ToLower();
            if (name.Contains("repair")) return "repair";
            if (name.Contains("install")) return "installation";
            if (name.Contains("inspect")) return "inspection";
            if (name.Contains("upgrade")) return "upgrade";
            return "maintenance";
        }

        /// <summary>
        /// Resolve every dispatch attributable to a service order through the same THREE paths used by the
        /// invoice summary, so downstream reads (materials, expenses, time, notes, dispatches list) and the
        /// invoice transfer all agree on the same set of dispatches:
        ///   (a) Dispatch.ServiceOrderId points at us (installation / whole-SO dispatches).
        ///   (b) DispatchJobs join table links to one of our jobs (multi-job dispatches).
        ///   (c) Legacy: Dispatch.JobId string equals one of our job ids (old single-job dispatches).
        ///   (d) Dispatch.InstallationId matches an installation on our jobs.
        /// Soft-deleted dispatches (and soft-deleted join rows) are always excluded.
        /// </summary>
        private async Task<List<int>> ResolveLinkedDispatchIdsAsync(ServiceOrder serviceOrder)
        {
            var jobIds = serviceOrder.Jobs?.Select(j => j.Id).ToList() ?? new List<int>();
            var installationIds = serviceOrder.Jobs?
                .Where(j => j.InstallationId.HasValue)
                .Select(j => j.InstallationId!.Value)
                .Distinct()
                .ToList() ?? new List<int>();
            var jobIdStrings = jobIds.Select(j => j.ToString()).ToList();

            var dispatchIdsViaJoin = await _context.Set<DispatchJob>()
                .Where(dj => !dj.IsDeleted && jobIds.Contains(dj.JobId))
                .Select(dj => dj.DispatchId)
                .Distinct()
                .ToListAsync();

            return await _context.Dispatches
                .Where(d => !d.IsDeleted && (
                       d.ServiceOrderId == serviceOrder.Id
                    || dispatchIdsViaJoin.Contains(d.Id)
                    || (d.JobId != null && jobIdStrings.Contains(d.JobId))
                    || (d.InstallationId.HasValue && installationIds.Contains(d.InstallationId.Value))
                ))
                .Select(d => d.Id)
                .Distinct()
                .ToListAsync();
        }

        /// <summary>
        /// Count billable actuals of a service order that have NOT yet been transferred to a sale.
        /// SO-level rows are tracked by their own InvoiceStatus column; dispatch-level rows carry no
        /// such column, so they are matched against SaleItem.SourceType/SourceId — the same identity
        /// key PrepareForInvoice writes. Used to stop the sale→SO cascade from auto-closing an order
        /// whose billable work would then be silently lost.
        /// </summary>
        private async Task<int> CountUntransferredBillablesAsync(ServiceOrder serviceOrder)
        {
            var soId = serviceOrder.Id;

            var pending = 0;

            pending += await _context.ServiceOrderMaterials
                .CountAsync(m => m.ServiceOrderId == soId && m.InvoiceStatus == null);

            pending += await _context.ServiceOrderTimeEntries
                .CountAsync(t => t.ServiceOrderId == soId && t.Billable && t.InvoiceStatus == null);

            pending += await _context.ServiceOrderExpenses
                .CountAsync(e => e.ServiceOrderId == soId && e.InvoiceStatus == null);

            var linkedDispatchIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);
            if (linkedDispatchIds.Count == 0) return pending;

            // Only dispatches that actually happened can produce billable actuals.
            var billableDispatchIds = await _context.Dispatches
                .Where(d => linkedDispatchIds.Contains(d.Id) && d.Status != "cancelled")
                .Select(d => d.Id)
                .ToListAsync();
            if (billableDispatchIds.Count == 0) return pending;

            var transferred = await _context.SaleItems
                .Where(si => si.ServiceOrderId == soId.ToString()
                    && si.SourceType != null && si.SourceId != null)
                .Select(si => si.SourceType + ":" + si.SourceId)
                .ToListAsync();
            var transferredKeys = new HashSet<string>(transferred, StringComparer.OrdinalIgnoreCase);

            var dispatchMaterialIds = await _context.DispatchMaterials
                .Where(m => billableDispatchIds.Contains(m.DispatchId))
                .Select(m => m.Id).ToListAsync();
            pending += dispatchMaterialIds.Count(mid => !transferredKeys.Contains($"dispatch_material:{mid}"));

            var dispatchExpenseIds = await _context.DispatchExpenses
                .Where(e => billableDispatchIds.Contains(e.DispatchId))
                .Select(e => e.Id).ToListAsync();
            pending += dispatchExpenseIds.Count(eid => !transferredKeys.Contains($"dispatch_expense:{eid}"));

            var dispatchTimeIds = await _context.TimeEntries
                .Where(t => billableDispatchIds.Contains(t.DispatchId) && t.Billable)
                .Select(t => t.Id).ToListAsync();
            pending += dispatchTimeIds.Count(tid => !transferredKeys.Contains($"dispatch_time_entry:{tid}"));

            return pending;
        }

        // ============== AGGREGATION METHODS ==============

        public async Task<List<DispatchDto>> GetDispatchesForServiceOrderAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Jobs)
                .FirstOrDefaultAsync(so => so.Id == serviceOrderId);
            
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            var linkedDispatchIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);

            // Include AssignedTechnicians to properly populate technician data
            var dispatches = await _context.Dispatches
                .Where(d => linkedDispatchIds.Contains(d.Id))
                .Include(d => d.AssignedTechnicians)
                .AsSingleQuery()
                .ToListAsync();

            // Get all technician IDs to fetch user names
            var allTechnicianIds = dispatches
                .SelectMany(d => d.AssignedTechnicians.Select(at => at.TechnicianId))
                .Distinct()
                .ToList();

            // Fetch user names for all technicians in one query
            var technicianUsers = await _context.Users
                .Where(u => allTechnicianIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToDictionaryAsync(u => u.Id);

            return dispatches.Select(d => new DispatchDto
            {
                Id = d.Id,
                DispatchNumber = d.DispatchNumber,
                JobId = int.TryParse(d.JobId, out var jid) ? jid : 0,
                ServiceOrderId = d.ServiceOrderId,
                ProjectId = d.ProjectId,
                Status = d.Status ?? "pending",
                Priority = d.Priority ?? "medium",
                AssignedTechnicians = d.AssignedTechnicians.Select(at => {
                    var user = technicianUsers.GetValueOrDefault(at.TechnicianId);
                    return new UserLightDto 
                    { 
                        Id = at.TechnicianId,
                        Name = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null,
                        Email = user?.Email
                    };
                }).ToList(),
                Scheduling = new SchedulingDto
                {
                    ScheduledDate = d.ScheduledDate,
                    EstimatedDuration = d.ActualDuration ?? 0
                },
                ScheduledDate = d.ScheduledDate,
                Notes = new System.Collections.Generic.List<object> { (object?)d.Description ?? string.Empty },
                DispatchedBy = d.DispatchedBy,
                DispatchedAt = d.DispatchedAt,
                CreatedAt = d.CreatedDate,
                UpdatedAt = d.ModifiedDate ?? d.CreatedDate
            }).ToList();
        }

        public async Task<List<TimeEntryDto>> GetTimeEntriesForServiceOrderAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Jobs)
                .FirstOrDefaultAsync(so => so.Id == serviceOrderId);
            
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            var allTimeEntries = new List<TimeEntryDto>();

            // 1. Get time entries directly on the service order (ServiceOrderTimeEntries table)
            var soTimeEntries = await _context.ServiceOrderTimeEntries
                .Where(t => t.ServiceOrderId == serviceOrderId)
                .ToListAsync();

            allTimeEntries.AddRange(soTimeEntries.Select(te => new TimeEntryDto
            {
                Id = te.Id,
                DispatchId = 0,
                TechnicianId = te.TechnicianId ?? "",
                WorkType = te.WorkType ?? "general",
                StartTime = te.StartTime,
                EndTime = te.EndTime,
                Duration = te.Duration,
                Description = te.Description,
                TotalCost = te.TotalCost ?? 0,
                Billable = te.Billable,
                HourlyRate = te.HourlyRate,
                CreatedAt = te.CreatedAt,
                InvoiceStatus = te.InvoiceStatus,
                SourceTable = "service_order"
            }));

            // 2. Get time entries from dispatches (installation / multi-job / legacy paths)
            var dispatchIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);

            var timeEntries = await _context.TimeEntries
                .Where(te => dispatchIds.Contains(te.DispatchId))
                .ToListAsync();

            // Dispatch TimeEntries carry no HourlyRate/TotalCost column, so this list used to
            // report every field-logged hour as costing 0 — the exact same lie the invoice
            // transfer avoids. Reuse the same fallback (most recent rate the technician used
            // on a ServiceOrderTimeEntry) so plan-vs-actual and the invoice agree.
            var technicianIdStrings = timeEntries.Select(t => t.TechnicianId.ToString()).Distinct().ToList();
            var fallbackRates = technicianIdStrings.Count == 0
                ? new Dictionary<string, decimal>()
                : await _context.ServiceOrderTimeEntries
                    .Where(t => t.TechnicianId != null && technicianIdStrings.Contains(t.TechnicianId) && t.HourlyRate != null)
                    .GroupBy(t => t.TechnicianId!)
                    .Select(g => new { TechnicianId = g.Key, Rate = g.OrderByDescending(x => x.CreatedAt).First().HourlyRate })
                    .ToDictionaryAsync(x => x.TechnicianId, x => x.Rate ?? 0m);

            allTimeEntries.AddRange(timeEntries.Select(te =>
            {
                var minutes = te.Duration ?? 0m;
                var rate = fallbackRates.TryGetValue(te.TechnicianId.ToString(), out var r) ? r : 0m;
                return new TimeEntryDto
                {
                    Id = te.Id,
                    DispatchId = te.DispatchId,
                    TechnicianId = te.TechnicianId.ToString(),
                    WorkType = te.WorkType ?? "general",
                    StartTime = te.StartTime,
                    EndTime = te.EndTime,
                    Duration = (int)minutes,
                    Description = te.Description,
                    HourlyRate = rate > 0m ? rate : (decimal?)null,
                    TotalCost = Math.Round(minutes / 60m * rate, 2),
                    Billable = te.Billable,
                    CreatedAt = te.CreatedDate,
                    InvoiceStatus = null, // Dispatch TimeEntries don't have InvoiceStatus
                    SourceTable = "dispatch"
                };
            }));

            return allTimeEntries;
        }

        public async Task<List<ExpenseDto>> GetExpensesForServiceOrderAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Jobs)
                .FirstOrDefaultAsync(so => so.Id == serviceOrderId);
            
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            var allExpenses = new List<ExpenseDto>();

            // 1. Get expenses directly on the service order (ServiceOrderExpenses table)
            var soExpenses = await _context.ServiceOrderExpenses
                .Where(e => e.ServiceOrderId == serviceOrderId)
                .ToListAsync();

            allExpenses.AddRange(soExpenses.Select(e => new ExpenseDto
            {
                Id = e.Id,
                DispatchId = 0,
                TechnicianId = e.TechnicianId ?? "",
                Type = e.Type ?? "other",
                Amount = e.Amount,
                Currency = e.Currency ?? "TND",
                Description = e.Description,
                Status = e.Status ?? "pending",
                Date = e.Date ?? e.CreatedAt,
                CreatedAt = e.CreatedAt,
                InvoiceStatus = e.InvoiceStatus,
                SourceTable = "service_order"
            }));

            // 2. Get expenses from dispatches (installation / multi-job / legacy paths)
            var dispatchIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);

            var expenses = await _context.DispatchExpenses
                .Where(e => dispatchIds.Contains(e.DispatchId))
                .ToListAsync();

            allExpenses.AddRange(expenses.Select(e => new ExpenseDto
            {
                Id = e.Id,
                DispatchId = e.DispatchId,
                TechnicianId = e.RecordedBy ?? "",
                Type = e.ExpenseType ?? "other",
                Amount = e.Amount,
                // Expose the real persisted currency (nullable). Callers that need to
                // compare against sale.Currency should treat null as "sale currency".
                Currency = e.Currency,
                Description = e.Description,
                Status = "pending",
                Date = e.ExpenseDate,
                CreatedAt = e.CreatedDate,
                InvoiceStatus = null, // Dispatch Expenses don't have InvoiceStatus
                SourceTable = "dispatch"
            }));

            return allExpenses;
        }

        /// <summary>
        /// ServiceOrder.SaleId is a free-text link: legacy/number-based rows store the sale NUMBER
        /// ("SAL-XL-00006") while newer rows store the numeric id. Resolving both keeps every
        /// sale-linked feature (materials backfill, invoicing, totals) working for both shapes.
        /// </summary>
        private async Task<int?> ResolveSaleIdAsync(string? saleRef)
        {
            if (string.IsNullOrWhiteSpace(saleRef)) return null;
            if (int.TryParse(saleRef, out var numeric)) return numeric;

            var trimmed = saleRef.Trim();
            var id = await _context.Sales
                .Where(s => s.SaleNumber == trimmed)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
            return id;
        }

        /// <summary>
        /// Ensures every non-service item of the originating sale exists as a
        /// ServiceOrderMaterial row. Idempotent: matches on SaleItemId, so it only
        /// inserts what is missing and never duplicates manual materials.
        /// </summary>
        private async Task BackfillSaleMaterialsAsync(ServiceOrder serviceOrder)

        {
            try
            {
                var resolvedSaleId = await ResolveSaleIdAsync(serviceOrder.SaleId);
                if (resolvedSaleId is null) return;
                var saleId = resolvedSaleId.Value;


                var saleItems = await _context.Set<Sales.Models.SaleItem>()
                    .Where(i => i.SaleId == saleId)
                    .ToListAsync();

                var materialItems = saleItems
                    .Where(i => !string.Equals(i.Type?.Trim(), "service", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (materialItems.Count == 0) return;

                var existingSaleItemIds = await _context.ServiceOrderMaterials
                    .Where(m => m.ServiceOrderId == serviceOrder.Id && m.SaleItemId != null)
                    .Select(m => m.SaleItemId!.Value)
                    .ToListAsync();

                var missing = materialItems
                    .Where(i => !existingSaleItemIds.Contains(i.Id))
                    .ToList();
                if (missing.Count == 0) return;

                var rows = missing.Select(item => new ServiceOrderMaterial
                {
                    ServiceOrderId = serviceOrder.Id,
                    SaleItemId = item.Id,
                    ArticleId = item.ArticleId,
                    Name = item.ItemName ?? "Material",
                    Sku = item.ItemCode,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    EstimatedQuantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.LineTotal > 0 ? item.LineTotal : (item.UnitPrice * item.Quantity),
                    Status = "pending",
                    Source = "sale_conversion",
                    InstallationId = int.TryParse(item.InstallationId, out var _bfIid) ? _bfIid : (int?)null,
                    InstallationName = item.InstallationName,
                    CreatedBy = "system",
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _context.ServiceOrderMaterials.AddRange(rows);
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Backfilled {Count} sale material(s) onto service order {ServiceOrderId} from sale {SaleId}",
                    rows.Count, serviceOrder.Id, saleId);
            }
            catch (Exception ex)
            {
                // Never break the read path because of a backfill problem.
                _logger.LogWarning(ex, "Material backfill failed for service order {ServiceOrderId}", serviceOrder.Id);
            }
        }

        public async Task<List<MaterialDto>> GetMaterialsForServiceOrderAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Jobs)
                .FirstOrDefaultAsync(so => so.Id == serviceOrderId);
            
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            // Self-heal: service orders converted before the material copy existed
            // (or whose sale items carried an unexpected type) have no material rows.
            // Backfill them from the originating sale so the Materials tab is always complete.
            await BackfillSaleMaterialsAsync(serviceOrder);

            var allMaterials = new List<MaterialDto>();


            // 1. Get materials directly linked to the service order (from sale conversion or manual)
            var directMaterials = await _context.ServiceOrderMaterials
                .Where(m => m.ServiceOrderId == serviceOrderId)
                .ToListAsync();

            allMaterials.AddRange(directMaterials.Select(m => new MaterialDto
            {
                Id = m.Id,
                ServiceOrderId = m.ServiceOrderId,
                ArticleId = m.ArticleId?.ToString(),
                ArticleName = m.Name,
                Sku = m.Sku,
                Description = m.Description ?? m.Name,
                Quantity = (int)m.Quantity,
                EstimatedQuantity = m.EstimatedQuantity ?? m.Quantity,
                UnitPrice = m.UnitPrice,
                TotalPrice = m.TotalPrice,
                Status = m.Status,
                Source = m.Source,
                InternalComment = m.InternalComment,
                ExternalComment = m.ExternalComment,
                Replacing = m.Replacing,
                OldArticleModel = m.OldArticleModel,
                OldArticleStatus = m.OldArticleStatus,
                InstallationId = m.InstallationId?.ToString(),
                InstallationName = m.InstallationName,
                CreatedBy = m.CreatedBy,
                CreatedAt = m.CreatedAt,
                InvoiceStatus = m.InvoiceStatus,
                SourceTable = "service_order"
            }));

            // 2. Get materials from dispatches (installation / multi-job / legacy paths)
            var dispatchIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);

            var dispatchMaterials = await _context.DispatchMaterials
                .Where(m => dispatchIds.Contains(m.DispatchId))
                .ToListAsync();

            allMaterials.AddRange(dispatchMaterials.Select(m => new MaterialDto
            {
                Id = m.Id, // Use real ID - SourceTable differentiates
                DispatchId = m.DispatchId,
                TechnicianId = m.RecordedBy,
                ArticleId = m.ArticleId?.ToString(),
                ArticleName = m.Description,
                Description = m.Description,
                Quantity = (int)m.Quantity,
                UnitPrice = m.UnitPrice,
                TotalPrice = m.TotalPrice,
                Status = "used",
                Source = "dispatch",
                CreatedBy = m.RecordedBy,
                CreatedAt = m.UsedDate,
                InvoiceStatus = null, // Dispatch materials don't have InvoiceStatus
                SourceTable = "dispatch"
            }));

            return allMaterials;
        }

        public async Task<List<NoteDto>> GetNotesForServiceOrderAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Jobs)
                .FirstOrDefaultAsync(so => so.Id == serviceOrderId);
            
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            var allNotes = new List<NoteDto>();

            // Get notes directly on the service order from ServiceOrderNotes table
            var serviceOrderNotes = await _context.ServiceOrderNotes
                .Where(n => n.ServiceOrderId == serviceOrderId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            allNotes.AddRange(serviceOrderNotes.Select(n => new NoteDto
            {
                Id = n.Id,
                DispatchId = 0, // No dispatch ID for service order notes
                Content = n.Content ?? "",
                Category = n.Type,
                CreatedBy = n.CreatedByName ?? n.CreatedBy,
                CreatedAt = n.CreatedAt
            }));

            // Also get notes from dispatches (installation / multi-job / legacy paths, soft-delete aware)
            var dispatchIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);

            var dispatchNotes = await _context.DispatchNotes
                .Where(n => dispatchIds.Contains(n.DispatchId))
                .ToListAsync();

            allNotes.AddRange(dispatchNotes.Select(n => new NoteDto
            {
                Id = n.Id,
                DispatchId = n.DispatchId,
                Content = n.Content ?? "",
                Category = n.NoteType,
                CreatedBy = n.CreatedBy,
                CreatedAt = n.CreatedDate
            }));

            // Return sorted by date, newest first
            return allNotes.OrderByDescending(n => n.CreatedAt).ToList();
        }

        public async Task<ServiceOrderFullSummaryDto> GetFullSummaryAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Jobs)
                .FirstOrDefaultAsync(so => so.Id == serviceOrderId);
            
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            // Get contact
            Contact? contact = null;
            if (serviceOrder.ContactId > 0)
            {
                contact = await _context.Contacts.FindAsync(serviceOrder.ContactId);
            }

            var jobIds = serviceOrder.Jobs?.Select(j => j.Id).ToList() ?? new List<int>();
            var installationIds = serviceOrder.Jobs?
                .Where(j => j.InstallationId.HasValue)
                .Select(j => j.InstallationId!.Value)
                .Distinct()
                .ToList() ?? new List<int>();

            // Resolve dispatches attributable to this SO through THREE paths (any one match):
            //   (a) Dispatch.ServiceOrderId points at us (installation / whole-SO dispatches).
            //   (b) DispatchJobs join table links to one of our jobs (multi-job dispatches).
            //   (c) Legacy: Dispatch.JobId string equals one of our job ids (old single-job dispatches).
            // Always exclude soft-deleted dispatches so rollups don't double-count.
            var jobIdStrings = jobIds.Select(j => j.ToString()).ToList();
            var dispatchIdsViaJoin = await _context.Set<DispatchJob>()
                .Where(dj => !dj.IsDeleted && jobIds.Contains(dj.JobId))
                .Select(dj => dj.DispatchId)
                .Distinct()
                .ToListAsync();

            var dispatches = await _context.Dispatches
                .Where(d => !d.IsDeleted && (
                       d.ServiceOrderId == serviceOrderId
                    || dispatchIdsViaJoin.Contains(d.Id)
                    || (d.JobId != null && jobIdStrings.Contains(d.JobId))
                    || (d.InstallationId.HasValue && installationIds.Contains(d.InstallationId.Value))
                ))
                .ToListAsync();

            var dispatchIds = dispatches.Select(d => d.Id).ToList();

            // Get all aggregated data
            var timeEntries = await _context.TimeEntries
                .Where(te => dispatchIds.Contains(te.DispatchId))
                .ToListAsync();

            var expenses = await _context.DispatchExpenses
                .Where(e => dispatchIds.Contains(e.DispatchId))
                .ToListAsync();

            var materials = await _context.DispatchMaterials
                .Where(m => dispatchIds.Contains(m.DispatchId))
                .ToListAsync();

            var notes = await _context.DispatchNotes
                .Where(n => dispatchIds.Contains(n.DispatchId))
                .ToListAsync();

            // Build dispatch summaries. JobId can come from legacy string, DispatchJobs join, or 0 for whole-SO/installation dispatches.
            var dispatchJobLinks = await _context.Set<DispatchJob>()
                .Where(dj => !dj.IsDeleted && dispatchIds.Contains(dj.DispatchId))
                .Select(dj => new { dj.DispatchId, dj.JobId })
                .ToListAsync();
            var jobLinkByDispatch = dispatchJobLinks
                .GroupBy(x => x.DispatchId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.JobId).FirstOrDefault());

            var dispatchSummaries = dispatches.Select(d => new DispatchSummaryDto
            {
                Id = d.Id,
                JobId = int.TryParse(d.JobId, out var jid) ? jid : jobLinkByDispatch.GetValueOrDefault(d.Id, 0),
                TechnicianId = d.AssignedTechnicians?.FirstOrDefault()?.TechnicianId.ToString(),
                Status = d.Status ?? "pending",
                ScheduledDate = d.ScheduledDate,
                TimeEntryCount = timeEntries.Count(te => te.DispatchId == d.Id),
                ExpenseCount = expenses.Count(e => e.DispatchId == d.Id),
                MaterialCount = materials.Count(m => m.DispatchId == d.Id)
            }).ToList();

            // Calculate totals
            var totalDuration = timeEntries.Sum(te => te.Duration ?? 0);
            var totalLaborCost = 0m; // No TotalCost in TimeEntry model
            var totalExpenses = expenses.Sum(e => e.Amount);
            var totalMaterialCost = materials.Sum(m => m.TotalPrice);

            return new ServiceOrderFullSummaryDto
            {
                ServiceOrderId = serviceOrder.Id,
                OrderNumber = serviceOrder.OrderNumber ?? "",
                Status = serviceOrder.Status ?? "",
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
                JobCount = serviceOrder.Jobs?.Count() ?? 0,
                Jobs = serviceOrder.Jobs?.Select(j => new ServiceOrderJobDto
                {
                    Id = j.Id,
                    ServiceOrderId = j.ServiceOrderId,
                    Title = j.Title ?? "",
                    Description = j.Description,
                    Status = j.Status ?? "unscheduled",
                    InstallationId = j.InstallationId?.ToString(),
                    WorkType = j.WorkType,
                    EstimatedDuration = j.EstimatedDuration,
                    EstimatedCost = j.EstimatedCost,
                    CompletionPercentage = j.CompletionPercentage,
                    AssignedTechnicianIds = j.AssignedTechnicianIds
                }).ToList() ?? new List<ServiceOrderJobDto>(),
                DispatchCount = dispatches.Count(),
                Dispatches = dispatchSummaries,
                TotalTimeEntries = timeEntries.Count(),
                TotalDuration = (int)totalDuration,
                TotalLaborCost = totalLaborCost,
                TotalExpenseCount = expenses.Count(),
                TotalExpenses = totalExpenses,
                TotalMaterialCount = materials.Count(),
                TotalMaterialCost = totalMaterialCost,
                TotalNoteCount = notes.Count(),
                GrandTotal = totalLaborCost + totalExpenses + totalMaterialCost
            };
        }

        public async Task<ServiceOrderMaterialDto> AddMaterialAsync(int serviceOrderId, CreateServiceOrderMaterialDto dto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            // Determine unit from article or DTO
            var unitValue = dto.Unit ?? "piece";
            if (string.IsNullOrEmpty(dto.Unit) && dto.ArticleId.HasValue)
            {
                var articleForUnit = await _context.Articles.FirstOrDefaultAsync(a => a.Id == dto.ArticleId.Value);
                if (articleForUnit != null && !string.IsNullOrEmpty(articleForUnit.Unit))
                    unitValue = articleForUnit.Unit;
            }

            var material = new ServiceOrderMaterial
            {
                ServiceOrderId = serviceOrderId,
                ArticleId = dto.ArticleId,
                Name = dto.Name,
                Sku = dto.Sku,
                Description = dto.Description,
                Quantity = dto.Quantity,
                EstimatedQuantity = dto.EstimatedQuantity ?? dto.Quantity,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.Quantity * dto.UnitPrice,
                Status = "pending",
                Source = "manual",
                InternalComment = dto.InternalComment,
                ExternalComment = dto.ExternalComment,
                Replacing = dto.Replacing,
                OldArticleModel = dto.OldArticleModel,
                OldArticleStatus = dto.OldArticleStatus,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                Unit = unitValue
            };

            _context.ServiceOrderMaterials.Add(material);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added material {MaterialId} to service order {ServiceOrderId}", material.Id, serviceOrderId);

            return new ServiceOrderMaterialDto
            {
                Id = material.Id,
                ServiceOrderId = material.ServiceOrderId,
                SaleItemId = material.SaleItemId,
                ArticleId = material.ArticleId,
                Name = material.Name,
                Sku = material.Sku,
                Description = material.Description,
                Quantity = material.Quantity,
                EstimatedQuantity = material.EstimatedQuantity ?? material.Quantity,
                UnitPrice = material.UnitPrice,
                TotalPrice = material.TotalPrice,
                Status = material.Status,
                Source = material.Source,
                InternalComment = material.InternalComment,
                ExternalComment = material.ExternalComment,
                Replacing = material.Replacing,
                OldArticleModel = material.OldArticleModel,
                OldArticleStatus = material.OldArticleStatus,
                InstallationId = material.InstallationId?.ToString(),
                InstallationName = material.InstallationName,
                CreatedBy = material.CreatedBy,
                CreatedAt = material.CreatedAt,
                Unit = material.Unit
            };
        }

        public async Task<ServiceOrderMaterialDto?> UpdateMaterialAsync(int serviceOrderId, int materialId, UpdateServiceOrderMaterialDto dto, string userId)
        {
            var material = await _context.ServiceOrderMaterials
                .FirstOrDefaultAsync(m => m.Id == materialId && m.ServiceOrderId == serviceOrderId);
            
            if (material == null)
                return null;

            if (dto.Name != null) material.Name = dto.Name;
            if (dto.Sku != null) material.Sku = dto.Sku;
            if (dto.Description != null) material.Description = dto.Description;
            if (dto.Quantity.HasValue) material.Quantity = dto.Quantity.Value;
            if (dto.EstimatedQuantity.HasValue) material.EstimatedQuantity = dto.EstimatedQuantity.Value;
            if (dto.UnitPrice.HasValue) material.UnitPrice = dto.UnitPrice.Value;
            if (dto.Quantity.HasValue || dto.UnitPrice.HasValue)
                material.TotalPrice = material.Quantity * material.UnitPrice;
            if (dto.InternalComment != null) material.InternalComment = dto.InternalComment;
            if (dto.ExternalComment != null) material.ExternalComment = dto.ExternalComment;
            if (dto.Replacing.HasValue) material.Replacing = dto.Replacing.Value;
            if (dto.OldArticleModel != null) material.OldArticleModel = dto.OldArticleModel;
            if (dto.OldArticleStatus != null) material.OldArticleStatus = dto.OldArticleStatus;
            if (dto.Status != null) material.Status = dto.Status;
            material.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated material {MaterialId} for service order {ServiceOrderId}", materialId, serviceOrderId);

            return new ServiceOrderMaterialDto
            {
                Id = material.Id,
                ServiceOrderId = material.ServiceOrderId,
                SaleItemId = material.SaleItemId,
                ArticleId = material.ArticleId,
                Name = material.Name,
                Sku = material.Sku,
                Description = material.Description,
                Quantity = material.Quantity,
                EstimatedQuantity = material.EstimatedQuantity ?? material.Quantity,
                UnitPrice = material.UnitPrice,
                TotalPrice = material.TotalPrice,
                Status = material.Status,
                Source = material.Source,
                InternalComment = material.InternalComment,
                ExternalComment = material.ExternalComment,
                Replacing = material.Replacing,
                OldArticleModel = material.OldArticleModel,
                OldArticleStatus = material.OldArticleStatus,
                InstallationId = material.InstallationId?.ToString(),
                InstallationName = material.InstallationName,
                CreatedBy = material.CreatedBy,
                CreatedAt = material.CreatedAt
            };
        }

        public async Task<bool> DeleteMaterialAsync(int serviceOrderId, int materialId, string userId)
        {
            var material = await _context.ServiceOrderMaterials
                .FirstOrDefaultAsync(m => m.Id == materialId && m.ServiceOrderId == serviceOrderId);
            
            if (material == null)
                return false;

            _context.ServiceOrderMaterials.Remove(material);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted material {MaterialId} from service order {ServiceOrderId}", materialId, serviceOrderId);

            return true;
        }

        // ========== TIME ENTRY MANAGEMENT ==========

        public async Task<ServiceOrderTimeEntryDto> AddTimeEntryAsync(int serviceOrderId, CreateServiceOrderTimeEntryDto dto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            var duration = (int)(dto.EndTime - dto.StartTime).TotalMinutes;
            var totalCost = dto.Billable && dto.HourlyRate.HasValue 
                ? (dto.HourlyRate.Value * duration / 60) 
                : (decimal?)null;

            var timeEntry = new ServiceOrderTimeEntry
            {
                ServiceOrderId = serviceOrderId,
                TechnicianId = dto.TechnicianId ?? userId,
                WorkType = dto.WorkType,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Duration = duration,
                Description = dto.Description,
                Billable = dto.Billable,
                HourlyRate = dto.HourlyRate,
                TotalCost = totalCost,
                Status = "pending",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ServiceOrderTimeEntries.Add(timeEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added time entry {TimeEntryId} to service order {ServiceOrderId}", timeEntry.Id, serviceOrderId);

            return new ServiceOrderTimeEntryDto
            {
                Id = timeEntry.Id,
                ServiceOrderId = timeEntry.ServiceOrderId,
                TechnicianId = timeEntry.TechnicianId,
                WorkType = timeEntry.WorkType,
                StartTime = timeEntry.StartTime,
                EndTime = timeEntry.EndTime,
                Duration = timeEntry.Duration,
                Description = timeEntry.Description,
                Billable = timeEntry.Billable,
                HourlyRate = timeEntry.HourlyRate,
                TotalCost = timeEntry.TotalCost,
                Status = timeEntry.Status,
                Source = "service_order",
                CreatedAt = timeEntry.CreatedAt
            };
        }

        public async Task<bool> DeleteTimeEntryAsync(int serviceOrderId, int timeEntryId, string userId)
        {
            var timeEntry = await _context.ServiceOrderTimeEntries
                .FirstOrDefaultAsync(t => t.Id == timeEntryId && t.ServiceOrderId == serviceOrderId);
            
            if (timeEntry == null)
                return false;

            _context.ServiceOrderTimeEntries.Remove(timeEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted time entry {TimeEntryId} from service order {ServiceOrderId}", timeEntryId, serviceOrderId);

            return true;
        }

        // ========== EXPENSE MANAGEMENT ==========

        public async Task<ServiceOrderExpenseDto> AddExpenseAsync(int serviceOrderId, CreateServiceOrderExpenseDto dto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            var expense = new ServiceOrderExpense
            {
                ServiceOrderId = serviceOrderId,
                TechnicianId = dto.TechnicianId ?? userId,
                Type = dto.Type,
                Amount = dto.Amount,
                Currency = dto.Currency,
                Description = dto.Description,
                Date = dto.Date ?? DateTime.UtcNow,
                Status = "pending",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ServiceOrderExpenses.Add(expense);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added expense {ExpenseId} to service order {ServiceOrderId}", expense.Id, serviceOrderId);

            return new ServiceOrderExpenseDto
            {
                Id = expense.Id,
                ServiceOrderId = expense.ServiceOrderId,
                TechnicianId = expense.TechnicianId,
                Type = expense.Type,
                Amount = expense.Amount,
                Currency = expense.Currency,
                Description = expense.Description,
                Date = expense.Date,
                Status = expense.Status,
                Source = "service_order",
                CreatedAt = expense.CreatedAt
            };
        }

        public async Task<bool> DeleteExpenseAsync(int serviceOrderId, int expenseId, string userId)
        {
            var expense = await _context.ServiceOrderExpenses
                .FirstOrDefaultAsync(e => e.Id == expenseId && e.ServiceOrderId == serviceOrderId);
            
            if (expense == null)
                return false;

            _context.ServiceOrderExpenses.Remove(expense);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted expense {ExpenseId} from service order {ServiceOrderId}", expenseId, serviceOrderId);

            return true;
        }

        // ========== SERVICE ORDER JOBS (routes: GET/PATCH .../jobs/{jobId}/status, PUT .../jobs/{jobId}) ==========

        public async Task<ServiceOrderJobDto?> GetServiceOrderJobAsync(int serviceOrderId, int jobId)
        {
            var job = await _context.ServiceOrderJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId && j.ServiceOrderId == serviceOrderId);
            return job == null ? null : MapServiceOrderJobToDto(job, null);
        }

        public async Task<ServiceOrderJobDto> CreateServiceOrderJobAsync(int serviceOrderId, CreateServiceOrderJobDto dto, string userId)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new ArgumentException("Title is required", nameof(dto));

            var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(so => so.Id == serviceOrderId);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {serviceOrderId} not found");

            var job = new ServiceOrderJob
            {
                ServiceOrderId = serviceOrderId,
                Title = dto.Title.Trim(),
                Description = dto.Description,
                // Older tenant schemas still enforce NOT NULL on JobDescription.
                // Keep the richer field when supplied, otherwise derive a safe value
                // from the normal description/title so job creation works across both
                // legacy and current schemas.
                JobDescription = !string.IsNullOrWhiteSpace(dto.JobDescription)
                    ? dto.JobDescription
                    : (!string.IsNullOrWhiteSpace(dto.Description) ? dto.Description : dto.Title.Trim()),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "unscheduled" : dto.Status,
                Priority = dto.Priority ?? "medium",
                WorkType = dto.WorkType,
                EstimatedDuration = dto.EstimatedDuration,
                EstimatedCost = dto.EstimatedCost ?? 0,
                InstallationId = dto.InstallationId,
                InstallationName = dto.InstallationName,
                Notes = dto.Notes,
                AssignedTechnicianIds = dto.AssignedTechnicianIds,
                RequiredSkills = dto.RequiredSkills,
                CompletionPercentage = 0,
                ActualCost = 0,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.ServiceOrderJobs.Add(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[SO-JOB-CREATE] Job {JobId} added to service order {ServiceOrderId} by {UserId}",
                job.Id, serviceOrderId, userId);

            return MapServiceOrderJobToDto(job, null);
        }

        /// <summary>
        /// Job status came straight off the DTO with no validation, so a typo (or an old
        /// client) could write any string into ServiceOrderJob.Status and permanently
        /// desync the job from the dispatcher board and the roll-up logic, which compare
        /// against fixed literals. Unknown values are rejected rather than silently stored.
        /// </summary>
        private static readonly HashSet<string> AllowedJobStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "unscheduled", "pending", "ready", "scheduled", "dispatched",
            "in_progress", "on_hold", "completed", "cancelled"
        };

        private static string NormalizeJobStatus(string? status)
        {
            var s = (status ?? "").Trim();
            if (s.Length == 0)
                throw new ArgumentException("Job status is required.");
            if (!AllowedJobStatuses.Contains(s))
                throw new ArgumentException(
                    $"Invalid job status '{status}'. Allowed: {string.Join(", ", AllowedJobStatuses)}.");
            return s.ToLowerInvariant();
        }


        public async Task<ServiceOrderJobDto> PatchServiceOrderJobStatusAsync(int serviceOrderId, int jobId, UpdateServiceOrderJobStatusDto dto, string userId)
        {
            var job = await _context.ServiceOrderJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.ServiceOrderId == serviceOrderId);
            if (job == null)
                throw new KeyNotFoundException($"Job {jobId} not found for service order {serviceOrderId}");
            var oldStatus = job.Status;
            job.Status = NormalizeJobStatus(dto.Status);
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Job {JobId} status set to {Status} on service order {ServiceOrderId} by {UserId}", jobId, dto.Status, serviceOrderId, userId);

            // Fire workflow trigger for job status change
            if (_workflowTriggerService != null && oldStatus != job.Status)
            {
                try
                {
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "job",
                        jobId,
                        oldStatus,
                        job.Status,
                        userId,
                        new { jobId, serviceOrderId, title = job.Title });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fire job workflow trigger for job {JobId}", jobId);
                }
            }
            return MapServiceOrderJobToDto(job, null);
        }

        public async Task<ServiceOrderJobDto> UpdateServiceOrderJobAsync(int serviceOrderId, int jobId, UpdateServiceOrderJobDto dto, string userId)
        {
            var job = await _context.ServiceOrderJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.ServiceOrderId == serviceOrderId);
            if (job == null)
                throw new KeyNotFoundException($"Job {jobId} not found for service order {serviceOrderId}");
            var oldStatus = job.Status;
            if (dto.Status != null) job.Status = NormalizeJobStatus(dto.Status);
            if (dto.Title != null) job.Title = dto.Title;
            if (dto.Description != null) job.Description = dto.Description;
            if (dto.WorkType != null) job.WorkType = dto.WorkType;
            if (dto.EstimatedDuration.HasValue) job.EstimatedDuration = dto.EstimatedDuration;
            if (dto.EstimatedCost.HasValue) job.EstimatedCost = dto.EstimatedCost;
            if (dto.CompletionPercentage.HasValue) job.CompletionPercentage = dto.CompletionPercentage;
            if (dto.AssignedTechnicianIds != null) job.AssignedTechnicianIds = dto.AssignedTechnicianIds;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Job {JobId} updated on service order {ServiceOrderId} by {UserId}", jobId, serviceOrderId, userId);

            if (_workflowTriggerService != null && dto.Status != null && oldStatus != job.Status)
            {
                try
                {
                    await _workflowTriggerService.TriggerStatusChangeAsync(
                        "job",
                        jobId,
                        oldStatus,
                        job.Status,
                        userId,
                        new { jobId, serviceOrderId, title = job.Title });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fire job workflow trigger for job {JobId}", jobId);
                }
            }
            return MapServiceOrderJobToDto(job, null);
        }

        private static ServiceOrderJobDto MapServiceOrderJobToDto(ServiceOrderJob j, Dictionary<string, string>? userNames)
        {
            return new ServiceOrderJobDto
            {
                Id = j.Id,
                ServiceOrderId = j.ServiceOrderId,
                Title = j.Title ?? string.Empty,
                Description = j.Description,
                Status = j.Status,
                InstallationId = j.InstallationId?.ToString(),
                WorkType = j.WorkType,
                EstimatedDuration = j.EstimatedDuration,
                EstimatedCost = j.EstimatedCost,
                CompletionPercentage = j.CompletionPercentage,
                AssignedTechnicianIds = j.AssignedTechnicianIds,
                RequiredSkills = j.RequiredSkills,
                AssignedTechnicians = j.AssignedTechnicianIds?.Select(id => new UserLightDto
                {
                    Id = int.TryParse(id, out var parsedId) ? parsedId : 0,
                    Name = userNames?.GetValueOrDefault(id) ?? id,
                    Email = null
                }).ToList()
            };
        }

        // ========== INVOICE PREPARATION ==========

        public async Task<ServiceOrderDto> PrepareForInvoiceAsync(int id, PrepareInvoiceDto dto, string userId)
        {
            var serviceOrder = await _context.ServiceOrders
                .Include(so => so.Jobs)
                .FirstOrDefaultAsync(so => so.Id == id);
            if (serviceOrder == null)
                throw new KeyNotFoundException($"Service order with ID {id} not found");

            // Accept every status that means "field work is done, billing can start":
            //  - technically_completed : all dispatches completed (BusinessWorkflowService)
            //  - partially_completed   : some dispatches completed (BusinessWorkflowService, line 836)
            //  - completed             : ApproveAsync / CompleteAsync final-completion paths
            //  - ready_for_invoice     : retry / add-more-items after a previous transfer
            // The FE normalizes partially_completed and completed to "ready_for_invoice"
            // for display, so refusing them here surfaced a confusing error on the exact
            // click the UI was inviting.
            var invoiceableStatuses = new[] { "technically_completed", "partially_completed", "completed", "ready_for_invoice" };
            if (!invoiceableStatuses.Contains(serviceOrder.Status))
                throw new InvalidOperationException(
                    $"Service order status '{serviceOrder.Status}' is not eligible for invoice preparation. " +
                    "Expected one of: technically_completed, partially_completed, completed, ready_for_invoice.");

            // Standalone service orders (created through /service-orders/direct, i.e. field work
            // that never came from a sale) have no sale to transfer items onto. Refusing them here
            // dead-ended every direct order: the field work completed and could never be billed.
            // There is nothing to transfer, so we simply move the order into the billing queue and
            // let the user invoice it from there.
            if (string.IsNullOrWhiteSpace(serviceOrder.SaleId))
            {
                if (!string.Equals(serviceOrder.Status, "ready_for_invoice", StringComparison.OrdinalIgnoreCase))
                {
                    serviceOrder.Status = "ready_for_invoice";
                    serviceOrder.ModifiedDate = DateTime.UtcNow;
                    serviceOrder.ModifiedBy = userId;
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation(
                    "PrepareForInvoice: SO {Id} has no linked sale (standalone order) → marked ready_for_invoice without item transfer",
                    id);

                return (await GetServiceOrderByIdAsync(id))!;
            }


            // SaleId is a free-text link column: some rows store the numeric sale id, others the
            // human sale number (e.g. "SAL-XL-00006"). Resolve both, otherwise every order created
            // through the number-based path was permanently un-invoiceable.
            Sales.Models.Sale? sale;
            if (int.TryParse(serviceOrder.SaleId, out int saleId))
            {
                sale = await _context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId);
            }
            else
            {
                var saleRef = serviceOrder.SaleId.Trim();
                sale = await _context.Sales.Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.SaleNumber == saleRef);
                saleId = sale?.Id ?? 0;
            }

            if (sale == null)
                throw new KeyNotFoundException($"Linked sale '{serviceOrder.SaleId}' not found");


            // A cancelled sale can never be invoiced (InvoiceService enforces this), so
            // transferring field work onto it silently buried the revenue.
            if (string.Equals(sale.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Sale {sale.Id} is cancelled — its service order items cannot be transferred for invoicing.");

            // SaleService.GuardSaleNotInvoicedAsync blocks manual item edits once any live
            // invoice exists, but this transfer path bypassed it entirely. Blocking outright
            // would break legitimate partial invoicing (invoice a first batch, keep working,
            // transfer the rest), so we only refuse when the sale is ALREADY FULLY invoiced —
            // at that point new lines can never be billed and would just be swallowed.
            var liveInvoices = await _context.Set<MyApi.Modules.Invoices.Models.Invoice>()
                .Where(i => !i.IsDeleted && i.SaleId == saleId && i.Status != "void")
                .Select(i => new { i.Id, i.InvoiceNumber, i.GrandTotal })
                .ToListAsync();
            if (liveInvoices.Count > 0)
            {
                // Recompute the ceiling from live items + header discount/tax/fiscal stamp,
                // exactly like InvoiceService does. The stored Sale.GrandTotal/TotalAmount are
                // transiently a raw sum of line totals during this very method, so reading them
                // here would block legitimate partial transfers (ceiling too low once tax and
                // the fiscal stamp apply) or miss fully-invoiced sales (ceiling too high with a
                // header discount).
                var ceilingItems = sale.Items ?? new List<Sales.Models.SaleItem>();
                var saleCeiling = ceilingItems.Count > 0
                    ? Sales.Services.SaleTotalsCalculator.Compute(
                        ceilingItems.Sum(i => Sales.Services.SaleTotalsCalculator.ComputeLineTotal(
                            i.Quantity, i.UnitPrice, i.Discount, i.DiscountType)),
                        sale.Discount, Sales.Services.SaleTotalsCalculator.HeaderDiscountType(sale),
                        sale.Taxes, sale.TaxType, sale.FiscalStamp).GrandTotal
                    : (sale.GrandTotal > 0m ? sale.GrandTotal : sale.TotalAmount);
                var invoicedTotal = liveInvoices.Sum(i => i.GrandTotal);
                if (saleCeiling > 0m && invoicedTotal >= saleCeiling - 0.009m)
                    throw new InvalidOperationException(
                        $"Sale {sale.Id} is already fully invoiced ({invoicedTotal:0.##} of {saleCeiling:0.##} {sale.Currency}) on " +
                        $"{string.Join(", ", liveInvoices.Select(i => i.InvoiceNumber ?? $"#{i.Id}"))}. " +
                        "Void an invoice before transferring more service order items to it.");

                _logger.LogInformation(
                    "PrepareForInvoice: SO {Id} - sale {SaleId} already has {Count} live invoice(s) totalling {Total}; transferring additional items for partial invoicing.",
                    id, saleId, liveInvoices.Count, invoicedTotal);
            }


            _logger.LogInformation("PrepareForInvoice: SO={Id}, SaleId={SaleId}, current sale items={Count}", id, saleId, sale.Items?.Count ?? 0);

            // Resolve linked dispatches through installation / multi-job / legacy paths, soft-delete aware.
            // Using the same helper as GetInvoiceSummary keeps the two views strictly in sync.
            var linkedDispatchIds = await ResolveLinkedDispatchIdsAsync(serviceOrder);

            // #6 fix: when the SO is only PARTIALLY completed, refuse to bill work from
            // dispatches that aren't done yet. Previously any linked-dispatch row could be
            // pushed to the sale even if the technician hadn't finished the visit. We keep
            // partial invoicing (its intended feature) but scope the "linked dispatches"
            // set to those with Status == "completed" for the partial case. Fully
            // technically_completed / completed / ready_for_invoice SOs are unaffected
            // because by definition all their dispatches are already done.
            if (string.Equals(serviceOrder.Status, "partially_completed", StringComparison.OrdinalIgnoreCase)
                && linkedDispatchIds.Count > 0)
            {
                var completedLinkedDispatchIds = await _context.Dispatches
                    .Where(d => linkedDispatchIds.Contains(d.Id) && d.Status == "completed")
                    .Select(d => d.Id)
                    .ToListAsync();
                var skipped = linkedDispatchIds.Except(completedLinkedDispatchIds).ToList();
                if (skipped.Any())
                {
                    _logger.LogInformation(
                        "PrepareForInvoice: SO {Id} is partially_completed; ignoring {Count} not-yet-completed dispatch(es): [{Ids}]",
                        id, skipped.Count, string.Join(",", skipped));
                }
                linkedDispatchIds = completedLinkedDispatchIds;
            }


            // Idempotency for dispatch-sourced lines: DispatchMaterials / DispatchExpenses / dispatch TimeEntries
            // do NOT carry an InvoiceStatus column, so we can't flip a per-row flag like we do for
            // SO-sourced entities. Instead we build a signature set from existing SaleItems already
            // tagged with this ServiceOrderId and silently skip any incoming dispatch row that would
            // produce the same signature. This lets the user re-open "Prepare for invoice" freely
            // to push newly-added dispatch items to the sale without being blocked, and without
            // double-billing rows that were transferred in a previous run.
            var existingSoSaleItems = (sale.Items ?? new List<Sales.Models.SaleItem>())
                .Where(si => si.ServiceOrderId == id.ToString())
                .ToList();

            // Fix §4.4: include ArticleId + InstallationId in the idempotency
            // signature. Two distinct dispatch/SO material lines that share the
            // same human-readable name/description/price/qty but hit different
            // installations (or reference different Articles) must NOT collide
            // and get silently dropped as "already transferred" — that was the
            // silent-under-billing bug.
            static string BuildSaleItemSignature(string? type, string? itemName, string? description, decimal unitPrice, decimal quantity, int? articleId, string? installationId)
                => string.Join("|",
                    (type ?? "").Trim().ToLowerInvariant(),
                    (itemName ?? "").Trim().ToLowerInvariant(),
                    (description ?? "").Trim().ToLowerInvariant(),
                    Math.Round(unitPrice, 4).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Math.Round(quantity, 4).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    articleId?.ToString() ?? "",
                    (installationId ?? "").Trim());

            var existingSignatures = new HashSet<string>(
                existingSoSaleItems.Select(si => BuildSaleItemSignature(si.Type, si.ItemName, si.Description, si.UnitPrice, si.Quantity, si.ArticleId, si.InstallationId)));

            // Primary idempotency key for dispatch-sourced lines: the identity of the source
            // row itself (SaleItem.SourceType/SourceId), which no edit to price, rate or
            // description can change. The value signature above is kept only as a fallback
            // for legacy rows transferred before SourceId existed, so historical sales are
            // still deduplicated. This is what makes re-planning a service order and
            // re-running "Prepare for invoice" any number of times safe.
            static string BuildSourceKey(string sourceType, int sourceId) => $"{sourceType}:{sourceId}";

            // Lines that end up on the sale without a usable price are still transferred
            // (so the work stays visible) but are now reported back to the caller instead
            // of only being written to the server log, where nobody saw them.
            var pricingWarnings = new List<string>();

            // Price resolution for transferred materials. Technicians frequently log a material by
            // article/SKU without a price, which used to land on the sale as a 0.00 line and got
            // invoiced for free. Fall back to the article's current SalesPrice, and always recompute
            // the line total from the resolved unit price so a stale/zero TotalPrice can't win.
            async Task<(decimal UnitPrice, decimal LineTotal)> ResolveMaterialPriceAsync(
                int? articleId, decimal unitPrice, decimal quantity, decimal storedTotal, string label)

            {
                var resolved = unitPrice;

                if (resolved <= 0m && quantity > 0m && storedTotal > 0m)
                    resolved = storedTotal / quantity;

                if (resolved <= 0m && articleId.HasValue)
                {
                    var articlePrice = await _context.Articles
                        .Where(a => a.Id == articleId.Value && !a.IsDeleted)
                        .Select(a => (decimal?)a.SalesPrice)
                        .FirstOrDefaultAsync();

                    if (articlePrice.HasValue && articlePrice.Value > 0m)
                    {
                        resolved = articlePrice.Value;
                        _logger.LogInformation(
                            "PrepareForInvoice: {Label} had no unit price — using article #{ArticleId} sales price {Price}",
                            label, articleId.Value, resolved);
                    }
                }

                if (resolved <= 0m)
                {
                    _logger.LogWarning(
                        "PrepareForInvoice: {Label} is being transferred with a zero unit price — no price on the line and none on its article",
                        label);
                    pricingWarnings.Add($"{label} was transferred with a zero price — set a price on the sale line before invoicing.");
                }


                return (resolved, Math.Round(resolved * quantity, 2));
            }



            var existingSourceKeys = new HashSet<string>(
                existingSoSaleItems
                    .Where(si => !string.IsNullOrWhiteSpace(si.SourceType) && !string.IsNullOrWhiteSpace(si.SourceId))
                    .Select(si => $"{si.SourceType!.Trim()}:{si.SourceId!.Trim()}"));

            // Legacy sales (rows transferred before SourceType/SourceId existed) have no
            // source keys to compare against, so for those we keep the old value-signature
            // dedup as the only available guard. Captured once, before the loops start
            // filling existingSourceKeys.
            var useLegacySignatureDedup = existingSoSaleItems.Any() && existingSourceKeys.Count == 0;

            var previouslyTransferred = existingSoSaleItems.Any();

            // Cross-service-order guard. Dispatches resolve to a service order partly through
            // a shared InstallationId, so the SAME dispatch material / expense / time entry can
            // legitimately resolve for two different service orders on two different sales.
            // The signature + source-key sets above only look at THIS service order's own sale
            // items, so the second transfer saw the row as brand new and billed it twice.
            // Source keys are globally unique per source row, so pull in every sale item
            // anywhere that has already consumed one of the rows requested right now.
            var requestedSourceKeys = new HashSet<string>();
            foreach (var mid in dto.DispatchMaterialIds ?? new List<int>())
                requestedSourceKeys.Add(BuildSourceKey("dispatch_material", mid));
            foreach (var eid in dto.DispatchExpenseIds ?? new List<int>())
                requestedSourceKeys.Add(BuildSourceKey("dispatch_expense", eid));
            foreach (var tid in dto.DispatchTimeEntryIds ?? new List<int>())
                requestedSourceKeys.Add(BuildSourceKey("dispatch_time_entry", tid));

            if (requestedSourceKeys.Count > 0)
            {
                var dispatchSourceTypes = new[] { "dispatch_material", "dispatch_expense", "dispatch_time_entry" };
                var requestedSourceIds = requestedSourceKeys.Select(k => k.Split(':')[1]).Distinct().ToList();
                var globallyTransferred = await _context.SaleItems
                    .Where(si => si.SourceType != null && si.SourceId != null
                                 && dispatchSourceTypes.Contains(si.SourceType)
                                 && requestedSourceIds.Contains(si.SourceId))
                    .Select(si => si.SourceType + ":" + si.SourceId)
                    .Distinct()
                    .ToListAsync();

                var alreadyBilledElsewhere = globallyTransferred
                    .Where(k => requestedSourceKeys.Contains(k) && !existingSourceKeys.Contains(k))
                    .ToList();

                foreach (var key in alreadyBilledElsewhere) existingSourceKeys.Add(key);

                if (alreadyBilledElsewhere.Count > 0)
                    _logger.LogWarning(
                        "PrepareForInvoice: SO {Id} - skipping {Count} dispatch row(s) already billed on another service order's sale: {Keys}",
                        id, alreadyBilledElsewhere.Count, string.Join(", ", alreadyBilledElsewhere));
            }


            // Currency guard: every SaleItem inherits `Sale.Currency` at write time (see below),
            // so we refuse to transfer any currency-carrying source row whose declared currency
            // differs from the sale to avoid silently billing e.g. a USD expense as if it were TND.
            // Rows with a null/empty Currency are trusted as "sale currency" (legacy + rows on
            // models that don't carry a currency column, like materials/time entries).
            var saleCurrency = (sale.Currency ?? "").Trim().ToUpperInvariant();
            var mismatches = new List<string>();

            if (dto.ExpenseIds?.Any() == true)
            {
                var soMismatched = await _context.ServiceOrderExpenses
                    .Where(e => dto.ExpenseIds.Contains(e.Id) && e.ServiceOrderId == id
                        && e.InvoiceStatus == null
                        && e.Currency != null && e.Currency != ""
                        && e.Currency.ToUpper() != saleCurrency)
                    .Select(e => new { e.Id, e.Currency })
                    .ToListAsync();
                mismatches.AddRange(soMismatched.Select(m => $"SO expense #{m.Id} ({m.Currency})"));
            }

            if (dto.DispatchExpenseIds?.Any() == true)
            {
                // Dispatch expenses may carry an explicit Currency now (post-migration). Legacy
                // rows without a currency default to null → treated as sale currency (no mismatch).
                var dispatchMismatched = await _context.DispatchExpenses
                    .Where(e => dto.DispatchExpenseIds.Contains(e.Id)
                        && linkedDispatchIds.Contains(e.DispatchId)
                        && e.Currency != null && e.Currency != ""
                        && e.Currency!.ToUpper() != saleCurrency)
                    .Select(e => new { e.Id, e.Currency })
                    .ToListAsync();
                mismatches.AddRange(dispatchMismatched.Select(m => $"Dispatch expense #{m.Id} ({m.Currency})"));
            }

            if (mismatches.Any())
            {
                throw new InvalidOperationException(
                    $"Currency mismatch: {mismatches.Count} line(s) are not in the sale's currency ({sale.Currency}). " +
                    $"Convert them before transferring. Offending items: {string.Join(", ", mismatches)}.");
            }

            var newSaleItems = new List<Sales.Models.SaleItem>();
            var currentDisplayOrder = (sale.Items?.Count ?? 0);

            // Track source entities to update InvoiceStatus AFTER successful save
            var soMaterialsToMark = new List<ServiceOrderMaterial>();
            var soExpensesToMark = new List<ServiceOrderExpense>();
            var soTimeEntriesToMark = new List<ServiceOrderTimeEntry>();

            // ===== MATERIALS FROM ServiceOrderMaterials =====
            if (dto.MaterialIds != null && dto.MaterialIds.Any())
            {
                var materials = await _context.ServiceOrderMaterials
                    .Where(m => dto.MaterialIds.Contains(m.Id) && m.ServiceOrderId == id
                        // Idempotency: only pick up rows that have NEVER been transferred.
                        // A row marked "selected_for_invoice" is already on the sale.
                        && m.InvoiceStatus == null)
                    .ToListAsync();

                _logger.LogInformation("PrepareForInvoice: Found {Count} SO materials (requested: {Requested})", materials.Count, dto.MaterialIds.Count);

                foreach (var mat in materials)
                {
                    var (matUnitPrice, matLineTotal) = await ResolveMaterialPriceAsync(
                        mat.ArticleId, mat.UnitPrice, mat.Quantity, mat.TotalPrice, $"SO material #{mat.Id} ({mat.Name})");

                    currentDisplayOrder++;
                    newSaleItems.Add(new Sales.Models.SaleItem
                    {
                        SaleId = saleId,
                        Type = "article",
                        ItemName = mat.Name,
                        ItemCode = mat.Sku,
                        Description = mat.Description ?? mat.Name,
                        Quantity = mat.Quantity,
                        UnitPrice = matUnitPrice,
                        LineTotal = matLineTotal,

                        ArticleId = mat.ArticleId,
                        InstallationId = mat.InstallationId?.ToString(),
                        InstallationName = mat.InstallationName,
                        ServiceOrderId = id.ToString(),
                        SourceType = "service_order_material",
                        SourceId = mat.Id.ToString(),
                        DisplayOrder = currentDisplayOrder,
                        Currency = sale.Currency
                    });
                    soMaterialsToMark.Add(mat);
                }
            }

            // ===== MATERIALS FROM DispatchMaterials =====
            if (dto.DispatchMaterialIds != null && dto.DispatchMaterialIds.Any())
            {
                var dispatchMats = await _context.DispatchMaterials
                    .Where(m => dto.DispatchMaterialIds.Contains(m.Id) && linkedDispatchIds.Contains(m.DispatchId)
                        // A rejected material line was refused (and its stock returned):
                        // it must never reach the customer's invoice.
                        && m.ApprovalStatus != "rejected")

                    .ToListAsync();

                _logger.LogInformation("PrepareForInvoice: Found {Count} dispatch materials (requested: {Requested})", dispatchMats.Count, dto.DispatchMaterialIds.Count);

                foreach (var mat in dispatchMats)
                {
                    // Prefer the material's own description; when the field app stored it
                    // empty, fall back to the linked article's name so the sale line does
                    // not surface as an anonymous "Item #<id>".
                    var articleName = mat.ArticleId.HasValue
                        ? await _context.Articles
                            .Where(a => a.Id == mat.ArticleId.Value && !a.IsDeleted)
                            .Select(a => a.Name)
                            .FirstOrDefaultAsync()
                        : null;
                    var name = !string.IsNullOrWhiteSpace(mat.Description)
                        ? mat.Description!
                        : (!string.IsNullOrWhiteSpace(articleName) ? articleName! : $"Material #{mat.Id}");
                    var desc = !string.IsNullOrWhiteSpace(mat.Description)
                        ? mat.Description!
                        : (!string.IsNullOrWhiteSpace(articleName) ? articleName! : "Material from dispatch");
                    // Fix §4.4: propagate ArticleId + InstallationId (from the parent Dispatch)
                    // into the signature so a truly-distinct line does not collide with an
                    // earlier transfer.
                    var dispatchInstallationId = await _context.Dispatches
                        .Where(d => d.Id == mat.DispatchId)
                        .Select(d => (int?)d.InstallationId)
                        .FirstOrDefaultAsync();
                    var sourceKey = BuildSourceKey("dispatch_material", mat.Id);
                    if (existingSourceKeys.Contains(sourceKey))
                    {
                        _logger.LogInformation("PrepareForInvoice: Skipping already-transferred dispatch material #{Id} (source key match)", mat.Id);
                        continue;
                    }
                    // Resolve the price BEFORE building the dedup signature: the signatures of
                    // already-transferred sale items were built from their *resolved* unit price,
                    // so signing this row with its raw (possibly 0.00) price would never match and
                    // a legacy sale could receive the same material twice.
                    var (dmUnitPrice, dmLineTotal) = await ResolveMaterialPriceAsync(
                        mat.ArticleId, mat.UnitPrice, mat.Quantity, mat.TotalPrice, $"Dispatch material #{mat.Id} ({name})");

                    var signature = BuildSaleItemSignature("article", name, desc, dmUnitPrice, mat.Quantity, mat.ArticleId, dispatchInstallationId?.ToString());
                    if (useLegacySignatureDedup && !existingSignatures.Add(signature))
                    {
                        _logger.LogInformation("PrepareForInvoice: Skipping duplicate dispatch material #{Id} (legacy signature match)", mat.Id);
                        continue;
                    }
                    existingSourceKeys.Add(sourceKey);

                    currentDisplayOrder++;
                    newSaleItems.Add(new Sales.Models.SaleItem
                    {
                        SaleId = saleId,
                        Type = "article",
                        ItemName = name,
                        Description = desc,
                        Quantity = mat.Quantity,
                        UnitPrice = dmUnitPrice,
                        LineTotal = dmLineTotal,

                        ArticleId = mat.ArticleId,
                        ServiceOrderId = id.ToString(),
                        SourceType = "dispatch_material",
                        SourceId = mat.Id.ToString(),
                        DisplayOrder = currentDisplayOrder,
                        Currency = sale.Currency
                    });
                }
            }


            // ===== EXPENSES FROM ServiceOrderExpenses =====
            if (dto.ExpenseIds != null && dto.ExpenseIds.Any())
            {
                var soExpenses = await _context.ServiceOrderExpenses
                    .Where(e => dto.ExpenseIds.Contains(e.Id) && e.ServiceOrderId == id
                        && e.InvoiceStatus == null)
                    .ToListAsync();

                _logger.LogInformation("PrepareForInvoice: Found {Count} SO expenses (requested: {Requested})", soExpenses.Count, dto.ExpenseIds.Count);

                foreach (var exp in soExpenses)
                {
                    currentDisplayOrder++;
                    newSaleItems.Add(new Sales.Models.SaleItem
                    {
                        SaleId = saleId,
                        Type = "service",
                        ItemName = $"Expense: {exp.Type}",
                        Description = exp.Description ?? $"Expense - {exp.Type}",
                        Quantity = 1,
                        UnitPrice = exp.Amount,
                        LineTotal = exp.Amount,
                        ServiceOrderId = id.ToString(),
                        SourceType = "service_order_expense",
                        SourceId = exp.Id.ToString(),
                        DisplayOrder = currentDisplayOrder,
                        Currency = sale.Currency
                    });
                    soExpensesToMark.Add(exp);
                }
            }

            // ===== EXPENSES FROM DispatchExpenses =====
            if (dto.DispatchExpenseIds != null && dto.DispatchExpenseIds.Any())
            {
                var dispatchExpenses = await _context.DispatchExpenses
                    .Where(e => dto.DispatchExpenseIds.Contains(e.Id) && linkedDispatchIds.Contains(e.DispatchId))
                    .ToListAsync();

                _logger.LogInformation("PrepareForInvoice: Found {Count} dispatch expenses (requested: {Requested})", dispatchExpenses.Count, dto.DispatchExpenseIds.Count);

                foreach (var dExp in dispatchExpenses)
                {
                    var name = $"Expense: {dExp.ExpenseType}";
                    var desc = dExp.Description ?? $"Expense - {dExp.ExpenseType}";
                    // Include installation from the source dispatch/expense so identical-named
                    // expenses on different installations do not collide (§4.4).
                    var installKey = dExp.InstallationId?.ToString() ?? "";
                    var sourceKey = BuildSourceKey("dispatch_expense", dExp.Id);
                    if (existingSourceKeys.Contains(sourceKey))
                    {
                        _logger.LogInformation("PrepareForInvoice: Skipping already-transferred dispatch expense #{Id} (source key match)", dExp.Id);
                        continue;
                    }
                    var signature = BuildSaleItemSignature("service", name, desc, dExp.Amount, 1m, null, installKey);
                    if (useLegacySignatureDedup && !existingSignatures.Add(signature))
                    {
                        _logger.LogInformation("PrepareForInvoice: Skipping duplicate dispatch expense #{Id} (legacy signature match)", dExp.Id);
                        continue;
                    }
                    existingSourceKeys.Add(sourceKey);

                    currentDisplayOrder++;
                    newSaleItems.Add(new Sales.Models.SaleItem
                    {
                        SaleId = saleId,
                        Type = "service",
                        ItemName = name,
                        Description = desc,
                        Quantity = 1,
                        UnitPrice = dExp.Amount,
                        LineTotal = dExp.Amount,
                        ServiceOrderId = id.ToString(),
                        SourceType = "dispatch_expense",
                        SourceId = dExp.Id.ToString(),
                        DisplayOrder = currentDisplayOrder,
                        Currency = sale.Currency
                    });
                }
            }


            // ===== TIME ENTRIES FROM ServiceOrderTimeEntries =====
            if (dto.TimeEntryIds != null && dto.TimeEntryIds.Any())
            {
                var timeEntries = await _context.ServiceOrderTimeEntries
                    .Where(t => dto.TimeEntryIds.Contains(t.Id) && t.ServiceOrderId == id && t.Billable
                        && t.InvoiceStatus == null)
                    .ToListAsync();

                _logger.LogInformation("PrepareForInvoice: Found {Count} SO time entries (requested: {Requested})", timeEntries.Count, dto.TimeEntryIds.Count);

                foreach (var te in timeEntries)
                {
                    currentDisplayOrder++;
                    var hours = te.Duration / 60.0m;
                    var rate = te.HourlyRate ?? 0;
                    var total = te.TotalCost ?? (hours * rate);

                    newSaleItems.Add(new Sales.Models.SaleItem
                    {
                        SaleId = saleId,
                        Type = "service",
                        ItemName = $"Labor: {te.WorkType}",
                        Description = te.Description ?? $"Time entry - {te.WorkType} ({te.Duration} min)",
                        Quantity = 1,
                        UnitPrice = total,
                        LineTotal = total,
                        ServiceOrderId = id.ToString(),
                        SourceType = "service_order_time_entry",
                        SourceId = te.Id.ToString(),
                        DisplayOrder = currentDisplayOrder,
                        Currency = sale.Currency
                    });
                    soTimeEntriesToMark.Add(te);
                }
            }

            // ===== TIME ENTRIES FROM Dispatch TimeEntries =====
            if (dto.DispatchTimeEntryIds != null && dto.DispatchTimeEntryIds.Any())
            {
                var dispatchTimeEntries = await _context.TimeEntries
                    .Where(t => dto.DispatchTimeEntryIds.Contains(t.Id) && linkedDispatchIds.Contains(t.DispatchId)
                        && t.Billable)
                    .ToListAsync();

                _logger.LogInformation("PrepareForInvoice: Found {Count} dispatch time entries (requested: {Requested})", dispatchTimeEntries.Count, dto.DispatchTimeEntryIds.Count);

                // Dispatch TimeEntries have no HourlyRate column. To avoid billing labor at $0 whenever a
                // technician logs time through the field app, fall back to the most recent HourlyRate the
                // same technician used on ServiceOrderTimeEntries. If none exists, we still record the line
                // (so the user sees the work) but flag it in the description for manual pricing.
                var technicianIdStrings = dispatchTimeEntries
                    .Select(t => t.TechnicianId.ToString())
                    .Distinct()
                    .ToList();
                var fallbackRates = await _context.ServiceOrderTimeEntries
                    .Where(t => t.TechnicianId != null && technicianIdStrings.Contains(t.TechnicianId) && t.HourlyRate != null)
                    .GroupBy(t => t.TechnicianId!)
                    .Select(g => new { TechnicianId = g.Key, Rate = g.OrderByDescending(x => x.CreatedAt).First().HourlyRate })
                    .ToDictionaryAsync(x => x.TechnicianId, x => x.Rate ?? 0m);

                foreach (var te in dispatchTimeEntries)
                {
                    var duration = te.Duration ?? 0;
                    var hours = duration / 60.0m;
                    var rate = fallbackRates.TryGetValue(te.TechnicianId.ToString(), out var r) ? r : 0m;
                    var total = hours * rate;
                    var needsPricing = rate == 0m && duration > 0;

                    var description = te.Description ?? $"Time entry - {te.WorkType ?? "work"} ({duration} min)";
                    if (needsPricing)
                    {
                        description += " [rate not set - edit before invoicing]";
                        _logger.LogWarning(
                            "PrepareForInvoice: Dispatch time entry {TeId} for technician {Tech} has no available hourly rate; line added at 0.",
                            te.Id, te.TechnicianId);
                        pricingWarnings.Add(
                            $"Labor from dispatch time entry #{te.Id} has no hourly rate — the sale line is 0 until you set a price.");
                    }


                    var name = $"Labor: {te.WorkType ?? "work"}";
                    // Include TechnicianId + InstallationId so identical labor rates
                    // logged by different technicians / on different installations
                    // don't collide into one (§4.4).
                    var teKey = $"tech:{te.TechnicianId}|inst:{te.InstallationId?.ToString() ?? ""}";
                    var sourceKey = BuildSourceKey("dispatch_time_entry", te.Id);
                    if (existingSourceKeys.Contains(sourceKey))
                    {
                        _logger.LogInformation("PrepareForInvoice: Skipping already-transferred dispatch time entry #{Id} (source key match)", te.Id);
                        continue;
                    }
                    var signature = BuildSaleItemSignature("service", name, description, total, 1m, null, teKey);
                    if (useLegacySignatureDedup && !existingSignatures.Add(signature))
                    {
                        _logger.LogInformation("PrepareForInvoice: Skipping duplicate dispatch time entry #{Id} (legacy signature match)", te.Id);
                        continue;
                    }
                    existingSourceKeys.Add(sourceKey);

                    currentDisplayOrder++;
                    newSaleItems.Add(new Sales.Models.SaleItem
                    {
                        SaleId = saleId,
                        Type = "service",
                        ItemName = name,
                        Description = description,
                        Quantity = 1,
                        UnitPrice = total,
                        LineTotal = total,
                        ServiceOrderId = id.ToString(),
                        SourceType = "dispatch_time_entry",
                        SourceId = te.Id.ToString(),
                        DisplayOrder = currentDisplayOrder,
                        Currency = sale.Currency
                    });
                }
            }


            _logger.LogInformation("PrepareForInvoice: Total new sale items to add: {Count}", newSaleItems.Count);

            // Check that at least something was found if IDs were requested
            var hasRequestedIds = (dto.MaterialIds?.Any() == true) || (dto.ExpenseIds?.Any() == true) || (dto.TimeEntryIds?.Any() == true)
                || (dto.DispatchMaterialIds?.Any() == true) || (dto.DispatchExpenseIds?.Any() == true) || (dto.DispatchTimeEntryIds?.Any() == true);

            if (hasRequestedIds && !newSaleItems.Any())
            {
                // Nothing new to add. If a prior run already transferred everything the caller re-selected,
                // treat this as a successful no-op (the SO status is still (re)promoted below) so the UI
                // is never blocked when the user re-opens "Prepare for invoice" without adding new items.
                if (previouslyTransferred)
                {
                    _logger.LogInformation(
                        "PrepareForInvoice: SO {Id} - all requested items were already on sale {SaleId}; no-op, status will still be advanced.",
                        id, saleId);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Items were requested for transfer but none could be found or matched. " +
                        "Check that the IDs exist and belong to this service order.");
                }
            }


            // Use execution strategy to support retrying transactions with Npgsql
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (newSaleItems.Any())
                    {
                        _logger.LogInformation("PrepareForInvoice: Adding {Count} new sale items to sale {SaleId}. Items: [{Items}]", 
                            newSaleItems.Count, saleId, 
                            string.Join(", ", newSaleItems.Select(i => $"{i.ItemName}({i.UnitPrice})")));

                        _context.SaleItems.AddRange(newSaleItems);
                        await _context.SaveChangesAsync();

                        // Mark SO source entities as transferred (dispatch entities don't have InvoiceStatus)
                        foreach (var mat in soMaterialsToMark) mat.InvoiceStatus = "selected_for_invoice";
                        foreach (var exp in soExpensesToMark) exp.InvoiceStatus = "selected_for_invoice";
                        foreach (var te in soTimeEntriesToMark) te.InvoiceStatus = "selected_for_invoice";
                        await _context.SaveChangesAsync();
                    }

                    // Update service order status
                    serviceOrder.Status = "ready_for_invoice";
                    serviceOrder.ModifiedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Recalculate sale totals
                    var updatedSale = await _context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId);
                    if (updatedSale != null)
                    {
                        // Recompute the header the same way every other sale write does:
                        // subtotal → header discount → tax → fiscal stamp. The previous raw
                        // `Sum(LineTotal)` assignment left GrandTotal discount/tax/stamp-blind
                        // until SyncSaleInvoiceStateAsync healed it, and the manual
                        // CreateDraftAsync over-invoicing guard reads that stored value.
                        Sales.Services.SaleTotalsCalculator.Apply(updatedSale, updatedSale.Items);
                        updatedSale.LastActivity = DateTime.UtcNow;

                        // #5 fix: mark SaleItems belonging to this SO as fulfilled once
                        // the SO is (technically) done. Was previously left at "pending"
                        // forever because CompleteAsync/PrepareForInvoiceAsync never touched it.
                        var soIdStr = id.ToString();
                        var soDone = new[] { "technically_completed", "completed", "ready_for_invoice" };
                        var soPartial = "partially_completed";
                        var isFullyDone = soDone.Contains(serviceOrder.Status);
                        var isPartial = string.Equals(serviceOrder.Status, soPartial, StringComparison.OrdinalIgnoreCase);
                        if ((isFullyDone || isPartial) && updatedSale.Items != null)
                        {
                            foreach (var si in updatedSale.Items.Where(i => i.ServiceOrderId == soIdStr))
                            {
                                si.FulfillmentStatus = isFullyDone ? "fulfilled" : "partial";
                            }
                        }

                        // #2 fix: advance the sale to "ready_to_invoice" so the pipeline
                        // reflects that field work is done and billing is queued. Only
                        // touch pre-invoice statuses; never override partially_invoiced /
                        // invoiced / any terminal state. SyncSaleInvoiceStateAsync (called
                        // by CreateDraftFromSaleAsync below) flips it onward to
                        // partially_invoiced / invoiced once the draft is created.
                        var preInvoice = new[] { "created", "in_progress" };
                        if (preInvoice.Contains((updatedSale.Status ?? "").ToLowerInvariant()))
                        {
                            updatedSale.Status = "ready_to_invoice";
                        }

                        await _context.SaveChangesAsync();

                        _logger.LogInformation("PrepareForInvoice: Sale {SaleId} now has {ItemCount} items, total: {Total}, status: {Status}",
                            saleId, updatedSale.Items?.Count ?? 0, updatedSale.TotalAmount, updatedSale.Status);
                    }


                    await transaction.CommitAsync();
                    _logger.LogInformation("PrepareForInvoice: Transaction committed. Transferred {ItemCount} items to sale {SaleId}", newSaleItems.Count, saleId);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "PrepareForInvoice: Transaction ROLLED BACK for SO {Id}. Error: {Error}. InnerException: {Inner}", 
                        id, ex.Message, ex.InnerException?.Message ?? "none");
                    throw new InvalidOperationException($"Failed to transfer items to sale: {ex.InnerException?.Message ?? ex.Message}");
                }
            });

            // Phase B: snapshot the sale into a draft invoice on the ledger.
            //
            // Fix #2: this used to swallow the exception. Phase A has already committed
            // (items transferred, SO moved to ready_for_invoice), so we must NOT roll that
            // back — but returning success while no invoice exists left the order silently
            // stranded with zero visibility. The transfer is durable and re-runnable, so we
            // surface the failure to the caller and let them retry Phase B.
            if (_invoiceService != null)
            {
                try
                {
                    await _invoiceService.CreateDraftFromSaleAsync(saleId, userId, id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PrepareForInvoice: draft invoice creation failed for SO {Id} / Sale {SaleId}", id, saleId);

                    // Leave a trail on the service order so the stalled state is visible
                    // even if the caller drops the error.
                    try
                    {
                        var so = await _context.ServiceOrders.FindAsync(id);
                        if (so != null)
                        {
                            so.Notes = string.Join("\n", new[]
                            {
                                so.Notes,
                                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] Draft invoice generation FAILED: {ex.Message}. " +
                                "Items were transferred to the sale; retry invoicing from the sale."
                            }.Where(s => !string.IsNullOrWhiteSpace(s)));
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception noteEx)
                    {
                        _logger.LogWarning(noteEx, "PrepareForInvoice: could not annotate SO {Id} after invoice failure", id);
                    }

                    throw new InvalidOperationException(
                        $"Items were transferred to sale {saleId} and this order is now ready for invoicing, " +
                        $"but the draft invoice could not be created: {ex.Message}. " +
                        "Open the sale and generate the invoice from there.", ex);
                }
            }

            var prepared = (await GetServiceOrderByIdAsync(id))!;
            if (pricingWarnings.Count > 0)
            {
                prepared.Warnings ??= new List<ServiceOrderCompletionWarningDto>();
                foreach (var w in pricingWarnings)
                {
                    prepared.Warnings.Add(new ServiceOrderCompletionWarningDto
                    {
                        Code = "zero_price_line",
                        Message = w
                    });
                }
            }
            return prepared;

        }

        // =====================================================================
        // CASCADE: Sale.Status → related ServiceOrder.Status
        //
        // Called after the Sale row has been saved with its new status (either
        // through SaleService.UpdateSaleAsync or InvoiceService.SyncSaleInvoiceStateAsync).
        // Eliminates the "double job" where users had to reopen each SO after
        // its items had already been transferred to the Sale.
        // =====================================================================
        public async Task CascadeSaleStatusToServiceOrdersAsync(
            int saleId, string newSaleStatus, string userId, bool throwOnFailure = false)
        {
            if (saleId <= 0 || string.IsNullOrWhiteSpace(newSaleStatus)) return;

            // Collected after the transaction commits so a workflow-trigger failure
            // can never roll back (or be rolled back with) the status changes.
            var pendingTriggers = new List<(int SoId, string OrderNumber, string From, string To)>();

            try
            {
                var newStatus = newSaleStatus.ToLowerInvariant();
                var saleIdStr = saleId.ToString();

                // SOs are linked either via the string SaleId (legacy / from-sale path)
                // or via AutoGeneratedSaleId (direct SOs that produced a shadow sale).
                var linkedOrders = await _context.ServiceOrders
                    .Where(so => !so.IsDeleted &&
                                 (so.SaleId == saleIdStr || so.AutoGeneratedSaleId == saleId))
                    .ToListAsync();

                if (linkedOrders.Count == 0) return;

                // --- Scope the cascade to the SOs this sale actually bills -------
                // PrepareForInvoiceAsync stamps every transferred line with
                // SaleItem.ServiceOrderId. When a sale carries items from several
                // service orders, only the stamped ones may be cascaded — otherwise
                // invoicing SO #1 would wrongly close SO #2 riding on the same sale.
                // Sales with no stamped items at all (legacy / manual sales) keep the
                // old behaviour and cascade to every linked SO.
                var billedSoIds = await _context.SaleItems
                    .Where(si => si.SaleId == saleId && si.ServiceOrderId != null && si.ServiceOrderId != "")
                    .Select(si => si.ServiceOrderId!)
                    .Distinct()
                    .ToListAsync();

                if (billedSoIds.Count > 0)
                {
                    var billedSet = new HashSet<string>(billedSoIds, StringComparer.OrdinalIgnoreCase);
                    var scoped = linkedOrders
                        .Where(so => billedSet.Contains(so.Id.ToString()))
                        .ToList();

                    if (scoped.Count == 0)
                    {
                        _logger.LogInformation(
                            "Cascade: Sale {SaleId} → {NewSaleStatus} skipped — none of the {Count} linked service orders have items on this sale",
                            saleId, newStatus, linkedOrders.Count);
                        return;
                    }

                    if (scoped.Count != linkedOrders.Count)
                    {
                        _logger.LogInformation(
                            "Cascade: Sale {SaleId} scoped from {Linked} linked to {Scoped} billed service order(s)",
                            saleId, linkedOrders.Count, scoped.Count);
                    }

                    linkedOrders = scoped;
                }

                // Statuses considered "past prepare-for-invoice" — safe to cascade
                // an invoicing/closing decision onto because their items already
                // moved to the sale.
                var invoiceableFromStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "in_progress", "partially_completed", "technically_completed",
                    "completed", "ready_for_invoice", "invoiced"
                };

                // --- Apply every status change atomically -------------------------
                async Task ApplyAsync()
                {
                    pendingTriggers.Clear();
                    // Join an ambient transaction when a caller already owns one.
                    var ownsTx = _context.Database.CurrentTransaction == null;
                    var tx = ownsTx ? await _context.Database.BeginTransactionAsync() : null;
                    try
                    {
                    foreach (var so in linkedOrders)
                    {
                        var current = (so.Status ?? string.Empty).ToLowerInvariant();
                        string? target = null;

                        switch (newStatus)
                        {
                            // Fully invoiced sale ⇒ the service order's work is billed.
                            // Close it automatically ONLY when nothing billable is left
                            // un-transferred; otherwise park it in "invoiced" so the
                            // remaining materials / time / expenses stay visible and can
                            // still be pushed to a sale instead of being lost on close.
                            case "invoiced":
                                if (current == "cancelled" || current == "closed") break;
                                if (invoiceableFromStatuses.Contains(current))
                                {
                                    var pending = await CountUntransferredBillablesAsync(so);
                                    if (pending > 0)
                                    {
                                        _logger.LogWarning(
                                            "Cascade: ServiceOrder {SoId} kept open ('invoiced') — {Count} billable actual(s) not yet transferred to a sale",
                                            so.Id, pending);
                                        target = current == "invoiced" ? null : "invoiced";
                                    }
                                    else
                                    {
                                        target = "closed";
                                    }
                                }
                                break;

                            // The sale is billed but not settled yet: make that visible on
                            // the service order instead of leaving it in ready_for_invoice
                            // forever. Never closes — more items may still be transferred.
                            case "partially_invoiced":
                                if (current == "cancelled" || current == "closed" || current == "invoiced") break;
                                if (invoiceableFromStatuses.Contains(current)) target = "invoiced";
                                break;

                            case "closed":
                            case "won":
                            case "completed":
                                if (current == "cancelled" || current == "closed") break;
                                if (invoiceableFromStatuses.Contains(current))
                                {
                                    var pendingClose = await CountUntransferredBillablesAsync(so);
                                    if (pendingClose > 0)
                                    {
                                        _logger.LogWarning(
                                            "Cascade: ServiceOrder {SoId} kept open ('invoiced') — {Count} billable actual(s) not yet transferred to a sale",
                                            so.Id, pendingClose);
                                        target = current == "invoiced" ? null : "invoiced";
                                    }
                                    else
                                    {
                                        target = "closed";
                                    }
                                }
                                break;

                            case "cancelled":
                            case "lost":
                                if (current == "cancelled" || current == "closed") break;
                                target = "cancelled";
                                break;

                            case "in_progress":
                            case "created":
                                // Sale was reopened / de-invoiced — pull the auto-closed
                                // SOs back so users can add more items and re-invoice.
                                if (current == "invoiced" || current == "closed")
                                    target = "ready_for_invoice";
                                break;

                            default:
                                break;
                        }

                        if (target == null || string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var previous = so.Status;
                        so.Status = target;
                        so.ModifiedDate = DateTime.UtcNow;
                        so.ModifiedBy = userId;

                        if (target == "closed")
                        {
                            if (!so.CompletedDate.HasValue) so.CompletedDate = DateTime.UtcNow;
                            so.CompletionPercentage = 100;
                        }
                        else if (target == "ready_for_invoice")
                        {
                            so.CompletedDate = null;
                        }

                        _logger.LogInformation(
                            "Cascade: Sale {SaleId} → {NewSaleStatus} propagated to ServiceOrder {SoId}: {From} → {To}",
                            saleId, newStatus, so.Id, previous, target);

                        pendingTriggers.Add((so.Id, so.OrderNumber ?? "", previous ?? string.Empty, target));
                    }

                    await _context.SaveChangesAsync();
                    if (tx != null) await tx.CommitAsync();
                    }
                    finally
                    {
                        if (tx != null) await tx.DisposeAsync();
                    }
                }

                // A retrying execution strategy cannot own a caller's transaction:
                // when one is already ambient, run inline and let that caller retry.
                if (_context.Database.CurrentTransaction != null)
                    await ApplyAsync();
                else
                    await _context.Database.CreateExecutionStrategy().ExecuteAsync(ApplyAsync);

                // Fire workflow triggers only for changes that are actually committed.
                if (_workflowTriggerService != null)
                {
                    foreach (var t in pendingTriggers)
                    {
                        try
                        {
                            await _workflowTriggerService.TriggerStatusChangeAsync(
                                "service_order",
                                t.SoId,
                                t.From,
                                t.To,
                                userId,
                                new Dictionary<string, object>
                                {
                                    { "serviceOrderId", t.SoId },
                                    { "orderNumber", t.OrderNumber },
                                    { "cascadedFromSaleId", saleId },
                                    { "cascadedFromSaleStatus", newSaleStatus.ToLowerInvariant() }
                                });
                        }
                        catch (Exception wfEx)
                        {
                            _logger.LogWarning(wfEx,
                                "Cascade: workflow trigger failed for SO {SoId} (Sale {SaleId} {NewSaleStatus})",
                                t.SoId, saleId, newSaleStatus);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CascadeSaleStatusToServiceOrdersAsync failed for Sale {SaleId} → {NewStatus}",
                    saleId, newSaleStatus);

                // Callers that own the surrounding unit of work can opt into failing loudly.
                if (throwOnFailure) throw;
            }
        }
    }
}

