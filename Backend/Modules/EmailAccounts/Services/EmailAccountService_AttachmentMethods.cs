using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService
    {
        // ═══════════════════════════════════════════════
        // DOWNLOAD ATTACHMENT ON-DEMAND
        // ═══════════════════════════════════════════════

        public async Task<AttachmentDownloadDto?> DownloadAttachmentAsync(Guid accountId, int userId, Guid emailId, Guid attachmentId)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return null;

            var syncedEmail = await _context.Set<SyncedEmail>()
                .FirstOrDefaultAsync(e => e.Id == emailId && e.ConnectedEmailAccountId == accountId);

            if (syncedEmail == null) return null;

            var attachment = await _context.Set<SyncedEmailAttachment>()
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.SyncedEmailId == emailId);

            if (attachment == null) return null;

            var contentBase64 = account.Provider switch
            {
                "google" => await DownloadGmailAttachmentAsync(account, syncedEmail.ExternalId, attachment.ExternalAttachmentId),
                "microsoft" => await DownloadOutlookAttachmentAsync(account, syncedEmail.ExternalId, attachment.ExternalAttachmentId),
                _ => null
            };

            if (contentBase64 == null) return null;

            return new AttachmentDownloadDto
            {
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                Size = attachment.Size,
                ContentBase64 = contentBase64,
            };
        }

        private async Task<string?> DownloadGmailAttachmentAsync(ConnectedEmailAccount account, string messageExternalId, string attachmentId)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidGoogleTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Gmail: GET /messages/{messageId}/attachments/{attachmentId}
            var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{messageExternalId}/attachments/{attachmentId}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gmail attachment download failed for {AttachmentId}: {Status}", attachmentId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // Gmail returns base64url-encoded data in "data" field
            if (data.TryGetProperty("data", out var dataField))
            {
                var base64Url = dataField.GetString() ?? "";
                // Convert base64url to standard base64
                var base64 = base64Url.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }
                return base64;
            }

            return null;
        }

        private async Task<string?> DownloadOutlookAttachmentAsync(ConnectedEmailAccount account, string messageExternalId, string attachmentId)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidMicrosoftTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Outlook: GET /messages/{messageId}/attachments/{attachmentId}
            var url = $"https://graph.microsoft.com/v1.0/me/messages/{messageExternalId}/attachments/{attachmentId}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Outlook attachment download failed for {AttachmentId}: {Status}", attachmentId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // Outlook returns base64-encoded content in "contentBytes" field
            if (data.TryGetProperty("contentBytes", out var contentBytes))
            {
                return contentBytes.GetString();
            }

            return null;
        }
    }
}
