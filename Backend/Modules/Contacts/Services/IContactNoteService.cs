using MyApi.Modules.Contacts.DTOs;

namespace MyApi.Modules.Contacts.Services
{
    public interface IContactNoteService
    {
        Task<ContactNoteListResponseDto> GetNotesByContactIdAsync(int contactId);
        Task<ContactNoteDto?> GetNoteByIdAsync(int id);
        Task<ContactNoteDto> CreateNoteAsync(CreateContactNoteRequestDto createDto, string createdByUser);
        Task<ContactNoteDto?> UpdateNoteAsync(int id, UpdateContactNoteRequestDto updateDto);
        Task<bool> DeleteNoteAsync(int id);
    }
}
