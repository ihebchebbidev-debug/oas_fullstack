using System.Text;
using System.Text.Json;
using System.IO;
using Microsoft.EntityFrameworkCore;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService
    {

public async Task<SendEmailResultDto> SendEmailAsync(Guid accountId, int userId, SendEmailDto dto, long? existingLogId = null)
{
    // Load-or-create the outbound log row FIRST so every send attempt is recorded,
    // even the ones that never reach a provider (account missing, unsupported provider,
    // token refresh crash, etc.). The retry handler passes existingLogId to reuse a row.
    Models.OutboundEmailLog? log = null;
    if (existingLogId.HasValue)
    {
        log = await _context.OutboundEmailLogs.FirstOrDefaultAsync(l => l.Id == existingLogId.Value);
    }
    if (log == null)
    {
        log = new Models.OutboundEmailLog
        {
            AccountId = accountId,
            UserId = userId,
            ToSummary = dto.To != null ? string.Join(", ", dto.To.Take(10)) : null,
            Subject = dto.Subject,
            PayloadJson = JsonSerializer.Serialize(dto),
            Status = "pending",
            MaxAttempts = 5,
        };
        _context.OutboundEmailLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    var account = await _context.ConnectedEmailAccounts
        .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

    log.Attempts += 1;
    log.LastAttemptAt = DateTime.UtcNow;

    if (account == null)
    {
        log.Status = log.Attempts >= log.MaxAttempts ? "gave_up" : "failed";
        log.LastError = "Account not found";
        log.NextRetryAt = log.Status == "failed" ? ComputeNextRetry(log.Attempts) : null;
        await _context.SaveChangesAsync();
        return new SendEmailResultDto { Success = false, Error = "Account not found" };
    }

    log.Provider = account.Provider ?? "";
    log.FromHandle = account.Handle;
    if (log.TenantId == 0) log.TenantId = account.TenantId;

    SendEmailResultDto result;
    try
    {
        result = account.Provider switch
        {
            "google" => await SendGmailEmailAsync(account, dto),
            "microsoft" => await SendOutlookEmailAsync(account, dto),
            "custom" => await SendCustomSmtpEmailAsync(account, dto),
            _ => new SendEmailResultDto { Success = false, Error = $"Unsupported provider: {account.Provider}" }
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send email via {Provider} for account {Handle}", account.Provider, account.Handle);
        result = new SendEmailResultDto { Success = false, Error = ex.Message };
    }

    if (result.Success)
    {
        log.Status = "success";
        log.MessageId = result.MessageId;
        log.SentAt = DateTime.UtcNow;
        log.NextRetryAt = null;
        log.LastError = null;
    }
    else
    {
        log.LastError = result.Error ?? "Unknown send failure";
        if (log.Attempts >= log.MaxAttempts)
        {
            log.Status = "gave_up";
            log.NextRetryAt = null;
        }
        else
        {
            log.Status = "failed";
            log.NextRetryAt = ComputeNextRetry(log.Attempts);
        }
    }

    await _context.SaveChangesAsync();
    return result;
}

/// <summary>Exponential backoff: 1, 2, 4, 8, 16 minutes (capped).</summary>
private static DateTime ComputeNextRetry(int attempt)
{
    var minutes = Math.Min(60, Math.Pow(2, Math.Max(0, attempt - 1)));
    return DateTime.UtcNow.AddMinutes(minutes);
}

private async Task<SendEmailResultDto> SendGmailEmailAsync(ConnectedEmailAccount account, SendEmailDto dto)
{
    var client = _httpClientFactory.CreateClient();
    var accessToken = await EnsureValidGoogleTokenAsync(account);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    var hasAttachments = dto.Attachments != null && dto.Attachments.Count > 0;
    string rawMessage;

    if (hasAttachments)
    {
        // Build multipart MIME with attachments
        var boundary = $"boundary_{Guid.NewGuid():N}";
        var mime = new StringBuilder();
        mime.AppendLine($"From: {account.Handle}");
        mime.AppendLine($"To: {string.Join(", ", dto.To)}");
        if (dto.Cc.Count > 0) mime.AppendLine($"Cc: {string.Join(", ", dto.Cc)}");
        if (dto.Bcc.Count > 0) mime.AppendLine($"Bcc: {string.Join(", ", dto.Bcc)}");
        mime.AppendLine($"Subject: {dto.Subject}");
        mime.AppendLine("MIME-Version: 1.0");
        mime.AppendLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
        mime.AppendLine();

        // Body part
        mime.AppendLine($"--{boundary}");
        if (!string.IsNullOrEmpty(dto.BodyHtml))
        {
            mime.AppendLine("Content-Type: text/html; charset=UTF-8");
            mime.AppendLine();
            mime.AppendLine(dto.BodyHtml);
        }
        else
        {
            mime.AppendLine("Content-Type: text/plain; charset=UTF-8");
            mime.AppendLine();
            mime.AppendLine(dto.Body);
        }

        // Attachment parts
        foreach (var att in dto.Attachments!)
        {
            mime.AppendLine($"--{boundary}");
            mime.AppendLine($"Content-Type: {att.ContentType}; name=\"{att.FileName}\"");
            mime.AppendLine("Content-Transfer-Encoding: base64");
            mime.AppendLine($"Content-Disposition: attachment; filename=\"{att.FileName}\"");
            mime.AppendLine();
            // Insert base64 content in 76-char lines
            var b64 = att.ContentBase64;
            for (int i = 0; i < b64.Length; i += 76)
            {
                mime.AppendLine(b64.Substring(i, Math.Min(76, b64.Length - i)));
            }
        }
        mime.AppendLine($"--{boundary}--");

        rawMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime.ToString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
    else
    {
        // Simple plain-text email (original logic)
        var mime = new StringBuilder();
        mime.AppendLine($"From: {account.Handle}");
        mime.AppendLine($"To: {string.Join(", ", dto.To)}");
        if (dto.Cc.Count > 0) mime.AppendLine($"Cc: {string.Join(", ", dto.Cc)}");
        if (dto.Bcc.Count > 0) mime.AppendLine($"Bcc: {string.Join(", ", dto.Bcc)}");
        mime.AppendLine($"Subject: {dto.Subject}");
        if (!string.IsNullOrEmpty(dto.BodyHtml))
        {
            mime.AppendLine("Content-Type: text/html; charset=UTF-8");
            mime.AppendLine();
            mime.AppendLine(dto.BodyHtml);
        }
        else
        {
            mime.AppendLine("Content-Type: text/plain; charset=UTF-8");
            mime.AppendLine();
            mime.AppendLine(dto.Body);
        }

        rawMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime.ToString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    var jsonBody = JsonSerializer.Serialize(new { raw = rawMessage });
    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

    var response = await client.PostAsync("https://gmail.googleapis.com/gmail/v1/users/me/messages/send", content);

    if (!response.IsSuccessStatusCode)
    {
        var errorBody = await response.Content.ReadAsStringAsync();
        _logger.LogError("Gmail send failed: {Status} - {Body}", response.StatusCode, errorBody);
        return new SendEmailResultDto { Success = false, Error = $"Gmail send failed: {response.StatusCode}" };
    }

    var responseJson = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);
    var messageId = responseData.TryGetProperty("id", out var id) ? id.GetString() : null;

    return new SendEmailResultDto { Success = true, MessageId = messageId };
    }

    private async Task<SendEmailResultDto> SendOutlookEmailAsync(ConnectedEmailAccount account, SendEmailDto dto)
    {
        var client = _httpClientFactory.CreateClient();
        var accessToken = await EnsureValidMicrosoftTokenAsync(account);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var toRecipients = dto.To.Select(e => new { emailAddress = new { address = e } }).ToList();
        var ccRecipients = dto.Cc.Select(e => new { emailAddress = new { address = e } }).ToList();
        var bccRecipients = dto.Bcc.Select(e => new { emailAddress = new { address = e } }).ToList();

        var bodyContentType = !string.IsNullOrEmpty(dto.BodyHtml) ? "HTML" : "Text";
        var bodyContent = !string.IsNullOrEmpty(dto.BodyHtml) ? dto.BodyHtml : dto.Body;

        var attachments = dto.Attachments?.Select(a => new
        {
            @odata_type = "#microsoft.graph.fileAttachment",
            name = a.FileName,
            contentType = a.ContentType,
            contentBytes = a.ContentBase64
        }).ToList();

        object emailPayload;
        if (attachments != null && attachments.Count > 0)
        {
            emailPayload = new
            {
                message = new
                {
                    subject = dto.Subject,
                    body = new
                    {
                        contentType = bodyContentType,
                        content = bodyContent
                    },
                    toRecipients,
                    ccRecipients,
                    bccRecipients,
                    attachments = attachments.Select(a => new Dictionary<string, object>
                    {
                        { "@odata.type", "#microsoft.graph.fileAttachment" },
                        { "name", a.name },
                        { "contentType", a.contentType },
                        { "contentBytes", a.contentBytes }
                    }).ToList()
                },
                saveToSentItems = true
            };
        }
        else
        {
            emailPayload = new
            {
                message = new
                {
                    subject = dto.Subject,
                    body = new
                    {
                        contentType = bodyContentType,
                        content = bodyContent
                    },
                    toRecipients,
                    ccRecipients,
                    bccRecipients
                },
                saveToSentItems = true
            };
        }

        var jsonBody = JsonSerializer.Serialize(emailPayload);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://graph.microsoft.com/v1.0/me/sendMail", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Outlook send failed: {Status} - {Body}", response.StatusCode, errorBody);
            return new SendEmailResultDto { Success = false, Error = $"Outlook send failed: {response.StatusCode}" };
        }

        return new SendEmailResultDto { Success = true };
    }

    private async Task<SendEmailResultDto> SendCustomSmtpEmailAsync(ConnectedEmailAccount account, SendEmailDto dto)
    {
        // Find persisted custom configuration
        var custom = await _context.CustomEmailAccounts
            .FirstOrDefaultAsync(c => c.Email == account.Handle && c.UserId == account.UserId);

        if (custom == null)
            return new SendEmailResultDto { Success = false, Error = "Custom SMTP configuration not found" };

        if (string.IsNullOrWhiteSpace(custom.Email))
            return new SendEmailResultDto { Success = false, Error = "Custom SMTP configuration missing email address" };

        string? password = null;
        try
        {
            if (!string.IsNullOrEmpty(custom.EncryptedPassword))
            {
                var protectedBytes = Convert.FromBase64String(custom.EncryptedPassword);
                var un = _protector.Unprotect(protectedBytes);
                password = System.Text.Encoding.UTF8.GetString(un);
            }
        }
        catch
        {
            password = custom.EncryptedPassword;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(custom.DisplayName ?? custom.Email, custom.Email));
        if (dto.To != null)
        {
            foreach (var to in dto.To.Where(s => !string.IsNullOrWhiteSpace(s)))
                message.To.Add(MailboxAddress.Parse(to));
        }

        if (dto.Cc != null)
        {
            foreach (var cc in dto.Cc.Where(s => !string.IsNullOrWhiteSpace(s)))
                message.Cc.Add(MailboxAddress.Parse(cc));
        }

        if (dto.Bcc != null)
        {
            foreach (var bcc in dto.Bcc.Where(s => !string.IsNullOrWhiteSpace(s)))
                message.Bcc.Add(MailboxAddress.Parse(bcc));
        }

        message.Subject = dto.Subject ?? "";

        var builder = new BodyBuilder();
        if (!string.IsNullOrEmpty(dto.BodyHtml)) builder.HtmlBody = dto.BodyHtml;
        else builder.TextBody = dto.Body;

        if (dto.Attachments != null)
        {
            foreach (var att in dto.Attachments)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(att?.ContentBase64)) continue;
                    var bytes = Convert.FromBase64String(att.ContentBase64);
                    var parts = (att.ContentType ?? "application/octet-stream").Split('/');
                    var mimeType = parts.Length > 0 ? parts[0] : "application";
                    var mimeSub = parts.Length > 1 ? parts[1] : "octet-stream";
                    var ct = new MimeKit.ContentType(mimeType, mimeSub);
                    var fileName = !string.IsNullOrWhiteSpace(att.FileName) ? att.FileName : "attachment";
                    builder.Attachments.Add(fileName, new MemoryStream(bytes), ct);
                }
                catch { /* ignore malformed attachment */ }
            }
        }

        message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

        var secure = custom.SmtpSecurity?.ToLower() == "ssl" ? SecureSocketOptions.SslOnConnect
            : custom.SmtpSecurity?.ToLower() == "tls" ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        await smtp.ConnectAsync(custom.SmtpServer ?? string.Empty, custom.SmtpPort ?? 25, secure);
        if (!string.IsNullOrEmpty(custom.Email) && !string.IsNullOrEmpty(password))
            await smtp.AuthenticateAsync(custom.Email, password);

        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);

        return new SendEmailResultDto { Success = true };
    }
}
}
