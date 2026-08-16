using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Settings.DTOs
{
    public class ModuleScopeDto
    {
        public string ModuleKey { get; set; } = string.Empty;
        public string Scope { get; set; } = "per_company";
        public System.DateTime UpdatedAt { get; set; }
    }

    public class UpdateModuleScopeRequest
    {
        [Required, RegularExpression("^(shared|per_company)$",
            ErrorMessage = "Scope must be 'shared' or 'per_company'.")]
        public string Scope { get; set; } = "per_company";

        /// <summary>
        /// When switching shared → per_company, how to handle existing TenantId=0 rows.
        ///   "keep_at_zero" (default) → leave rows visible only in view-all
        ///   "migrate_to_current"     → re-stamp rows to the current company
        /// </summary>
        public string MigrationStrategy { get; set; } = "keep_at_zero";
    }

    public class BulkModuleScopeItem
    {
        [Required] public string ModuleKey { get; set; } = string.Empty;
        [Required, RegularExpression("^(shared|per_company)$")]
        public string Scope { get; set; } = "per_company";
    }
}

