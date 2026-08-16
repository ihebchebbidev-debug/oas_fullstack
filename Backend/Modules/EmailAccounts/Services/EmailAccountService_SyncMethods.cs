using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService
    {

// ═══════════════════════════════════════════════
// EMAIL SYNC
// ═══════════════════════════════════════════════

public async Task<SyncResultDto> SyncEmailsAsync(Guid accountId, int userId, int maxResults = 50)
{
    _logger.LogInformation("SyncEmailsAsync called with accountId={AccountId}, userId={UserId}", accountId, userId);

    var account = await _context.ConnectedEmailAccounts
        .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

    if (account == null)
    {
        // Debug: check if account exists at all (without userId filter)
        var anyAccount = await _context.ConnectedEmailAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (anyAccount != null)
            _logger.LogWarning("Account {AccountId} exists but belongs to userId={OwnerUserId}, not {RequestUserId}", accountId, anyAccount.UserId, userId);
        else
            _logger.LogWarning("Account {AccountId} does not exist in the database at all", accountId);

        throw new InvalidOperationException("Account not found");
    }

    if (!account.IsEmailSyncEnabled)
        throw new InvalidOperationException("Email sync is disabled for this account");

    _logger.LogInformation("Starting email sync for account {Handle} ({Provider})", account.Handle, account.Provider);

    var result = account.Provider switch
    {
        "google" => await SyncGmailEmailsAsync(account, maxResults),
        "microsoft" => await SyncOutlookEmailsAsync(account, maxResults),
        _ => throw new InvalidOperationException($"Unsupported provider: {account.Provider}")
    };

    // Update last synced timestamp
    account.LastSyncedAt = DateTime.UtcNow;
    account.SyncStatus = "active";
    account.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    return result;
}

private async Task<SyncResultDto> SyncGmailEmailsAsync(ConnectedEmailAccount account, int maxResults)
{
    var client = _httpClientFactory.CreateClient();
    var accessToken = await EnsureValidGoogleTokenAsync(account);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    // 1. List message IDs
    var listUrl = $"https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults={maxResults}&labelIds=INBOX";
    var listResponse = await client.GetAsync(listUrl);

    if (!listResponse.IsSuccessStatusCode)
    {
        _logger.LogError("Gmail list messages failed: {Status}", listResponse.StatusCode);
        account.SyncStatus = "failed";
        await _context.SaveChangesAsync();
        throw new InvalidOperationException("Failed to fetch emails from Gmail");
    }

    var listJson = await listResponse.Content.ReadAsStringAsync();
    var listData = JsonSerializer.Deserialize<JsonElement>(listJson);

    int newCount = 0, updatedCount = 0;

    if (listData.TryGetProperty("messages", out var messages))
    {
        foreach (var msg in messages.EnumerateArray())
        {
            var messageId = msg.GetProperty("id").GetString()!;
            var threadId = msg.TryGetProperty("threadId", out var tid) ? tid.GetString() : null;

            // Check if already synced
            var existing = await _context.Set<SyncedEmail>()
                .FirstOrDefaultAsync(e => e.ConnectedEmailAccountId == account.Id && e.ExternalId == messageId);

            if (existing != null)
            {
                updatedCount++;
                continue;
            }

            // 2. Get full message details (include parts to detect attachments)
            var detailUrl = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{messageId}?format=full";
            var detailResponse = await client.GetAsync(detailUrl);

            if (!detailResponse.IsSuccessStatusCode) continue;

            var detailJson = await detailResponse.Content.ReadAsStringAsync();
            var detail = JsonSerializer.Deserialize<JsonElement>(detailJson);

            var headers = detail.GetProperty("payload").GetProperty("headers");
            var subject = "";
            var from = "";
            var to = "";
            var cc = "";
            var dateStr = "";

            foreach (var header in headers.EnumerateArray())
            {
                var name = header.GetProperty("name").GetString();
                var value = header.GetProperty("value").GetString() ?? "";
                switch (name?.ToLower())
                {
                    case "subject": subject = value; break;
                    case "from": from = value; break;
                    case "to": to = value; break;
                    case "cc": cc = value; break;
                    case "date": dateStr = value; break;
                }
            }

            var snippet = detail.TryGetProperty("snippet", out var snip) ? snip.GetString() : "";
            var labelIds = detail.TryGetProperty("labelIds", out var labels)
                ? JsonSerializer.Serialize(labels.EnumerateArray().Select(l => l.GetString()).ToList())
                : "[]";

            // Parse from field: "Name <email>" or just "email"
            var (fromName, fromEmail) = ParseEmailAddress(from);

            // Parse received date — ensure UTC for Npgsql
            DateTime receivedAt;
            if (!DateTime.TryParse(dateStr, out receivedAt))
                receivedAt = DateTime.UtcNow;
            else if (receivedAt.Kind == DateTimeKind.Local)
                receivedAt = receivedAt.ToUniversalTime();
            else if (receivedAt.Kind == DateTimeKind.Unspecified)
                receivedAt = DateTime.SpecifyKind(receivedAt, DateTimeKind.Utc);

            var isRead = !labelIds.Contains("UNREAD");

            // Detect attachments from payload parts
            var hasAttachments = false;
            var attachmentList = new List<SyncedEmailAttachment>();
            if (detail.TryGetProperty("payload", out var payload) && payload.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    var partFileName = part.TryGetProperty("filename", out var fn) ? fn.GetString() : null;
                    if (string.IsNullOrEmpty(partFileName)) continue;

                    hasAttachments = true;
                    var partBody = part.TryGetProperty("body", out var bodyObj) ? bodyObj : default;
                    var attachmentId = partBody.ValueKind != JsonValueKind.Undefined && partBody.TryGetProperty("attachmentId", out var aid)
                        ? aid.GetString() ?? "" : "";
                    var size = partBody.ValueKind != JsonValueKind.Undefined && partBody.TryGetProperty("size", out var sz)
                        ? sz.GetInt64() : 0;
                    var mimeType = part.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "application/octet-stream" : "application/octet-stream";

                    attachmentList.Add(new SyncedEmailAttachment
                    {
                        ExternalAttachmentId = attachmentId,
                        FileName = partFileName,
                        ContentType = mimeType,
                        Size = size,
                    });
                }
            }

            var syncedEmail = new SyncedEmail
            {
                ConnectedEmailAccountId = account.Id,
                ExternalId = messageId,
                ThreadId = threadId,
                Subject = subject,
                Snippet = snippet,
                FromEmail = fromEmail,
                FromName = fromName,
                ToEmails = string.IsNullOrEmpty(to) ? null : JsonSerializer.Serialize(to.Split(',').Select(e => e.Trim()).ToList()),
                CcEmails = string.IsNullOrEmpty(cc) ? null : JsonSerializer.Serialize(cc.Split(',').Select(e => e.Trim()).ToList()),
                BodyPreview = snippet?.Length > 200 ? snippet.Substring(0, 200) : snippet,
                IsRead = isRead,
                HasAttachments = hasAttachments,
                Labels = labelIds,
                ReceivedAt = receivedAt,
            };

            _context.Set<SyncedEmail>().Add(syncedEmail);

            // Add attachment metadata records
            foreach (var att in attachmentList)
            {
                att.SyncedEmailId = syncedEmail.Id;
                _context.Set<SyncedEmailAttachment>().Add(att);
            }

            newCount++;
        }

        await _context.SaveChangesAsync();
    }

    return new SyncResultDto { NewEmails = newCount, UpdatedEmails = updatedCount, SyncedAt = DateTime.UtcNow };
}

