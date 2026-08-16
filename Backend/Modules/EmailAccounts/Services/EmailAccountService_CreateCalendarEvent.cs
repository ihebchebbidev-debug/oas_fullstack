using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService
    {
        // ═══════════════════════════════════════════════
        // CREATE CALENDAR EVENT ON EXTERNAL PROVIDER
        // ═══════════════════════════════════════════════

        public async Task<CreateExternalCalendarEventResultDto> CreateCalendarEventAsync(
            Guid accountId, int userId, CreateExternalCalendarEventDto dto)
        {
            _logger.LogInformation("CreateCalendarEventAsync for accountId={AccountId}, userId={UserId}", accountId, userId);

            var account = await _context.ConnectedEmailAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null)
                return new CreateExternalCalendarEventResultDto { Success = false, Error = "Account not found" };

            if (!account.IsCalendarSyncEnabled)
                return new CreateExternalCalendarEventResultDto { Success = false, Error = "Calendar sync is disabled for this account" };

            try
            {
                var result = account.Provider switch
                {
                    "google" => await CreateGoogleCalendarEventAsync(account, dto),
                    "microsoft" => await CreateOutlookCalendarEventAsync(account, dto),
                    _ => new CreateExternalCalendarEventResultDto { Success = false, Error = $"Unsupported provider: {account.Provider}" }
                };

                // If successful, also save a local copy in SyncedCalendarEvents
                if (result.Success && !string.IsNullOrEmpty(result.ExternalId))
                {
                    var localEvent = new SyncedCalendarEvent
                    {
                        ConnectedEmailAccountId = account.Id,
                        ExternalId = result.ExternalId,
                        Title = dto.Title,
                        Description = dto.Description,
                        Location = dto.Location,
                        StartTime = EnsureUtc(dto.StartTime),
                        EndTime = EnsureUtc(dto.EndTime),
                        IsAllDay = dto.IsAllDay,
                        Status = "confirmed",
                        OrganizerEmail = account.Handle,
                        Attendees = dto.Attendees?.Count > 0 ? JsonSerializer.Serialize(dto.Attendees) : null,
                    };
                    _context.Set<SyncedCalendarEvent>().Add(localEvent);
                    await _context.SaveChangesAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create calendar event on provider for account {AccountId}", accountId);
                return new CreateExternalCalendarEventResultDto { Success = false, Error = ex.Message };
            }
        }

        private async Task<CreateExternalCalendarEventResultDto> CreateGoogleCalendarEventAsync(
            ConnectedEmailAccount account, CreateExternalCalendarEventDto dto)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidGoogleTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Build Google Calendar event payload
            var eventBody = new Dictionary<string, object>
            {
                ["summary"] = dto.Title
            };

            if (!string.IsNullOrEmpty(dto.Description))
                eventBody["description"] = dto.Description;

            if (!string.IsNullOrEmpty(dto.Location))
                eventBody["location"] = dto.Location;

            if (dto.IsAllDay)
            {
                eventBody["start"] = new Dictionary<string, string>
                {
                    ["date"] = dto.StartTime.ToString("yyyy-MM-dd")
                };
                eventBody["end"] = new Dictionary<string, string>
                {
                    ["date"] = dto.EndTime.ToString("yyyy-MM-dd")
                };
            }
            else
            {
                eventBody["start"] = new Dictionary<string, string>
                {
                    ["dateTime"] = dto.StartTime.ToUniversalTime().ToString("o"),
                    ["timeZone"] = "UTC"
                };
                eventBody["end"] = new Dictionary<string, string>
                {
                    ["dateTime"] = dto.EndTime.ToUniversalTime().ToString("o"),
                    ["timeZone"] = "UTC"
                };
            }

            if (dto.Attendees?.Count > 0)
            {
                eventBody["attendees"] = dto.Attendees.Select(email => new Dictionary<string, string>
                {
                    ["email"] = email
                }).ToList();
            }

            var json = JsonSerializer.Serialize(eventBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                "https://www.googleapis.com/calendar/v3/calendars/primary/events?sendUpdates=all",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Google Calendar create event failed: {Status} - {Body}", response.StatusCode, errorBody);
                return new CreateExternalCalendarEventResultDto
                {
                    Success = false,
                    Error = $"Google Calendar API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var externalId = responseData.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

            return new CreateExternalCalendarEventResultDto
            {
                Success = true,
                ExternalId = externalId
            };
        }

        private async Task<CreateExternalCalendarEventResultDto> CreateOutlookCalendarEventAsync(
            ConnectedEmailAccount account, CreateExternalCalendarEventDto dto)
        {
            var client = _httpClientFactory.CreateClient();
            var accessToken = await EnsureValidMicrosoftTokenAsync(account);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Build Microsoft Graph event payload
            var eventBody = new Dictionary<string, object>
            {
                ["subject"] = dto.Title
            };

            if (!string.IsNullOrEmpty(dto.Description))
            {
                eventBody["body"] = new Dictionary<string, string>
                {
                    ["contentType"] = "text",
                    ["content"] = dto.Description
                };
            }

            if (!string.IsNullOrEmpty(dto.Location))
            {
                eventBody["location"] = new Dictionary<string, string>
                {
                    ["displayName"] = dto.Location
                };
            }

            eventBody["isAllDay"] = dto.IsAllDay;

            eventBody["start"] = new Dictionary<string, string>
            {
                ["dateTime"] = dto.StartTime.ToUniversalTime().ToString("o"),
                ["timeZone"] = "UTC"
            };
            eventBody["end"] = new Dictionary<string, string>
            {
                ["dateTime"] = dto.EndTime.ToUniversalTime().ToString("o"),
                ["timeZone"] = "UTC"
            };

            if (dto.Attendees?.Count > 0)
            {
                eventBody["attendees"] = dto.Attendees.Select(email => new Dictionary<string, object>
                {
                    ["emailAddress"] = new Dictionary<string, string>
                    {
                        ["address"] = email
                    },
                    ["type"] = "required"
                }).ToList();
            }

            var json = JsonSerializer.Serialize(eventBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://graph.microsoft.com/v1.0/me/events", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Outlook Calendar create event failed: {Status} - {Body}", response.StatusCode, errorBody);
                return new CreateExternalCalendarEventResultDto
                {
                    Success = false,
                    Error = $"Outlook Calendar API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var externalId = responseData.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

            return new CreateExternalCalendarEventResultDto
            {
                Success = true,
                ExternalId = externalId
            };
        }
    }
}
