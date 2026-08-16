using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("DispatchJobs")]
    public class DispatchJob : ITenantEntity
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
        [Column("JobId")]
        public int JobId { get; set; }

        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("DeletedAt")]
        public DateTime? DeletedAt { get; set; }

        [Column("DeletedBy")]
        [MaxLength(100)]
        public string? DeletedBy { get; set; }
    }
}