private async Task<SyncResultDto> SyncOutlookEmailsAsync(ConnectedEmailAccount account, int maxResults)
{
    var client = _httpClientFactory.CreateClient();
    var accessToken = await EnsureValidMicrosoftTokenAsync(account);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    var url = $"https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages?$top={maxResults}&$orderby=receivedDateTime desc&$select=id,conversationId,subject,bodyPreview,from,toRecipients,ccRecipients,isRead,hasAttachments,receivedDateTime,flag";
    var response = await client.GetAsync(url);

    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError("Outlook fetch messages failed: {Status}", response.StatusCode);
        account.SyncStatus = "failed";
        await _context.SaveChangesAsync();
        throw new InvalidOperationException("Failed to fetch emails from Outlook");
    }

    var json = await response.Content.ReadAsStringAsync();
    var data = JsonSerializer.Deserialize<JsonElement>(json);

    int newCount = 0, updatedCount = 0;

    if (data.TryGetProperty("value", out var messages))
    {
        foreach (var msg in messages.EnumerateArray())
        {
            var messageId = msg.GetProperty("id").GetString()!;
            var threadId = msg.TryGetProperty("conversationId", out var cid) ? cid.GetString() : null;

            var existing = await _context.Set<SyncedEmail>()
                .FirstOrDefaultAsync(e => e.ConnectedEmailAccountId == account.Id && e.ExternalId == messageId);

            if (existing != null) { updatedCount++; continue; }

            var subject = msg.TryGetProperty("subject", out var sub) ? sub.GetString() ?? "" : "";
            var bodyPreview = msg.TryGetProperty("bodyPreview", out var bp) ? bp.GetString() : "";
            var isRead = msg.TryGetProperty("isRead", out var ir) && ir.GetBoolean();
            var hasAttachments = msg.TryGetProperty("hasAttachments", out var ha) && ha.GetBoolean();

            var fromEmail = "";
            var fromName = "";
            if (msg.TryGetProperty("from", out var fromObj) && fromObj.TryGetProperty("emailAddress", out var ea))
            {
                fromEmail = ea.TryGetProperty("address", out var addr) ? addr.GetString() ?? "" : "";
                fromName = ea.TryGetProperty("name", out var fn) ? fn.GetString() : null;
            }

            var toList = new List<string>();
            if (msg.TryGetProperty("toRecipients", out var tos))
            {
                foreach (var r in tos.EnumerateArray())
                {
                    if (r.TryGetProperty("emailAddress", out var tea) && tea.TryGetProperty("address", out var ta))
                        toList.Add(ta.GetString() ?? "");
                }
            }

            var ccList = new List<string>();
            if (msg.TryGetProperty("ccRecipients", out var ccs))
            {
                foreach (var r in ccs.EnumerateArray())
                {
                    if (r.TryGetProperty("emailAddress", out var cea) && cea.TryGetProperty("address", out var ca))
                        ccList.Add(ca.GetString() ?? "");
                }
            }

            var receivedStr = msg.TryGetProperty("receivedDateTime", out var rd) ? rd.GetString() : null;
            DateTime receivedAt = DateTime.TryParse(receivedStr, out var dt) ? EnsureUtc(dt) : DateTime.UtcNow;

            // Fetch attachment metadata from Outlook if hasAttachments
            var outlookAttachments = new List<SyncedEmailAttachment>();
            if (hasAttachments)
            {
                var attUrl = $"https://graph.microsoft.com/v1.0/me/messages/{messageId}/attachments?$select=id,name,contentType,size";
                var attResponse = await client.GetAsync(attUrl);
                if (attResponse.IsSuccessStatusCode)
                {
                    var attJson = await attResponse.Content.ReadAsStringAsync();
                    var attData = JsonSerializer.Deserialize<JsonElement>(attJson);
                    if (attData.TryGetProperty("value", out var attItems))
                    {
                        foreach (var att in attItems.EnumerateArray())
                        {
                            var attId = att.TryGetProperty("id", out var aId) ? aId.GetString() ?? "" : "";
                            var attName = att.TryGetProperty("name", out var aN) ? aN.GetString() ?? "" : "";
                            var attType = att.TryGetProperty("contentType", out var aCt) ? aCt.GetString() ?? "application/octet-stream" : "application/octet-stream";
                            var attSize = att.TryGetProperty("size", out var aS) ? aS.GetInt64() : 0;

                            outlookAttachments.Add(new SyncedEmailAttachment
                            {
                                ExternalAttachmentId = attId,
                                FileName = attName,
                                ContentType = attType,
                                Size = attSize,
                            });
                        }
                    }
                }
            }

            var syncedEmail = new SyncedEmail
            {
                ConnectedEmailAccountId = account.Id,
                ExternalId = messageId,
                ThreadId = threadId,
                Subject = subject,
                Snippet = bodyPreview?.Length > 100 ? bodyPreview.Substring(0, 100) : bodyPreview,
                FromEmail = fromEmail,
                FromName = fromName,
                ToEmails = toList.Count > 0 ? JsonSerializer.Serialize(toList) : null,
                CcEmails = ccList.Count > 0 ? JsonSerializer.Serialize(ccList) : null,
                BodyPreview = bodyPreview?.Length > 200 ? bodyPreview.Substring(0, 200) : bodyPreview,
                IsRead = isRead,
                HasAttachments = hasAttachments,
                ReceivedAt = receivedAt,
            };

            _context.Set<SyncedEmail>().Add(syncedEmail);

            // Add attachment metadata records
            foreach (var att in outlookAttachments)
            {
                att.SyncedEmailId = syncedEmail.Id;
                _context.Set<SyncedEmailAttachment>().Add(att);
            }

            newCount++;
        }

        await _context.SaveChangesAsync();
    }

    return new SyncResultDto { NewEmails = newCount, UpdatedEmails = updatedCount, SyncedAt = DateTime.UtcNow };
}

