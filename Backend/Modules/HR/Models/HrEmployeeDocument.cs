using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_employee_documents")]
    public class HrEmployeeDocument : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        // contract | payslip | id_card | cnss | medical | other
        [Column("doc_type")]
        [MaxLength(40)]
        public string DocType { get; set; } = "other";

        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Column("file_url")]
        [MaxLength(1000)]
        public string FileUrl { get; set; } = string.Empty;

        [Column("file_name")]
        [MaxLength(300)]
        public string? FileName { get; set; }

        [Column("mime_type")]
        [MaxLength(120)]
        public string? MimeType { get; set; }

        [Column("file_size")]
        public long? FileSize { get; set; }

        [Column("issued_date")]
        public DateTime? IssuedDate { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [Column("uploaded_by")]
        public int? UploadedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }
}
