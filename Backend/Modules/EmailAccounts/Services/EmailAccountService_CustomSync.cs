using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService
    {
        public async Task<SyncResultDto> SyncCustomAccountAsync(Guid customAccountId, int userId, int maxResults = 50)
        {
            var custom = await _context.Set<CustomEmailAccount>().FirstOrDefaultAsync(c => c.Id == customAccountId && c.UserId == userId);
            if (custom == null) throw new InvalidOperationException("Custom account not found");
            if (!custom.IsActive) throw new InvalidOperationException("Custom account is disabled");

            // Find connected account to associate synced emails
            var connected = await _context.ConnectedEmailAccounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Handle == custom.Email && a.Provider == "custom");
            if (connected == null) throw new InvalidOperationException("Connected account record for custom account not found");

            // Decrypt password (stored as base64 of protected bytes)
            if (string.IsNullOrEmpty(custom.EncryptedPassword)) throw new InvalidOperationException("No password available for custom account");
            string password;
            try
            {
                var protectedBytes = Convert.FromBase64String(custom.EncryptedPassword);
                var un = _protector.Unprotect(protectedBytes);
                password = System.Text.Encoding.UTF8.GetString(un);
            }
            catch
            {
                password = custom.EncryptedPassword;
            }

            int newCount = 0, updatedCount = 0;

            // Prefer IMAP
            if (!string.IsNullOrEmpty(custom.ImapServer) && custom.ImapPort.HasValue)
            {
                using var imap = new ImapClient();
                imap.ServerCertificateValidationCallback = (s, c, h, e) => true;
                var secure = string.Equals(custom.ImapSecurity, "ssl", StringComparison.OrdinalIgnoreCase) ? MailKit.Security.SecureSocketOptions.SslOnConnect
                    : string.Equals(custom.ImapSecurity, "tls", StringComparison.OrdinalIgnoreCase) ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None;
                imap.Connect(custom.ImapServer, custom.ImapPort.Value, secure);
                imap.Authenticate(custom.Email, password);

                var inbox = imap.Inbox;
                if (inbox == null) return new SyncResultDto { NewEmails = 0, UpdatedEmails = 0, SyncedAt = DateTime.UtcNow };
                inbox.Open(MailKit.FolderAccess.ReadOnly);
                var uids = inbox.Search(SearchQuery.All);
                var take = uids.Count > maxResults ? uids.TakeLast(maxResults) : uids;

                foreach (var uid in take)
                {
                    var existing = await _context.Set<SyncedEmail>().FirstOrDefaultAsync(e => e.ConnectedEmailAccountId == connected.Id && e.ExternalId == uid.Id.ToString());
                    if (existing != null) { updatedCount++; continue; }

                    var msg = inbox.GetMessage(uid);
                    var from = msg.From.Mailboxes.FirstOrDefault();
                    var toList = msg.To.Mailboxes.Select(m => m.Address).ToList();
                    var ccList = msg.Cc.Mailboxes.Select(m => m.Address).ToList();

                    var synced = new SyncedEmail
                    {
                        ConnectedEmailAccountId = connected.Id,
                        ExternalId = uid.Id.ToString(),
                        ThreadId = msg.MessageId,
                        Subject = msg.Subject ?? string.Empty,
                        Snippet = (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Substring(0, Math.Min(200, (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Length)),
                        FromEmail = from?.Address ?? string.Empty,
                        FromName = from?.Name,
                        ToEmails = toList.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(toList) : null,
                        CcEmails = ccList.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(ccList) : null,
                        BodyPreview = (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Length > 200 ? (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Substring(0, 200) : (msg.TextBody ?? msg.HtmlBody ?? string.Empty),
                        IsRead = false,
                        HasAttachments = msg.Attachments.Any(),
                        Labels = null,
                        ReceivedAt = msg.Date.UtcDateTime,
                    };

                    _context.Set<SyncedEmail>().Add(synced);

                    // Attachments metadata
                    foreach (var att in msg.Attachments)
                    {
                        var part = (MimePart)att;
                        var attach = new SyncedEmailAttachment
                        {
                            SyncedEmailId = synced.Id,
                            ExternalAttachmentId = Guid.NewGuid().ToString(),
                            FileName = part.FileName ?? "attachment",
                            ContentType = part.ContentType.MimeType,
                            Size = part.ContentDisposition?.Size ?? 0,
                        };
                        _context.Set<SyncedEmailAttachment>().Add(attach);
                    }

                    newCount++;
                }

                await _context.SaveChangesAsync();
                imap.Disconnect(true);
            }
            else if (!string.IsNullOrEmpty(custom.Pop3Server) && custom.Pop3Port.HasValue)
            {
                using var pop = new Pop3Client();
                pop.ServerCertificateValidationCallback = (s, c, h, e) => true;
                var secure = string.Equals(custom.Pop3Security, "ssl", StringComparison.OrdinalIgnoreCase) ? MailKit.Security.SecureSocketOptions.SslOnConnect
                    : string.Equals(custom.Pop3Security, "tls", StringComparison.OrdinalIgnoreCase) ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None;
                pop.Connect(custom.Pop3Server, custom.Pop3Port.Value, secure);
                pop.Authenticate(custom.Email, password);

                var count = pop.Count;
                var start = Math.Max(0, count - maxResults);
                for (int i = start; i < count; i++)
                {
                    var msg = pop.GetMessage(i);
                    var externalId = i.ToString();
                    var existing = await _context.Set<SyncedEmail>().FirstOrDefaultAsync(e => e.ConnectedEmailAccountId == connected.Id && e.ExternalId == externalId);
                    if (existing != null) { updatedCount++; continue; }

                    var from = msg.From.Mailboxes.FirstOrDefault();
                    var toList = msg.To.Mailboxes.Select(m => m.Address).ToList();
                    var ccList = msg.Cc.Mailboxes.Select(m => m.Address).ToList();

                    var synced = new SyncedEmail
                    {
                        ConnectedEmailAccountId = connected.Id,
                        ExternalId = externalId,
                        ThreadId = msg.MessageId,
                        Subject = msg.Subject ?? string.Empty,
                        Snippet = (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Substring(0, Math.Min(200, (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Length)),
                        FromEmail = from?.Address ?? string.Empty,
                        FromName = from?.Name,
                        ToEmails = toList.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(toList) : null,
                        CcEmails = ccList.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(ccList) : null,
                        BodyPreview = (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Length > 200 ? (msg.TextBody ?? msg.HtmlBody ?? string.Empty).Substring(0, 200) : (msg.TextBody ?? msg.HtmlBody ?? string.Empty),
                        IsRead = false,
                        HasAttachments = msg.Attachments.Any(),
                        Labels = null,
                        ReceivedAt = msg.Date.UtcDateTime,
                    };

                    _context.Set<SyncedEmail>().Add(synced);

                    foreach (var att in msg.Attachments)
                    {
                        var part = (MimePart)att;
                        var attach = new SyncedEmailAttachment
                        {
                            SyncedEmailId = synced.Id,
                            ExternalAttachmentId = Guid.NewGuid().ToString(),
                            FileName = part.FileName ?? "attachment",
                            ContentType = part.ContentType.MimeType,
                            Size = part.ContentDisposition?.Size ?? 0,
                        };
                        _context.Set<SyncedEmailAttachment>().Add(attach);
                    }

                    newCount++;
                }

                await _context.SaveChangesAsync();
                pop.Disconnect(true);
            }
            else
            {
                throw new InvalidOperationException("No IMAP or POP3 configuration available for this account");
            }

            // Update connected account metadata
            connected.LastSyncedAt = DateTime.UtcNow;
            connected.SyncStatus = "active";
            connected.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new SyncResultDto { NewEmails = newCount, UpdatedEmails = updatedCount, SyncedAt = DateTime.UtcNow };
        }
    }
}
