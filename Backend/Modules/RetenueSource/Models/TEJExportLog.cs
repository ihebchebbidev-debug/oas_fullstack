using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.RetenueSource.Models
{
    [Table("TEJExportLogs")]
    public class TEJExportLog : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("FileName")]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [Column("ExportDate")]
        public DateTime ExportDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("ExportedBy")]
        [MaxLength(100)]
        public string ExportedBy { get; set; } = string.Empty;

        [Required]
        [Column("Month")]
        public int Month { get; set; }

        [Required]
        [Column("Year")]
        public int Year { get; set; }

        [Column("RecordCount")]
        public int RecordCount { get; set; }

        [Column("TotalRSAmount", TypeName = "decimal(15,2)")]
        public decimal TotalRSAmount { get; set; }

        [Required]
        [Column("Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "success"; // success, error

        [Column("ErrorMessage")]
        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Optional: Document ID reference if TEJ XML is stored as a Document
        /// </summary>
        [Column("DocumentId")]
        public int? DocumentId { get; set; }
    }
}
