using MyApi.Data;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService : IEmailAccountService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailAccountService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Microsoft.AspNetCore.DataProtection.IDataProtector _protector;

        public EmailAccountService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<EmailAccountService> logger,
            IHttpClientFactory httpClientFactory,
            Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _protector = dataProtectionProvider.CreateProtector("CustomEmailAccountPasswords");
        }

        // ────────────────────────────────────────────────
        // OAuth Configuration
        // ────────────────────────────────────────────────

        public Task<OAuthConfigDto> GetOAuthConfigAsync(string provider)
        {
            var config = provider.ToLower() switch
            {
                "google" => new OAuthConfigDto
                {
                    Provider = "google",
                    AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth",
                    ClientId = _configuration["OAuth:Google:ClientId"] ?? "",
                    Scopes = new[]
                    {
                        "openid",
                        "email",
                        "profile",
                        "https://www.googleapis.com/auth/gmail.modify",
                        "https://www.googleapis.com/auth/gmail.send",
                        "https://www.googleapis.com/auth/calendar.events",
                        "https://www.googleapis.com/auth/profile.emails.read"
                    },
                    RedirectUri = _configuration["OAuth:Google:RedirectUri"] ?? ""
                },
                "microsoft" => new OAuthConfigDto
                {
                    Provider = "microsoft",
                    AuthUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                    ClientId = _configuration["OAuth:Microsoft:ClientId"] ?? "",
                    Scopes = new[]
                    {
                        "openid",
                        "email",
                        "profile",
                        "offline_access",
                        "Mail.ReadWrite",
                        "Mail.Send",
                        "Calendars.Read",
                        "User.Read"
                    },
                    RedirectUri = _configuration["OAuth:Microsoft:RedirectUri"] ?? ""
                },
                _ => throw new ArgumentException($"Unsupported provider: {provider}")
            };

            return Task.FromResult(config);
        }

        // ────────────────────────────────────────────────
        // OAuth Callback — exchange code for tokens
        // ────────────────────────────────────────────────

        public async Task<ConnectedEmailAccountDto> HandleOAuthCallbackAsync(int userId, OAuthCallbackDto callbackDto)
        {
            var provider = callbackDto.Provider.ToLower();
            _logger.LogInformation("Handling OAuth callback for provider {Provider}, user {UserId}", provider, userId);

            // Exchange authorization code for tokens
            var (accessToken, refreshToken, email, scopes) = provider switch
            {
                "google" => await ExchangeGoogleCodeAsync(callbackDto.Code, callbackDto.RedirectUri),
                "microsoft" => await ExchangeMicrosoftCodeAsync(callbackDto.Code, callbackDto.RedirectUri),
                _ => throw new ArgumentException($"Unsupported provider: {provider}")
            };

            // Check if account already exists for this user + handle
            var existing = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Handle == email && a.Provider == provider);

            if (existing != null)
            {
                // Reconnect — update tokens
                existing.AccessToken = accessToken;
                existing.RefreshToken = refreshToken;
                existing.Scopes = scopes;
                existing.AuthFailedAt = null;
                existing.SyncStatus = "active";
                existing.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(callbackDto.EmailVisibility))
                    existing.EmailVisibility = callbackDto.EmailVisibility;
                if (!string.IsNullOrEmpty(callbackDto.CalendarVisibility))
                    existing.CalendarVisibility = callbackDto.CalendarVisibility;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Reconnected existing account {Handle} for user {UserId}", email, userId);
                return MapToDto(existing);
            }

            // Create new connected account
            var account = new ConnectedEmailAccount
            {
                UserId = userId,
                Handle = email,
                Provider = provider,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Scopes = scopes,
                SyncStatus = "active",
                LastSyncedAt = DateTime.UtcNow,
                EmailVisibility = callbackDto.EmailVisibility ?? "share_everything",
                CalendarVisibility = callbackDto.CalendarVisibility ?? "share_everything",
            };

            _context.ConnectedEmailAccounts.Add(account);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new connected account {Handle} ({Provider}) for user {UserId}", email, provider, userId);
            return MapToDto(account);
        }

        // ────────────────────────────────────────────────
        // Token exchange helpers
        // ────────────────────────────────────────────────

        private async Task<(string accessToken, string refreshToken, string email, string scopes)> ExchangeGoogleCodeAsync(string code, string? redirectUri)
        {
            var client = _httpClientFactory.CreateClient();
            var tokenEndpoint = "https://oauth2.googleapis.com/token";

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _configuration["OAuth:Google:ClientId"] ?? "",
                ["client_secret"] = _configuration["OAuth:Google:ClientSecret"] ?? "",
                ["redirect_uri"] = redirectUri ?? _configuration["OAuth:Google:RedirectUri"] ?? "",
                ["grant_type"] = "authorization_code"
            });

            var response = await client.PostAsync(tokenEndpoint, body);
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            if (!response.IsSuccessStatusCode || json == null)
            {
                _logger.LogError("Google token exchange failed: {Status}", response.StatusCode);
                throw new InvalidOperationException("Failed to exchange Google authorization code for tokens");
            }

            var accessToken = json.GetValueOrDefault("access_token")?.ToString() ?? "";
            var refreshToken = json.GetValueOrDefault("refresh_token")?.ToString() ?? "";
            var scope = json.GetValueOrDefault("scope")?.ToString() ?? "";

            // Get user email from Google userinfo
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var userInfoResponse = await client.GetFromJsonAsync<Dictionary<string, object>>("https://www.googleapis.com/oauth2/v2/userinfo");
            var email = userInfoResponse?.GetValueOrDefault("email")?.ToString() ?? "";

            return (accessToken, refreshToken, email, scope);
        }

        private async Task<(string accessToken, string refreshToken, string email, string scopes)> ExchangeMicrosoftCodeAsync(string code, string? redirectUri)
        {
            var client = _httpClientFactory.CreateClient();
            var tokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _configuration["OAuth:Microsoft:ClientId"] ?? "",
                ["client_secret"] = _configuration["OAuth:Microsoft:ClientSecret"] ?? "",
                ["redirect_uri"] = redirectUri ?? _configuration["OAuth:Microsoft:RedirectUri"] ?? "",
                ["grant_type"] = "authorization_code",
                ["scope"] = "openid email profile offline_access Mail.ReadWrite Mail.Send Calendars.Read User.Read"
            });

            var response = await client.PostAsync(tokenEndpoint, body);
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            if (!response.IsSuccessStatusCode || json == null)
            {
                _logger.LogError("Microsoft token exchange failed: {Status}", response.StatusCode);
                throw new InvalidOperationException("Failed to exchange Microsoft authorization code for tokens");
            }

            var accessToken = json.GetValueOrDefault("access_token")?.ToString() ?? "";
            var refreshToken = json.GetValueOrDefault("refresh_token")?.ToString() ?? "";
            var scope = json.GetValueOrDefault("scope")?.ToString() ?? "";

            // Get user email from Microsoft Graph
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var meResponse = await client.GetFromJsonAsync<Dictionary<string, object>>("https://graph.microsoft.com/v1.0/me");
            var email = meResponse?.GetValueOrDefault("mail")?.ToString()
                     ?? meResponse?.GetValueOrDefault("userPrincipalName")?.ToString()
                     ?? "";

            return (accessToken, refreshToken, email, scope);
        }

        // ────────────────────────────────────────────────
        // Connected accounts CRUD
        // ────────────────────────────────────────────────

        public async Task<IEnumerable<ConnectedEmailAccountDto>> GetAccountsByUserAsync(int userId)
        {
            var accounts = await _context.ConnectedEmailAccounts
                .Include(a => a.BlocklistItems)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return accounts.Select(MapToDto);
        }

        public async Task<ConnectedEmailAccountDto?> GetAccountByIdAsync(Guid id)
        {
            var account = await _context.ConnectedEmailAccounts
                .Include(a => a.BlocklistItems)
                .FirstOrDefaultAsync(a => a.Id == id);

            return account != null ? MapToDto(account) : null;
        }

        public async Task<bool> DisconnectAccountAsync(Guid id, int userId)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return false;

            _context.ConnectedEmailAccounts.Remove(account);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Disconnected account {Handle} for user {UserId}", account.Handle, userId);
            return true;
        }

        public async Task<ConnectedEmailAccountDto?> ReconnectAccountAsync(Guid id, int userId, OAuthCallbackDto callbackDto)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return null;

            // Re-run OAuth exchange
            callbackDto.Provider = account.Provider;
            var result = await HandleOAuthCallbackAsync(userId, callbackDto);
            return result;
        }

        // ────────────────────────────────────────────────
        // Email & Calendar settings
        // ────────────────────────────────────────────────

        public async Task<ConnectedEmailAccountDto?> UpdateEmailSettingsAsync(Guid accountId, int userId, UpdateEmailSettingsDto dto)
        {
            var account = await _context.ConnectedEmailAccounts
                .Include(a => a.BlocklistItems)
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return null;

            if (dto.EmailVisibility != null) account.EmailVisibility = dto.EmailVisibility;
            if (dto.ContactAutoCreationPolicy != null) account.ContactAutoCreationPolicy = dto.ContactAutoCreationPolicy;
            if (dto.ExcludeGroupEmails.HasValue) account.ExcludeGroupEmails = dto.ExcludeGroupEmails.Value;
            if (dto.ExcludeNonProfessionalEmails.HasValue) account.ExcludeNonProfessionalEmails = dto.ExcludeNonProfessionalEmails.Value;
            if (dto.IsEmailSyncEnabled.HasValue) account.IsEmailSyncEnabled = dto.IsEmailSyncEnabled.Value;

            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(account);
        }

        public async Task<ConnectedEmailAccountDto?> UpdateCalendarSettingsAsync(Guid accountId, int userId, UpdateCalendarSettingsDto dto)
        {
            var account = await _context.ConnectedEmailAccounts
                .Include(a => a.BlocklistItems)
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return null;

            if (dto.CalendarVisibility != null) account.CalendarVisibility = dto.CalendarVisibility;
            if (dto.IsCalendarContactAutoCreationEnabled.HasValue) account.IsCalendarContactAutoCreationEnabled = dto.IsCalendarContactAutoCreationEnabled.Value;
            if (dto.IsCalendarSyncEnabled.HasValue) account.IsCalendarSyncEnabled = dto.IsCalendarSyncEnabled.Value;

            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(account);
        }

        // ────────────────────────────────────────────────
        // Blocklist
        // ────────────────────────────────────────────────

        public async Task<BlocklistItemDto?> AddBlocklistItemAsync(Guid accountId, int userId, CreateBlocklistItemDto dto)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return null;

            // Check duplicate
            var exists = await _context.EmailBlocklistItems
                .AnyAsync(b => b.ConnectedEmailAccountId == accountId && b.Handle == dto.Handle);

            if (exists) return null;

            var item = new EmailBlocklistItem
            {
                ConnectedEmailAccountId = accountId,
                Handle = dto.Handle
            };

            _context.EmailBlocklistItems.Add(item);
            await _context.SaveChangesAsync();

            return new BlocklistItemDto { Id = item.Id, Handle = item.Handle, CreatedAt = item.CreatedAt };
        }

        public async Task<bool> RemoveBlocklistItemAsync(Guid accountId, Guid itemId, int userId)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return false;

            var item = await _context.EmailBlocklistItems
                .FirstOrDefaultAsync(b => b.Id == itemId && b.ConnectedEmailAccountId == accountId);

            if (item == null) return false;

            _context.EmailBlocklistItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<BlocklistItemDto>> GetBlocklistAsync(Guid accountId, int userId)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return Enumerable.Empty<BlocklistItemDto>();

            var items = await _context.EmailBlocklistItems
                .Where(b => b.ConnectedEmailAccountId == accountId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlocklistItemDto { Id = b.Id, Handle = b.Handle, CreatedAt = b.CreatedAt })
                .ToListAsync();

            return items;
        }

        // ────────────────────────────────────────────────
        // Mapping
        // ────────────────────────────────────────────────

        private static ConnectedEmailAccountDto MapToDto(ConnectedEmailAccount a)
        {
            return new ConnectedEmailAccountDto
            {
                Id = a.Id,
                UserId = a.UserId,
                Handle = a.Handle,
                Provider = a.Provider,
                SyncStatus = a.SyncStatus,
                LastSyncedAt = a.LastSyncedAt,
                AuthFailedAt = a.AuthFailedAt,
                EmailVisibility = a.EmailVisibility,
                CalendarVisibility = a.CalendarVisibility,
                ContactAutoCreationPolicy = a.ContactAutoCreationPolicy,
                IsEmailSyncEnabled = a.IsEmailSyncEnabled,
                IsCalendarSyncEnabled = a.IsCalendarSyncEnabled,
                ExcludeGroupEmails = a.ExcludeGroupEmails,
                ExcludeNonProfessionalEmails = a.ExcludeNonProfessionalEmails,
                IsCalendarContactAutoCreationEnabled = a.IsCalendarContactAutoCreationEnabled,
                CreatedAt = a.CreatedAt,
                BlocklistItems = a.BlocklistItems?.Select(b => new BlocklistItemDto
                {
                    Id = b.Id,
                    Handle = b.Handle,
                    CreatedAt = b.CreatedAt
                }).ToList() ?? new List<BlocklistItemDto>()
            };
        }
    }
}
