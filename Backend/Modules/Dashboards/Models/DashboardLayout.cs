using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Dashboards.Models;

/// <summary>
/// Per-user customization of the main "/dashboard" landing page, scoped per
/// tenant/company. Stores a single JSON blob describing card ordering and which
/// default cards the user removed, so the layout follows the user across
/// devices and logins.
/// </summary>
[Table("DashboardLayouts")]
public class DashboardLayout : ITenantEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
    public int UserId { get; set; }

    /// <summary>Company/view scope key (e.g. "all", "c:12", "default").</summary>
    [Required]
    [MaxLength(64)]
    public string Scope { get; set; } = "default";

    /// <summary>JSONB: ordered array of card ids (default + pinned reporting widget ids).</summary>
    [Column(TypeName = "jsonb")]
    public string OrderJson { get; set; } = "[]";

    /// <summary>JSONB: array of default card ids the user hid/removed.</summary>
    [Column(TypeName = "jsonb")]
    public string HiddenJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
