using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.EmailAccounts.Services;
using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.Notifications.Services;
using MyApi.Modules.Notifications.DTOs;

namespace MyApi.Modules.Payments.Services
{
    /// <summary>
    /// Dedicated service for sending payment-related emails and notifications.
    /// Used by both the background reminder service and manual API triggers.
    /// </summary>
    public interface IPaymentEmailService
    {
        /// <summary>
        /// Sends an email reminder for a specific installment using the user's connected email account.
        /// Also creates an in-app notification.
        /// </summary>
        Task<PaymentEmailResult> SendInstallmentReminderAsync(
            string entityType, string entityId, string installmentId, int userId);

        /// <summary>
        /// Sends a payment confirmation email to the contact after a payment is recorded.
        /// </summary>
        Task<PaymentEmailResult> SendPaymentConfirmationAsync(
            string entityType, string entityId, string paymentId, int userId);
    }

    public class PaymentEmailResult
    {
        public bool Success { get; set; }
        public bool EmailSent { get; set; }
        public bool NotificationCreated { get; set; }
        public string? Error { get; set; }
        public string? MessageId { get; set; }
    }

    public class PaymentEmailService : IPaymentEmailService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailAccountService _emailAccountService;
        private readonly INotificationService _notificationService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentEmailService> _logger;

        public PaymentEmailService(
            ApplicationDbContext context,
            IEmailAccountService emailAccountService,
            INotificationService notificationService,
            IPaymentService paymentService,
            ILogger<PaymentEmailService> logger)
        {
            _context = context;
            _emailAccountService = emailAccountService;
            _notificationService = notificationService;
            _paymentService = paymentService;
            _logger = logger;
        }

