using MyApi.Modules.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MyApi.Modules.Auth.Controllers
{
    /// <summary>
    /// Handles OAuth provider redirects (GET requests from Google/Microsoft).
    /// Google/Microsoft redirect here with ?code=...&amp;state=... after user consent.
    /// This controller exchanges the code for tokens, resolves the user,
    /// and redirects back to the frontend with auth data.
    /// </summary>
    [ApiController]
    [Route("oauth")]
    [AllowAnonymous]
    public class OAuthCallbackController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OAuthCallbackController> _logger;

        // Base domain for tenant subdomains
        private const string BaseDomain = "flowentra.app";

        // Default frontend origin — override via env var FRONTEND_ORIGIN
        private string FrontendOrigin =>
            Environment.GetEnvironmentVariable("FRONTEND_ORIGIN")
            ?? _configuration["Frontend:Origin"]
            ?? $"https://{BaseDomain}";

        public OAuthCallbackController(
            IAuthService authService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<OAuthCallbackController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  GOOGLE CALLBACK
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /oauth/google/callback?code=...&amp;state=...&amp;scope=...
        /// Called by Google after user grants consent.
        /// </summary>
        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Google OAuth error: {Error}", error);
                if (IsEmailConnectFlow(state))
                    return Redirect($"{GetEmailConnectRedirectOrigin(state)}/oauth/callback?error={Uri.EscapeDataString(error)}");
                return Redirect($"{FrontendOrigin}/login?oauth_error={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Google OAuth callback received without code");
                if (IsEmailConnectFlow(state))
                    return Redirect($"{GetEmailConnectRedirectOrigin(state)}/oauth/callback?error=missing_code");
                return Redirect($"{FrontendOrigin}/login?oauth_error=missing_code");
            }

            // ── Email/Calendar connect flow: just forward the code to the tenant frontend ──
            if (IsEmailConnectFlow(state))
            {
                var origin = GetEmailConnectRedirectOrigin(state);
                _logger.LogInformation("Email-connect OAuth flow detected, redirecting code to {Origin}", origin);
                return Redirect($"{origin}/oauth/callback?code={Uri.EscapeDataString(code)}");
            }

            // ── Auth login flow: exchange code for tokens and create session ──
            try
            {
                var section = _configuration.GetSection("OAuth:Google");
                var clientId = section["ClientId"];
                var clientSecret = section["ClientSecret"];
                var redirectUri = section["RedirectUri"]
                    ?? $"{Request.Scheme}://{Request.Host}/oauth/google/callback";

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("Google OAuth is not configured (missing ClientId/ClientSecret)");
                    return Redirect($"{FrontendOrigin}/login?oauth_error=provider_not_configured");
                }

                var tokenResponse = await ExchangeCodeAsync(
                    code, clientId, clientSecret, redirectUri,
                    "https://oauth2.googleapis.com/token");

                if (tokenResponse == null)
                    return Redirect($"{FrontendOrigin}/login?oauth_error=token_exchange_failed");

                var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken);
                if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
                {
                    _logger.LogWarning("Could not retrieve email from Google user info");
                    return Redirect($"{FrontendOrigin}/login?oauth_error=email_not_found");
                }

                _logger.LogInformation("Google OAuth callback for email: {Email}", userInfo.Email);
                return await CompleteOAuthLogin(userInfo, "google");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Google OAuth callback");
                return Redirect($"{FrontendOrigin}/login?oauth_error=internal_error");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  MICROSOFT CALLBACK
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /oauth/microsoft/callback?code=...&amp;state=...
        /// Called by Microsoft after user grants consent.
        /// </summary>
        [HttpGet("microsoft/callback")]
        public async Task<IActionResult> MicrosoftCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery(Name = "error_description")] string? errorDescription)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Microsoft OAuth error: {Error} — {Description}", error, errorDescription);
                if (IsEmailConnectFlow(state))
                    return Redirect($"{GetEmailConnectRedirectOrigin(state)}/oauth/callback?error={Uri.EscapeDataString(error)}");
                return Redirect($"{FrontendOrigin}/login?oauth_error={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Microsoft OAuth callback received without code");
                if (IsEmailConnectFlow(state))
                    return Redirect($"{GetEmailConnectRedirectOrigin(state)}/oauth/callback?error=missing_code");
                return Redirect($"{FrontendOrigin}/login?oauth_error=missing_code");
            }

            // ── Email/Calendar connect flow: just forward the code to the tenant frontend ──
            if (IsEmailConnectFlow(state))
            {
                var origin = GetEmailConnectRedirectOrigin(state);
                _logger.LogInformation("Email-connect OAuth flow detected, redirecting code to {Origin}", origin);
                return Redirect($"{origin}/oauth/callback?code={Uri.EscapeDataString(code)}");
            }

            // ── Auth login flow ──
            try
            {
                var section = _configuration.GetSection("OAuth:Microsoft");
                var clientId = section["ClientId"];
                var clientSecret = section["ClientSecret"];
                var redirectUri = section["RedirectUri"]
                    ?? $"{Request.Scheme}://{Request.Host}/oauth/microsoft/callback";

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("Microsoft OAuth is not configured (missing ClientId/ClientSecret)");
                    return Redirect($"{FrontendOrigin}/login?oauth_error=provider_not_configured");
                }

                var tokenResponse = await ExchangeCodeAsync(
                    code, clientId, clientSecret, redirectUri,
                    "https://login.microsoftonline.com/common/oauth2/v2.0/token");

                if (tokenResponse == null)
                    return Redirect($"{FrontendOrigin}/login?oauth_error=token_exchange_failed");

                var userInfo = await GetMicrosoftUserInfoAsync(tokenResponse.AccessToken);
                if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
                {
                    _logger.LogWarning("Could not retrieve email from Microsoft user info");
                    return Redirect($"{FrontendOrigin}/login?oauth_error=email_not_found");
                }

                _logger.LogInformation("Microsoft OAuth callback for email: {Email}", userInfo.Email);
                return await CompleteOAuthLogin(userInfo, "microsoft");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Microsoft OAuth callback");
                return Redirect($"{FrontendOrigin}/login?oauth_error=internal_error");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  HELPER: Detect email-connect flow & resolve tenant origin from state
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true if the OAuth state indicates an email/calendar connect flow.
        /// State format: "email:{tenant}:{uuid}"
        /// </summary>
        private static bool IsEmailConnectFlow(string? state) =>
            !string.IsNullOrEmpty(state) && state.StartsWith("email:", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Resolves the frontend origin for an email-connect redirect.
        /// Extracts the tenant from state "email:{tenant}:{uuid}" and builds
        /// https://{tenant}.flowentra.app. Falls back to the default FrontendOrigin.
        /// Validates tenant name to prevent open redirect attacks.
        /// </summary>
        private string GetEmailConnectRedirectOrigin(string? state)
        {
            if (string.IsNullOrEmpty(state)) return FrontendOrigin;

            var parts = state.Split(':');
            // Expected: ["email", "{tenant}", "{uuid}"]
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[1])) return FrontendOrigin;

            var tenant = parts[1].Trim().ToLower();

            // "_default" means no tenant (bare domain)
            if (tenant == "_default") return FrontendOrigin;

            // Validate tenant: only alphanumeric + hyphens (prevent open redirect)
            if (!System.Text.RegularExpressions.Regex.IsMatch(tenant, @"^[a-z0-9\-]+$"))
            {
                _logger.LogWarning("Invalid tenant in OAuth state: {Tenant}", tenant);
                return FrontendOrigin;
            }

            // Optional allowlist via env var ALLOWED_TENANTS (comma-separated)
            var allowed = Environment.GetEnvironmentVariable("ALLOWED_TENANTS");
            if (!string.IsNullOrEmpty(allowed))
            {
                var allowedList = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!allowedList.Contains(tenant, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Tenant {Tenant} not in ALLOWED_TENANTS list", tenant);
                    return FrontendOrigin;
                }
            }

            return $"https://{tenant}.{BaseDomain}";
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SHARED: Complete login & redirect
        // ═══════════════════════════════════════════════════════════════════════

        private async Task<IActionResult> CompleteOAuthLogin(OAuthUserInfo userInfo, string provider)
        {
            var authResponse = await _authService.OAuthLoginAsync(userInfo.Email!);

            if (!authResponse.Success)
            {
                // User doesn't exist — redirect to signup with pre-filled data
                var signupParams = $"oauth_provider={provider}"
                    + $"&email={Uri.EscapeDataString(userInfo.Email ?? "")}"
                    + $"&first_name={Uri.EscapeDataString(userInfo.FirstName ?? "")}"
                    + $"&last_name={Uri.EscapeDataString(userInfo.LastName ?? "")}"
                    + $"&profile_picture={Uri.EscapeDataString(userInfo.Picture ?? "")}";

                return Redirect($"{FrontendOrigin}/signup?{signupParams}");
            }

            // Redirect with tokens in fragment (# keeps them out of server logs / referer headers)
            var fragment = $"access_token={Uri.EscapeDataString(authResponse.AccessToken ?? "")}"
                + $"&refresh_token={Uri.EscapeDataString(authResponse.RefreshToken ?? "")}"
                + $"&expires_at={Uri.EscapeDataString(authResponse.ExpiresAt?.ToString("o") ?? "")}"
                + $"&user_id={authResponse.User?.Id}"
                + $"&email={Uri.EscapeDataString(authResponse.User?.Email ?? "")}"
                + $"&provider={provider}";

            return Redirect($"{FrontendOrigin}/oauth/callback#{fragment}");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SHARED: Token Exchange (Google & Microsoft use the same OAuth2 flow)
        // ═══════════════════════════════════════════════════════════════════════

        private async Task<OAuthTokenResponse?> ExchangeCodeAsync(
            string code, string clientId, string clientSecret, string redirectUri, string tokenEndpoint)
        {
            var client = _httpClientFactory.CreateClient();

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

            var response = await client.PostAsync(tokenEndpoint, body);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token exchange failed at {Endpoint}: {Status} {Body}", tokenEndpoint, response.StatusCode, json);
                return null;
            }

            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
            return new OAuthTokenResponse
            {
                AccessToken = tokenData.GetProperty("access_token").GetString() ?? "",
                RefreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                IdToken = tokenData.TryGetProperty("id_token", out var idt) ? idt.GetString() : null,
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PROVIDER-SPECIFIC: User Info
        // ═══════════════════════════════════════════════════════════════════════

        private async Task<OAuthUserInfo?> GetGoogleUserInfoAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get Google user info: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            return new OAuthUserInfo
            {
                Email = data.TryGetProperty("email", out var e) ? e.GetString() : null,
                FirstName = data.TryGetProperty("given_name", out var fn) ? fn.GetString() : null,
                LastName = data.TryGetProperty("family_name", out var ln) ? ln.GetString() : null,
                Picture = data.TryGetProperty("picture", out var pic) ? pic.GetString() : null,
            };
        }

        private async Task<OAuthUserInfo?> GetMicrosoftUserInfoAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get Microsoft user info: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // Microsoft Graph returns mail or userPrincipalName for email
            var email = data.TryGetProperty("mail", out var mail) ? mail.GetString() : null;
            if (string.IsNullOrEmpty(email))
                email = data.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null;

            return new OAuthUserInfo
            {
                Email = email,
                FirstName = data.TryGetProperty("givenName", out var fn) ? fn.GetString() : null,
                LastName = data.TryGetProperty("surname", out var ln) ? ln.GetString() : null,
                // Microsoft Graph photo requires a separate call; skip for now
                Picture = null,
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  INTERNAL DTOs
        // ═══════════════════════════════════════════════════════════════════════

        private class OAuthTokenResponse
        {
            public string AccessToken { get; set; } = "";
            public string? RefreshToken { get; set; }
            public string? IdToken { get; set; }
        }

        private class OAuthUserInfo
        {
            public string? Email { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Picture { get; set; }
        }
    }
}
