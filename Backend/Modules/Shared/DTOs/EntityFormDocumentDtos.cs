using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Shared.DTOs
{
    /// <summary>
    /// DTO for reading entity form document data
    /// </summary>
    public class EntityFormDocumentDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public int FormId { get; set; }
        public int FormVersion { get; set; }
        public string FormNameEn { get; set; } = string.Empty;
        public string FormNameFr { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Status { get; set; } = "draft";
        public Dictionary<string, object> Responses { get; set; } = new();
        public string? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for creating a new entity form document
    /// </summary>
    public class CreateEntityFormDocumentDto
    {
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;

        [Required]
        public int EntityId { get; set; }

        [Required]
        public int FormId { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        /// <summary>
        /// Optional status - defaults to Draft if not provided
        /// </summary>
        public string? Status { get; set; }

        public Dictionary<string, object>? Responses { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing entity form document
    /// </summary>
    public class UpdateEntityFormDocumentDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        public string? Status { get; set; }

        public Dictionary<string, object>? Responses { get; set; }
    }
}
