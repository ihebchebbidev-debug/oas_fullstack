using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Installations.DTOs
{
    public class InstallationNoteDto
    {
        public int Id { get; set; }
        public int InstallationId { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class CreateInstallationNoteRequestDto
    {
        [Required]
        public int InstallationId { get; set; }

        [Required]
        public string Note { get; set; } = string.Empty;
    }

    public class UpdateInstallationNoteRequestDto
    {
        [Required]
        public string Note { get; set; } = string.Empty;
    }

    public class InstallationNoteListResponseDto
    {
        public List<InstallationNoteDto> Notes { get; set; } = new List<InstallationNoteDto>();
        public int TotalCount { get; set; }
    }
}
