using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Modules.EmailAccounts.Controllers
{
    /// <summary>
    /// Manages connected email/calendar accounts (Gmail, Outlook) with OAuth
    /// </summary>
    [ApiController]
    [Route("api/email-accounts")]
    [Authorize]
    public partial class EmailAccountsController : ControllerBase
    {
        private readonly IEmailAccountService _emailAccountService;
        private readonly ILogger<EmailAccountsController> _logger;

        public EmailAccountsController(IEmailAccountService emailAccountService, ILogger<EmailAccountsController> logger)
        {
            _emailAccountService = emailAccountService;
            _logger = logger;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst("UserId")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // ─── OAuth Flow ───

        /// <summary>
        /// Get OAuth configuration for a provider (client ID, auth URL, scopes).
        /// Frontend uses this to initiate the OAuth popup/redirect.
        /// </summary>
        [HttpGet("oauth-config/{provider}")]
        public async Task<ActionResult<OAuthConfigDto>> GetOAuthConfig(string provider)
        {
            try
            {
                var config = await _emailAccountService.GetOAuthConfigAsync(provider);
                return Ok(config);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OAuth config for {Provider}", provider);
                return StatusCode(500, new { message = "Failed to get OAuth configuration" });
            }
        }

        /// <summary>
        /// Handle OAuth callback — exchange authorization code for tokens and create connected account.
        /// </summary>
        [HttpPost("oauth-callback")]
        public async Task<ActionResult<ConnectedEmailAccountDto>> OAuthCallback([FromBody] OAuthCallbackDto callbackDto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return Unauthorized(new { message = "Invalid user token" });

                var account = await _emailAccountService.HandleOAuthCallbackAsync(userId, callbackDto);
                return Ok(account);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "OAuth callback failed for provider {Provider}", callbackDto.Provider);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling OAuth callback");
                return StatusCode(500, new { message = "Failed to connect account" });
            }
        }

        // ─── Connected Accounts CRUD ───

        /// <summary>
        /// Get all connected email accounts for the current user
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConnectedEmailAccountDto>>> GetMyAccounts()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var accounts = await _emailAccountService.GetAccountsByUserAsync(userId);
            return Ok(accounts);
        }

        /// <summary>
        /// Get a specific connected account by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ConnectedEmailAccountDto>> GetAccount(Guid id)
        {
            var account = await _emailAccountService.GetAccountByIdAsync(id);
            if (account == null) return NotFound();
            return Ok(account);
        }

        /// <summary>
        /// Disconnect (delete) a connected account
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DisconnectAccount(Guid id)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var result = await _emailAccountService.DisconnectAccountAsync(id, userId);
            if (!result) return NotFound();
            return NoContent();
        }

        /// <summary>
        /// Reconnect an existing account (re-authorize OAuth)
        /// </summary>
        [HttpPost("{id}/reconnect")]
        public async Task<ActionResult<ConnectedEmailAccountDto>> ReconnectAccount(Guid id, [FromBody] OAuthCallbackDto callbackDto)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var account = await _emailAccountService.ReconnectAccountAsync(id, userId, callbackDto);
            if (account == null) return NotFound();
            return Ok(account);
        }

        // ─── Email Settings ───

        /// <summary>
        /// Update email sync settings for a connected account
        /// </summary>
        [HttpPatch("{id}/email-settings")]
        public async Task<ActionResult<ConnectedEmailAccountDto>> UpdateEmailSettings(Guid id, [FromBody] UpdateEmailSettingsDto dto)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var account = await _emailAccountService.UpdateEmailSettingsAsync(id, userId, dto);
            if (account == null) return NotFound();
            return Ok(account);
        }

        // ─── Calendar Settings ───

        /// <summary>
        /// Update calendar sync settings for a connected account
        /// </summary>
        [HttpPatch("{id}/calendar-settings")]
        public async Task<ActionResult<ConnectedEmailAccountDto>> UpdateCalendarSettings(Guid id, [FromBody] UpdateCalendarSettingsDto dto)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var account = await _emailAccountService.UpdateCalendarSettingsAsync(id, userId, dto);
            if (account == null) return NotFound();
            return Ok(account);
        }

        // ─── Blocklist ───

        /// <summary>
        /// Get blocklist for a connected account
        /// </summary>
        [HttpGet("{id}/blocklist")]
        public async Task<ActionResult<IEnumerable<BlocklistItemDto>>> GetBlocklist(Guid id)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var items = await _emailAccountService.GetBlocklistAsync(id, userId);
            return Ok(items);
        }

        /// <summary>
        /// Add an email or domain to the blocklist
        /// </summary>
        [HttpPost("{id}/blocklist")]
        public async Task<ActionResult<BlocklistItemDto>> AddBlocklistItem(Guid id, [FromBody] CreateBlocklistItemDto dto)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var item = await _emailAccountService.AddBlocklistItemAsync(id, userId, dto);
            if (item == null) return BadRequest(new { message = "Account not found or entry already exists" });
            return Ok(item);
        }

        /// <summary>
        /// Remove an item from the blocklist
        /// </summary>
        [HttpDelete("{id}/blocklist/{itemId}")]
        public async Task<IActionResult> RemoveBlocklistItem(Guid id, Guid itemId)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var result = await _emailAccountService.RemoveBlocklistItemAsync(id, itemId, userId);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
