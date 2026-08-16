using MyApi.Modules.EmailAccounts.DTOs;

namespace MyApi.Modules.EmailAccounts.Services
{
    public interface IEmailAccountService
    {
        // OAuth configuration (tells frontend where to redirect for consent)
        Task<OAuthConfigDto> GetOAuthConfigAsync(string provider);

        // OAuth callback (exchange code for tokens, create connected account)
        Task<ConnectedEmailAccountDto> HandleOAuthCallbackAsync(int userId, OAuthCallbackDto callbackDto);

        // Connected accounts CRUD
        Task<IEnumerable<ConnectedEmailAccountDto>> GetAccountsByUserAsync(int userId);
        Task<ConnectedEmailAccountDto?> GetAccountByIdAsync(Guid id);
        Task<bool> DisconnectAccountAsync(Guid id, int userId);

        // Reconnect (re-trigger OAuth for an existing account)
        Task<ConnectedEmailAccountDto?> ReconnectAccountAsync(Guid id, int userId, OAuthCallbackDto callbackDto);

        // Email settings
        Task<ConnectedEmailAccountDto?> UpdateEmailSettingsAsync(Guid accountId, int userId, UpdateEmailSettingsDto dto);

        // Calendar settings
        Task<ConnectedEmailAccountDto?> UpdateCalendarSettingsAsync(Guid accountId, int userId, UpdateCalendarSettingsDto dto);

        // Blocklist
        Task<BlocklistItemDto?> AddBlocklistItemAsync(Guid accountId, int userId, CreateBlocklistItemDto dto);
        Task<bool> RemoveBlocklistItemAsync(Guid accountId, Guid itemId, int userId);
        Task<IEnumerable<BlocklistItemDto>> GetBlocklistAsync(Guid accountId, int userId);

        // ─── Email Sync & Fetch ───

        Task<SyncResultDto> SyncEmailsAsync(Guid accountId, int userId, int maxResults = 50);
        Task<SyncedEmailsPageDto> GetSyncedEmailsAsync(Guid accountId, int userId, int page = 1, int pageSize = 25, string? search = null);

        // ─── Calendar Sync & Fetch ───

        Task<CalendarSyncResultDto> SyncCalendarAsync(Guid accountId, int userId, int maxResults = 50);
        Task<SyncedCalendarEventsPageDto> GetCalendarEventsAsync(Guid accountId, int userId, int page = 1, int pageSize = 25, string? search = null);

        // ─── Create Calendar Event on External Provider ───
        Task<CreateExternalCalendarEventResultDto> CreateCalendarEventAsync(Guid accountId, int userId, CreateExternalCalendarEventDto dto);

        // ─── Send Email ───
        // existingLogId is set by the retry handler so the same OutboundEmailLog row is
        // updated instead of a new one being created. Callers should leave it null.
        Task<SendEmailResultDto> SendEmailAsync(Guid accountId, int userId, SendEmailDto dto, long? existingLogId = null);

        // ─── Star / Delete Email ───
        Task<bool> ToggleStarEmailAsync(Guid accountId, int userId, Guid emailId);
        Task<bool> ToggleReadEmailAsync(Guid accountId, int userId, Guid emailId);
        Task<bool> DeleteEmailAsync(Guid accountId, int userId, Guid emailId);

        // ─── Attachment Download ───
        Task<AttachmentDownloadDto?> DownloadAttachmentAsync(Guid accountId, int userId, Guid emailId, Guid attachmentId);

        // ─── Custom (SMTP/IMAP/POP3) Accounts CRUD ───
        Task<IEnumerable<CustomEmailAccountDto>> GetCustomAccountsByUserAsync(int userId);
        Task<CustomEmailAccountDto?> GetCustomAccountByIdAsync(Guid id, int userId);
        Task<CustomEmailAccountDto> CreateCustomAccountAsync(int userId, CreateCustomEmailAccountDto dto);
        Task<CustomEmailAccountDto?> UpdateCustomAccountAsync(Guid id, int userId, CreateCustomEmailAccountDto dto);
        Task<bool> DeleteCustomAccountAsync(Guid id, int userId);
        Task<SyncResultDto> SyncCustomAccountAsync(Guid customAccountId, int userId, int maxResults = 50);
    }
}
