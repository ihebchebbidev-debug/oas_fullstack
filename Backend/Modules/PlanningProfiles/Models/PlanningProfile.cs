using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;
using MyApi.Modules.Shared.Models;

namespace MyApi.Modules.PlanningProfiles.Models
{
    [Table("planning_profiles")]
    public class PlanningProfile : ITenantEntity, ISoftDeletable
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("owner_user_id")]
        [MaxLength(64)]
        public string OwnerUserId { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("color")]
        [MaxLength(16)]
        public string? Color { get; set; }

        [Column("icon")]
        [MaxLength(64)]
        public string? Icon { get; set; }

        [Column("is_shared")]
        public bool IsShared { get; set; } = false;

        [Column("visible_user_ids", TypeName = "jsonb")]
        public string VisibleUserIdsJson { get; set; } = "[]";

        [Column("required_skill_ids", TypeName = "jsonb")]
        public string? RequiredSkillIdsJson { get; set; }

        [Column("settings", TypeName = "jsonb")]
        public string SettingsJson { get; set; } = "{}";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_by")]
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        [Column("deleted_by")]
        public string? DeletedBy { get; set; }

        [NotMapped]
        public bool IsDeleted
        {
            get => DeletedAt.HasValue;
            set
            {
                if (value && !DeletedAt.HasValue) DeletedAt = DateTime.UtcNow;
                else if (!value) DeletedAt = null;
            }
        }
    }
}
