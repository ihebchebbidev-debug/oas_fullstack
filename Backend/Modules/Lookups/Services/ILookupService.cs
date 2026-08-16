using MyApi.Modules.Lookups.DTOs;

namespace MyApi.Modules.Lookups.Services
{
    public interface ILookupService
    {
        // Article Categories
        Task<LookupListResponseDto> GetArticleCategoriesAsync();
        Task<LookupItemDto?> GetArticleCategoryByIdAsync(int id);
        Task<LookupItemDto> CreateArticleCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateArticleCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteArticleCategoryAsync(int id, string deletedByUser);

        // Article Groups
        Task<LookupListResponseDto> GetArticleGroupsAsync();
        Task<LookupItemDto?> GetArticleGroupByIdAsync(int id);
        Task<LookupItemDto> CreateArticleGroupAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateArticleGroupAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteArticleGroupAsync(int id, string deletedByUser);

        // Document Types
        Task<LookupListResponseDto> GetDocumentTypesAsync();
        Task<LookupItemDto?> GetDocumentTypeByIdAsync(int id);
        Task<LookupItemDto> CreateDocumentTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateDocumentTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteDocumentTypeAsync(int id, string deletedByUser);

        // Article Statuses
        Task<LookupListResponseDto> GetArticleStatusesAsync();
        Task<LookupItemDto?> GetArticleStatusByIdAsync(int id);
        Task<LookupItemDto> CreateArticleStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateArticleStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteArticleStatusAsync(int id, string deletedByUser);

        // Service Categories
        Task<LookupListResponseDto> GetServiceCategoriesAsync();
        Task<LookupItemDto?> GetServiceCategoryByIdAsync(int id);
        Task<LookupItemDto> CreateServiceCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateServiceCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteServiceCategoryAsync(int id, string deletedByUser);

        // Task Statuses
        Task<LookupListResponseDto> GetTaskStatusesAsync();
        Task<LookupItemDto?> GetTaskStatusByIdAsync(int id);
        Task<LookupItemDto> CreateTaskStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateTaskStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteTaskStatusAsync(int id, string deletedByUser);

        // Event Types
        Task<LookupListResponseDto> GetEventTypesAsync();
        Task<LookupItemDto?> GetEventTypeByIdAsync(int id);
        Task<LookupItemDto> CreateEventTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateEventTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteEventTypeAsync(int id, string deletedByUser);

        // Priorities
        Task<LookupListResponseDto> GetPrioritiesAsync();
        Task<LookupItemDto?> GetPriorityByIdAsync(int id);
        Task<LookupItemDto> CreatePriorityAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdatePriorityAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeletePriorityAsync(int id, string deletedByUser);

        // Technician Statuses
        Task<LookupListResponseDto> GetTechnicianStatusesAsync();
        Task<LookupItemDto?> GetTechnicianStatusByIdAsync(int id);
        Task<LookupItemDto> CreateTechnicianStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateTechnicianStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteTechnicianStatusAsync(int id, string deletedByUser);

        // Leave Types
        Task<LookupListResponseDto> GetLeaveTypesAsync();
        Task<LookupItemDto?> GetLeaveTypeByIdAsync(int id);
        Task<LookupItemDto> CreateLeaveTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateLeaveTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteLeaveTypeAsync(int id, string deletedByUser);

        // Project Statuses
        Task<LookupListResponseDto> GetProjectStatusesAsync();
        Task<LookupItemDto?> GetProjectStatusByIdAsync(int id);
        Task<LookupItemDto> CreateProjectStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateProjectStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteProjectStatusAsync(int id, string deletedByUser);

        // Project Types
        Task<LookupListResponseDto> GetProjectTypesAsync();
        Task<LookupItemDto?> GetProjectTypeByIdAsync(int id);
        Task<LookupItemDto> CreateProjectTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateProjectTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteProjectTypeAsync(int id, string deletedByUser);

        // Offer Statuses
        Task<LookupListResponseDto> GetOfferStatusesAsync();
        Task<LookupItemDto?> GetOfferStatusByIdAsync(int id);
        Task<LookupItemDto> CreateOfferStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateOfferStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteOfferStatusAsync(int id, string deletedByUser);

        // Sale Statuses
        Task<LookupListResponseDto> GetSaleStatusesAsync();
        Task<LookupItemDto?> GetSaleStatusByIdAsync(int id);
        Task<LookupItemDto> CreateSaleStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateSaleStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteSaleStatusAsync(int id, string deletedByUser);

