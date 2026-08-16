using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dispatches.Models
{
    [ModuleScope("dispatches")]
    [Table("Expenses")]
    public class Expense : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DispatchId")]
        public int DispatchId { get; set; }

        // Which job of a multi-job dispatch this expense belongs to (null = whole dispatch / legacy).
        [Column("ServiceOrderJobId")]
        public int? ServiceOrderJobId { get; set; }

        // Denormalized installation the expense rolls up to (see TimeEntry.InstallationId).
        [Column("InstallationId")]
        public int? InstallationId { get; set; }

        [Required]
        [Column("ExpenseType")]
        [MaxLength(50)]
        public string ExpenseType { get; set; } = string.Empty;

        [Column("TechnicianId")]
        [MaxLength(50)]
        public string? TechnicianId { get; set; }

        [Required]
        [Column("Amount", TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        // ISO-like currency code (TND, EUR, USD, ...). Nullable so legacy rows
        // (pre-currency migration) stay valid and are interpreted as the sale's
        // currency at invoice-prep time. See PrepareForInvoiceAsync currency guard.
        [Column("Currency")]
        [MaxLength(10)]
        public string? Currency { get; set; }

        [Column("Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column("ExpenseDate")]
        public DateTime ExpenseDate { get; set; }

        [Column("ReceiptPath")]
        [MaxLength(500)]
        public string? ReceiptPath { get; set; }

        [Required]
        [Column("RecordedBy")]
        [MaxLength(100)]
        public string RecordedBy { get; set; } = string.Empty;

        [Required]
        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Column("OverrunFlag")]
        public bool OverrunFlag { get; set; } = false;

        [Column("OverrunReason")]
        [MaxLength(500)]
        public string? OverrunReason { get; set; }
    }
}
