using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_departments")]
    public class HrDepartment : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Column("code")]
        [MaxLength(50)]
        public string? Code { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }

        [Column("manager_id")]
        public int? ManagerId { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("position")]
        public int? Position { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
