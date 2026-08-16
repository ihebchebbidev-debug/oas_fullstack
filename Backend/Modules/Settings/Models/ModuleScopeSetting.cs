using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.Settings.Models
{
    /// <summary>
    /// Per-module scope configuration. One row per logical module key
    /// (contacts, articles, offers, sales, purchases, hr, ...).
    /// NOTE: Not an ITenantEntity — it configures tenant behaviour itself.
    /// </summary>
    [Table("ModuleScopeSettings")]
    public class ModuleScopeSetting
    {
        [Key]
        [MaxLength(64)]
        public string ModuleKey { get; set; } = string.Empty;

        /// <summary>"per_company" (default) or "shared".</summary>
        [Required]
        [MaxLength(16)]
        public string Scope { get; set; } = "per_company";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedByUserId { get; set; }
    }
}