        public async Task<PaymentEmailResult> SendInstallmentReminderAsync(
            string entityType, string entityId, string installmentId, int userId)
        {
            var result = new PaymentEmailResult();

            try
            {
                // Get installment info
                var installment = await _context.PaymentPlanInstallments
                    .Include(i => i.Plan)
                    .FirstOrDefaultAsync(i => i.Id == installmentId);

                if (installment?.Plan == null)
                    return new PaymentEmailResult { Success = false, Error = "Installment not found" };

                // Get entity and contact info
                var (entityTitle, contactName) = await GetEntityInfoAsync(entityType, entityId);
                var contactEmail = await GetContactEmailAsync(entityType, entityId);

                if (string.IsNullOrEmpty(contactEmail))
                    return new PaymentEmailResult { Success = false, Error = "Contact has no email address" };

                var daysUntilDue = (installment.DueDate.Date - DateTime.UtcNow.Date).Days;
                var title = daysUntilDue <= 0
                    ? $"⚠️ Payment Due — {entityTitle}"
                    : daysUntilDue == 1
                        ? $"⏰ Payment Due Tomorrow — {entityTitle}"
                        : $"📅 Payment Due in {daysUntilDue} days — {entityTitle}";

                var description = $"Installment #{installment.InstallmentNumber} of plan \"{installment.Plan.Name}\" " +
                                  $"for {installment.Plan.Currency} {installment.Amount:N2} is due on {installment.DueDate:dd/MM/yyyy}. " +
                                  $"Contact: {contactName ?? "N/A"}";

                // 1) Create in-app notification
                try
                {
                    await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                    {
                        UserId = 0,
                        Title = title,
                        Description = description,
                        Type = daysUntilDue <= 0 ? "warning" : "info",
                        Category = entityType == "sale" ? "sale" : "offer",
                        Link = entityType == "sale" ? $"/dashboard/sales/{entityId}" : $"/dashboard/offers/{entityId}",
                        RelatedEntityId = int.TryParse(entityId, out var eid) ? eid : null,
                        RelatedEntityType = $"payment_reminder_{entityType}",
                    });
                    result.NotificationCreated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create notification for installment {Id}", installmentId);
                }

                // 2) Send email via connected account
                var connectedAccount = await _context.ConnectedEmailAccounts
                    .Where(a => a.UserId == userId && a.SyncStatus == "active")
                    .OrderBy(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                // Fallback: try any active account
                connectedAccount ??= await _context.ConnectedEmailAccounts
                    .Where(a => a.SyncStatus == "active")
                    .OrderBy(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                if (connectedAccount == null)
                {
                    result.Success = true;
                    result.Error = "No connected email account available — notification created but email not sent";
                    return result;
                }

                var htmlBody = BuildReminderHtml(new ReminderData
                {
                    EntityType = entityType,
                    EntityTitle = entityTitle,
                    ContactName = contactName,
                    PlanName = installment.Plan.Name,
                    InstallmentNumber = installment.InstallmentNumber,
                    Amount = installment.Amount,
                    Currency = installment.Plan.Currency,
                    DueDate = installment.DueDate,
                });

                var sendResult = await _emailAccountService.SendEmailAsync(
                    connectedAccount.Id, connectedAccount.UserId,
                    new SendEmailDto
                    {
                        To = new List<string> { contactEmail },
                        Subject = title,
                        Body = description,
                        BodyHtml = htmlBody,
                    });

                result.EmailSent = sendResult.Success;
                result.MessageId = sendResult.MessageId;
                result.Success = true;

                if (!sendResult.Success)
                {
                    _logger.LogWarning("Failed to send reminder email to {Email}: {Error}", contactEmail, sendResult.Error);
                    result.Error = sendResult.Error;
                }
                else
                {
                    _logger.LogInformation("📩 Reminder email sent to {Email} via {Provider}", contactEmail, connectedAccount.Provider);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendInstallmentReminderAsync");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        public async Task<PaymentEmailResult> SendPaymentConfirmationAsync(
            string entityType, string entityId, string paymentId, int userId)
        {
            var result = new PaymentEmailResult();

            try
            {
                var payment = await _context.Payments
                    .Include(p => p.ItemAllocations)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null)
                    return new PaymentEmailResult { Success = false, Error = "Payment not found" };

                var (entityTitle, contactName) = await GetEntityInfoAsync(entityType, entityId);
                var contactEmail = await GetContactEmailAsync(entityType, entityId);

                if (string.IsNullOrEmpty(contactEmail))
                    return new PaymentEmailResult { Success = false, Error = "Contact has no email address" };

                // Get current summary for remaining balance
                var summary = await _paymentService.GetPaymentSummaryAsync(entityType, entityId);

                var subject = $"✅ Payment Received — {entityTitle} — {payment.Currency} {payment.Amount:N2}";
                var plainBody = $"Payment of {payment.Currency} {payment.Amount:N2} received for {entityTitle}. " +
                                $"Receipt: {payment.ReceiptNumber}. Remaining balance: {payment.Currency} {summary.RemainingAmount:N2}";

                var htmlBody = BuildConfirmationHtml(entityType, entityTitle, contactName, payment, summary);

                // Find connected account
                var connectedAccount = await _context.ConnectedEmailAccounts
                    .Where(a => a.UserId == userId && a.SyncStatus == "active")
                    .OrderBy(a => a.CreatedAt)
                    .FirstOrDefaultAsync()
                    ?? await _context.ConnectedEmailAccounts
                        .Where(a => a.SyncStatus == "active")
                        .OrderBy(a => a.CreatedAt)
                        .FirstOrDefaultAsync();

                if (connectedAccount == null)
                    return new PaymentEmailResult { Success = false, Error = "No connected email account available" };

                var sendResult = await _emailAccountService.SendEmailAsync(
                    connectedAccount.Id, connectedAccount.UserId,
                    new SendEmailDto
                    {
                        To = new List<string> { contactEmail },
                        Subject = subject,
                        Body = plainBody,
                        BodyHtml = htmlBody,
                    });

                result.Success = sendResult.Success;
                result.EmailSent = sendResult.Success;
                result.MessageId = sendResult.MessageId;
                if (!sendResult.Success) result.Error = sendResult.Error;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendPaymentConfirmationAsync");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        // ── Helpers ─────────────────────────────────────

        private async Task<(string title, string contactName)> GetEntityInfoAsync(string entityType, string entityId)
        {
            if (entityType == "sale")
            {
                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id.ToString() == entityId);
                if (sale != null)
                {
                    var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == sale.ContactId);
                    return (sale.Title ?? $"Sale #{sale.SaleNumber}", contact?.Name ?? "");
                }
            }
            else
            {
                var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id.ToString() == entityId);
                if (offer != null)
                {
                    var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == offer.ContactId);
                    return (offer.Title ?? $"Offer #{offer.Id}", contact?.Name ?? "");
                }
            }
            return ("", "");
        }