public async Task<SyncedEmailsPageDto> GetSyncedEmailsAsync(Guid accountId, int userId, int page = 1, int pageSize = 25, string? search = null)
{
    var account = await _context.ConnectedEmailAccounts
        .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

    if (account == null)
        return new SyncedEmailsPageDto();

    var query = _context.Set<SyncedEmail>()
        .Where(e => e.ConnectedEmailAccountId == accountId);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.ToLower();
        query = query.Where(e =>
            e.Subject.ToLower().Contains(term) ||
            e.FromEmail.ToLower().Contains(term) ||
            (e.FromName != null && e.FromName.ToLower().Contains(term)) ||
            (e.Snippet != null && e.Snippet.ToLower().Contains(term))
        );
    }

    var totalCount = await query.CountAsync();

    var emails = await query
        .OrderByDescending(e => e.ReceivedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(e => new SyncedEmailDto
        {
            Id = e.Id,
            ExternalId = e.ExternalId,
            ThreadId = e.ThreadId,
            Subject = e.Subject,
            Snippet = e.Snippet,
            FromEmail = e.FromEmail,
            FromName = e.FromName,
            ToEmails = e.ToEmails,
            CcEmails = e.CcEmails,
            BodyPreview = e.BodyPreview,
            IsRead = e.IsRead,
            IsStarred = e.IsStarred,
            HasAttachments = e.HasAttachments,
            Labels = e.Labels,
            ReceivedAt = e.ReceivedAt,
            Attachments = e.Attachments.Select(a => new SyncedEmailAttachmentDto
            {
                Id = a.Id,
                ExternalAttachmentId = a.ExternalAttachmentId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Size = a.Size,
            }).ToList(),
        })
        .ToListAsync();

    return new SyncedEmailsPageDto
    {
        Emails = emails,
        TotalCount = totalCount,
    };
}

