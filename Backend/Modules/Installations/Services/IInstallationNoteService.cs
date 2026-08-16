using MyApi.Modules.Installations.DTOs;

namespace MyApi.Modules.Installations.Services
{
    public interface IInstallationNoteService
    {
        Task<InstallationNoteListResponseDto> GetNotesByInstallationIdAsync(int installationId);
        Task<InstallationNoteDto?> GetNoteByIdAsync(int id);
        Task<InstallationNoteDto> CreateNoteAsync(CreateInstallationNoteRequestDto createDto, string createdByUser);
        Task<InstallationNoteDto?> UpdateNoteAsync(int id, UpdateInstallationNoteRequestDto updateDto);
        Task<bool> DeleteNoteAsync(int id);
    }
}
