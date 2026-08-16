namespace MyApi.Modules.Contacts.DTOs
{
    public class ContactActivityDto
    {
        public int Id { get; set; }
        public int ContactId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? Description { get; set; }
        public string? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class ContactActivityListResponseDto
    {
        public List<ContactActivityDto> Activities { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
