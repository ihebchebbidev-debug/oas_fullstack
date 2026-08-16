using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WebsiteBuilder.Models
{
    [Table("WB_Templates")]
    public class WBTemplate : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Description { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = "general";

        [MaxLength(2000)]
        public string? PreviewImageUrl { get; set; }

        [Required]
        [Column("ThemeJson", TypeName = "jsonb")]
        public string ThemeJson { get; set; } = "{}";

        [Required]
        [Column("PagesJson", TypeName = "jsonb")]
        public string PagesJson { get; set; } = "[]";

        [Column(TypeName = "text")]
        public string? Tags { get; set; }

        public bool IsPremium { get; set; } = false;

        public bool IsBuiltIn { get; set; } = false;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = "system";

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        [MaxLength(100)]
        public string? DeletedBy { get; set; }
    }
}
