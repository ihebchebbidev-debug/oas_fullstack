using MyApi.Modules.Numbering.DTOs;

namespace MyApi.Modules.Numbering.Services
{
    public interface INumberingService
    {
        /// <summary>
        /// Atomically generate and consume the next document number for the given entity.
        /// Falls back to legacy logic if no custom config exists or is disabled.
        /// </summary>
        Task<string> GetNextAsync(string entity);

        /// <summary>
        /// Preview the next N values for a given entity using current or provided settings (non-consuming).
        /// </summary>
        Task<List<string>> PreviewAsync(string entity, int count = 5);

        /// <summary>
        /// Preview using ad-hoc settings (for the admin UI before saving).
        /// </summary>
        List<string> PreviewFromTemplate(NumberingPreviewRequest request);

        /// <summary>
        /// Validate a template string and return any warnings/errors.
        /// </summary>
        (bool IsValid, List<string> Errors, List<string> Warnings) ValidateTemplate(string template, string strategy);

        /// <summary>
        /// Get all numbering settings.
        /// </summary>
        Task<List<NumberingSettingsDto>> GetAllSettingsAsync();

        /// <summary>
        /// Get settings for a specific entity.
        /// </summary>
        Task<NumberingSettingsDto?> GetSettingsAsync(string entity);

        /// <summary>
        /// Create or update settings for an entity.
        /// </summary>
        Task<NumberingSettingsDto> SaveSettingsAsync(string entity, UpdateNumberingSettingsRequest request);
    }
}
