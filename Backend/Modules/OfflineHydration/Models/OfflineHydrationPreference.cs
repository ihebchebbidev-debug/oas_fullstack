using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.OfflineHydration.Models;

/// <summary>
/// Per-user, per-tenant offline hydration module toggles (JSON map of moduleId → disabled).
/// </summary>
[Table("OfflineHydrationPreferences")]
public class OfflineHydrationPreference : ITenantEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>
    /// Matches JWT <c>UserId</c> claim: <b>1</b> = MainAdminUsers (main admin), <b>2+</b> = Users table (tenant users).
    /// Combined with <see cref="TenantId"/> this row is unique per principal; no collision between tables.
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// JSON object: keys are module ids, values are false when the module is disabled (omitted or true = default enabled).
    /// Example: <c>{"contacts":false,"documents":false}</c>
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public string ModulesJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
