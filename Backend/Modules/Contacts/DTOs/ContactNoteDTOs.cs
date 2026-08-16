using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Contacts.DTOs
{
    public class ContactNoteDto
    {
        public int Id { get; set; }
        public int ContactId { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class CreateContactNoteRequestDto
    {
        [Required]
        public int ContactId { get; set; }

        [Required]
        public string Note { get; set; } = string.Empty;
    }

    public class UpdateContactNoteRequestDto
    {
        [Required]
        public string Note { get; set; } = string.Empty;
    }

    public class ContactNoteListResponseDto
    {
        public List<ContactNoteDto> Notes { get; set; } = new List<ContactNoteDto>();
        public int TotalCount { get; set; }
    }
}
