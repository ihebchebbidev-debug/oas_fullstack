using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.PlanningProfiles.Models
{
    [Table("user_active_planning_profile")]
    public class UserActivePlanningProfile : ITenantEntity
    {
        public int TenantId { get; set; }

        [Column("user_id")]
        [MaxLength(64)]
        public string UserId { get; set; } = string.Empty;

        [Column("profile_id")]
        public int ProfileId { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
