using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Payments.DTOs;
using MyApi.Modules.Payments.Services;
using System.Security.Claims;

namespace MyApi.Modules.Payments.Controllers
{
    [Authorize]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IPaymentEmailService _paymentEmailService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IPaymentService paymentService,
            IPaymentEmailService paymentEmailService,
            ILogger<PaymentsController> logger)
        {
            _paymentService = paymentService;
            _paymentEmailService = paymentEmailService;
            _logger = logger;
        }

        private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        private int GetUserIdInt() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        private string GetUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? 
            User.FindFirst("FirstName")?.Value + " " + User.FindFirst("LastName")?.Value ?? "anonymous";

        // ══════════════════════════════════════════════
        // Sales Payments
        // ══════════════════════════════════════════════

        [HttpGet("api/sales/{entityId}/payments")]
        public async Task<IActionResult> GetSalePayments(string entityId)
        {
            try
            {
                var payments = await _paymentService.GetPaymentsAsync("sale", entityId);
                return Ok(new { success = true, data = new { payments } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sale payments for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch payments" } });
            }
        }

        [HttpPost("api/sales/{entityId}/payments")]
        public async Task<IActionResult> CreateSalePayment(string entityId, [FromBody] CreatePaymentDto dto)
        {
            try
            {
                var payment = await _paymentService.CreatePaymentAsync("sale", entityId, dto, GetUserId(), GetUserName());
                return Created($"/api/sales/{entityId}/payments/{payment.Id}", new { success = true, data = payment });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sale payment for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to create payment" } });
            }
        }

        [HttpDelete("api/sales/{entityId}/payments/{paymentId}")]
        public async Task<IActionResult> DeleteSalePayment(string entityId, string paymentId)
        {
            try
            {
                var result = await _paymentService.DeletePaymentAsync("sale", entityId, paymentId);
                if (!result) return NotFound(new { success = false, error = new { message = "Payment not found" } });
                return Ok(new { success = true, message = "Payment deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sale payment {PaymentId}", paymentId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to delete payment" } });
            }
        }

        [HttpGet("api/sales/{entityId}/payments/summary")]
        public async Task<IActionResult> GetSalePaymentSummary(string entityId)
        {
            try
            {
                var summary = await _paymentService.GetPaymentSummaryAsync("sale", entityId);
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sale payment summary for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch summary" } });
            }
        }

        [HttpGet("api/sales/{entityId}/payments/statement")]
        public async Task<IActionResult> GetSalePaymentStatement(string entityId)
        {
            try
            {
                var statement = await _paymentService.GetPaymentStatementAsync("sale", entityId);
                return Ok(new { success = true, data = statement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sale payment statement for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch statement" } });
            }
        }

        [HttpGet("api/sales/{entityId}/payment-plans")]
        public async Task<IActionResult> GetSalePaymentPlans(string entityId)
        {
            try
            {
                var plans = await _paymentService.GetPaymentPlansAsync("sale", entityId);
                return Ok(new { success = true, data = new { plans } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sale payment plans for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch plans" } });
            }
        }

        [HttpPost("api/sales/{entityId}/payment-plans")]
        public async Task<IActionResult> CreateSalePaymentPlan(string entityId, [FromBody] CreatePaymentPlanDto dto)
        {
            try
            {
                var plan = await _paymentService.CreatePaymentPlanAsync("sale", entityId, dto, GetUserId());
                return Created($"/api/sales/{entityId}/payment-plans/{plan.Id}", new { success = true, data = plan });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sale payment plan for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to create plan" } });
            }
        }

        [HttpDelete("api/sales/{entityId}/payment-plans/{planId}")]
        public async Task<IActionResult> DeleteSalePaymentPlan(string entityId, string planId)
        {
            try
            {
                var result = await _paymentService.DeletePaymentPlanAsync("sale", entityId, planId);
                if (!result) return NotFound(new { success = false, error = new { message = "Plan not found" } });
                return Ok(new { success = true, message = "Plan deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sale payment plan {PlanId}", planId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to delete plan" } });
            }
        }

        // ══════════════════════════════════════════════
        // Offers Payments (same structure, different entity type)
        // ══════════════════════════════════════════════

        [HttpGet("api/offers/{entityId}/payments")]
        public async Task<IActionResult> GetOfferPayments(string entityId)
        {
            try
            {
                var payments = await _paymentService.GetPaymentsAsync("offer", entityId);
                return Ok(new { success = true, data = new { payments } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offer payments for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch payments" } });
            }
        }

        [HttpPost("api/offers/{entityId}/payments")]
        public async Task<IActionResult> CreateOfferPayment(string entityId, [FromBody] CreatePaymentDto dto)
        {
            try
            {
                var payment = await _paymentService.CreatePaymentAsync("offer", entityId, dto, GetUserId(), GetUserName());
                return Created($"/api/offers/{entityId}/payments/{payment.Id}", new { success = true, data = payment });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer payment for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to create payment" } });
            }
        }

        [HttpDelete("api/offers/{entityId}/payments/{paymentId}")]
        public async Task<IActionResult> DeleteOfferPayment(string entityId, string paymentId)
        {
            try
            {
                var result = await _paymentService.DeletePaymentAsync("offer", entityId, paymentId);
                if (!result) return NotFound(new { success = false, error = new { message = "Payment not found" } });
                return Ok(new { success = true, message = "Payment deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting offer payment {PaymentId}", paymentId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to delete payment" } });
            }
        }

        [HttpGet("api/offers/{entityId}/payments/summary")]
        public async Task<IActionResult> GetOfferPaymentSummary(string entityId)
        {
            try
            {
                var summary = await _paymentService.GetPaymentSummaryAsync("offer", entityId);
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offer payment summary for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch summary" } });
            }
        }

        [HttpGet("api/offers/{entityId}/payments/statement")]
        public async Task<IActionResult> GetOfferPaymentStatement(string entityId)
        {
            try
            {
                var statement = await _paymentService.GetPaymentStatementAsync("offer", entityId);
                return Ok(new { success = true, data = statement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offer payment statement for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch statement" } });
            }
        }

        [HttpGet("api/offers/{entityId}/payment-plans")]
        public async Task<IActionResult> GetOfferPaymentPlans(string entityId)
        {
            try
            {
                var plans = await _paymentService.GetPaymentPlansAsync("offer", entityId);
                return Ok(new { success = true, data = new { plans } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offer payment plans for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch plans" } });
            }
        }

        [HttpPost("api/offers/{entityId}/payment-plans")]
        public async Task<IActionResult> CreateOfferPaymentPlan(string entityId, [FromBody] CreatePaymentPlanDto dto)
        {
            try
            {
                var plan = await _paymentService.CreatePaymentPlanAsync("offer", entityId, dto, GetUserId());
                return Created($"/api/offers/{entityId}/payment-plans/{plan.Id}", new { success = true, data = plan });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer payment plan for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to create plan" } });
            }
        }

        [HttpDelete("api/offers/{entityId}/payment-plans/{planId}")]
        public async Task<IActionResult> DeleteOfferPaymentPlan(string entityId, string planId)
        {
            try
            {
                var result = await _paymentService.DeletePaymentPlanAsync("offer", entityId, planId);
                if (!result) return NotFound(new { success = false, error = new { message = "Plan not found" } });
                return Ok(new { success = true, message = "Plan deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting offer payment plan {PlanId}", planId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to delete plan" } });
            }
        }

        // ══════════════════════════════════════════════
        // Invoice Payments (customer invoice ledger)
        // ══════════════════════════════════════════════

        [HttpGet("api/invoices/{entityId}/payments")]
        public async Task<IActionResult> GetInvoicePayments(string entityId)
        {
            try
            {
                var payments = await _paymentService.GetPaymentsAsync("invoice", entityId);
                return Ok(new { success = true, data = new { payments } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice payments for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch payments" } });
            }
        }

        [HttpPost("api/invoices/{entityId}/payments")]
        public async Task<IActionResult> CreateInvoicePayment(string entityId, [FromBody] CreatePaymentDto dto)
        {
            try
            {
                var payment = await _paymentService.CreatePaymentAsync("invoice", entityId, dto, GetUserId(), GetUserName());
                return Created($"/api/invoices/{entityId}/payments/{payment.Id}", new { success = true, data = payment });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { message = ex.Message } });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, error = new { message = ex.Message } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = new { message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice payment for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to create payment" } });
            }
        }

        [HttpDelete("api/invoices/{entityId}/payments/{paymentId}")]
        public async Task<IActionResult> DeleteInvoicePayment(string entityId, string paymentId)
        {
            try
            {
                var result = await _paymentService.DeletePaymentAsync("invoice", entityId, paymentId);
                if (!result) return NotFound(new { success = false, error = new { message = "Payment not found" } });
                return Ok(new { success = true, message = "Payment deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice payment {PaymentId}", paymentId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to delete payment" } });
            }
        }

        [HttpGet("api/invoices/{entityId}/payments/summary")]
        public async Task<IActionResult> GetInvoicePaymentSummary(string entityId)
        {
            try
            {
                var summary = await _paymentService.GetPaymentSummaryAsync("invoice", entityId);
                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice payment summary for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch summary" } });
            }
        }

        [HttpGet("api/invoices/{entityId}/payments/statement")]
        public async Task<IActionResult> GetInvoicePaymentStatement(string entityId)
        {
            try
            {
                var statement = await _paymentService.GetPaymentStatementAsync("invoice", entityId);
                return Ok(new { success = true, data = statement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice payment statement for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch statement" } });
            }
        }

        [HttpGet("api/invoices/{entityId}/payment-plans")]
        public async Task<IActionResult> GetInvoicePaymentPlans(string entityId)
        {
            try
            {
                var plans = await _paymentService.GetPaymentPlansAsync("invoice", entityId);
                return Ok(new { success = true, data = new { plans } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice payment plans for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch plans" } });
            }
        }

        [HttpPost("api/invoices/{entityId}/payment-plans")]
        public async Task<IActionResult> CreateInvoicePaymentPlan(string entityId, [FromBody] CreatePaymentPlanDto dto)
        {
            try
            {
                var plan = await _paymentService.CreatePaymentPlanAsync("invoice", entityId, dto, GetUserId());
                return Created($"/api/invoices/{entityId}/payment-plans/{plan.Id}", new { success = true, data = plan });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice payment plan for {EntityId}", entityId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to create plan" } });
            }
        }

        [HttpDelete("api/invoices/{entityId}/payment-plans/{planId}")]
        public async Task<IActionResult> DeleteInvoicePaymentPlan(string entityId, string planId)
        {
            try
            {
                var result = await _paymentService.DeletePaymentPlanAsync("invoice", entityId, planId);
                if (!result) return NotFound(new { success = false, error = new { message = "Plan not found" } });
                return Ok(new { success = true, message = "Plan deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice payment plan {PlanId}", planId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to delete plan" } });
            }
        }


        // ══════════════════════════════════════════════
        // Email Reminders & Confirmations (shared)
        // ══════════════════════════════════════════════

        [HttpPost("api/{entityType}/email/send-reminder")]
        public async Task<IActionResult> SendInstallmentReminder(string entityType, [FromBody] SendReminderRequest request)
        {
            try
            {
                var type = entityType.TrimEnd('s'); // "sales" → "sale", "offers" → "offer"
                var result = await _paymentEmailService.SendInstallmentReminderAsync(
                    type, request.EntityId, request.InstallmentId, GetUserIdInt());
                return Ok(new { success = result.Success, emailSent = result.EmailSent, notificationCreated = result.NotificationCreated, error = result.Error });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending installment reminder");
                return StatusCode(500, new { success = false, error = new { message = "Failed to send reminder" } });
            }
        }

        [HttpPost("api/{entityType}/email/send-confirmation")]
        public async Task<IActionResult> SendPaymentConfirmation(string entityType, [FromBody] SendConfirmationRequest request)
        {
            try
            {
                var type = entityType.TrimEnd('s');
                var result = await _paymentEmailService.SendPaymentConfirmationAsync(
                    type, request.EntityId, request.PaymentId, GetUserIdInt());
                return Ok(new { success = result.Success, emailSent = result.EmailSent, error = result.Error });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payment confirmation");
                return StatusCode(500, new { success = false, error = new { message = "Failed to send confirmation" } });
            }
        }


        // ══════════════════════════════════════════════
        // Proof-of-payment documents (multiple per payment)
        // Shared routes for sales / offers / invoices.
        // ══════════════════════════════════════════════

        private static string? NormalizeEntityType(string entityType) => entityType?.ToLowerInvariant() switch
        {
            "sales" or "sale" => "sale",
            "offers" or "offer" => "offer",
            "invoices" or "invoice" => "invoice",
            _ => null,
        };

        [HttpGet("api/{entityType}/{entityId}/payments/{paymentId}/proofs")]
        public async Task<IActionResult> GetPaymentProofs(string entityType, string entityId, string paymentId)
        {
            var type = NormalizeEntityType(entityType);
            if (type == null) return BadRequest(new { success = false, error = new { message = "Unsupported entity type" } });
            try
            {
                var proofs = await _paymentService.GetPaymentProofsAsync(type, entityId, paymentId);
                return Ok(new { success = true, data = new { proofs } });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payment proofs for {PaymentId}", paymentId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to fetch proofs" } });
            }
        }

        [HttpPost("api/{entityType}/{entityId}/payments/{paymentId}/proofs")]
        public async Task<IActionResult> AddPaymentProofs(
            string entityType, string entityId, string paymentId, [FromBody] List<CreatePaymentProofDocumentDto> proofs)
        {
            var type = NormalizeEntityType(entityType);
            if (type == null) return BadRequest(new { success = false, error = new { message = "Unsupported entity type" } });
            if (proofs == null || proofs.Count == 0)
                return BadRequest(new { success = false, error = new { message = "No proof documents supplied" } });
            try
            {
                var result = await _paymentService.AddPaymentProofsAsync(type, entityId, paymentId, proofs, GetUserId());
                return Ok(new { success = true, data = new { proofs = result } });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding payment proofs for {PaymentId}", paymentId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to add proofs" } });
            }
        }

        [HttpPut("api/{entityType}/{entityId}/payments/{paymentId}/proofs/{proofId}")]
        public async Task<IActionResult> UpdatePaymentProof(
            string entityType, string entityId, string paymentId, string proofId,
            [FromBody] UpdatePaymentProofDocumentDto dto)
        {
            var type = NormalizeEntityType(entityType);
            if (type == null) return BadRequest(new { success = false, error = new { message = "Unsupported entity type" } });
            if (string.IsNullOrWhiteSpace(dto?.DocumentName))
                return BadRequest(new { success = false, error = new { message = "Document name is required" } });
            try
            {
                var updated = await _paymentService.UpdatePaymentProofAsync(type, entityId, paymentId, proofId, dto.DocumentName.Trim());
                if (updated == null) return NotFound(new { success = false, error = new { message = "Proof not found" } });
                return Ok(new { success = true, data = updated });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment proof {ProofId}", proofId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to update proof" } });
            }
        }

        [HttpDelete("api/{entityType}/{entityId}/payments/{paymentId}/proofs/{proofId}")]
        public async Task<IActionResult> DeletePaymentProof(string entityType, string entityId, string paymentId, string proofId)
        {
            var type = NormalizeEntityType(entityType);
            if (type == null) return BadRequest(new { success = false, error = new { message = "Unsupported entity type" } });
            try
            {
                var removed = await _paymentService.DeletePaymentProofAsync(type, entityId, paymentId, proofId);
                if (!removed) return NotFound(new { success = false, error = new { message = "Proof not found" } });
                return Ok(new { success = true, message = "Proof removed" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, error = new { message = ex.Message } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payment proof {ProofId}", proofId);
                return StatusCode(500, new { success = false, error = new { message = "Failed to delete proof" } });
            }
        }
    }


    public class SendReminderRequest
    {
        public string EntityId { get; set; } = "";
        public string InstallmentId { get; set; } = "";
    }

    public class SendConfirmationRequest
    {
        public string EntityId { get; set; } = "";
        public string PaymentId { get; set; } = "";
    }
}
