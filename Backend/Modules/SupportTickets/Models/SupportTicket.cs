using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.SupportTickets.Models
{
    public class SupportTicket : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Urgency { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }

        [StringLength(500)]
        public string? CurrentPage { get; set; }

        [StringLength(1000)]
        public string? RelatedUrl { get; set; }

        [Required]
        [StringLength(100)]
        public string Tenant { get; set; } = string.Empty;

        [StringLength(255)]
        public string? UserEmail { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(20)]
        public string Source { get; set; } = "manual";

        [StringLength(64)]
        public string? ErrorFingerprint { get; set; }

        public int? SystemLogId { get; set; }

        public int OccurrenceCount { get; set; } = 1;

        public DateTime? LastOccurredAt { get; set; }

        [StringLength(50)]
        public string? IncidentType { get; set; }

        [StringLength(100)]
        public string? Module { get; set; }

        public virtual ICollection<SupportTicketAttachment> Attachments { get; set; } = new List<SupportTicketAttachment>();
    }

    public class SupportTicketAttachment : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SupportTicketId { get; set; }

        [Required]
        [StringLength(500)]
        public string FileName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? FilePath { get; set; }

        public long FileSize { get; set; }

        [StringLength(200)]
        public string? ContentType { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SupportTicketId")]
        public virtual SupportTicket Ticket { get; set; } = null!;
    }

    public class SupportTicketComment : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SupportTicketId { get; set; }

        [StringLength(255)]
        public string Author { get; set; } = string.Empty;

        [StringLength(255)]
        public string? AuthorEmail { get; set; }

        public string Text { get; set; } = string.Empty;

        public bool IsInternal { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SupportTicketId")]
        public virtual SupportTicket Ticket { get; set; } = null!;
    }

    public class SupportTicketLink : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SourceTicketId { get; set; }

        [Required]
        public int TargetTicketId { get; set; }

        [StringLength(30)]
        public string LinkType { get; set; } = "related";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SourceTicketId")]
        public virtual SupportTicket SourceTicket { get; set; } = null!;

        [ForeignKey("TargetTicketId")]
        public virtual SupportTicket TargetTicket { get; set; } = null!;
    }
}
