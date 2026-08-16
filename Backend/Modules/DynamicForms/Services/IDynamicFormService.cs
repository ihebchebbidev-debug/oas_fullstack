using MyApi.Modules.DynamicForms.DTOs;
using MyApi.Modules.DynamicForms.Models;

namespace MyApi.Modules.DynamicForms.Services
{
    /// <summary>
    /// Service interface for Dynamic Forms operations
    /// </summary>
    public interface IDynamicFormService
    {
        // Form CRUD operations
        Task<PagedResultDto<DynamicFormDto>> GetAllAsync(DynamicFormQueryParams? queryParams = null);
        Task<DynamicFormDto?> GetByIdAsync(int id);
        Task<DynamicFormDto> CreateAsync(CreateDynamicFormDto dto, string userId);
        Task<DynamicFormDto> UpdateAsync(int id, UpdateDynamicFormDto dto, string userId);
        Task<bool> DeleteAsync(int id);
        Task<DynamicFormDto> DuplicateAsync(int id, string userId);
        Task<DynamicFormDto> ChangeStatusAsync(int id, string status, string userId);

        // Response operations
        Task<PagedResultDto<DynamicFormResponseDto>> GetResponsesAsync(int formId, int? page = null, int? pageSize = null);
        Task<DynamicFormResponseDto> SubmitResponseAsync(SubmitFormResponseDto dto, string userId);
        Task<int> GetResponseCountAsync(int formId);

        // Public form operations
        Task<DynamicFormDto?> GetBySlugAsync(string slug);
        Task<DynamicFormResponseDto> SubmitPublicResponseAsync(string slug, PublicSubmitFormResponseDto dto);
        Task<string> GenerateUniqueSlugAsync(string baseName);
    }
}
