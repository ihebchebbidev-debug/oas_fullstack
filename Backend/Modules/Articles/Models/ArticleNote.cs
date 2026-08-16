using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Articles.Models
{
    [ModuleScope("articles")]
    [Table("ArticleNotes")]
    public class ArticleNote : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ArticleId { get; set; }

        [Required]
        [Column(TypeName = "text")]
        public string Note { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
    }
}