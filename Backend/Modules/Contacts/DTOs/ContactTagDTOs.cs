using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Contacts.DTOs
{
    public class ContactTagDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public int ContactCount { get; set; } = 0;
    }

    public class CreateContactTagRequestDto
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(7)]
        public string Color { get; set; } = "#3b82f6";
    }

    public class UpdateContactTagRequestDto
    {
        [StringLength(50)]
        public string? Name { get; set; }

        [StringLength(7)]
        public string? Color { get; set; }
    }

    public class ContactTagListResponseDto
    {
        public List<ContactTagDto> Tags { get; set; } = new List<ContactTagDto>();
        public int TotalCount { get; set; }
    }

    public class AssignTagToContactRequestDto
    {
        [Required]
        public int ContactId { get; set; }

        [Required]
        public int TagId { get; set; }
    }
}
