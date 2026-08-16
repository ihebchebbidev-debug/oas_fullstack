using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Reporting.Models;

/// <summary>
/// A widget pinned by a user to their reporting dashboard, scoped per tenant/company.
/// Ordering is controlled by <see cref="Position"/> (ascending).
/// </summary>
[Table("ReportingFavorites")]
public class ReportingFavorite : ITenantEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Company/view scope key (e.g. "all", "c:12", "default").
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Scope { get; set; } = "default";

    [Required]
    [MaxLength(200)]
    public string WidgetId { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Source { get; set; } = string.Empty;

    public int Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}