// ═══════════════════════════════════════════════
// CALENDAR SYNC
// ═══════════════════════════════════════════════

public async Task<CalendarSyncResultDto> SyncCalendarAsync(Guid accountId, int userId, int maxResults = 50)
{
    _logger.LogInformation("SyncCalendarAsync called with accountId={AccountId}, userId={UserId}", accountId, userId);

    var account = await _context.ConnectedEmailAccounts
        .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

    if (account == null)
    {
        var anyAccount = await _context.ConnectedEmailAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (anyAccount != null)
            _logger.LogWarning("Calendar sync: Account {AccountId} exists but belongs to userId={OwnerUserId}, not {RequestUserId}", accountId, anyAccount.UserId, userId);
        else
            _logger.LogWarning("Calendar sync: Account {AccountId} does not exist in the database at all", accountId);

        throw new InvalidOperationException("Account not found");
    }

    if (!account.IsCalendarSyncEnabled)
        throw new InvalidOperationException("Calendar sync is disabled for this account");

    _logger.LogInformation("Starting calendar sync for account {Handle} ({Provider})", account.Handle, account.Provider);

    var result = account.Provider switch
    {
        "google" => await SyncGoogleCalendarAsync(account, maxResults),
        "microsoft" => await SyncOutlookCalendarAsync(account, maxResults),
        _ => throw new InvalidOperationException($"Unsupported provider: {account.Provider}")
    };

    account.LastSyncedAt = DateTime.UtcNow;
    account.SyncStatus = "active";
    account.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    return result;
}

