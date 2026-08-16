using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Invoices.DTOs;
using MyApi.Modules.Invoices.Services;

namespace MyApi.Modules.Invoices.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/invoices")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _service;
        private readonly ILogger<InvoicesController>? _logger;
        public InvoicesController(IInvoiceService service, ILogger<InvoicesController>? logger = null)
        {
            _service = service;
            _logger = logger;
        }

        private string UserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] InvoiceQueryParams q)
            => Ok(await _service.GetInvoicesAsync(q));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var invoice = await _service.GetInvoiceByIdAsync(id);
            return invoice == null ? NotFound() : Ok(invoice);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInvoiceDto dto)
        {
            var invoice = await _service.CreateDraftAsync(dto, UserId());
            return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
        }

        [HttpPost("from-sale/{saleId:int}")]
        public async Task<IActionResult> CreateFromSale(int saleId, [FromQuery] int? serviceOrderId = null, [FromQuery] bool post = true)
        {
            const string trigger = "auto:create_from_sale";
            using var logScope = _logger?.BeginScope(new Dictionary<string, object?>
            {
                ["Operation"] = "InvoiceCreateFromSale",
                ["SaleId"] = saleId,
                ["ServiceOrderId"] = serviceOrderId,
                ["AutoPostRequested"] = post,
                ["UserId"] = UserId(),
            });

            var invoice = await _service.CreateDraftFromSaleAsync(saleId, UserId(), serviceOrderId);
            _logger?.LogInformation(
                "Draft invoice {InvoiceId} created from sale {SaleId} (total {Total} {Currency}); auto-post requested: {AutoPostRequested}",
                invoice.Id, saleId, invoice.GrandTotal, invoice.Currency, post);

            // Invoices raised from the sale's Invoices tab go straight to "posted" so the
            // user can record payments immediately. Pass ?post=false to keep it a draft.
            if (!post)
            {
                _logger?.LogInformation("Auto-post disabled by caller for invoice {InvoiceId} — left as draft", invoice.Id);
                await _service.LogAutoPostSkippedAsync(invoice.Id, UserId(), trigger, "auto-post disabled by the caller (post=false)");
            }
            else if (invoice.Status != "draft")
            {
                _logger?.LogInformation("Invoice {InvoiceId} already '{Status}' after creation — no auto-post needed", invoice.Id, invoice.Status);
            }
            else
            {
                try
                {
                    invoice = await _service.PostAsync(invoice.Id, new PostInvoiceDto(), UserId(), trigger);
                    _logger?.LogInformation("Invoice {InvoiceId} auto-posted as {Number} for sale {SaleId}",
                        invoice.Id, invoice.InvoiceNumber, saleId);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                {
                    // Auto-post is best-effort: keep the created draft rather than failing.
                    _logger?.LogWarning(ex, "Auto-post failed for invoice {InvoiceId} (sale {SaleId}); left as draft: {Reason}",
                        invoice.Id, saleId, ex.Message);
                    await _service.LogAutoPostSkippedAsync(invoice.Id, UserId(), trigger, ex.Message);
                }
            }
            return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
        }



        [HttpPatch("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateInvoiceDto dto)
            => Ok(await _service.UpdateDraftAsync(id, dto, UserId()));

        [HttpPost("{id:int}/post")]
        public async Task<IActionResult> Post(int id, [FromBody] PostInvoiceDto dto)
            => Ok(await _service.PostAsync(id, dto ?? new PostInvoiceDto(), UserId()));

        [HttpPost("{id:int}/void")]
        public async Task<IActionResult> Void(int id, [FromBody] VoidInvoiceDto dto)
            => Ok(await _service.VoidAsync(id, dto ?? new VoidInvoiceDto(), UserId()));

        [HttpPost("{id:int}/mark-paid")]
        public async Task<IActionResult> MarkPaid(int id, [FromBody] MarkPaidInvoiceDto dto)
            => Ok(await _service.MarkPaidAsync(id, dto ?? new MarkPaidInvoiceDto(), UserId()));

        [HttpPost("{id:int}/reopen")]
        public async Task<IActionResult> Reopen(int id, [FromBody] ReopenInvoiceDto dto)
            => Ok(await _service.ReopenAsync(id, dto ?? new ReopenInvoiceDto(), UserId()));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteDraftAsync(id, UserId());
            return ok ? NoContent() : NotFound();
        }

        [HttpGet("{id:int}/activities")]
        public async Task<IActionResult> Activities(int id)
            => Ok(await _service.GetActivitiesAsync(id));
    }
}