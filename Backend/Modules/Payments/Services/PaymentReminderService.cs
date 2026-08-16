using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Payments.Services;
using MyApi.Modules.Notifications.Services;
using MyApi.Modules.Notifications.DTOs;
using MyApi.Modules.EmailAccounts.Services;
using MyApi.Modules.EmailAccounts.DTOs;

namespace MyApi.Modules.Payments.Services
{
    /// <summary>
    /// Background service that runs every hour and checks for installments due within 3 days.
    /// Creates in-app notifications AND sends email reminders via connected email accounts (Gmail/Outlook).
    /// </summary>
    public class PaymentReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentReminderService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
        private readonly HashSet<string> _remindedInstallments = new();

        public PaymentReminderService(IServiceProvider serviceProvider, ILogger<PaymentReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("💰 PaymentReminderService started — checking every {Interval}", _checkInterval);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckUpcomingInstallments(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PaymentReminderService loop");
                }
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        /// <summary>
        /// A BackgroundService has no HttpContext, so ApplicationDbContext keeps its
        /// default _currentTenantId = 0 and every installment query would be filtered
        /// to tenant 0 — meaning no real company ever gets a reminder. Run one pass
        /// per active tenant instead.
        /// </summary>
        private async Task<List<int>> ResolveTenantIdsAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ids = await db.Tenants
                .Where(t => t.IsActive)
                .Select(t => t.Id)
                .ToListAsync(ct);
            if (!ids.Contains(0)) ids.Insert(0, 0);
            return ids;
        }

