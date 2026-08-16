using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Signatures.Models;

namespace MyApi.Modules.Signatures.Services
{
    public class SignatureService : ISignatureService
    {
        private readonly ApplicationDbContext _context;

        public SignatureService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetSignatureUrlAsync(int userId)
        {
            var signature = await _context.Set<UserSignature>()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            return signature?.SignatureUrl;
        }

        public async Task<string> SaveSignatureAsync(int userId, string signatureUrl)
        {
            var existing = await _context.Set<UserSignature>()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (existing != null)
            {
                existing.SignatureUrl = signatureUrl;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.Set<UserSignature>().Add(new UserSignature
                {
                    UserId = userId,
                    SignatureUrl = signatureUrl,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return signatureUrl;
        }

        public async Task<bool> DeleteSignatureAsync(int userId)
        {
            var existing = await _context.Set<UserSignature>()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (existing == null) return false;

            _context.Set<UserSignature>().Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
