using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Installations.DTOs;
using MyApi.Modules.Installations.Models;

namespace MyApi.Modules.Installations.Services
{
    public class InstallationService : IInstallationService
    {
        private readonly ApplicationDbContext _context;
        private readonly MyApi.Modules.Contacts.Services.IContactActivityService? _contactActivity;

        public InstallationService(
            ApplicationDbContext context,
            MyApi.Modules.Contacts.Services.IContactActivityService? contactActivity = null)
        {
            _context = context;
            _contactActivity = contactActivity;
        }

        public async Task<PaginatedInstallationResponse> GetInstallationsAsync(
            string? search = null,
            string? status = null,
            string? contactId = null,
            int page = 1,
            int pageSize = 20,
            string sortBy = "created_at",
            string sortOrder = "desc"
        )
        {
            var query = _context.Installations.AsNoTracking().Where(i => !i.IsDeleted).AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(i =>
                    i.InstallationNumber.ToLower().Contains(searchLower) ||
                    i.SiteAddress.ToLower().Contains(searchLower) ||
                    i.InstallationType.ToLower().Contains(searchLower) ||
                    (i.Name != null && i.Name.ToLower().Contains(searchLower)) ||
                    (i.Model != null && i.Model.ToLower().Contains(searchLower)) ||
                    (i.Manufacturer != null && i.Manufacturer.ToLower().Contains(searchLower)) ||
                    (i.Notes != null && i.Notes.ToLower().Contains(searchLower)) ||
                    (i.Matricule != null && i.Matricule.ToLower().Contains(searchLower))
                );
            }

            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);

            if (!string.IsNullOrEmpty(contactId) && int.TryParse(contactId, out int contactIdInt))
                query = query.Where(i => i.ContactId == contactIdInt);