private async Task<CalendarSyncResultDto> SyncGoogleCalendarAsync(ConnectedEmailAccount account, int maxResults)
{
    var client = _httpClientFactory.CreateClient();
    var accessToken = await EnsureValidGoogleTokenAsync(account);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    var timeMin = DateTime.UtcNow.AddDays(-7).ToString("o");
    var timeMax = DateTime.UtcNow.AddDays(30).ToString("o");
    var url = $"https://www.googleapis.com/calendar/v3/calendars/primary/events?maxResults={maxResults}&timeMin={timeMin}&timeMax={timeMax}&singleEvents=true&orderBy=startTime";

    var response = await client.GetAsync(url);
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError("Google Calendar sync failed: {Status}", response.StatusCode);
        account.SyncStatus = "failed";
        await _context.SaveChangesAsync();
        throw new InvalidOperationException("Failed to fetch events from Google Calendar");
    }

    var json = await response.Content.ReadAsStringAsync();
    var data = JsonSerializer.Deserialize<JsonElement>(json);

    int newCount = 0, updatedCount = 0;

    if (data.TryGetProperty("items", out var items))
    {
        foreach (var item in items.EnumerateArray())
        {
            var externalId = item.GetProperty("id").GetString()!;

            var existing = await _context.Set<SyncedCalendarEvent>()
                .FirstOrDefaultAsync(e => e.ConnectedEmailAccountId == account.Id && e.ExternalId == externalId);

            var summary = item.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
            var description = item.TryGetProperty("description", out var d) ? d.GetString() : null;
            var location = item.TryGetProperty("location", out var loc) ? loc.GetString() : null;
            var status = item.TryGetProperty("status", out var st) ? st.GetString() ?? "confirmed" : "confirmed";

            // Parse start/end times — ensure UTC for Npgsql
            DateTime startTime = DateTime.UtcNow, endTime = DateTime.UtcNow;
            bool isAllDay = false;
            if (item.TryGetProperty("start", out var startObj))
            {
                if (startObj.TryGetProperty("dateTime", out var sdt))
                {
                    DateTime.TryParse(sdt.GetString(), out startTime);
                    startTime = EnsureUtc(startTime);
                }
                else if (startObj.TryGetProperty("date", out var sd))
                {
                    DateTime.TryParse(sd.GetString(), out startTime);
                    startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
                    isAllDay = true;
                }
            }
            if (item.TryGetProperty("end", out var endObj))
            {
                if (endObj.TryGetProperty("dateTime", out var edt))
                {
                    DateTime.TryParse(edt.GetString(), out endTime);
                    endTime = EnsureUtc(endTime);
                }
                else if (endObj.TryGetProperty("date", out var ed))
                {
                    DateTime.TryParse(ed.GetString(), out endTime);
                    endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
                }
            }

            // Parse attendees
            var attendees = new List<string>();
            if (item.TryGetProperty("attendees", out var atts))
            {
                foreach (var att in atts.EnumerateArray())
                {
                    if (att.TryGetProperty("email", out var email))
                        attendees.Add(email.GetString() ?? "");
                }
            }

            var organizerEmail = "";
            if (item.TryGetProperty("organizer", out var org) && org.TryGetProperty("email", out var oe))
                organizerEmail = oe.GetString() ?? "";

            if (existing != null)
            {
                // Update existing event
                existing.Title = summary;
                existing.Description = description;
                existing.Location = location;
                existing.StartTime = startTime;
                existing.EndTime = endTime;
                existing.IsAllDay = isAllDay;
                existing.Status = status;
                existing.Attendees = attendees.Count > 0 ? JsonSerializer.Serialize(attendees) : null;
                existing.OrganizerEmail = organizerEmail;
                existing.UpdatedAt = DateTime.UtcNow;
                updatedCount++;
            }
            else
            {
                var evt = new SyncedCalendarEvent
                {
                    ConnectedEmailAccountId = account.Id,
                    ExternalId = externalId,
                    Title = summary,
                    Description = description,
                    Location = location,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsAllDay = isAllDay,
                    Status = status,
                    OrganizerEmail = organizerEmail,
                    Attendees = attendees.Count > 0 ? JsonSerializer.Serialize(attendees) : null,
                };
                _context.Set<SyncedCalendarEvent>().Add(evt);
                newCount++;
            }
        }

        await _context.SaveChangesAsync();
    }

    return new CalendarSyncResultDto { NewEvents = newCount, UpdatedEvents = updatedCount, SyncedAt = DateTime.UtcNow };
}

