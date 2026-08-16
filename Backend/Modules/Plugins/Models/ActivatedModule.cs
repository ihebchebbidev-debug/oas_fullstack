using System;
using MyApi.Infrastructure;

namespace MyApi.Modules.Plugins.Models
{
    /// <summary>
    /// Per-tenant override of a plugin's enabled state.
    /// Absence of a row = enabled (default-on semantics).
    /// </summary>
    public class ActivatedModule : ITenantEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        /// <summary>Plugin code, e.g. "PL0002SALES". Matches the frontend manifest.</summary>
        public string PluginCode { get; set; } = "";

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedBy { get; set; }
    }
}