            // Count total
            var total = await query.CountAsync();

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "installation_number" => sortOrder.ToLower() == "asc" ? query.OrderBy(i => i.InstallationNumber) : query.OrderByDescending(i => i.InstallationNumber),
                "status" => sortOrder.ToLower() == "asc" ? query.OrderBy(i => i.Status) : query.OrderByDescending(i => i.Status),
                "installation_date" => sortOrder.ToLower() == "asc" ? query.OrderBy(i => i.InstallationDate) : query.OrderByDescending(i => i.InstallationDate),
                "modified_date" => sortOrder.ToLower() == "asc" ? query.OrderBy(i => i.ModifiedDate) : query.OrderByDescending(i => i.ModifiedDate),
                "name" => sortOrder.ToLower() == "asc" ? query.OrderBy(i => i.Name) : query.OrderByDescending(i => i.Name),
                _ => sortOrder.ToLower() == "asc" ? query.OrderBy(i => i.CreatedDate) : query.OrderByDescending(i => i.CreatedDate)
            };

            // Apply pagination
            var installations = await query
                .Include(i => i.MaintenanceHistories)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var installationDtos = installations.Select(MapToDto).ToList();

            return new PaginatedInstallationResponse
            {
                Installations = installationDtos,
                Pagination = new PaginationInfo
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = total,
                    TotalPages = (int)Math.Ceiling((double)total / pageSize),
                    HasNextPage = page * pageSize < total,
                    HasPreviousPage = page > 1
                }
            };
        }

        public async Task<InstallationDto?> GetInstallationByIdAsync(int id)
        {
            var installation = await _context.Installations
                .AsNoTracking()
                .Include(i => i.MaintenanceHistories)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            return installation == null ? null : MapToDto(installation);
        }

        public async Task<InstallationDto> CreateInstallationAsync(CreateInstallationDto createDto, string userId)
        {
            // Verify contact exists if contactId is provided
            if (createDto.ContactId > 0)
            {
                var contactExists = await _context.Contacts.AnyAsync(c => c.Id == createDto.ContactId && !c.IsDeleted);
                if (!contactExists)
                    throw new KeyNotFoundException($"Contact with ID {createDto.ContactId} not found");
            }

            // Build SiteAddress from Name if not provided
            var siteAddress = createDto.SiteAddress;
            if (string.IsNullOrEmpty(siteAddress))
            {
                siteAddress = createDto.Name ?? "Default Site";
            }

            // Build InstallationType from Type/Category if not provided
            var installationType = createDto.InstallationType;
            if (string.IsNullOrEmpty(installationType))
            {
                installationType = createDto.Type ?? createDto.Category ?? "general";
            }

            // Parse warranty dates
            DateTime? warrantyExpiry = createDto.WarrantyExpiry;
            DateTime? warrantyFrom = null;
            if (createDto.Warranty?.HasWarranty == true)
            {
                if (!string.IsNullOrEmpty(createDto.Warranty.WarrantyTo) && DateTime.TryParse(createDto.Warranty.WarrantyTo, out var parsedExpiry))
                {
                    warrantyExpiry = parsedExpiry;
                }
                if (!string.IsNullOrEmpty(createDto.Warranty.WarrantyFrom) && DateTime.TryParse(createDto.Warranty.WarrantyFrom, out var parsedFrom))
                {
                    warrantyFrom = parsedFrom;
                }
            }

            var installation = new Installation
            {
                InstallationNumber = string.Empty, // set below inside retry loop
                ContactId = createDto.ContactId,
                SiteAddress = siteAddress,
                InstallationType = installationType,
                InstallationDate = createDto.InstallationDate ?? DateTime.UtcNow,
                Status = createDto.Status ?? "active",
                WarrantyExpiry = warrantyExpiry,
                WarrantyFrom = warrantyFrom,
                Notes = createDto.Notes,
                Name = createDto.Name,
                Model = createDto.Model,
                Manufacturer = createDto.Manufacturer,
                SerialNumber = createDto.SerialNumber,
                Matricule = createDto.Matricule,
                Category = createDto.Category,
                Type = createDto.Type,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };

            // Number-generation race is now guarded by the partial unique index
            // UX_Installations_Tenant_InstallationNumber_Active. Retry up to 5
            // times on unique-violation from a concurrent inserter.
            const int maxAttempts = 5;
            for (int attempt = 1; ; attempt++)
            {
                var lastInstallation = await _context.Installations
                    .OrderByDescending(i => i.Id)
                    .Select(i => new { i.Id })
                    .FirstOrDefaultAsync();
                var nextNumber = (lastInstallation?.Id ?? 0) + attempt;
                installation.InstallationNumber = $"INST-{DateTime.UtcNow.Year}-{nextNumber:D6}";

                _context.Installations.Add(installation);
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    // Detach the failed entity, bump the number, retry.
                    _context.Entry(installation).State = EntityState.Detached;
                }
            }

            if (_contactActivity != null && installation.ContactId > 0)
            {
                await _contactActivity.LogAsync(
                    contactId: installation.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.InstallationCreated,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Installation,
                    relatedEntityId: installation.Id,
                    description: $"Installation {installation.InstallationNumber} was created",
                    metadata: new { number = installation.InstallationNumber, name = installation.Name, installationType = installation.InstallationType, status = installation.Status },
                    createdBy: userId);
            }

            return MapToDto(installation);
        }

        public async Task<InstallationDto?> UpdateInstallationAsync(int id, UpdateInstallationDto updateDto, string userId)
        {
            var installation = await _context.Installations
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (installation == null)
                return null;

            // Snapshot status BEFORE mutation so we can detect a completion transition below.
            var oldStatus = installation.Status;

            // Update only provided fields
            if (updateDto.ContactId.HasValue) installation.ContactId = updateDto.ContactId.Value;
            if (updateDto.SiteAddress != null) installation.SiteAddress = updateDto.SiteAddress;
            if (updateDto.InstallationType != null) installation.InstallationType = updateDto.InstallationType;
            if (updateDto.InstallationDate.HasValue) installation.InstallationDate = updateDto.InstallationDate.Value;
            if (updateDto.Status != null) installation.Status = updateDto.Status;
            if (updateDto.Notes != null) installation.Notes = updateDto.Notes;
            if (updateDto.Name != null) installation.Name = updateDto.Name;
            if (updateDto.Model != null) installation.Model = updateDto.Model;
            if (updateDto.Manufacturer != null) installation.Manufacturer = updateDto.Manufacturer;
            if (updateDto.SerialNumber != null) installation.SerialNumber = updateDto.SerialNumber;
            if (updateDto.Matricule != null) installation.Matricule = updateDto.Matricule;
            if (updateDto.Category != null) installation.Category = updateDto.Category;
            if (updateDto.Type != null) installation.Type = updateDto.Type;

            // Warranty resolution: the structured Warranty DTO always wins over
            // the flat WarrantyExpiry field to avoid the previous order-of-apply
            // bug where WarrantyExpiry was set and then immediately nulled by
            // Warranty.HasWarranty=false in the same request.
            if (updateDto.Warranty != null)
            {
                if (updateDto.Warranty.HasWarranty)
                {
                    installation.WarrantyExpiry = null;
                    installation.WarrantyFrom = null;
                    if (!string.IsNullOrEmpty(updateDto.Warranty.WarrantyTo) && DateTime.TryParse(updateDto.Warranty.WarrantyTo, out var parsedExpiry))
                    {
                        installation.WarrantyExpiry = parsedExpiry;
                    }
                    if (!string.IsNullOrEmpty(updateDto.Warranty.WarrantyFrom) && DateTime.TryParse(updateDto.Warranty.WarrantyFrom, out var parsedFrom))
                    {
                        installation.WarrantyFrom = parsedFrom;
                    }
                }
                else
                {
                    installation.WarrantyExpiry = null;
                    installation.WarrantyFrom = null;
                }
            }
            else if (updateDto.WarrantyExpiry.HasValue)
            {
                installation.WarrantyExpiry = updateDto.WarrantyExpiry;
            }

            installation.ModifiedDate = DateTime.UtcNow;
            installation.ModifiedBy = userId;

            var newStatus = updateDto.Status;

            await _context.SaveChangesAsync();

            // Log installation completion to the contact activity feed
            if (_contactActivity != null && installation.ContactId > 0 &&
                newStatus != null && oldStatus != newStatus &&
                (newStatus == "completed" || newStatus == "installed"))
            {
                await _contactActivity.LogAsync(
                    contactId: installation.ContactId,
                    type: MyApi.Modules.Contacts.Models.ContactActivityTypes.InstallationCompleted,
                    relatedEntityType: MyApi.Modules.Contacts.Models.ContactActivityEntityTypes.Installation,
                    relatedEntityId: installation.Id,
                    description: $"Installation {installation.InstallationNumber} was completed",
                    metadata: new { number = installation.InstallationNumber, name = installation.Name, oldStatus, status = newStatus },
                    createdBy: userId);
            }

            return MapToDto(installation);
        }

        public async Task<bool> DeleteInstallationAsync(int id, string userId)
        {
            var installation = await _context.Installations
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (installation == null)
                return false;

            // Soft delete: hard delete would orphan denormalized InstallationId
            // rows on TimeEntries/Expenses/MaterialUsage/ServiceOrderJobs/
            // ServiceOrderMaterials (see 20260717 denormalization migration).
            installation.IsDeleted = true;
            installation.DeletedAt = DateTime.UtcNow;
            installation.DeletedBy = userId;
            installation.ModifiedDate = DateTime.UtcNow;
            installation.ModifiedBy = userId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<MaintenanceHistoryDto>> GetMaintenanceHistoryAsync(int installationId, int page = 1, int pageSize = 20)
        {
            // Same visibility rule as every other read path: soft-deleted
            // installations expose no maintenance history.
            var installationVisible = await _context.Installations
                .AnyAsync(i => i.Id == installationId && !i.IsDeleted);
            if (!installationVisible)
                return new List<MaintenanceHistoryDto>();

            var histories = await _context.MaintenanceHistories
                .Where(m => m.InstallationId == installationId)
                .OrderByDescending(m => m.MaintenanceDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return histories.Select(MapMaintenanceHistoryToDto).ToList();
        }

        public async Task<MaintenanceHistoryDto> AddMaintenanceHistoryAsync(int installationId, CreateMaintenanceHistoryDto historyDto, string userId)
        {
            // Filter soft-deleted installations to match every other read path in
            // this service (Get/Update/Delete) — prevents orphan maintenance rows
            // being attached to installations users can no longer see.
            var installation = await _context.Installations
                .FirstOrDefaultAsync(i => i.Id == installationId && !i.IsDeleted);
            if (installation == null)
                throw new KeyNotFoundException($"Installation with ID {installationId} not found");

            var history = new MaintenanceHistory
            {
                InstallationId = installationId,
                MaintenanceDate = historyDto.MaintenanceDate,
                MaintenanceType = historyDto.MaintenanceType,
                Description = historyDto.Description,
                PerformedBy = historyDto.PerformedBy,
                Cost = historyDto.Cost,
                NextMaintenanceDate = historyDto.NextMaintenanceDate,
                CreatedDate = DateTime.UtcNow
            };

            _context.MaintenanceHistories.Add(history);
            await _context.SaveChangesAsync();

            return MapMaintenanceHistoryToDto(history);
        }

        private InstallationDto MapToDto(Installation installation)
        {
            return new InstallationDto
            {
                Id = installation.Id,
                InstallationNumber = installation.InstallationNumber,
                ContactId = installation.ContactId,
                SiteAddress = installation.SiteAddress,
                InstallationType = installation.InstallationType,
                InstallationDate = installation.InstallationDate,
                Status = installation.Status,
                WarrantyExpiry = installation.WarrantyExpiry,
                Notes = installation.Notes,
                Name = installation.Name,
                Model = installation.Model,
                Manufacturer = installation.Manufacturer,
                SerialNumber = installation.SerialNumber,
                Matricule = installation.Matricule,
                Category = installation.Category,
                Type = installation.Type,
                Warranty = new WarrantyDto
                {
                    HasWarranty = installation.WarrantyExpiry.HasValue || installation.WarrantyFrom.HasValue,
                    WarrantyFrom = installation.WarrantyFrom?.ToString("yyyy-MM-dd"),
                    WarrantyTo = installation.WarrantyExpiry?.ToString("yyyy-MM-dd")
                },
                CreatedDate = installation.CreatedDate,
                CreatedBy = installation.CreatedBy,
                ModifiedDate = installation.ModifiedDate,
                ModifiedBy = installation.ModifiedBy,
                MaintenanceHistories = installation.MaintenanceHistories?.Select(MapMaintenanceHistoryToDto).ToList() ?? new List<MaintenanceHistoryDto>()
            };
        }

        private MaintenanceHistoryDto MapMaintenanceHistoryToDto(MaintenanceHistory history)
        {
            return new MaintenanceHistoryDto
            {
                Id = history.Id,
                InstallationId = history.InstallationId,
                MaintenanceDate = history.MaintenanceDate,
                MaintenanceType = history.MaintenanceType,
                Description = history.Description,
                PerformedBy = history.PerformedBy,
                Cost = history.Cost,
                NextMaintenanceDate = history.NextMaintenanceDate,
                CreatedDate = history.CreatedDate
            };
        }

        // =====================================================
        // Bulk Import - Supports up to 10,000+ records
        // =====================================================

        /// <summary>
        /// High-performance bulk import with batch processing.
        /// Uses AddRange for optimal database performance.
        /// </summary>
        public async Task<BulkImportInstallationResultDto> BulkImportInstallationsAsync(BulkImportInstallationRequestDto importRequest, string userId)
        {
            const int BATCH_SIZE = 100;

            var result = new BulkImportInstallationResultDto
            {
                TotalProcessed = importRequest.Installations.Count
            };

            try
            {
                // Pre-fetch existing serial numbers for duplicate detection
                var serialNumbersToCheck = importRequest.Installations
                    .Where(i => !string.IsNullOrEmpty(i.SerialNumber))
                    .Select(i => i.SerialNumber!.ToLower())
                    .Distinct()
                    .ToList();

                var existingSerialNumbers = await _context.Installations
                    .AsNoTracking()
                    .Where(i => i.SerialNumber != null && serialNumbersToCheck.Contains(i.SerialNumber.ToLower()))
                    .Select(i => new { i.Id, SerialNumber = i.SerialNumber!.ToLower() })
                    .ToDictionaryAsync(i => i.SerialNumber, i => i.Id);

                // Validate contact IDs exist
                var contactIds = importRequest.Installations
                    .Where(i => i.ContactId > 0)
                    .Select(i => i.ContactId)
                    .Distinct()
                    .ToList();

                var validContactIds = (await _context.Contacts
                    .AsNoTracking()
                    .Where(c => contactIds.Contains(c.Id) && c.IsActive)
                    .Select(c => c.Id)
                    .ToListAsync()).ToHashSet();

                // Get next installation number
                var lastInstallation = await _context.Installations
                    .OrderByDescending(i => i.Id)
                    .FirstOrDefaultAsync();
                var nextNumber = (lastInstallation?.Id ?? 0) + 1;

                // Process in batches
                var batches = importRequest.Installations
                    .Select((installation, index) => new { installation, index })
                    .GroupBy(x => x.index / BATCH_SIZE)
                    .Select(g => g.Select(x => x.installation).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    var newInstallations = new List<Installation>();
                    var installationsToUpdate = new List<(Installation existing, CreateInstallationDto dto)>();

                    foreach (var dto in batch)
                    {
                        try
                        {
                            // Validate contact ID
                            if (dto.ContactId > 0 && !validContactIds.Contains(dto.ContactId))
                            {
                                result.FailedCount++;
                                result.Errors.Add($"Contact ID {dto.ContactId} not found for installation {dto.Name ?? dto.SerialNumber}");
                                continue;
                            }

                            var serialNumber = dto.SerialNumber?.ToLower();
                            int? existingId = null;

                            if (!string.IsNullOrEmpty(serialNumber) && existingSerialNumbers.TryGetValue(serialNumber, out var id))
                            {
                                existingId = id;
                            }

                            if (existingId.HasValue)
                            {
                                if (importRequest.SkipDuplicates)
                                {
                                    result.SkippedCount++;
                                    continue;
                                }
                                else if (importRequest.UpdateExisting)
                                {
                                    var existing = await _context.Installations.FindAsync(existingId.Value);
                                    if (existing != null)
                                    {
                                        installationsToUpdate.Add((existing, dto));
                                    }
                                }
                                else
                                {
                                    result.FailedCount++;
                                    result.Errors.Add($"Duplicate serial number: {dto.SerialNumber}");
                                }
                            }
                            else
                            {
                                var installationNumber = $"INST-{DateTime.UtcNow.Year}-{nextNumber:D6}";
                                nextNumber++;

                                // Normalize status
                                var status = dto.Status?.ToLower()?.Trim();
                            var validStatuses = new[] { "active", "inactive", "maintenance", "decommissioned", "pending", "installed", "retired" };
                            if (string.IsNullOrEmpty(status) || !validStatuses.Contains(status))
                                {
                                    status = "active";
                                }

                                // Build SiteAddress and InstallationType
                                var siteAddress = dto.SiteAddress;
                                if (string.IsNullOrEmpty(siteAddress))
                                {
                                    siteAddress = dto.Name ?? "Default Site";
                                }

                                var installationType = dto.InstallationType;
                                if (string.IsNullOrEmpty(installationType))
                                {
                                    installationType = dto.Type ?? dto.Category ?? "general";
                                }

                                // Parse warranty dates
                                DateTime? warrantyExpiry = dto.WarrantyExpiry;
                                DateTime? warrantyFrom = null;
                                if (dto.Warranty?.HasWarranty == true)
                                {
                                    if (!string.IsNullOrEmpty(dto.Warranty.WarrantyTo) && DateTime.TryParse(dto.Warranty.WarrantyTo, out var parsedExpiry))
                                    {
                                        warrantyExpiry = parsedExpiry;
                                    }
                                    if (!string.IsNullOrEmpty(dto.Warranty.WarrantyFrom) && DateTime.TryParse(dto.Warranty.WarrantyFrom, out var parsedFrom))
                                    {
                                        warrantyFrom = parsedFrom;
                                    }
                                }

                                var installation = new Installation
                                {
                                    InstallationNumber = installationNumber,
                                    ContactId = dto.ContactId,
                                    SiteAddress = siteAddress,
                                    InstallationType = installationType,
                                    InstallationDate = dto.InstallationDate ?? DateTime.UtcNow,
                                    Status = status,
                                    WarrantyExpiry = warrantyExpiry,
                                    WarrantyFrom = warrantyFrom,
                                    Notes = dto.Notes,
                                    Name = dto.Name,
                                    Model = dto.Model,
                                    Manufacturer = dto.Manufacturer,
                                    SerialNumber = dto.SerialNumber,
                                    Matricule = dto.Matricule,
                                    Category = dto.Category,
                                    Type = dto.Type,
                                    CreatedDate = DateTime.UtcNow,
                                    CreatedBy = userId
                                };

                                newInstallations.Add(installation);

                                if (!string.IsNullOrEmpty(dto.SerialNumber))
                                {
                                    existingSerialNumbers[dto.SerialNumber.ToLower()] = 0;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"Failed to process installation {dto.Name ?? dto.SerialNumber}: {ex.Message}");
                        }
                    }

                    // Batch insert new installations
                    if (newInstallations.Any())
                    {
                        await _context.Installations.AddRangeAsync(newInstallations);
                        await _context.SaveChangesAsync();
                        result.SuccessCount += newInstallations.Count;

                        foreach (var installation in newInstallations)
                        {
                            result.ImportedInstallations.Add(MapToDto(installation));
                        }
                    }

                    // Batch update existing installations
                    foreach (var (existing, dto) in installationsToUpdate)
                    {
                        try
                        {
                            if (dto.ContactId > 0) existing.ContactId = dto.ContactId;
                            if (!string.IsNullOrEmpty(dto.Name)) existing.Name = dto.Name;
                            if (!string.IsNullOrEmpty(dto.Model)) existing.Model = dto.Model;
                            if (!string.IsNullOrEmpty(dto.Manufacturer)) existing.Manufacturer = dto.Manufacturer;
                            if (!string.IsNullOrEmpty(dto.Category)) existing.Category = dto.Category;
                            if (!string.IsNullOrEmpty(dto.Type)) existing.Type = dto.Type;
                            if (!string.IsNullOrEmpty(dto.SiteAddress)) existing.SiteAddress = dto.SiteAddress;
                            if (!string.IsNullOrEmpty(dto.InstallationType)) existing.InstallationType = dto.InstallationType;
                            if (dto.InstallationDate.HasValue) existing.InstallationDate = dto.InstallationDate.Value;
                            if (!string.IsNullOrEmpty(dto.Status)) existing.Status = dto.Status;
                            if (dto.WarrantyExpiry.HasValue) existing.WarrantyExpiry = dto.WarrantyExpiry;
                            if (dto.Notes != null) existing.Notes = dto.Notes;
                            existing.ModifiedDate = DateTime.UtcNow;
                            existing.ModifiedBy = userId;

                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"Failed to update installation: {dto.SerialNumber} - {ex.Message}");
                        }
                    }

                    if (installationsToUpdate.Any())
                    {
                        await _context.SaveChangesAsync();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Bulk import failed: {ex.Message}");
                throw;
            }
        }
    }
}