        private async Task CheckUpcomingInstallments(CancellationToken ct)
        {
            var tenantIds = await ResolveTenantIdsAsync(ct);

            // Purge dedupe keys from previous days once per cycle (not per installment).
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            foreach (var old in _remindedInstallments.Where(k => !k.EndsWith(today)).ToList())
                _remindedInstallments.Remove(old);

            foreach (var tenantId in tenantIds)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await CheckUpcomingInstallmentsForTenant(tenantId, ct);
                }
                catch (Exception ex)
                {
                    // One bad tenant must never abort the whole cycle.
                    _logger.LogError(ex, "Payment reminder cycle failed for tenant {TenantId}", tenantId);
                }
            }
        }

        private async Task CheckUpcomingInstallmentsForTenant(int tenantId, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Scope reads AND writes to this tenant before resolving anything that uses it.
            context.SetTenantId(tenantId);

            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var emailAccountService = scope.ServiceProvider.GetRequiredService<IEmailAccountService>();

            var upcoming = await paymentService.GetUpcomingInstallmentsAsync(daysAhead: 3);

            if (upcoming.Count == 0)
            {
                _logger.LogDebug("No upcoming installments found for tenant {TenantId}", tenantId);
                return;
            }

            _logger.LogInformation("Found {Count} upcoming installment reminders to process for tenant {TenantId}",
                upcoming.Count, tenantId);

            foreach (var inst in upcoming)
            {
                // Installment ids are only unique within a tenant — key the dedupe cache
                // by tenant too, otherwise two tenants suppress each other's reminders.
                var reminderKey = $"{tenantId}_{inst.InstallmentId}_{DateTime.UtcNow:yyyy-MM-dd}";
                if (_remindedInstallments.Contains(reminderKey))
                    continue;

                try
                {
                    var daysUntilDue = (inst.DueDate.Date - DateTime.UtcNow.Date).Days;
                    var entityLabel = inst.EntityType == "sale" ? "Sale" : "Offer";
                    var title = daysUntilDue == 0
                        ? $"⚠️ Payment Due Today — {inst.EntityTitle}"
                        : daysUntilDue == 1
                            ? $"⏰ Payment Due Tomorrow — {inst.EntityTitle}"
                            : $"📅 Payment Due in {daysUntilDue} days — {inst.EntityTitle}";

                    var description = $"Installment #{inst.InstallmentNumber} of plan \"{inst.PlanName}\" " +
                                     $"for {inst.Currency} {inst.Amount:N2} is due on {inst.DueDate:dd/MM/yyyy}. " +
                                     $"Contact: {inst.ContactName ?? "N/A"}";

                    var link = inst.EntityType == "sale"
                        ? $"/dashboard/sales/{inst.EntityId}"
                        : $"/dashboard/offers/{inst.EntityId}";

                    // 1) Create in-app notification (broadcast to all users)
                    await notificationService.CreateNotificationAsync(new CreateNotificationDto
                    {
                        UserId = 0,
                        Title = title,
                        Description = description,
                        Type = daysUntilDue == 0 ? "warning" : "info",
                        Category = inst.EntityType == "sale" ? "sale" : "offer",
                        Link = link,
                        RelatedEntityId = int.TryParse(inst.EntityId, out var eid) ? eid : null,
                        RelatedEntityType = $"payment_reminder_{inst.EntityType}",
                    });

                    _logger.LogInformation(
                        "🔔 Payment reminder notification created for installment #{Number} — {EntityType} #{EntityId}, due {DueDate:yyyy-MM-dd}",
                        inst.InstallmentNumber, inst.EntityType, inst.EntityId, inst.DueDate);

                    // 2) Send email reminder via connected accounts if contact has email
                    if (!string.IsNullOrEmpty(inst.ContactEmail))
                    {
                        await SendEmailReminderViaConnectedAccountAsync(
                            emailAccountService, context, inst, title, description);
                    }

                    _remindedInstallments.Add(reminderKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create reminder for installment {InstallmentId}", inst.InstallmentId);
                }
            }
        }

        /// <summary>
        /// Finds the first connected email account in the system and uses it to send a payment reminder email.
        /// Supports Gmail, Outlook, and custom SMTP accounts.
        /// </summary>
        private async Task SendEmailReminderViaConnectedAccountAsync(
            IEmailAccountService emailAccountService,
            ApplicationDbContext context,
            InstallmentReminderInfo inst,
            string subject,
            string plainTextBody)
        {
            try
            {
                // Find any active connected email account to send from
                var connectedAccount = await context.ConnectedEmailAccounts
                    .Where(a => a.SyncStatus == "active")
                    .OrderBy(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                if (connectedAccount == null)
                {
                    _logger.LogWarning("📧 No connected email accounts available to send payment reminder to {Email}", inst.ContactEmail);
                    return;
                }

                var htmlBody = BuildReminderEmailHtml(inst);

                var sendDto = new SendEmailDto
                {
                    To = new List<string> { inst.ContactEmail! },
                    Subject = subject,
                    Body = plainTextBody,
                    BodyHtml = htmlBody,
                };

                var result = await emailAccountService.SendEmailAsync(
                    connectedAccount.Id, connectedAccount.UserId, sendDto);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "📩 Payment reminder email sent to {Email} via {Provider} ({Handle}) — MessageId: {MessageId}",
                        inst.ContactEmail, connectedAccount.Provider, connectedAccount.Handle, result.MessageId);
                }
                else
                {
                    _logger.LogWarning(
                        "❌ Failed to send payment reminder email to {Email}: {Error}",
                        inst.ContactEmail, result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payment reminder email to {Email}", inst.ContactEmail);
            }
        }

        /// <summary>
        /// Builds a professional HTML email body for a payment reminder.
        /// </summary>
        private static string BuildReminderEmailHtml(InstallmentReminderInfo inst)
        {
            var daysUntilDue = (inst.DueDate.Date - DateTime.UtcNow.Date).Days;
            var urgencyColor = daysUntilDue == 0 ? "#dc2626" : daysUntilDue == 1 ? "#f59e0b" : "#3b82f6";
            var urgencyLabel = daysUntilDue == 0 ? "Due Today" : daysUntilDue == 1 ? "Due Tomorrow" : $"Due in {daysUntilDue} days";
            var entityLabel = inst.EntityType == "sale" ? "Sale" : "Offer";

            return $@"
<!DOCTYPE html>
<html>
<head><meta charset='UTF-8'></head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif; margin: 0; padding: 0; background-color: #f8fafc;'>
  <div style='max-width: 600px; margin: 0 auto; padding: 40px 20px;'>
    <div style='background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1);'>
      <!-- Header -->
      <div style='background: {urgencyColor}; padding: 24px 32px; text-align: center;'>
        <h1 style='color: white; margin: 0; font-size: 20px; font-weight: 600;'>Payment Reminder</h1>
        <p style='color: rgba(255,255,255,0.9); margin: 8px 0 0; font-size: 14px;'>{urgencyLabel}</p>
      </div>
      <!-- Body -->
      <div style='padding: 32px;'>
        <p style='color: #334155; font-size: 15px; line-height: 1.6; margin: 0 0 20px;'>
          Dear {inst.ContactName ?? "Customer"},
        </p>
        <p style='color: #334155; font-size: 15px; line-height: 1.6; margin: 0 0 24px;'>
          This is a friendly reminder about an upcoming payment for <strong>{inst.EntityTitle}</strong>.
        </p>
        <!-- Payment Details Card -->
        <div style='background: #f1f5f9; border-radius: 8px; padding: 20px; margin-bottom: 24px;'>
          <table style='width: 100%; border-collapse: collapse;'>
            <tr>
              <td style='padding: 6px 0; color: #64748b; font-size: 13px;'>{entityLabel}</td>
              <td style='padding: 6px 0; color: #0f172a; font-size: 13px; font-weight: 600; text-align: right;'>{inst.EntityTitle}</td>
            </tr>
            <tr>
              <td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Installment</td>
              <td style='padding: 6px 0; color: #0f172a; font-size: 13px; font-weight: 600; text-align: right;'>#{inst.InstallmentNumber} — {inst.PlanName}</td>
            </tr>
            <tr>
              <td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Amount Due</td>
              <td style='padding: 6px 0; color: #0f172a; font-size: 18px; font-weight: 700; text-align: right;'>{inst.Currency} {inst.Amount:N2}</td>
            </tr>
            <tr>
              <td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Due Date</td>
              <td style='padding: 6px 0; color: {urgencyColor}; font-size: 13px; font-weight: 600; text-align: right;'>{inst.DueDate:dd/MM/yyyy}</td>
            </tr>
          </table>
        </div>
        <p style='color: #64748b; font-size: 13px; line-height: 1.6; margin: 0;'>
          Please ensure payment is made by the due date. If you have any questions, please don't hesitate to contact us.
        </p>
      </div>
      <!-- Footer -->
      <div style='border-top: 1px solid #e2e8f0; padding: 16px 32px; text-align: center;'>
        <p style='color: #94a3b8; font-size: 12px; margin: 0;'>
          This is an automated payment reminder. Please do not reply to this email.
        </p>
      </div>
    </div>
  </div>
</body>
</html>";
        }
    }
}