private async Task<CalendarSyncResultDto> SyncOutlookCalendarAsync(ConnectedEmailAccount account, int maxResults)
{
    var client = _httpClientFactory.CreateClient();
    var accessToken = await EnsureValidMicrosoftTokenAsync(account);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    var startDate = DateTime.UtcNow.AddDays(-7).ToString("o");
    var endDate = DateTime.UtcNow.AddDays(30).ToString("o");
    var url = $"https://graph.microsoft.com/v1.0/me/calendarView?startDateTime={startDate}&endDateTime={endDate}&$top={maxResults}&$orderby=start/dateTime&$select=id,subject,bodyPreview,start,end,location,isAllDay,organizer,attendees,showAs";

    var response = await client.GetAsync(url);
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError("Outlook Calendar sync failed: {Status}", response.StatusCode);
        account.SyncStatus = "failed";
        await _context.SaveChangesAsync();
        throw new InvalidOperationException("Failed to fetch events from Outlook Calendar");
    }

    var json = await response.Content.ReadAsStringAsync();
    var data = JsonSerializer.Deserialize<JsonElement>(json);

    int newCount = 0, updatedCount = 0;

    if (data.TryGetProperty("value", out var events))
    {
        foreach (var evt in events.EnumerateArray())
        {
            var externalId = evt.GetProperty("id").GetString()!;

            var existing = await _context.Set<SyncedCalendarEvent>()
                .FirstOrDefaultAsync(e => e.ConnectedEmailAccountId == account.Id && e.ExternalId == externalId);

            var subject = evt.TryGetProperty("subject", out var sub) ? sub.GetString() ?? "" : "";
            var bodyPreview = evt.TryGetProperty("bodyPreview", out var bp) ? bp.GetString() : null;
            var isAllDay = evt.TryGetProperty("isAllDay", out var iad) && iad.GetBoolean();

            DateTime startTime = DateTime.UtcNow, endTime = DateTime.UtcNow;
            if (evt.TryGetProperty("start", out var startObj) && startObj.TryGetProperty("dateTime", out var sdt))
            {
                DateTime.TryParse(sdt.GetString(), out startTime);
                startTime = EnsureUtc(startTime);
            }
            if (evt.TryGetProperty("end", out var endObj) && endObj.TryGetProperty("dateTime", out var edt))
            {
                DateTime.TryParse(edt.GetString(), out endTime);
                endTime = EnsureUtc(endTime);
            }

            var location = "";
            if (evt.TryGetProperty("location", out var locObj) && locObj.TryGetProperty("displayName", out var dn))
                location = dn.GetString();

            var organizerEmail = "";
            if (evt.TryGetProperty("organizer", out var org) && org.TryGetProperty("emailAddress", out var oea) && oea.TryGetProperty("address", out var oa))
                organizerEmail = oa.GetString() ?? "";

            var attendees = new List<string>();
            if (evt.TryGetProperty("attendees", out var atts))
            {
                foreach (var att in atts.EnumerateArray())
                {
                    if (att.TryGetProperty("emailAddress", out var aea) && aea.TryGetProperty("address", out var aa))
                        attendees.Add(aa.GetString() ?? "");
                }
            }

            if (existing != null)
            {
                existing.Title = subject;
                existing.Description = bodyPreview;
                existing.Location = location;
                existing.StartTime = startTime;
                existing.EndTime = endTime;
                existing.IsAllDay = isAllDay;
                existing.OrganizerEmail = organizerEmail;
                existing.Attendees = attendees.Count > 0 ? JsonSerializer.Serialize(attendees) : null;
                existing.UpdatedAt = DateTime.UtcNow;
                updatedCount++;
            }
            else
            {
                var calEvent = new SyncedCalendarEvent
                {
                    ConnectedEmailAccountId = account.Id,
                    ExternalId = externalId,
                    Title = subject,
                    Description = bodyPreview,
                    Location = location,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsAllDay = isAllDay,
                    Status = "confirmed",
                    OrganizerEmail = organizerEmail,
                    Attendees = attendees.Count > 0 ? JsonSerializer.Serialize(attendees) : null,
                };
                _context.Set<SyncedCalendarEvent>().Add(calEvent);
                newCount++;
            }
        }

        await _context.SaveChangesAsync();
    }

    return new CalendarSyncResultDto { NewEvents = newCount, UpdatedEvents = updatedCount, SyncedAt = DateTime.UtcNow };
}

