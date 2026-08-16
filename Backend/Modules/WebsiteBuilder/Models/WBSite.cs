using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.WebsiteBuilder.Models
{
    [Table("WB_Sites")]
    public class WBSite : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? Favicon { get; set; }

        [Required]
        [Column("ThemeJson", TypeName = "jsonb")]
        public string ThemeJson { get; set; } = "{}";

        public bool Published { get; set; } = false;

        public DateTime? PublishedAt { get; set; }

        [MaxLength(500)]
        public string? PublishedUrl { get; set; }

        [MaxLength(10)]
        public string? DefaultLanguage { get; set; } = "en";

        [Column("LanguagesJson", TypeName = "jsonb")]
        public string? LanguagesJson { get; set; }

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

        // Navigation properties
        public virtual ICollection<WBPage> Pages { get; set; } = new List<WBPage>();
        public virtual ICollection<WBFormSubmission> FormSubmissions { get; set; } = new List<WBFormSubmission>();
        public virtual ICollection<WBMedia> Media { get; set; } = new List<WBMedia>();
        public virtual ICollection<WBActivityLog> ActivityLogs { get; set; } = new List<WBActivityLog>();
    }
}
