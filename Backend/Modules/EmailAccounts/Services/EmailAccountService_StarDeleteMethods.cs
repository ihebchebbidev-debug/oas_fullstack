using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Modules.EmailAccounts.Models;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService
    {
        // ═══════════════════════════════════════════════
        // TOGGLE STAR (Gmail: STARRED label, Outlook: flag)
        // ═══════════════════════════════════════════════

        public async Task<bool> ToggleStarEmailAsync(Guid accountId, int userId, Guid emailId)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return false;

            var syncedEmail = await _context.Set<SyncedEmail>()
                .FirstOrDefaultAsync(e => e.Id == emailId && e.ConnectedEmailAccountId == accountId);

            if (syncedEmail == null) return false;

            var newStarred = !syncedEmail.IsStarred;

            // Call provider API
            var success = account.Provider switch
            {
                "google" => await ToggleStarGmailAsync(account, syncedEmail.ExternalId, newStarred),
                "microsoft" => await ToggleStarOutlookAsync(account, syncedEmail.ExternalId, newStarred),
                _ => throw new InvalidOperationException($"Unsupported provider: {account.Provider}")
            };

            if (!success) return false;

            // Update local DB
            syncedEmail.IsStarred = newStarred;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Toggled star on email {EmailId} to {Starred} for account {Handle}",
                emailId, newStarred, account.Handle);

            return true;
        }

        private async Task<bool> ToggleStarGmailAsync(ConnectedEmailAccount account, string externalId, bool star)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidGoogleTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Gmail: POST /messages/{id}/modify with addLabelIds or removeLabelIds
            var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{externalId}/modify";

            var body = star
                ? new { addLabelIds = new[] { "STARRED" }, removeLabelIds = Array.Empty<string>() }
                : new { addLabelIds = Array.Empty<string>(), removeLabelIds = new[] { "STARRED" } };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gmail toggle star failed for {ExternalId}: {Status}", externalId, response.StatusCode);
                return false;
            }

            return true;
        }

        private async Task<bool> ToggleStarOutlookAsync(ConnectedEmailAccount account, string externalId, bool star)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidMicrosoftTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Outlook: PATCH /messages/{id} with flag.flagStatus
            var url = $"https://graph.microsoft.com/v1.0/me/messages/{externalId}";

            var body = new
            {
                flag = new
                {
                    flagStatus = star ? "flagged" : "notFlagged"
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Outlook toggle star failed for {ExternalId}: {Status}", externalId, response.StatusCode);
                return false;
            }

            return true;
        }

        // ═══════════════════════════════════════════════
        // TOGGLE READ/UNREAD (Gmail: UNREAD label, Outlook: isRead)
        // ═══════════════════════════════════════════════

        public async Task<bool> ToggleReadEmailAsync(Guid accountId, int userId, Guid emailId)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return false;

            var syncedEmail = await _context.Set<SyncedEmail>()
                .FirstOrDefaultAsync(e => e.Id == emailId && e.ConnectedEmailAccountId == accountId);

            if (syncedEmail == null) return false;

            var newIsRead = !syncedEmail.IsRead;

            var success = account.Provider switch
            {
                "google" => await ToggleReadGmailAsync(account, syncedEmail.ExternalId, newIsRead),
                "microsoft" => await ToggleReadOutlookAsync(account, syncedEmail.ExternalId, newIsRead),
                _ => throw new InvalidOperationException($"Unsupported provider: {account.Provider}")
            };

            if (!success) return false;

            syncedEmail.IsRead = newIsRead;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Toggled read on email {EmailId} to {IsRead} for account {Handle}",
                emailId, newIsRead, account.Handle);

            return true;
        }

        private async Task<bool> ToggleReadGmailAsync(ConnectedEmailAccount account, string externalId, bool markRead)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidGoogleTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Gmail: POST /messages/{id}/modify — add UNREAD to mark unread, remove UNREAD to mark read
            var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{externalId}/modify";

            var body = markRead
                ? new { addLabelIds = Array.Empty<string>(), removeLabelIds = new[] { "UNREAD" } }
                : new { addLabelIds = new[] { "UNREAD" }, removeLabelIds = Array.Empty<string>() };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gmail toggle read failed for {ExternalId}: {Status}", externalId, response.StatusCode);
                return false;
            }

            return true;
        }

        private async Task<bool> ToggleReadOutlookAsync(ConnectedEmailAccount account, string externalId, bool markRead)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidMicrosoftTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Outlook: PATCH /messages/{id} with isRead
            var url = $"https://graph.microsoft.com/v1.0/me/messages/{externalId}";

            var body = new { isRead = markRead };

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Outlook toggle read failed for {ExternalId}: {Status}", externalId, response.StatusCode);
                return false;
            }

            return true;
        }

        // ═══════════════════════════════════════════════
        // DELETE / TRASH EMAIL
        // ═══════════════════════════════════════════════

        public async Task<bool> DeleteEmailAsync(Guid accountId, int userId, Guid emailId)
        {
            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null) return false;

            var syncedEmail = await _context.Set<SyncedEmail>()
                .FirstOrDefaultAsync(e => e.Id == emailId && e.ConnectedEmailAccountId == accountId);

            if (syncedEmail == null) return false;

            // Call provider API to trash (not permanently delete)
            var success = account.Provider switch
            {
                "google" => await TrashGmailAsync(account, syncedEmail.ExternalId),
                "microsoft" => await TrashOutlookAsync(account, syncedEmail.ExternalId),
                _ => throw new InvalidOperationException($"Unsupported provider: {account.Provider}")
            };

            if (!success) return false;

            // Remove from local DB
            _context.Set<SyncedEmail>().Remove(syncedEmail);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Trashed email {EmailId} (external: {ExternalId}) for account {Handle}",
                emailId, syncedEmail.ExternalId, account.Handle);

            return true;
        }

        private async Task<bool> TrashGmailAsync(ConnectedEmailAccount account, string externalId)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidGoogleTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Gmail: POST /messages/{id}/trash
            var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{externalId}/trash";
            var response = await client.PostAsync(url, null);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gmail trash failed for {ExternalId}: {Status}", externalId, response.StatusCode);
                return false;
            }

            return true;
        }

        private async Task<bool> TrashOutlookAsync(ConnectedEmailAccount account, string externalId)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidMicrosoftTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Outlook: POST /messages/{id}/move to deletedItems folder
            var url = $"https://graph.microsoft.com/v1.0/me/messages/{externalId}/move";

            var body = new { destinationId = "deleteditems" };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Outlook trash failed for {ExternalId}: {Status}", externalId, response.StatusCode);
                return false;
            }

            return true;
        }
    }
}