        // Service Order Statuses
        Task<LookupListResponseDto> GetServiceOrderStatusesAsync();
        Task<LookupItemDto?> GetServiceOrderStatusByIdAsync(int id);
        Task<LookupItemDto> CreateServiceOrderStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateServiceOrderStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteServiceOrderStatusAsync(int id, string deletedByUser);

        // Dispatch Statuses
        Task<LookupListResponseDto> GetDispatchStatusesAsync();
        Task<LookupItemDto?> GetDispatchStatusByIdAsync(int id);
        Task<LookupItemDto> CreateDispatchStatusAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateDispatchStatusAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteDispatchStatusAsync(int id, string deletedByUser);

        // Offer Categories
        Task<LookupListResponseDto> GetOfferCategoriesAsync();
        Task<LookupItemDto?> GetOfferCategoryByIdAsync(int id);
        Task<LookupItemDto> CreateOfferCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateOfferCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteOfferCategoryAsync(int id, string deletedByUser);

        // Offer Sources
        Task<LookupListResponseDto> GetOfferSourcesAsync();
        Task<LookupItemDto?> GetOfferSourceByIdAsync(int id);
        Task<LookupItemDto> CreateOfferSourceAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateOfferSourceAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteOfferSourceAsync(int id, string deletedByUser);

        // Skills
        Task<LookupListResponseDto> GetSkillsAsync();
        Task<LookupItemDto?> GetSkillByIdAsync(int id);
        Task<LookupItemDto> CreateSkillAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateSkillAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteSkillAsync(int id, string deletedByUser);

        // Countries
        Task<LookupListResponseDto> GetCountriesAsync();
        Task<LookupItemDto?> GetCountryByIdAsync(int id);
        Task<LookupItemDto> CreateCountryAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateCountryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteCountryAsync(int id, string deletedByUser);

        // Locations
        Task<LookupListResponseDto> GetLocationsAsync();
        Task<LookupItemDto?> GetLocationByIdAsync(int id);
        Task<LookupItemDto> CreateLocationAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateLocationAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteLocationAsync(int id, string deletedByUser);

        // Installation Types
        Task<LookupListResponseDto> GetInstallationTypesAsync();
        Task<LookupItemDto?> GetInstallationTypeByIdAsync(int id);
        Task<LookupItemDto> CreateInstallationTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateInstallationTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteInstallationTypeAsync(int id, string deletedByUser);

        // Installation Categories
        Task<LookupListResponseDto> GetInstallationCategoriesAsync();
        Task<LookupItemDto?> GetInstallationCategoryByIdAsync(int id);
        Task<LookupItemDto> CreateInstallationCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateInstallationCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteInstallationCategoryAsync(int id, string deletedByUser);

        // Work Types
        Task<LookupListResponseDto> GetWorkTypesAsync();
        Task<LookupItemDto?> GetWorkTypeByIdAsync(int id);
        Task<LookupItemDto> CreateWorkTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateWorkTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteWorkTypeAsync(int id, string deletedByUser);

        // Expense Types
        Task<LookupListResponseDto> GetExpenseTypesAsync();
        Task<LookupItemDto?> GetExpenseTypeByIdAsync(int id);
        Task<LookupItemDto> CreateExpenseTypeAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateExpenseTypeAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteExpenseTypeAsync(int id, string deletedByUser);

        // Form Categories
        Task<LookupListResponseDto> GetFormCategoriesAsync();
        Task<LookupItemDto?> GetFormCategoryByIdAsync(int id);
        Task<LookupItemDto> CreateFormCategoryAsync(CreateLookupItemRequestDto createDto, string createdByUser);
        Task<LookupItemDto?> UpdateFormCategoryAsync(int id, UpdateLookupItemRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteFormCategoryAsync(int id, string deletedByUser);

        // Currencies
        Task<CurrencyListResponseDto> GetCurrenciesAsync();
        Task<CurrencyDto?> GetCurrencyByIdAsync(int id);
        Task<CurrencyDto> CreateCurrencyAsync(CreateCurrencyRequestDto createDto, string createdByUser);
        Task<CurrencyDto?> UpdateCurrencyAsync(int id, UpdateCurrencyRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteCurrencyAsync(int id, string deletedByUser);
    }
}
