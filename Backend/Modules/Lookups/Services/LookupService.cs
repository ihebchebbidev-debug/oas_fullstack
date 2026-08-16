using MyApi.Modules.Lookups.DTOs;
using MyApi.Modules.Lookups.Models;
using MyApi.Data;
using Microsoft.EntityFrameworkCore;
// caching removed for Lookups service

namespace MyApi.Modules.Lookups.Services
{
    public class LookupService : ILookupService
    {
        private readonly ApplicationDbContext _context;

        public LookupService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Article Categories
        public async Task<LookupListResponseDto> GetArticleCategoriesAsync() => await GetLookupsByTypeAsync("article-category");
        public async Task<LookupItemDto?> GetArticleCategoryByIdAsync(int id) => await GetLookupByIdAsync(id, "article-category");
        public async Task<LookupItemDto> CreateArticleCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "article-category", createdByUser);
        public async Task<LookupItemDto?> UpdateArticleCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "article-category", modifiedByUser);
        public async Task<bool> DeleteArticleCategoryAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "article-category", deletedByUser);

        // Article Groups
        public async Task<LookupListResponseDto> GetArticleGroupsAsync() => await GetLookupsByTypeAsync("article-group");
        public async Task<LookupItemDto?> GetArticleGroupByIdAsync(int id) => await GetLookupByIdAsync(id, "article-group");
        public async Task<LookupItemDto> CreateArticleGroupAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "article-group", createdByUser);
        public async Task<LookupItemDto?> UpdateArticleGroupAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "article-group", modifiedByUser);
        public async Task<bool> DeleteArticleGroupAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "article-group", deletedByUser);

        // Document Types
        public async Task<LookupListResponseDto> GetDocumentTypesAsync() => await GetLookupsByTypeAsync("document-type");
        public async Task<LookupItemDto?> GetDocumentTypeByIdAsync(int id) => await GetLookupByIdAsync(id, "document-type");
        public async Task<LookupItemDto> CreateDocumentTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "document-type", createdByUser);
        public async Task<LookupItemDto?> UpdateDocumentTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "document-type", modifiedByUser);
        public async Task<bool> DeleteDocumentTypeAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "document-type", deletedByUser);

        // Article Statuses
        public async Task<LookupListResponseDto> GetArticleStatusesAsync() => await GetLookupsByTypeAsync("article-status");
        public async Task<LookupItemDto?> GetArticleStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "article-status");
        public async Task<LookupItemDto> CreateArticleStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "article-status", createdByUser);
        public async Task<LookupItemDto?> UpdateArticleStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "article-status", modifiedByUser);
        public async Task<bool> DeleteArticleStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "article-status", deletedByUser);

        // Service Categories
        public async Task<LookupListResponseDto> GetServiceCategoriesAsync() => await GetLookupsByTypeAsync("service-category");
        public async Task<LookupItemDto?> GetServiceCategoryByIdAsync(int id) => await GetLookupByIdAsync(id, "service-category");
        public async Task<LookupItemDto> CreateServiceCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "service-category", createdByUser);
        public async Task<LookupItemDto?> UpdateServiceCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "service-category", modifiedByUser);
        public async Task<bool> DeleteServiceCategoryAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "service-category", deletedByUser);

        // Task Statuses
        public async Task<LookupListResponseDto> GetTaskStatusesAsync() => await GetLookupsByTypeAsync("task-status");
        public async Task<LookupItemDto?> GetTaskStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "task-status");
        public async Task<LookupItemDto> CreateTaskStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "task-status", createdByUser);
        public async Task<LookupItemDto?> UpdateTaskStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "task-status", modifiedByUser);
        public async Task<bool> DeleteTaskStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "task-status", deletedByUser);

        // Event Types
        public async Task<LookupListResponseDto> GetEventTypesAsync() => await GetLookupsByTypeAsync("event-type");
        public async Task<LookupItemDto?> GetEventTypeByIdAsync(int id) => await GetLookupByIdAsync(id, "event-type");
        public async Task<LookupItemDto> CreateEventTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "event-type", createdByUser);
        public async Task<LookupItemDto?> UpdateEventTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "event-type", modifiedByUser);
        public async Task<bool> DeleteEventTypeAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "event-type", deletedByUser);

        // Priorities
        public async Task<LookupListResponseDto> GetPrioritiesAsync() => await GetLookupsByTypeAsync("priority");
        public async Task<LookupItemDto?> GetPriorityByIdAsync(int id) => await GetLookupByIdAsync(id, "priority");
        public async Task<LookupItemDto> CreatePriorityAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "priority", createdByUser);
        public async Task<LookupItemDto?> UpdatePriorityAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "priority", modifiedByUser);
        public async Task<bool> DeletePriorityAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "priority", deletedByUser);

        // Technician Statuses
        public async Task<LookupListResponseDto> GetTechnicianStatusesAsync() => await GetLookupsByTypeAsync("technician-status");
        public async Task<LookupItemDto?> GetTechnicianStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "technician-status");
        public async Task<LookupItemDto> CreateTechnicianStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "technician-status", createdByUser);
        public async Task<LookupItemDto?> UpdateTechnicianStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "technician-status", modifiedByUser);
        public async Task<bool> DeleteTechnicianStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "technician-status", deletedByUser);

        // Leave Types
        public async Task<LookupListResponseDto> GetLeaveTypesAsync() => await GetLookupsByTypeAsync("leave-type");
        public async Task<LookupItemDto?> GetLeaveTypeByIdAsync(int id) => await GetLookupByIdAsync(id, "leave-type");
        public async Task<LookupItemDto> CreateLeaveTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "leave-type", createdByUser);
        public async Task<LookupItemDto?> UpdateLeaveTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "leave-type", modifiedByUser);
        public async Task<bool> DeleteLeaveTypeAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "leave-type", deletedByUser);

        // Project Statuses
        public async Task<LookupListResponseDto> GetProjectStatusesAsync() => await GetLookupsByTypeAsync("project-status");
        public async Task<LookupItemDto?> GetProjectStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "project-status");
        public async Task<LookupItemDto> CreateProjectStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "project-status", createdByUser);
        public async Task<LookupItemDto?> UpdateProjectStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "project-status", modifiedByUser);
        public async Task<bool> DeleteProjectStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "project-status", deletedByUser);

        // Project Types
        public async Task<LookupListResponseDto> GetProjectTypesAsync() => await GetLookupsByTypeAsync("project-type");
        public async Task<LookupItemDto?> GetProjectTypeByIdAsync(int id) => await GetLookupByIdAsync(id, "project-type");
        public async Task<LookupItemDto> CreateProjectTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "project-type", createdByUser);
        public async Task<LookupItemDto?> UpdateProjectTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "project-type", modifiedByUser);
        public async Task<bool> DeleteProjectTypeAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "project-type", deletedByUser);

        // Offer Statuses
        public async Task<LookupListResponseDto> GetOfferStatusesAsync() => await GetLookupsByTypeAsync("offer-status");
        public async Task<LookupItemDto?> GetOfferStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "offer-status");
        public async Task<LookupItemDto> CreateOfferStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "offer-status", createdByUser);
        public async Task<LookupItemDto?> UpdateOfferStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "offer-status", modifiedByUser);
        public async Task<bool> DeleteOfferStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "offer-status", deletedByUser);

        // Sale Statuses
        public async Task<LookupListResponseDto> GetSaleStatusesAsync() => await GetLookupsByTypeAsync("sale-status");
        public async Task<LookupItemDto?> GetSaleStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "sale-status");
        public async Task<LookupItemDto> CreateSaleStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "sale-status", createdByUser);
        public async Task<LookupItemDto?> UpdateSaleStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "sale-status", modifiedByUser);
        public async Task<bool> DeleteSaleStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "sale-status", deletedByUser);

        // Service Order Statuses
        public async Task<LookupListResponseDto> GetServiceOrderStatusesAsync() => await GetLookupsByTypeAsync("service-order-status");
        public async Task<LookupItemDto?> GetServiceOrderStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "service-order-status");
        public async Task<LookupItemDto> CreateServiceOrderStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "service-order-status", createdByUser);
        public async Task<LookupItemDto?> UpdateServiceOrderStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "service-order-status", modifiedByUser);
        public async Task<bool> DeleteServiceOrderStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "service-order-status", deletedByUser);

        // Dispatch Statuses
        public async Task<LookupListResponseDto> GetDispatchStatusesAsync() => await GetLookupsByTypeAsync("dispatch-status");
        public async Task<LookupItemDto?> GetDispatchStatusByIdAsync(int id) => await GetLookupByIdAsync(id, "dispatch-status");
        public async Task<LookupItemDto> CreateDispatchStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "dispatch-status", createdByUser);
        public async Task<LookupItemDto?> UpdateDispatchStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "dispatch-status", modifiedByUser);
        public async Task<bool> DeleteDispatchStatusAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "dispatch-status", deletedByUser);

        // Offer Categories
        public async Task<LookupListResponseDto> GetOfferCategoriesAsync() => await GetLookupsByTypeAsync("offer-category");
        public async Task<LookupItemDto?> GetOfferCategoryByIdAsync(int id) => await GetLookupByIdAsync(id, "offer-category");
        public async Task<LookupItemDto> CreateOfferCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "offer-category", createdByUser);
        public async Task<LookupItemDto?> UpdateOfferCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "offer-category", modifiedByUser);
        public async Task<bool> DeleteOfferCategoryAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "offer-category", deletedByUser);

        // Offer Sources
        public async Task<LookupListResponseDto> GetOfferSourcesAsync() => await GetLookupsByTypeAsync("offer-source");
        public async Task<LookupItemDto?> GetOfferSourceByIdAsync(int id) => await GetLookupByIdAsync(id, "offer-source");
        public async Task<LookupItemDto> CreateOfferSourceAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "offer-source", createdByUser);
        public async Task<LookupItemDto?> UpdateOfferSourceAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "offer-source", modifiedByUser);
        public async Task<bool> DeleteOfferSourceAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "offer-source", deletedByUser);

        // Skills
        public async Task<LookupListResponseDto> GetSkillsAsync() => await GetLookupsByTypeAsync("skill");
        public async Task<LookupItemDto?> GetSkillByIdAsync(int id) => await GetLookupByIdAsync(id, "skill");
        public async Task<LookupItemDto> CreateSkillAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "skill", createdByUser);
        public async Task<LookupItemDto?> UpdateSkillAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "skill", modifiedByUser);
        public async Task<bool> DeleteSkillAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "skill", deletedByUser);

        // Countries
        public async Task<LookupListResponseDto> GetCountriesAsync() => await GetLookupsByTypeAsync("country");
        public async Task<LookupItemDto?> GetCountryByIdAsync(int id) => await GetLookupByIdAsync(id, "country");
        public async Task<LookupItemDto> CreateCountryAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "country", createdByUser);
        public async Task<LookupItemDto?> UpdateCountryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "country", modifiedByUser);
        public async Task<bool> DeleteCountryAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "country", deletedByUser);

        // Locations
        public async Task<LookupListResponseDto> GetLocationsAsync() => await GetLookupsByTypeAsync("location");
        public async Task<LookupItemDto?> GetLocationByIdAsync(int id) => await GetLookupByIdAsync(id, "location");
        public async Task<LookupItemDto> CreateLocationAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "location", createdByUser);
        public async Task<LookupItemDto?> UpdateLocationAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "location", modifiedByUser);
        public async Task<bool> DeleteLocationAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "location", deletedByUser);

        // Installation Types
        public async Task<LookupListResponseDto> GetInstallationTypesAsync() => await GetLookupsByTypeAsync(InstallationTypeConstants.LookupType);
        public async Task<LookupItemDto?> GetInstallationTypeByIdAsync(int id) => await GetLookupByIdAsync(id, InstallationTypeConstants.LookupType);
        public async Task<LookupItemDto> CreateInstallationTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, InstallationTypeConstants.LookupType, createdByUser);
        public async Task<LookupItemDto?> UpdateInstallationTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, InstallationTypeConstants.LookupType, modifiedByUser);
        public async Task<bool> DeleteInstallationTypeAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, InstallationTypeConstants.LookupType, deletedByUser);

        // Installation Categories
        public async Task<LookupListResponseDto> GetInstallationCategoriesAsync() => await GetLookupsByTypeAsync(InstallationCategoryConstants.LookupType);
        public async Task<LookupItemDto?> GetInstallationCategoryByIdAsync(int id) => await GetLookupByIdAsync(id, InstallationCategoryConstants.LookupType);
        public async Task<LookupItemDto> CreateInstallationCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, InstallationCategoryConstants.LookupType, createdByUser);
        public async Task<LookupItemDto?> UpdateInstallationCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, InstallationCategoryConstants.LookupType, modifiedByUser);
        public async Task<bool> DeleteInstallationCategoryAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, InstallationCategoryConstants.LookupType, deletedByUser);

        // Work Types
        public async Task<LookupListResponseDto> GetWorkTypesAsync() => await GetLookupsByTypeAsync("work-type");
        public async Task<LookupItemDto?> GetWorkTypeByIdAsync(int id) => await GetLookupByIdAsync(id, "work-type");
        public async Task<LookupItemDto> CreateWorkTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "work-type", createdByUser);
        public async Task<LookupItemDto?> UpdateWorkTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "work-type", modifiedByUser);
        public async Task<bool> DeleteWorkTypeAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "work-type", deletedByUser);

        // Expense Types
        public async Task<LookupListResponseDto> GetExpenseTypesAsync() => await GetLookupsByTypeAsync("expense-type");
        public async Task<LookupItemDto?> GetExpenseTypeByIdAsync(int id) => await GetLookupByIdAsync(id, "expense-type");
        public async Task<LookupItemDto> CreateExpenseTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "expense-type", createdByUser);
        public async Task<LookupItemDto?> UpdateExpenseTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "expense-type", modifiedByUser);
        public async Task<bool> DeleteExpenseTypeAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "expense-type", deletedByUser);

        // Form Categories
        public async Task<LookupListResponseDto> GetFormCategoriesAsync() => await GetLookupsByTypeAsync("form-category");
        public async Task<LookupItemDto?> GetFormCategoryByIdAsync(int id) => await GetLookupByIdAsync(id, "form-category");
        public async Task<LookupItemDto> CreateFormCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser) => await CreateLookupAsync(createDto, "form-category", createdByUser);
        public async Task<LookupItemDto?> UpdateFormCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser) => await UpdateLookupAsync(id, updateDto, "form-category", modifiedByUser);
        public async Task<bool> DeleteFormCategoryAsync(int id, string deletedByUser) => await DeleteLookupAsync(id, "form-category", deletedByUser);

        // Currencies
        public async Task<CurrencyListResponseDto> GetCurrenciesAsync()
        {
            var currencies = await _context.Currencies
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return new CurrencyListResponseDto
            {
                currencies = currencies.Select(MapCurrencyToDto).ToList(),
                totalCount = currencies.Count
            };
        }

        public async Task<CurrencyDto?> GetCurrencyByIdAsync(int id)
        {
            var currency = await _context.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            return currency != null ? MapCurrencyToDto(currency) : null;
        }

        public async Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyRequestDto createDto, string createdByUser)
        {
            var entity = new Currency
            {
                Name = createDto.Name,
                Symbol = createDto.Symbol,
                Code = createDto.Code.ToUpper(),
                IsActive = createDto.IsActive,
                IsDefault = createDto.IsDefault,
                SortOrder = createDto.SortOrder,
                CreatedUser = createdByUser,
                CreatedAt = DateTime.UtcNow
            };

            _context.Currencies.Add(entity);
            await _context.SaveChangesAsync();

            return MapCurrencyToDto(entity);
        }

        public async Task<CurrencyDto?> UpdateCurrencyAsync(int id, UpdateCurrencyRequestDto updateDto, string modifiedByUser)
        {
            var entity = await _context.Currencies
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (entity == null) return null;

            if (updateDto.Name != null) entity.Name = updateDto.Name;
            if (updateDto.Symbol != null) entity.Symbol = updateDto.Symbol;
            if (updateDto.Code != null) entity.Code = updateDto.Code.ToUpper();
            if (updateDto.IsActive.HasValue) entity.IsActive = updateDto.IsActive.Value;
            if (updateDto.IsDefault.HasValue) entity.IsDefault = updateDto.IsDefault.Value;
            if (updateDto.SortOrder.HasValue) entity.SortOrder = updateDto.SortOrder.Value;

            await _context.SaveChangesAsync();
            return MapCurrencyToDto(entity);
        }

        public async Task<bool> DeleteCurrencyAsync(int id, string deletedByUser)
        {
            var entity = await _context.Currencies
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (entity == null) return false;

            entity.IsDeleted = true;

            await _context.SaveChangesAsync();
            return true;
        }
        private async Task<LookupListResponseDto> GetLookupsByTypeAsync(string lookupType)
        {
            var items = await _context.LookupItems
                .AsNoTracking()
                .Where(x => x.LookupType == lookupType && !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return new LookupListResponseDto
            {
                items = items.Select(MapToDto).ToList(),
                totalCount = items.Count
            };
        }

        private async Task<LookupItemDto?> GetLookupByIdAsync(int id, string lookupType)
        {
            var item = await _context.LookupItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.LookupType == lookupType && !x.IsDeleted);

            return item != null ? MapToDto(item) : null;
        }

        private async Task<LookupItemDto> CreateLookupAsync(CreateLookupItemRequestDto createDto, string lookupType, string createdByUser)
        {
            var entity = new LookupItem
            {
                Name = createDto.Name,
                Description = createDto.Description,
                Color = createDto.Color,
                LookupType = lookupType,
                IsActive = createDto.IsActive,
                SortOrder = createDto.SortOrder,
                CreatedUser = createdByUser,
                CreatedAt = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                Category = createDto.Category,
                Value = createDto.Value
            };

            // optional paid flag (used for leave types)
            entity.IsPaid = createDto.IsPaid;

            _context.LookupItems.Add(entity);
            await _context.SaveChangesAsync();

            return MapToDto(entity);
        }

        private async Task<LookupItemDto?> UpdateLookupAsync(int id, UpdateLookupItemRequestDto updateDto, string lookupType, string modifiedByUser)
        {
            var entity = await _context.LookupItems
                .FirstOrDefaultAsync(x => x.Id == id && x.LookupType == lookupType && !x.IsDeleted);

            if (entity == null) return null;

            if (updateDto.Name != null) entity.Name = updateDto.Name;
            if (updateDto.Description != null) entity.Description = updateDto.Description;
            if (updateDto.Color != null) entity.Color = updateDto.Color;
            if (updateDto.IsActive.HasValue) entity.IsActive = updateDto.IsActive.Value;
            if (updateDto.SortOrder.HasValue) entity.SortOrder = updateDto.SortOrder.Value;
            if (updateDto.Category != null) entity.Category = updateDto.Category;
            if (updateDto.Value != null) entity.Value = updateDto.Value;
            if (updateDto.IsPaid.HasValue) entity.IsPaid = updateDto.IsPaid.Value;
            
            // Handle IsDefault - if setting to true, unset other defaults of same type
            if (updateDto.IsDefault.HasValue)
            {
                if (updateDto.IsDefault.Value)
                {
                    var existingDefaults = await _context.LookupItems
                        .Where(x => x.LookupType == lookupType && x.IsDefault && x.Id != id && !x.IsDeleted)
                        .ToListAsync();
                    
                    foreach (var existing in existingDefaults)
                    {
                        existing.IsDefault = false;
                    }
                }
                entity.IsDefault = updateDto.IsDefault.Value;
            }

            entity.ModifyUser = modifiedByUser;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToDto(entity);
        }

        private async Task<bool> DeleteLookupAsync(int id, string lookupType, string deletedByUser)
        {
            var entity = await _context.LookupItems
                .FirstOrDefaultAsync(x => x.Id == id && x.LookupType == lookupType && !x.IsDeleted);

            if (entity == null) return false;

            entity.IsDeleted = true;
            entity.ModifyUser = deletedByUser;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        private static LookupItemDto MapToDto(LookupItem entity)
        {
            return new LookupItemDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Color = entity.Color,
                IsActive = entity.IsActive,
                IsDefault = entity.IsDefault,
                SortOrder = entity.SortOrder,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                CreatedUser = entity.CreatedUser,
                ModifyUser = entity.ModifyUser,
                Category = entity.Category,
                Value = entity.Value,
                LookupType = entity.LookupType
                ,
                IsPaid = entity.IsPaid
            };
        }

        private static CurrencyDto MapCurrencyToDto(Currency entity)
        {
            return new CurrencyDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Symbol = entity.Symbol,
                Code = entity.Code,
                IsActive = entity.IsActive,
                IsDefault = entity.IsDefault,
                SortOrder = entity.SortOrder,
                CreatedAt = entity.CreatedAt,
                CreatedUser = entity.CreatedUser
            };
        }
    }
}
