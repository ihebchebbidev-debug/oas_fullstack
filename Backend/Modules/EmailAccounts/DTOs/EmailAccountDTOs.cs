using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.EmailAccounts.DTOs
{
    // ─── Response DTOs ───

    public class ConnectedEmailAccountDto
    {
        public Guid Id { get; set; }
        public int UserId { get; set; }
        public string Handle { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string SyncStatus { get; set; } = "not_synced";
        public DateTime? LastSyncedAt { get; set; }
        public DateTime? AuthFailedAt { get; set; }
        public string EmailVisibility { get; set; } = "share_everything";
        public string CalendarVisibility { get; set; } = "share_everything";
        public string ContactAutoCreationPolicy { get; set; } = "sent_and_received";
        public bool IsEmailSyncEnabled { get; set; }
        public bool IsCalendarSyncEnabled { get; set; }
        public bool ExcludeGroupEmails { get; set; }
        public bool ExcludeNonProfessionalEmails { get; set; }
        public bool IsCalendarContactAutoCreationEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<BlocklistItemDto> BlocklistItems { get; set; } = new();
    }

    public class BlocklistItemDto
    {
        public Guid Id { get; set; }
        public string Handle { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // ─── OAuth Callback DTO ───

    /// <summary>
    /// Sent by the frontend after completing OAuth with Google/Microsoft.
    /// The frontend handles the OAuth popup/redirect and sends the code here.
    /// </summary>
    public class OAuthCallbackDto
    {
        [Required]
        public string Provider { get; set; } = string.Empty; // "google" or "microsoft"

        [Required]
        public string Code { get; set; } = string.Empty; // Authorization code from OAuth

        public string? RedirectUri { get; set; } // The redirect URI used in the OAuth flow

        public string? EmailVisibility { get; set; }
        public string? CalendarVisibility { get; set; }
    }

    // ─── Settings Update DTOs ───

    public class UpdateEmailSettingsDto
    {
        public string? EmailVisibility { get; set; }
        public string? ContactAutoCreationPolicy { get; set; }
        public bool? ExcludeGroupEmails { get; set; }
        public bool? ExcludeNonProfessionalEmails { get; set; }
        public bool? IsEmailSyncEnabled { get; set; }
    }

    public class UpdateCalendarSettingsDto
    {
        public string? CalendarVisibility { get; set; }
        public bool? IsCalendarContactAutoCreationEnabled { get; set; }
        public bool? IsCalendarSyncEnabled { get; set; }
    }

    // ─── Blocklist DTOs ───

    public class CreateBlocklistItemDto
    {
        [Required]
        [MaxLength(255)]
        public string Handle { get; set; } = string.Empty; // email or @domain
    }

    // ─── OAuth Config (returned to frontend so it knows where to redirect) ───

    public class OAuthConfigDto
    {
        public string Provider { get; set; } = string.Empty;
        public string AuthUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = Array.Empty<string>();
        public string RedirectUri { get; set; } = string.Empty;
    }
}
