using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dashboards.Models
{
    [ModuleScope("dashboards")]
    [Table("Dashboards")]
    public class Dashboard : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(50)]
        public string? TemplateKey { get; set; }

        public bool IsDefault { get; set; }

        public bool IsShared { get; set; }

        /// <summary>JSONB array of role IDs</summary>
        [Column(TypeName = "jsonb")]
        public string? SharedWithRoles { get; set; }

        /// <summary>JSONB array of widget configs</summary>
        [Column(TypeName = "jsonb")]
        public string Widgets { get; set; } = "[]";

        /// <summary>JSONB grid layout settings</summary>
        [Column(TypeName = "jsonb")]
        public string? GridSettings { get; set; }

        public bool IsPublic { get; set; }

        [MaxLength(100)]
        public string? ShareToken { get; set; }

        public DateTime? SharedAt { get; set; }

        /// <summary>JSONB snapshot of CRM data at share time</summary>
        [Column(TypeName = "jsonb")]
        public string? SnapshotData { get; set; }

        public DateTime? SnapshotAt { get; set; }

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }
}
