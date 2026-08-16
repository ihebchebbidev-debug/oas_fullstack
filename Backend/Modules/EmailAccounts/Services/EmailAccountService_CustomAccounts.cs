using MyApi.Modules.EmailAccounts.DTOs;
using MyApi.Modules.EmailAccounts.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.EmailAccounts.Services
{
    public partial class EmailAccountService
    {
        public async Task<IEnumerable<CustomEmailAccountDto>> GetCustomAccountsByUserAsync(int userId)
        {
            try
            {
                var list = await _context.Set<CustomEmailAccount>()
                    .Where(c => c.UserId == userId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return list.Select(MapCustomToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get custom accounts for user {UserId}", userId);
                throw;
            }
        }

        public async Task<CustomEmailAccountDto?> GetCustomAccountByIdAsync(Guid id, int userId)
        {
            var item = await _context.Set<CustomEmailAccount>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            return item == null ? null : MapCustomToDto(item);
        }

        public async Task<CustomEmailAccountDto> CreateCustomAccountAsync(int userId, CreateCustomEmailAccountDto dto)
        {
            var cfg = dto.Config;
            var model = new CustomEmailAccount
            {
                UserId = userId,
                Email = cfg.Email,
                DisplayName = dto.DisplayName ?? cfg.DisplayName,
                SmtpServer = cfg.SmtpServer,
                SmtpPort = cfg.SmtpPort,
                SmtpSecurity = cfg.SmtpSecurity,
                ImapServer = cfg.ImapServer,
                ImapPort = cfg.ImapPort,
                ImapSecurity = cfg.ImapSecurity,
                Pop3Server = cfg.Pop3Server,
                Pop3Port = cfg.Pop3Port,
                Pop3Security = cfg.Pop3Security,
                EncryptedPassword = string.IsNullOrEmpty(cfg.Password) ? null : Convert.ToBase64String(_protector.Protect(System.Text.Encoding.UTF8.GetBytes(cfg.Password))),
                IsActive = true,
            };

            _context.Set<CustomEmailAccount>().Add(model);
            await _context.SaveChangesAsync();

                // Ensure a ConnectedEmailAccount record exists so synced emails can reference it
                var existingConn = await _context.ConnectedEmailAccounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Handle == model.Email && a.Provider == "custom");
                if (existingConn == null)
                {
                    var conn = new ConnectedEmailAccount
                    {
                        UserId = userId,
                        Handle = model.Email,
                        Provider = "custom",
                        AccessToken = "",
                        RefreshToken = "",
                        SyncStatus = "not_synced",
                        LastSyncedAt = null,
                        EmailVisibility = "share_everything",
                        CalendarVisibility = "share_everything",
                    };
                    _context.ConnectedEmailAccounts.Add(conn);
                    await _context.SaveChangesAsync();
                }

                return MapCustomToDto(model);
        }

        public async Task<CustomEmailAccountDto?> UpdateCustomAccountAsync(Guid id, int userId, CreateCustomEmailAccountDto dto)
        {
            var item = await _context.Set<CustomEmailAccount>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (item == null) return null;

            var cfg = dto.Config;
            item.DisplayName = dto.DisplayName ?? cfg.DisplayName ?? item.DisplayName;
            item.SmtpServer = cfg.SmtpServer ?? item.SmtpServer;
            item.SmtpPort = cfg.SmtpPort ?? item.SmtpPort;
            item.SmtpSecurity = cfg.SmtpSecurity ?? item.SmtpSecurity;
            item.ImapServer = cfg.ImapServer ?? item.ImapServer;
            item.ImapPort = cfg.ImapPort ?? item.ImapPort;
            item.ImapSecurity = cfg.ImapSecurity ?? item.ImapSecurity;
            item.Pop3Server = cfg.Pop3Server ?? item.Pop3Server;
            item.Pop3Port = cfg.Pop3Port ?? item.Pop3Port;
            item.Pop3Security = cfg.Pop3Security ?? item.Pop3Security;
            if (!string.IsNullOrEmpty(cfg.Password)) item.EncryptedPassword = Convert.ToBase64String(_protector.Protect(System.Text.Encoding.UTF8.GetBytes(cfg.Password)));
            item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapCustomToDto(item);
        }

        public async Task<bool> DeleteCustomAccountAsync(Guid id, int userId)
        {
            var item = await _context.Set<CustomEmailAccount>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (item == null) return false;
            _context.Set<CustomEmailAccount>().Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        // Utility mapping
        private CustomEmailAccountDto MapCustomToDto(CustomEmailAccount c)
        {
            Guid? connectedId = null;
            var connected = _context.ConnectedEmailAccounts.FirstOrDefault(a => a.Handle == c.Email && a.Provider == "custom");
            if (connected != null) connectedId = connected.Id;

            return new CustomEmailAccountDto
            {
                Id = c.Id,
                UserId = c.UserId,
                Email = c.Email,
                DisplayName = c.DisplayName,
                SmtpServer = c.SmtpServer,
                SmtpPort = c.SmtpPort,
                SmtpSecurity = c.SmtpSecurity,
                ImapServer = c.ImapServer,
                ImapPort = c.ImapPort,
                ImapSecurity = c.ImapSecurity,
                Pop3Server = c.Pop3Server,
                Pop3Port = c.Pop3Port,
                Pop3Security = c.Pop3Security,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                ConnectedAccountId = connectedId
            };
        }
    }
}