        private async Task<string?> GetContactEmailAsync(string entityType, string entityId)
        {
            int? contactId = null;
            if (entityType == "sale")
            {
                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id.ToString() == entityId);
                contactId = sale?.ContactId;
            }
            else
            {
                var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id.ToString() == entityId);
                contactId = offer?.ContactId;
            }
            if (contactId == null) return null;
            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId.Value);
            return contact?.Email;
        }

        private class ReminderData
        {
            public string EntityType { get; set; } = "";
            public string? EntityTitle { get; set; }
            public string? ContactName { get; set; }
            public string PlanName { get; set; } = "";
            public int InstallmentNumber { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "TND";
            public DateTime DueDate { get; set; }
        }

        private static string BuildReminderHtml(ReminderData inst)
        {
            var daysUntilDue = (inst.DueDate.Date - DateTime.UtcNow.Date).Days;
            var urgencyColor = daysUntilDue <= 0 ? "#dc2626" : daysUntilDue == 1 ? "#f59e0b" : "#3b82f6";
            var urgencyLabel = daysUntilDue <= 0 ? "Due Now" : daysUntilDue == 1 ? "Due Tomorrow" : $"Due in {daysUntilDue} days";
            var entityLabel = inst.EntityType == "sale" ? "Sale" : "Offer";

            return $@"
<!DOCTYPE html>
<html><head><meta charset='UTF-8'></head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif; margin: 0; padding: 0; background-color: #f8fafc;'>
  <div style='max-width: 600px; margin: 0 auto; padding: 40px 20px;'>
    <div style='background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1);'>
      <div style='background: {urgencyColor}; padding: 24px 32px; text-align: center;'>
        <h1 style='color: white; margin: 0; font-size: 20px;'>Payment Reminder</h1>
        <p style='color: rgba(255,255,255,0.9); margin: 8px 0 0; font-size: 14px;'>{urgencyLabel}</p>
      </div>
      <div style='padding: 32px;'>
        <p style='color: #334155; font-size: 15px; line-height: 1.6;'>Dear {inst.ContactName ?? "Customer"},</p>
        <p style='color: #334155; font-size: 15px; line-height: 1.6;'>
          This is a reminder about an upcoming payment for <strong>{inst.EntityTitle}</strong>.
        </p>
        <div style='background: #f1f5f9; border-radius: 8px; padding: 20px; margin: 24px 0;'>
          <table style='width: 100%; border-collapse: collapse;'>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>{entityLabel}</td>
                <td style='padding: 6px 0; color: #0f172a; font-size: 13px; font-weight: 600; text-align: right;'>{inst.EntityTitle}</td></tr>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Installment</td>
                <td style='padding: 6px 0; color: #0f172a; font-size: 13px; font-weight: 600; text-align: right;'>#{inst.InstallmentNumber} — {inst.PlanName}</td></tr>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Amount Due</td>
                <td style='padding: 6px 0; color: #0f172a; font-size: 18px; font-weight: 700; text-align: right;'>{inst.Currency} {inst.Amount:N2}</td></tr>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Due Date</td>
                <td style='padding: 6px 0; color: {urgencyColor}; font-size: 13px; font-weight: 600; text-align: right;'>{inst.DueDate:dd/MM/yyyy}</td></tr>
          </table>
        </div>
        <p style='color: #64748b; font-size: 13px;'>Please ensure payment is made by the due date.</p>
      </div>
      <div style='border-top: 1px solid #e2e8f0; padding: 16px 32px; text-align: center;'>
        <p style='color: #94a3b8; font-size: 12px; margin: 0;'>Automated payment reminder</p>
      </div>
    </div>
  </div>
</body></html>";
        }

        private static string BuildConfirmationHtml(
            string entityType, string? entityTitle, string? contactName,
            Models.Payment payment, DTOs.PaymentSummaryDto summary)
        {
            var entityLabel = entityType == "sale" ? "Sale" : "Offer";
            return $@"
<!DOCTYPE html>
<html><head><meta charset='UTF-8'></head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif; margin: 0; padding: 0; background-color: #f8fafc;'>
  <div style='max-width: 600px; margin: 0 auto; padding: 40px 20px;'>
    <div style='background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1);'>
      <div style='background: #16a34a; padding: 24px 32px; text-align: center;'>
        <h1 style='color: white; margin: 0; font-size: 20px;'>Payment Received</h1>
        <p style='color: rgba(255,255,255,0.9); margin: 8px 0 0; font-size: 14px;'>Thank you for your payment</p>
      </div>
      <div style='padding: 32px;'>
        <p style='color: #334155; font-size: 15px;'>Dear {contactName ?? "Customer"},</p>
        <p style='color: #334155; font-size: 15px;'>We've received your payment for <strong>{entityTitle}</strong>.</p>
        <div style='background: #f1f5f9; border-radius: 8px; padding: 20px; margin: 24px 0;'>
          <table style='width: 100%; border-collapse: collapse;'>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Receipt #</td>
                <td style='padding: 6px 0; color: #0f172a; font-size: 13px; font-weight: 600; text-align: right;'>{payment.ReceiptNumber}</td></tr>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Amount Paid</td>
                <td style='padding: 6px 0; color: #16a34a; font-size: 18px; font-weight: 700; text-align: right;'>{payment.Currency} {payment.Amount:N2}</td></tr>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Payment Method</td>
                <td style='padding: 6px 0; color: #0f172a; font-size: 13px; text-align: right;'>{payment.PaymentMethod}</td></tr>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Date</td>
                <td style='padding: 6px 0; color: #0f172a; font-size: 13px; text-align: right;'>{payment.PaymentDate:dd/MM/yyyy}</td></tr>
            <tr style='border-top: 1px solid #e2e8f0;'>
                <td style='padding: 10px 0 6px; color: #64748b; font-size: 13px;'>Total Paid</td>
                <td style='padding: 10px 0 6px; color: #0f172a; font-size: 13px; font-weight: 600; text-align: right;'>{summary.Currency} {summary.PaidAmount:N2}</td></tr>
            <tr><td style='padding: 6px 0; color: #64748b; font-size: 13px;'>Remaining Balance</td>
                <td style='padding: 6px 0; color: {(summary.RemainingAmount > 0 ? "#f59e0b" : "#16a34a")}; font-size: 13px; font-weight: 600; text-align: right;'>{summary.Currency} {summary.RemainingAmount:N2}</td></tr>
          </table>
        </div>
      </div>
      <div style='border-top: 1px solid #e2e8f0; padding: 16px 32px; text-align: center;'>
        <p style='color: #94a3b8; font-size: 12px; margin: 0;'>Automated payment confirmation</p>
      </div>
    </div>
  </div>
</body></html>";
        }
    }
}
