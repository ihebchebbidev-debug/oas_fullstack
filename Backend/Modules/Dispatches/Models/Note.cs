using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("Notes")]
    public class Note : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchId")]
        public int DispatchId { get; set; }

        [Required]
        [Column("NoteText")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Column("NoteType")]
        [MaxLength(50)]
        public string NoteType { get; set; } = "general";

        [Column("IsInternal")]
        public bool IsInternal { get; set; }

        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("CreatedBy")]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
    }
}
