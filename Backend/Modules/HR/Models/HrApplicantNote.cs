using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_applicant_notes")]
    public class HrApplicantNote : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("applicant_id")]
        public int ApplicantId { get; set; }

        [Column("author_user_id")]
        public int? AuthorUserId { get; set; }

        [Column("body", TypeName = "text")]
        public string Body { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}