public async Task<SyncedCalendarEventsPageDto> GetCalendarEventsAsync(Guid accountId, int userId, int page = 1, int pageSize = 25, string? search = null)
{
    var account = await _context.ConnectedEmailAccounts
        .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

    if (account == null)
        return new SyncedCalendarEventsPageDto();

    var query = _context.Set<SyncedCalendarEvent>()
        .Where(e => e.ConnectedEmailAccountId == accountId);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.ToLower();
        query = query.Where(e =>
            e.Title.ToLower().Contains(term) ||
            (e.Description != null && e.Description.ToLower().Contains(term)) ||
            (e.Location != null && e.Location.ToLower().Contains(term)) ||
            (e.OrganizerEmail != null && e.OrganizerEmail.ToLower().Contains(term))
        );
    }

    var totalCount = await query.CountAsync();

    var events = await query
        .OrderBy(e => e.StartTime)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(e => new SyncedCalendarEventDto
        {
            Id = e.Id,
            ExternalId = e.ExternalId,
            Title = e.Title,
            Description = e.Description,
            Location = e.Location,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            IsAllDay = e.IsAllDay,
            Status = e.Status,
            OrganizerEmail = e.OrganizerEmail,
            Attendees = e.Attendees,
        })
        .ToListAsync();

    return new SyncedCalendarEventsPageDto
    {
        Events = events,
        TotalCount = totalCount,
    };
}

