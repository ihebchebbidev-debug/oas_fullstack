using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;
using MyApi.Modules.ModuleRequests.DTOs;

namespace MyApi.Modules.ModuleRequests.Services
{
    public interface IModuleRequestEmailService
    {
        /// <summary>Sends a module activation/deactivation request to the Flowentra sales inbox.</summary>
        Task<bool> SendModuleRequestAsync(ModuleRequestDto request);

        string RecipientAddress { get; }
    }

    /// <summary>
    /// Sends module purchase / deactivation requests through the same OVH SMTP
    /// mailbox used by the platform's transactional emails (password reset, OTP).
    /// Destination inbox: contact@flowentra.io
    /// </summary>
    public class ModuleRequestEmailService : IModuleRequestEmailService
    {
        private readonly ILogger<ModuleRequestEmailService> _logger;

        // OVH SMTP — identical configuration to ForgotEmailService
        private const string SMTP_HOST = "ssl0.ovh.net";
        private const int SMTP_PORT = 465;
        private const string SMTP_USERNAME = "support@flowentra.app";
        private const string SMTP_PASSWORD = "Zaleyo2026";
        private const bool USE_SSL = true;

        private const string RECIPIENT = "contact@flowentra.io";

        public string RecipientAddress => RECIPIENT;

        public ModuleRequestEmailService(ILogger<ModuleRequestEmailService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> SendModuleRequestAsync(ModuleRequestDto r)
        {
            try
            {
                var isDeactivate = string.Equals(r.Action, "deactivate", StringComparison.OrdinalIgnoreCase);
                var actionLabel = isDeactivate ? "DEACTIVATION" : "PURCHASE / ACTIVATION";
                var tenant = string.IsNullOrWhiteSpace(r.TenantSlug) ? "unknown" : r.TenantSlug!.Trim();
                var moduleName = string.IsNullOrWhiteSpace(r.ModuleName) ? r.ModuleKey : r.ModuleName;
                var nowUtc = DateTime.UtcNow;

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Flowentra Platform", SMTP_USERNAME));
                email.To.Add(new MailboxAddress("Flowentra Contact", RECIPIENT));
                if (!string.IsNullOrWhiteSpace(r.UserEmail))
                {
                    try { email.ReplyTo.Add(MailboxAddress.Parse(r.UserEmail)); } catch { /* ignore bad address */ }
                }

                email.Subject = $"[{tenant.ToUpperInvariant()}] Module {actionLabel} request — {moduleName}";

                var html = BuildHtml(r, tenant, moduleName, isDeactivate, nowUtc);
                var text = BuildText(r, tenant, moduleName, isDeactivate, nowUtc);

                email.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(SMTP_HOST, SMTP_PORT, USE_SSL);
                    await client.AuthenticateAsync(SMTP_USERNAME, SMTP_PASSWORD);
                    await client.SendAsync(email);
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation(
                    "Module {Action} request sent to {Recipient} — tenant={Tenant} module={Module} user={User}",
                    r.Action, RECIPIENT, tenant, r.ModuleCode, r.UserEmail);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send module request email: {Message}", ex.Message);
                return false;
            }
        }

        private static string E(string? value) => WebUtility.HtmlEncode(value ?? "—");

        private static string BuildText(ModuleRequestDto r, string tenant, string moduleName, bool isDeactivate, DateTime nowUtc)
        {
            var action = isDeactivate ? "Deactivate module" : "Activate / purchase module";
            return
$@"Module request — Flowentra

Action requested : {action}
Module           : {moduleName} ({r.ModuleKey}) — code {r.ModuleCode}
Currently        : {(r.CurrentlyEnabled ? "Active" : "Inactive")}

Tenant (URL)     : {r.AppUrl ?? "—"}
Tenant slug      : {tenant}

Requested by     : {r.UserName ?? "—"} <{r.UserEmail ?? "—"}>
Requested at UTC : {nowUtc:yyyy-MM-dd HH:mm:ss} UTC
Client local time: {r.ClientTime ?? "—"} ({r.TimeZone ?? "—"})

Reason:
{(string.IsNullOrWhiteSpace(r.Reason) ? "(none provided)" : r.Reason)}

Action needed:
{(isDeactivate
    ? "1. Confirm the deactivation with the customer.\n2. Disable the module for this tenant.\n3. Adjust the subscription/billing for the next period.\n4. Reply to the requester to confirm."
    : "1. Confirm pricing and availability for this module.\n2. Send/validate the quote or contract update.\n3. Enable the module for this tenant.\n4. Reply to the requester to confirm.")}
";
        }

        private static string BuildHtml(ModuleRequestDto r, string tenant, string moduleName, bool isDeactivate, DateTime nowUtc)
        {
            var accent = isDeactivate ? "#b42318" : "#1570ef";
            var actionTitle = isDeactivate ? "Module deactivation request" : "Module purchase / activation request";
            var badge = isDeactivate ? "DEACTIVATE" : "ACTIVATE";
            var steps = isDeactivate
                ? new[]
                {
                    "Confirm the deactivation with the customer.",
                    "Disable the module for this tenant.",
                    "Adjust the subscription / billing for the next period.",
                    "Reply to the requester to confirm."
                }
                : new[]
                {
                    "Confirm pricing and availability for this module.",
                    "Send or validate the quote / contract update.",
                    "Enable the module for this tenant.",
                    "Reply to the requester to confirm."
                };

            var stepsHtml = string.Join("", Array.ConvertAll(steps, s => $"<li style='margin-bottom:6px;'>{E(s)}</li>"));

            string Row(string label, string? value) =>
                $@"<tr>
                     <td style='padding:8px 12px;background:#f8fafc;border:1px solid #e5e7eb;font-size:13px;color:#475467;width:190px;'>{E(label)}</td>
                     <td style='padding:8px 12px;border:1px solid #e5e7eb;font-size:13px;color:#101828;'>{E(value)}</td>
                   </tr>";

            return $@"
<!DOCTYPE html>
<html><body style='margin:0;padding:24px;background:#f2f4f7;font-family:Segoe UI,Arial,sans-serif;'>
  <div style='max-width:640px;margin:0 auto;background:#ffffff;border-radius:10px;overflow:hidden;border:1px solid #e5e7eb;'>
    <div style='background:{accent};padding:18px 24px;color:#ffffff;'>
      <div style='font-size:12px;letter-spacing:1px;opacity:.85;'>FLOWENTRA · SUBSCRIPTION</div>
      <div style='font-size:19px;font-weight:600;margin-top:4px;'>{E(actionTitle)}</div>
    </div>
    <div style='padding:22px 24px;'>
      <span style='display:inline-block;padding:4px 10px;border-radius:999px;background:{accent}1a;color:{accent};font-size:11px;font-weight:700;letter-spacing:.5px;'>{badge}</span>
      <h2 style='font-size:16px;color:#101828;margin:14px 0 12px;'>{E(moduleName)}</h2>
      <table style='width:100%;border-collapse:collapse;'>
        {Row("Module name", moduleName)}
        {Row("Module key", r.ModuleKey)}
        {Row("Module code", r.ModuleCode)}
        {Row("Current status", r.CurrentlyEnabled ? "Active" : "Inactive")}
        {Row("Tenant URL", r.AppUrl)}
        {Row("Tenant", tenant)}
        {Row("Requested by", r.UserName)}
        {Row("User email", r.UserEmail)}
        {Row("Requested at (UTC)", nowUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC")}
        {Row("Client local time", (r.ClientTime ?? "—") + " (" + (r.TimeZone ?? "—") + ")")}
      </table>

      <div style='margin-top:18px;'>
        <div style='font-size:13px;color:#475467;font-weight:600;margin-bottom:6px;'>Reason from the customer</div>
        <div style='padding:12px;border:1px solid #e5e7eb;border-radius:8px;background:#fcfcfd;font-size:13px;color:#101828;white-space:pre-wrap;'>{E(string.IsNullOrWhiteSpace(r.Reason) ? "(none provided)" : r.Reason)}</div>
      </div>

      <div style='margin-top:20px;padding:14px 16px;border-left:3px solid {accent};background:#f8fafc;'>
        <div style='font-size:13px;font-weight:700;color:#101828;margin-bottom:8px;'>Action needed</div>
        <ol style='margin:0;padding-left:18px;font-size:13px;color:#344054;'>{stepsHtml}</ol>
      </div>

      <p style='margin-top:22px;font-size:11px;color:#98a2b3;'>Automated message from the Flowentra platform. Reply directly to reach the requester.</p>
    </div>
  </div>
</body></html>";
        }
    }
}