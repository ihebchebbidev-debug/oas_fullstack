using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace MyApi.Modules.Shared.Services
{
    public interface IForgotEmailService
    {
        Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string resetLink, string recipientName = "User", string language = "en");
        Task<bool> SendOtpEmailAsync(string recipientEmail, string otpCode, string recipientName = "User", string language = "en");
        Task<bool> SendEmailVerificationAsync(string recipientEmail, string otpCode, string recipientName = "User", string language = "en", int expiryMinutes = 10);
    }

    /// <summary>
    /// Service for sending password reset emails via OVH SMTP
    /// Uses: testadminsupportgermararaza@spadadibattaglia.com
    /// SMTP: ssl0.ovh.net:465 (SSL)
    /// </summary>
    public class ForgotEmailService : IForgotEmailService
    {
        private readonly ILogger<ForgotEmailService> _logger;
        private readonly IConfiguration _configuration;

        // OVH SMTP Configuration
        private const string SMTP_HOST = "ssl0.ovh.net";
        private const int SMTP_PORT = 465;
        private const string SMTP_USERNAME = "support@flowentra.app";
        private const string SMTP_PASSWORD = "Zaleyo2026";
        private const bool USE_SSL = true;

        public ForgotEmailService(
            ILogger<ForgotEmailService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Sends a password reset email with HTML template
        /// </summary>
        /// <param name="recipientEmail">Email address of the user requesting password reset</param>
        /// <param name="resetLink">Full URL link to reset password (e.g., https://localhost:3000/reset-password?token=xxx)</param>
        /// <param name="recipientName">Name of the user (optional, defaults to "User")</param>
        /// <param name="language">Email language ("en" for English, "fr" for French, defaults to "en")</param>
        /// <returns>True if email sent successfully, false otherwise</returns>
        public async Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string resetLink, string recipientName = "User", string language = "en")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recipientEmail) || string.IsNullOrWhiteSpace(resetLink))
                {
                    _logger.LogWarning("Invalid email or reset link provided for password reset");
                    return false;
                }

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Flowentra Support", SMTP_USERNAME));
                email.To.Add(new MailboxAddress(recipientName, recipientEmail));
                
                // Generate subject based on language
                string subject = language.ToLower() == "fr" 
                    ? "Réinitialisez votre mot de passe - Flowentra"
                    : "Reset Your Password - Flowentra";
                
                email.Subject = subject;

                // Create HTML body
                var htmlBody = GeneratePasswordResetEmailHtml(recipientName, resetLink, language);

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = language.ToLower() == "fr"
                        ? $"Cliquez sur le lien pour réinitialiser votre mot de passe: {resetLink}"
                        : $"Click the link to reset your password: {resetLink}"
                };

                email.Body = bodyBuilder.ToMessageBody();

                // Send via SMTP
                using (var client = new SmtpClient())
                {
                    // Connect with SSL
                    await client.ConnectAsync(SMTP_HOST, SMTP_PORT, USE_SSL);

                    // Authenticate
                    await client.AuthenticateAsync(SMTP_USERNAME, SMTP_PASSWORD);

                    // Send email
                    await client.SendAsync(email);

                    // Disconnect gracefully
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation($"Password reset email sent successfully to {recipientEmail} (Language: {language})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send password reset email to {recipientEmail}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates professional HTML email template for password reset (Multilingual)
        /// </summary>
        private string GeneratePasswordResetEmailHtml(string recipientName, string resetLink, string language = "en")
        {
            if (language.ToLower() == "fr")
            {
                // French Version
                return $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Réinitialiser votre mot de passe</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Helvetica Neue', sans-serif;
            background-color: #f8f9fa;
            color: #1a1a1a;
            line-height: 1.6;
        }}
        .wrapper {{
            width: 100%;
            background-color: #f8f9fa;
            padding: 40px 0;
        }}
        .container {{
            max-width: 580px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
            overflow: hidden;
        }}
        .header {{
            padding: 32px 40px;
            border-bottom: 1px solid #e8e8e8;
            display: flex;
            align-items: center;
            gap: 16px;
        }}
        .logo {{
            width: 70px;
            height: 70px;
            flex-shrink: 0;
        }}
        .header-text {{
            flex: 1;
        }}
        .header-text h1 {{
            font-size: 20px;
            font-weight: 600;
            color: #1a1a1a;
            margin: 0;
        }}
        .content {{
            padding: 40px;
        }}
        .greeting {{
            font-size: 16px;
            font-weight: 600;
            color: #1a1a1a;
            margin-bottom: 20px;
        }}
        .description {{
            font-size: 14px;
            color: #555555;
            line-height: 1.8;
            margin-bottom: 32px;
        }}
        .cta-button {{
            display: inline-block;
            background-color: #003d7a;
            color: white;
            padding: 12px 32px;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            font-size: 14px;
            text-align: center;
            margin: 24px 0;
            transition: background-color 0.2s;
        }}
        .cta-button:hover {{
            background-color: #002d5e;
        }}
        .link-section {{
            margin: 24px 0;
            padding: 16px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border-left: 4px solid #003d7a;
        }}
        .link-label {{
            font-size: 12px;
            color: #888888;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
        }}
        .link-text {{
            font-size: 12px;
            color: #003d7a;
            word-break: break-all;
            font-family: 'Courier New', monospace;
        }}
        .warning-box {{
            background-color: #fff8e6;
            border-left: 4px solid #f0ad4e;
            padding: 16px;
            border-radius: 6px;
            margin: 24px 0;
            font-size: 13px;
            color: #7d6608;
            line-height: 1.6;
        }}
        .warning-box strong {{
            display: block;
            margin-bottom: 8px;
        }}
        .footer {{
            padding: 32px 40px;
            background-color: #f8f9fa;
            border-top: 1px solid #e8e8e8;
            text-align: center;
        }}
        .footer-text {{
            font-size: 12px;
            color: #888888;
            margin: 4px 0;
            line-height: 1.6;
        }}
        .divider {{
            height: 1px;
            background-color: #e8e8e8;
            margin: 16px 0;
        }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <img src='https://www.flowentra.io/assets/flowentra-logo-C6CB7Ftw.png' alt='Flowentra' class='logo' />
            </div>

            <div class='content'>
                <div class='greeting'>Bonjour {recipientName},</div></div>
                
                <div class='description'>
                    Nous avons reçu une demande de réinitialisation du mot de passe associé à votre compte Flowentra. Cliquez sur le bouton ci-dessous pour créer un nouveau mot de passe sécurisé.
                </div>

                <center>
                    <a href='{resetLink}' class='cta-button'>Réinitialiser mon mot de passe</a>
                </center>

                <div class='link-section'>
                    <div class='link-label'>Ou copiez ce lien:</div>
                    <div class='link-text'>{resetLink}</div>
                </div>


                <div style='font-size: 13px; color: #555555; margin-top: 24px; line-height: 1.8;'>
                    <strong>Pour votre sécurité :</strong><br/>
                    Ne partagez jamais ce lien avec quiconque. L'équipe Flowentra ne vous demandera jamais votre mot de passe par email.
                </div>
            </div>

            <div class='footer'>
                <div class='footer-text'><strong>Flowentra™</strong> | Gestion intelligente des workflows</div>
                <div class='divider'></div>
                <div class='footer-text'>© {DateTime.Now.Year} Flowentra. Tous les droits réservés.</div>
                <div class='footer-text' style='margin-top: 12px; font-size: 11px;'>Ceci est un message automatisé. Veuillez ne pas répondre à cet email.</div>
            </div>
        </div>
    </div>
</body>
</html>";
            }
            else
            {
                // English Version (Default)
                return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Reset Your Password</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Helvetica Neue', sans-serif;
            background-color: #f8f9fa;
            color: #1a1a1a;
            line-height: 1.6;
        }}
        .wrapper {{
            width: 100%;
            background-color: #f8f9fa;
            padding: 40px 0;
        }}
        .container {{
            max-width: 580px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
            overflow: hidden;
        }}
        .header {{
            padding: 32px 40px;
            border-bottom: 1px solid #e8e8e8;
            display: flex;
            align-items: center;
            gap: 16px;
        }}
        .logo {{
            width: 70px;
            height: 70px;
            flex-shrink: 0;
        }}
        .header-text {{
            flex: 1;
        }}
        .header-text h1 {{
            font-size: 20px;
            font-weight: 600;
            color: #1a1a1a;
            margin: 0;
        }}
        .content {{
            padding: 40px;
        }}
        .greeting {{
            font-size: 16px;
            font-weight: 600;
            color: #1a1a1a;
            margin-bottom: 20px;
        }}
        .description {{
            font-size: 14px;
            color: #555555;
            line-height: 1.8;
            margin-bottom: 32px;
        }}
        .cta-button {{
            display: inline-block;
            background-color: #003d7a;
            color: white;
            padding: 12px 32px;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            font-size: 14px;
            text-align: center;
            margin: 24px 0;
            transition: background-color 0.2s;
        }}
        .cta-button:hover {{
            background-color: #002d5e;
        }}
        .link-section {{
            margin: 24px 0;
            padding: 16px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border-left: 4px solid #003d7a;
        }}
        .link-label {{
            font-size: 12px;
            color: #888888;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
        }}
        .link-text {{
            font-size: 12px;
            color: #003d7a;
            word-break: break-all;
            font-family: 'Courier New', monospace;
        }}
        .warning-box {{
            background-color: #fff8e6;
            border-left: 4px solid #f0ad4e;
            padding: 16px;
            border-radius: 6px;
            margin: 24px 0;
            font-size: 13px;
            color: #7d6608;
            line-height: 1.6;
        }}
        .warning-box strong {{
            display: block;
            margin-bottom: 8px;
        }}
        .footer {{
            padding: 32px 40px;
            background-color: #f8f9fa;
            border-top: 1px solid #e8e8e8;
            text-align: center;
        }}
        .footer-text {{
            font-size: 12px;
            color: #888888;
            margin: 4px 0;
            line-height: 1.6;
        }}
        .divider {{
            height: 1px;
            background-color: #e8e8e8;
            margin: 16px 0;
        }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <img src='https://www.flowentra.io/assets/flowentra-logo-C6CB7Ftw.png' alt='Flowentra' class='logo' />
            </div>

            <div class='content'>
                <div class='greeting'>Hello {recipientName},</div>
                
                <div class='description'>
                    We received a request to reset the password associated with your Flowentra account. Click the button below to create a new secure password.
                </div>

                <center>
                    <a href='{resetLink}' class='cta-button'>Reset My Password</a>
                </center>

                <div class='link-section'>
                    <div class='link-label'>Or copy this link:</div>
                    <div class='link-text'>{resetLink}</div>
                </div>

                <div style='font-size: 13px; color: #555555; margin-top: 24px; line-height: 1.8;'>
                    <strong>For your security:</strong><br/>
                    Never share this link with anyone. The Flowentra team will never ask for your password via email.
                </div>
            </div>

            <div class='footer'>
                <div class='footer-text'><strong>Flowentra™</strong> | Intelligent Workflow Management</div>
                <div class='divider'></div>
                <div class='footer-text'>© {DateTime.Now.Year} Flowentra. All rights reserved.</div>
                <div class='footer-text' style='margin-top: 12px; font-size: 11px;'>This is an automated message. Please do not reply to this email.</div>
            </div>
        </div>
    </div>
</body>
</html>";
            }
        }

        /// <summary>
        /// Sends OTP code email for password reset verification
        /// </summary>
        /// <param name="recipientEmail">Email address to send OTP to</param>
        /// <param name="otpCode">6-digit OTP code</param>
        /// <param name="recipientName">Name of the user (optional)</param>
        /// <param name="language">Email language ("en" for English, "fr" for French, defaults to "en")</param>
        /// <returns>True if email sent successfully, false otherwise</returns>
        public async Task<bool> SendOtpEmailAsync(string recipientEmail, string otpCode, string recipientName = "User", string language = "en")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recipientEmail) || string.IsNullOrWhiteSpace(otpCode))
                {
                    _logger.LogWarning("Invalid email or OTP provided for sending");
                    return false;
                }

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Flowentra Support", SMTP_USERNAME));
                email.To.Add(new MailboxAddress(recipientName, recipientEmail));
                
                // Generate subject based on language
                string subject = language.ToLower() == "fr"
                    ? "Votre code de réinitialisation - Flowentra"
                    : "Your Password Reset Code - Flowentra";
                
                email.Subject = subject;

                // Create HTML body with OTP
                var htmlBody = GenerateOtpEmailHtml(recipientName, otpCode, language);

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = language.ToLower() == "fr"
                        ? $"Votre code OTP est: {otpCode}. Ce code expirera dans 5 minutes."
                        : $"Your OTP code is: {otpCode}. This code will expire in 5 minutes."
                };

                email.Body = bodyBuilder.ToMessageBody();

                // Send via SMTP
                using (var client = new SmtpClient())
                {
                    // Connect with SSL
                    await client.ConnectAsync(SMTP_HOST, SMTP_PORT, USE_SSL);

                    // Authenticate
                    await client.AuthenticateAsync(SMTP_USERNAME, SMTP_PASSWORD);

                    // Send email
                    await client.SendAsync(email);

                    // Disconnect gracefully
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation($"OTP email sent successfully to {recipientEmail} (Language: {language})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send OTP email to {recipientEmail}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates professional HTML email template for OTP verification (Multilingual)
        /// </summary>
        private string GenerateOtpEmailHtml(string recipientName, string otpCode, string language = "en")
        {
            if (language.ToLower() == "fr")
            {
                // French Version
                return $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Votre code de réinitialisation</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Helvetica Neue', sans-serif;
            background-color: #f8f9fa;
            color: #1a1a1a;
            line-height: 1.6;
        }}
        .wrapper {{
            width: 100%;
            background-color: #f8f9fa;
            padding: 40px 0;
        }}
        .container {{
            max-width: 580px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
            overflow: hidden;
        }}
        .header {{
            padding: 32px 40px;
            border-bottom: 1px solid #e8e8e8;
            display: flex;
            align-items: center;
            gap: 16px;
        }}
        .logo {{
            width: 70px;
            height: 70px;
            flex-shrink: 0;
        }}
        .header-text {{
            flex: 1;
        }}
        .header-text h1 {{
            font-size: 20px;
            font-weight: 600;
            color: #1a1a1a;
            margin: 0;
        }}
        .content {{
            padding: 40px;
        }}
        .greeting {{
            font-size: 16px;
            font-weight: 600;
            color: #1a1a1a;
            margin-bottom: 20px;
        }}
        .description {{
            font-size: 14px;
            color: #555555;
            line-height: 1.8;
            margin-bottom: 32px;
        }}
        .otp-container {{
            background: linear-gradient(135deg, #f8f9fa 0%, #ffffff 100%);
            border: 2px solid #e8e8e8;
            border-radius: 8px;
            padding: 32px;
            text-align: center;
            margin: 32px 0;
        }}
        .otp-label {{
            font-size: 12px;
            color: #888888;
            text-transform: uppercase;
            letter-spacing: 1px;
            margin-bottom: 16px;
            font-weight: 600;
        }}
        .otp-code {{
            font-size: 48px;
            font-weight: 700;
            color: #003d7a;
            letter-spacing: 12px;
            font-family: 'Courier New', monospace;
            margin: 16px 0;
            word-spacing: 8px;
        }}
        .otp-note {{
            font-size: 13px;
            color: #888888;
            margin-top: 12px;
        }}
        .warning-box {{
            background-color: #fff8e6;
            border-left: 4px solid #f0ad4e;
            padding: 16px;
            border-radius: 6px;
            margin: 24px 0;
            font-size: 13px;
            color: #7d6608;
            line-height: 1.6;
        }}
        .warning-box strong {{
            display: block;
            margin-bottom: 8px;
        }}
        .footer {{
            padding: 32px 40px;
            background-color: #f8f9fa;
            border-top: 1px solid #e8e8e8;
            text-align: center;
        }}
        .footer-text {{
            font-size: 12px;
            color: #888888;
            margin: 4px 0;
            line-height: 1.6;
        }}
        .divider {{
            height: 1px;
            background-color: #e8e8e8;
            margin: 16px 0;
        }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <img src='https://www.flowentra.io/assets/flowentra-logo-C6CB7Ftw.png' alt='Flowentra' class='logo' />
               
            </div>

            <div class='content'>
                <div class='greeting'>Bonjour {recipientName},</div>
                
                <div class='description'>
                    Vous avez demandé la réinitialisation du mot de passe de votre compte Flowentra. Veuillez entrer le code de vérification ci-dessous.
                </div>

                <div class='otp-container'>
                    <div class='otp-label'>Votre code de vérification</div>
                    <div class='otp-code'>{otpCode}</div>
                    <div class='otp-note'>Ce code expire dans 5 minutes</div>
                </div>

                <div style='font-size: 13px; color: #555555; line-height: 1.8;'>
                    Si vous n'avez pas demandé une réinitialisation de mot de passe, vous pouvez ignorer ce message. Votre mot de passe restera inchangé.
                </div>
            </div>

            <div class='footer'>
                <div class='footer-text'><strong>Flowentra™</strong> | Gestion intelligente des workflows</div>
                <div class='divider'></div>
                <div class='footer-text'>© {DateTime.Now.Year} Flowentra. Tous les droits réservés.</div>
                <div class='footer-text' style='margin-top: 12px; font-size: 11px;'>Ceci est un message automatisé. Veuillez ne pas répondre à cet email.</div>
            </div>
        </div>
    </div>
</body>
</html>";
            }
            else
            {
                // English Version (Default)
                return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Your Password Reset Code</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Helvetica Neue', sans-serif;
            background-color: #f8f9fa;
            color: #1a1a1a;
            line-height: 1.6;
        }}
        .wrapper {{
            width: 100%;
            background-color: #f8f9fa;
            padding: 40px 0;
        }}
        .container {{
            max-width: 580px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
            overflow: hidden;
        }}
        .header {{
            padding: 32px 40px;
            border-bottom: 1px solid #e8e8e8;
            display: flex;
            align-items: center;
            gap: 16px;
        }}
        .logo {{
            width: 70px;
            height: 70px;
            flex-shrink: 0;
        }}
        .header-text {{
            flex: 1;
        }}
        .header-text h1 {{
            font-size: 20px;
            font-weight: 600;
            color: #1a1a1a;
            margin: 0;
        }}
        .content {{
            padding: 40px;
        }}
        .greeting {{
            font-size: 16px;
            font-weight: 600;
            color: #1a1a1a;
            margin-bottom: 20px;
        }}
        .description {{
            font-size: 14px;
            color: #555555;
            line-height: 1.8;
            margin-bottom: 32px;
        }}
        .otp-container {{
            background: linear-gradient(135deg, #f8f9fa 0%, #ffffff 100%);
            border: 2px solid #e8e8e8;
            border-radius: 8px;
            padding: 32px;
            text-align: center;
            margin: 32px 0;
        }}
        .otp-label {{
            font-size: 12px;
            color: #888888;
            text-transform: uppercase;
            letter-spacing: 1px;
            margin-bottom: 16px;
            font-weight: 600;
        }}
        .otp-code {{
            font-size: 48px;
            font-weight: 700;
            color: #003d7a;
            letter-spacing: 12px;
            font-family: 'Courier New', monospace;
            margin: 16px 0;
            word-spacing: 8px;
        }}
        .otp-note {{
            font-size: 13px;
            color: #888888;
            margin-top: 12px;
        }}
        .warning-box {{
            background-color: #fff8e6;
            border-left: 4px solid #f0ad4e;
            padding: 16px;
            border-radius: 6px;
            margin: 24px 0;
            font-size: 13px;
            color: #7d6608;
            line-height: 1.6;
        }}
        .warning-box strong {{
            display: block;
            margin-bottom: 8px;
        }}
        .footer {{
            padding: 32px 40px;
            background-color: #f8f9fa;
            border-top: 1px solid #e8e8e8;
            text-align: center;
        }}
        .footer-text {{
            font-size: 12px;
            color: #888888;
            margin: 4px 0;
            line-height: 1.6;
        }}
        .divider {{
            height: 1px;
            background-color: #e8e8e8;
            margin: 16px 0;
        }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <img src='https://www.flowentra.io/assets/flowentra-logo-C6CB7Ftw.png' alt='Flowentra' class='logo' />
            </div>

            <div class='content'>
                <div class='greeting'>Hello {recipientName},</div>
                
                <div class='description'>
                    You requested to reset your Flowentra account password. Please enter the verification code below.
                </div>

                <div class='otp-container'>
                    <div class='otp-label'>Your Verification Code</div>
                    <div class='otp-code'>{otpCode}</div>
                    <div class='otp-note'>This code expires in 5 minutes</div>
                </div>

                <div style='font-size: 13px; color: #555555; line-height: 1.8;'>
                    If you didn't request a password reset, you can safely ignore this message. Your password will remain unchanged.
                </div>
            </div>

            <div class='footer'>
                <div class='footer-text'><strong>Flowentra™</strong> | Intelligent Workflow Management</div>
                <div class='divider'></div>
                <div class='footer-text'>© {DateTime.Now.Year} Flowentra. All rights reserved.</div>
                <div class='footer-text' style='margin-top: 12px; font-size: 11px;'>This is an automated message. Please do not reply to this email.</div>
            </div>
        </div>
    </div>
</body>
</html>";
            }
        }

        // =====================================================
        // Email Verification (dedicated template, not password reset)
        // =====================================================
        public async Task<bool> SendEmailVerificationAsync(string recipientEmail, string otpCode, string recipientName = "User", string language = "en", int expiryMinutes = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recipientEmail) || string.IsNullOrWhiteSpace(otpCode))
                {
                    _logger.LogWarning("Invalid email or code provided for email verification");
                    return false;
                }

                var isFr = string.Equals(language, "fr", StringComparison.OrdinalIgnoreCase);

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress("Flowentra", SMTP_USERNAME));
                email.To.Add(new MailboxAddress(recipientName, recipientEmail));

                email.Subject = isFr
                    ? "Vérifiez votre adresse e-mail - Flowentra"
                    : "Verify your email address - Flowentra";

                // Anti-spam / deliverability headers
                email.Headers.Add("X-Entity-Ref-ID", Guid.NewGuid().ToString());
                email.Headers.Add("X-Auto-Response-Suppress", "All");
                email.Headers.Add("Auto-Submitted", "auto-generated");
                email.Headers.Add("List-Unsubscribe", $"<mailto:{SMTP_USERNAME}?subject=unsubscribe>");
                email.Headers.Add("Precedence", "transactional");

                var htmlBody = GenerateEmailVerificationHtml(recipientName, otpCode, isFr, expiryMinutes);

                var textBody = isFr
                    ? $"Bonjour {recipientName},\n\nVotre code de vérification Flowentra est : {otpCode}\nCe code expire dans {expiryMinutes} minutes.\n\nSi vous n'avez pas demandé ce code, ignorez cet e-mail.\n\n— L'équipe Flowentra"
                    : $"Hello {recipientName},\n\nYour Flowentra verification code is: {otpCode}\nThis code expires in {expiryMinutes} minutes.\n\nIf you did not request this code, please ignore this email.\n\n— The Flowentra Team";

                email.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(SMTP_HOST, SMTP_PORT, USE_SSL);
                    await client.AuthenticateAsync(SMTP_USERNAME, SMTP_PASSWORD);
                    await client.SendAsync(email);
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation("Verification email sent to {Email} (lang={Lang})", recipientEmail, isFr ? "fr" : "en");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}: {Msg}", recipientEmail, ex.Message);
                return false;
            }
        }

        private string GenerateEmailVerificationHtml(string recipientName, string otpCode, bool isFr, int expiryMinutes)
        {
            var title = isFr ? "Vérifiez votre e-mail" : "Verify your email";
            var preheader = isFr
                ? $"Votre code Flowentra : {otpCode}. Valide {expiryMinutes} minutes."
                : $"Your Flowentra code: {otpCode}. Valid for {expiryMinutes} minutes.";
            var greeting = isFr ? $"Bonjour {recipientName}," : $"Hello {recipientName},";
            var intro = isFr
                ? "Merci de rejoindre Flowentra. Pour confirmer votre adresse e-mail, saisissez le code ci-dessous dans l'application :"
                : "Thanks for joining Flowentra. To confirm your email address, enter the code below in the app:";
            var codeLabel = isFr ? "Votre code de vérification" : "Your verification code";
            var expiryNote = isFr
                ? $"Ce code expire dans {expiryMinutes} minutes."
                : $"This code expires in {expiryMinutes} minutes.";
            var securityHeading = isFr ? "Vous n'avez pas demandé ce code ?" : "Didn't request this code?";
            var securityText = isFr
                ? "Vous pouvez ignorer cet e-mail en toute sécurité. Personne d'autre ne peut accéder à votre compte sans ce code."
                : "You can safely ignore this email. Nobody can access your account without this code.";
            var footerLegal = isFr
                ? $"Message automatique. © {DateTime.Now.Year} Flowentra. Tous droits réservés."
                : $"Automated message. © {DateTime.Now.Year} Flowentra. All rights reserved.";
            var footerNoReply = isFr
                ? "Cet e-mail a été envoyé automatiquement, merci de ne pas y répondre."
                : "This is an automated message, please do not reply.";

            var lang = isFr ? "fr" : "en";
            return $@"<!DOCTYPE html>
<html lang='{lang}'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <meta name='color-scheme' content='light'>
  <meta name='supported-color-schemes' content='light'>
  <title>{title}</title>
</head>
<body style='margin:0;padding:0;background:#f4f6fb;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1a1f2c;'>
  <span style='display:none!important;visibility:hidden;opacity:0;color:transparent;height:0;width:0;overflow:hidden;mso-hide:all;'>{preheader}</span>
  <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background:#f4f6fb;padding:32px 12px;'>
    <tr><td align='center'>
      <table role='presentation' width='560' cellspacing='0' cellpadding='0' border='0' style='max-width:560px;width:100%;background:#ffffff;border-radius:14px;box-shadow:0 6px 24px rgba(20,30,60,0.08);overflow:hidden;'>
        <tr><td style='background:linear-gradient(135deg,#4f46e5 0%,#7c3aed 100%);padding:28px 32px;text-align:center;color:#fff;'>
          <div style='font-size:22px;font-weight:700;letter-spacing:.3px;'>Flowentra</div>
          <div style='font-size:14px;opacity:.9;margin-top:4px;'>{title}</div>
        </td></tr>
        <tr><td style='padding:32px;'>
          <p style='margin:0 0 8px;font-size:16px;font-weight:600;'>{greeting}</p>
          <p style='margin:0 0 20px;font-size:14px;line-height:1.6;color:#4a4f5c;'>{intro}</p>
          <div style='margin:24px 0;text-align:center;background:#f7f8fc;border:1px solid #e6e9f2;border-radius:12px;padding:22px 12px;'>
            <div style='font-size:12px;letter-spacing:2px;color:#6b7280;text-transform:uppercase;font-weight:600;'>{codeLabel}</div>
            <div style='font-size:38px;font-weight:800;letter-spacing:10px;color:#4f46e5;margin-top:10px;font-family:Menlo,Consolas,monospace;'>{otpCode}</div>
            <div style='font-size:12px;color:#6b7280;margin-top:8px;'>{expiryNote}</div>
          </div>
          <div style='margin-top:20px;padding:14px 16px;background:#fff7ed;border-left:3px solid #f59e0b;border-radius:8px;'>
            <div style='font-size:13px;font-weight:600;color:#92400e;margin-bottom:4px;'>{securityHeading}</div>
            <div style='font-size:13px;color:#78350f;line-height:1.5;'>{securityText}</div>
          </div>
        </td></tr>
        <tr><td style='padding:20px 32px;background:#fafbff;border-top:1px solid #eef1f8;text-align:center;'>
          <div style='font-size:12px;color:#6b7280;'>{footerNoReply}</div>
          <div style='font-size:11px;color:#9ca3af;margin-top:6px;'>{footerLegal}</div>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}