// ────────────────────────────────────────────────
// Token refresh helpers
// ────────────────────────────────────────────────

private async Task<string> EnsureValidGoogleTokenAsync(ConnectedEmailAccount account)
{
    // Try the existing token first — if it fails, refresh
    var client = _httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", account.AccessToken);

    var testResponse = await client.GetAsync("https://www.googleapis.com/oauth2/v1/tokeninfo?access_token=" + account.AccessToken);

    if (testResponse.IsSuccessStatusCode)
        return account.AccessToken;

    // Token expired — refresh it
    _logger.LogInformation("Refreshing Google token for {Handle}", account.Handle);

    var refreshClient = _httpClientFactory.CreateClient();
    var body = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = _configuration["OAuth:Google:ClientId"] ?? "",
        ["client_secret"] = _configuration["OAuth:Google:ClientSecret"] ?? "",
        ["refresh_token"] = account.RefreshToken,
        ["grant_type"] = "refresh_token"
    });

    var response = await refreshClient.PostAsync("https://oauth2.googleapis.com/token", body);
    if (!response.IsSuccessStatusCode)
    {
        account.AuthFailedAt = DateTime.UtcNow;
        account.SyncStatus = "failed";
        await _context.SaveChangesAsync();
        throw new InvalidOperationException("Failed to refresh Google access token");
    }

    var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    var newToken = json?.GetValueOrDefault("access_token")?.ToString() ?? "";

    account.AccessToken = newToken;
    account.AuthFailedAt = null;
    account.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    return newToken;
}

private async Task<string> EnsureValidMicrosoftTokenAsync(ConnectedEmailAccount account)
{
    var client = _httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", account.AccessToken);

    var testResponse = await client.GetAsync("https://graph.microsoft.com/v1.0/me");

    if (testResponse.IsSuccessStatusCode)
        return account.AccessToken;

    // Token expired — refresh it
    _logger.LogInformation("Refreshing Microsoft token for {Handle}", account.Handle);

    var refreshClient = _httpClientFactory.CreateClient();
    var body = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = _configuration["OAuth:Microsoft:ClientId"] ?? "",
        ["client_secret"] = _configuration["OAuth:Microsoft:ClientSecret"] ?? "",
        ["refresh_token"] = account.RefreshToken,
        ["grant_type"] = "refresh_token",
        ["scope"] = "openid email profile offline_access Mail.ReadWrite Mail.Send Calendars.Read Calendars.ReadWrite User.Read"
    });

    var response = await refreshClient.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", body);
    if (!response.IsSuccessStatusCode)
    {
        account.AuthFailedAt = DateTime.UtcNow;
        account.SyncStatus = "failed";
        await _context.SaveChangesAsync();
        throw new InvalidOperationException("Failed to refresh Microsoft access token");
    }

    var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    var newToken = json?.GetValueOrDefault("access_token")?.ToString() ?? "";
    var newRefresh = json?.GetValueOrDefault("refresh_token")?.ToString();

    account.AccessToken = newToken;
    if (!string.IsNullOrEmpty(newRefresh)) account.RefreshToken = newRefresh;
    account.AuthFailedAt = null;
    account.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    return newToken;
}

// ────────────────────────────────────────────────
// Helper: parse "Display Name <email@example.com>" format
// ────────────────────────────────────────────────

private static (string? name, string email) ParseEmailAddress(string raw)
{
    if (string.IsNullOrEmpty(raw)) return (null, "");

    var match = System.Text.RegularExpressions.Regex.Match(raw, @"^(.+?)\s*<(.+?)>$");
    if (match.Success)
    {
        return (match.Groups[1].Value.Trim().Trim('"'), match.Groups[2].Value.Trim());
    }

    return (null, raw.Trim());
    }

// Helper: ensure DateTime is UTC for Npgsql compatibility
private static DateTime EnsureUtc(DateTime dt)
{
    return dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
    };
}
}
}
