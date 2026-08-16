namespace MyApi.Modules.Signatures.Services
{
    public interface ISignatureService
    {
        Task<string?> GetSignatureUrlAsync(int userId);
        Task<string> SaveSignatureAsync(int userId, string signatureUrl);
        Task<bool> DeleteSignatureAsync(int userId);
    }
}
