using MyApi.Modules.WebsiteBuilder.DTOs;

namespace MyApi.Modules.WebsiteBuilder.Services
{
    public interface IWBSiteService
    {
        Task<WBSiteListResponseDto> GetAllSitesAsync(WBSiteSearchRequestDto? searchRequest = null);
        Task<WBSiteResponseDto?> GetSiteByIdAsync(int id);
        Task<WBSiteResponseDto?> GetSiteBySlugAsync(string slug);
        /// <summary>Public render path — bypasses the global tenant filter; only returns published sites.</summary>
        Task<WBSiteResponseDto?> GetPublishedSiteBySlugAsync(string slug);
        Task<WBSiteResponseDto> CreateSiteAsync(CreateWBSiteRequestDto createDto, string createdByUser);
        Task<WBSiteResponseDto?> UpdateSiteAsync(int id, UpdateWBSiteRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteSiteAsync(int id, string deletedByUser);
        Task<WBSiteResponseDto?> DuplicateSiteAsync(int id, string createdByUser);
        Task<WBSiteResponseDto?> PublishSiteAsync(int id, string publishedByUser);
        Task<WBSiteResponseDto?> UnpublishSiteAsync(int id, string modifiedByUser);
    }

    public interface IWBPageService
    {
        Task<List<WBPageResponseDto>> GetPagesBySiteIdAsync(int siteId);
        Task<WBPageResponseDto?> GetPageByIdAsync(int id);
        Task<WBPageResponseDto> CreatePageAsync(CreateWBPageRequestDto createDto, string createdByUser);
        Task<WBPageResponseDto?> UpdatePageAsync(int id, UpdateWBPageRequestDto updateDto, string modifiedByUser);
        /// <summary>Wave 2: returns the new UpdatedAt on success, or throws WBConcurrencyException when the optimistic-concurrency check fails.</summary>
        Task<WBPageSaveResultDto?> UpdatePageComponentsAsync(int id, UpdateWBPageComponentsRequestDto updateDto, string modifiedByUser);
        Task<bool> DeletePageAsync(int id, string deletedByUser);
        Task<bool> ReorderPagesAsync(ReorderWBPagesRequestDto reorderDto, string modifiedByUser);
        
        // Versioning
        Task<List<WBPageVersionResponseDto>> GetPageVersionsAsync(int pageId);
        Task<WBPageVersionResponseDto> SavePageVersionAsync(int pageId, CreateWBPageVersionRequestDto createDto, string createdByUser);
        Task<bool> RestorePageVersionAsync(int pageId, int versionId, string modifiedByUser);
    }

    public interface IWBGlobalBlockService
    {
        Task<List<WBGlobalBlockResponseDto>> GetAllGlobalBlocksAsync();
        Task<WBGlobalBlockResponseDto?> GetGlobalBlockByIdAsync(int id);
        Task<WBGlobalBlockResponseDto> CreateGlobalBlockAsync(CreateWBGlobalBlockRequestDto createDto, string createdByUser);
        Task<WBGlobalBlockResponseDto?> UpdateGlobalBlockAsync(int id, UpdateWBGlobalBlockRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteGlobalBlockAsync(int id, string deletedByUser);
        Task<bool> TrackUsageAsync(int globalBlockId, int siteId, int? pageId);
    }

    public interface IWBBrandProfileService
    {
        Task<List<WBBrandProfileResponseDto>> GetAllBrandProfilesAsync();
        Task<WBBrandProfileResponseDto?> GetBrandProfileByIdAsync(int id);
        Task<WBBrandProfileResponseDto> CreateBrandProfileAsync(CreateWBBrandProfileRequestDto createDto, string createdByUser);
        Task<WBBrandProfileResponseDto?> UpdateBrandProfileAsync(int id, UpdateWBBrandProfileRequestDto updateDto, string modifiedByUser);
        Task<bool> DeleteBrandProfileAsync(int id, string deletedByUser);
    }

    public interface IWBFormSubmissionService
    {
        Task<WBFormSubmissionListResponseDto> GetSubmissionsBySiteIdAsync(int siteId, int pageNumber = 1, int pageSize = 50);
        Task<WBFormSubmissionResponseDto> CreateSubmissionAsync(CreateWBFormSubmissionRequestDto createDto, string? ipAddress);
        Task<bool> DeleteSubmissionAsync(int id);
        Task<bool> ClearSubmissionsAsync(int siteId, string? formComponentId = null);
    }

    public interface IWBMediaService
    {
        Task<List<WBMediaResponseDto>> GetMediaAsync(int? siteId = null, string? folder = null);
        Task<WBMediaResponseDto?> GetMediaByIdAsync(int id);
        /// <summary>
        /// Returns internal DTO including FilePath for disk operations (upload controller, delete).
        /// </summary>
        Task<WBMediaInternalDto?> GetMediaByIdInternalAsync(int id);
        /// <summary>Public render path — bypasses the global tenant filter; only returns media whose owning site is published (or unassigned).</summary>
        Task<WBMediaInternalDto?> GetPublicMediaByIdAsync(int id);
        Task<WBMediaResponseDto> CreateMediaAsync(CreateWBMediaRequestDto createDto, string uploadedByUser);
        Task<bool> UpdateFileUrlAsync(int id, string fileUrl);
        Task<bool> DeleteMediaAsync(int id);
    }

    public interface IWBTemplateService
    {
        Task<List<WBTemplateResponseDto>> GetAllTemplatesAsync();
        Task<WBTemplateResponseDto?> GetTemplateByIdAsync(int id);
        Task<List<string>> GetTemplateCategoriesAsync();
    }

    public interface IWBActivityLogService
    {
        Task LogActivityAsync(int siteId, int? pageId, string action, string entityType, string? details, string createdByUser);
        Task<List<WBActivityLogResponseDto>> GetActivityLogAsync(int siteId, int count = 50);
    }
}