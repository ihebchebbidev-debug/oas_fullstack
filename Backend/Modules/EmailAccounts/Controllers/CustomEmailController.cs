using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MyApi.Modules.EmailAccounts.DTOs;

namespace MyApi.Modules.EmailAccounts.Controllers
{
    [ApiController]
    [Route("api/email-accounts/custom")]
    [Authorize]
    public class CustomEmailController : ControllerBase
    {
        private readonly ILogger<CustomEmailController> _logger;
        private readonly Services.IEmailAccountService _emailAccountService;

        public CustomEmailController(ILogger<CustomEmailController> logger, Services.IEmailAccountService emailAccountService)
        {
            _logger = logger;
            _emailAccountService = emailAccountService;
        }

        [HttpPost("test")]
        public ActionResult TestConnection([FromBody] CustomEmailConfigDto cfg)
        {
            try
            {
                // Test SMTP
                if (!string.IsNullOrEmpty(cfg.SmtpServer) && cfg.SmtpPort.HasValue)
                {
                    using var smtp = new SmtpClient();
                    smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    var secure = cfg.SmtpSecurity?.ToLower() == "ssl" ? SecureSocketOptions.SslOnConnect
                        : cfg.SmtpSecurity?.ToLower() == "tls" ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                    smtp.Connect(cfg.SmtpServer, cfg.SmtpPort.Value, secure);
                    if (!string.IsNullOrEmpty(cfg.Email) && !string.IsNullOrEmpty(cfg.Password))
                        smtp.Authenticate(cfg.Email, cfg.Password);
                    smtp.Disconnect(true);
                }

                // Test IMAP
                if (!string.IsNullOrEmpty(cfg.ImapServer) && cfg.ImapPort.HasValue)
                {
                    using var imap = new ImapClient();
                    imap.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    var secure = cfg.ImapSecurity?.ToLower() == "ssl" ? SecureSocketOptions.SslOnConnect
                        : cfg.ImapSecurity?.ToLower() == "tls" ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                    imap.Connect(cfg.ImapServer, cfg.ImapPort.Value, secure);
                    if (!string.IsNullOrEmpty(cfg.Email) && !string.IsNullOrEmpty(cfg.Password))
                        imap.Authenticate(cfg.Email, cfg.Password);
                    imap.Disconnect(true);
                }

                // Test POP3
                if (!string.IsNullOrEmpty(cfg.Pop3Server) && cfg.Pop3Port.HasValue)
                {
                    using var pop = new Pop3Client();
                    pop.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    var secure = cfg.Pop3Security?.ToLower() == "ssl" ? SecureSocketOptions.SslOnConnect
                        : cfg.Pop3Security?.ToLower() == "tls" ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                    pop.Connect(cfg.Pop3Server, cfg.Pop3Port.Value, secure);
                    if (!string.IsNullOrEmpty(cfg.Email) && !string.IsNullOrEmpty(cfg.Password))
                        pop.Authenticate(cfg.Email, cfg.Password);
                    pop.Disconnect(true);
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Custom email test connection failed");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("send")]
        public ActionResult Send([FromBody] SendCustomEmailDto dto)
        {
            try
            {
                var cfg = dto.Config;
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(cfg.DisplayName ?? cfg.Email, cfg.Email));
                foreach (var to in dto.To) message.To.Add(MailboxAddress.Parse(to));
                if (dto.Cc != null) foreach (var cc in dto.Cc) message.Cc.Add(MailboxAddress.Parse(cc));
                if (dto.Bcc != null) foreach (var bcc in dto.Bcc) message.Bcc.Add(MailboxAddress.Parse(bcc));
                message.Subject = dto.Subject;
                if (!string.IsNullOrEmpty(dto.BodyHtml))
                    message.Body = new TextPart("html") { Text = dto.BodyHtml };
                else
                    message.Body = new TextPart("plain") { Text = dto.Body };

                using var smtp = new SmtpClient();
                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                var secure = cfg.SmtpSecurity?.ToLower() == "ssl" ? SecureSocketOptions.SslOnConnect
                    : cfg.SmtpSecurity?.ToLower() == "tls" ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                smtp.Connect(cfg.SmtpServer ?? string.Empty, cfg.SmtpPort ?? 25, secure);
                if (!string.IsNullOrEmpty(cfg.Email) && !string.IsNullOrEmpty(cfg.Password))
                    smtp.Authenticate(cfg.Email, cfg.Password);
                smtp.Send(message);
                smtp.Disconnect(true);

                return Ok(new { success = true, messageId = Guid.NewGuid().ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send custom email");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("fetch")]
        public ActionResult Fetch([FromBody] FetchCustomEmailsDto dto)
        {
            try
            {
                var cfg = dto.Config;
                // Prefer IMAP if configured
                if (!string.IsNullOrEmpty(cfg.ImapServer) && cfg.ImapPort.HasValue)
                {
                    using var imap = new ImapClient();
                    imap.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    var secure = cfg.ImapSecurity?.ToLower() == "ssl" ? SecureSocketOptions.SslOnConnect
                        : cfg.ImapSecurity?.ToLower() == "tls" ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                    imap.Connect(cfg.ImapServer, cfg.ImapPort.Value, secure);
                    if (!string.IsNullOrEmpty(cfg.Email) && !string.IsNullOrEmpty(cfg.Password))
                        imap.Authenticate(cfg.Email, cfg.Password);

                    var inbox = imap.GetFolder(dto.Folder ?? "INBOX");
                    inbox.Open(MailKit.FolderAccess.ReadOnly);
                    var uids = inbox.Search(MailKit.Search.SearchQuery.All);
                    var list = new List<object>();
                    var take = uids.Count > dto.Limit ? uids.TakeLast(dto.Limit) : uids;
                    foreach (var uid in take)
                    {
                        var msg = inbox.GetMessage(uid);
                        list.Add(new { id = uid.Id, subject = msg.Subject, from = msg.From.ToString(), date = msg.Date.DateTime, body = msg.HtmlBody ?? msg.TextBody });
                    }
                    imap.Disconnect(true);
                    return Ok(new { success = true, emails = list });
                }

                // Fallback to POP3
                if (!string.IsNullOrEmpty(cfg.Pop3Server) && cfg.Pop3Port.HasValue)
                {
                    using var pop = new Pop3Client();
                    pop.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    var secure = cfg.Pop3Security?.ToLower() == "ssl" ? SecureSocketOptions.SslOnConnect
                        : cfg.Pop3Security?.ToLower() == "tls" ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                    pop.Connect(cfg.Pop3Server, cfg.Pop3Port.Value, secure);
                    if (!string.IsNullOrEmpty(cfg.Email) && !string.IsNullOrEmpty(cfg.Password))
                        pop.Authenticate(cfg.Email, cfg.Password);

                    var count = pop.Count;
                    var list = new List<object>();
                    for (int i = Math.Max(0, count - dto.Limit); i < count; i++)
                    {
                        var msg = pop.GetMessage(i);
                        list.Add(new { id = i, subject = msg.Subject, from = msg.From.ToString(), date = msg.Date.DateTime, body = msg.HtmlBody ?? msg.TextBody });
                    }
                    pop.Disconnect(true);
                    return Ok(new { success = true, emails = list });
                }

                return BadRequest(new { success = false, error = "No IMAP or POP3 configuration provided" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch custom emails");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // ────────────────────────────────
        // Persisted custom account CRUD
        // ────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> List()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "0");
                var list = await _emailAccountService.GetCustomAccountsByUserAsync(userId);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list custom email accounts for user");
                // Return error details to help debug 500 during development
                return StatusCode(500, new { success = false, error = ex.Message, trace = ex.StackTrace });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "0");
            var item = await _emailAccountService.GetCustomAccountByIdAsync(id, userId);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] CreateCustomEmailAccountDto dto)
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "0");
            var created = await _emailAccountService.CreateCustomAccountAsync(userId, dto);
            return Ok(created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCustomEmailAccountDto dto)
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "0");
            var updated = await _emailAccountService.UpdateCustomAccountAsync(id, userId, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "0");
            var ok = await _emailAccountService.DeleteCustomAccountAsync(id, userId);
            if (!ok) return NotFound();
            return Ok(new { success = true });
        }

        [HttpPost("{id}/sync")]
        public async Task<IActionResult> Sync(Guid id, [FromQuery] int maxResults = 50)
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "0");
            try
            {
                var result = await _emailAccountService.SyncCustomAccountAsync(id, userId, maxResults);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Custom account sync failed for {Id}", id);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during custom account sync for {Id}", id);
                return StatusCode(500, new { message = "Failed to sync custom account" });
            }
        }
    }
}
