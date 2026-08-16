using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.Lookups.Models
{
    /// <summary>
    /// Installation Type lookup - stored in LookupItems table with LookupType = 'installation-type'
    /// Examples: Internal (Sold by us), External (Customer owned), Lease, Rental
    /// </summary>
    /// <remarks>
    /// This is a reference model. Installation types are stored in the LookupItems table
    /// using the generic lookup pattern with LookupType = 'installation-type'.
    /// 
    /// To seed default installation types, run:
    /// INSERT INTO "LookupItems" ("Name", "Description", "Color", "LookupType", "IsActive", "SortOrder", "CreatedUser", "Value", "IsDefault")
    /// VALUES 
    ///     ('Internal', 'Equipment sold by us', '#3b82f6', 'installation-type', true, 1, 'system', 'internal', true),
    ///     ('External', 'Customer owned equipment', '#10b981', 'installation-type', true, 2, 'system', 'external', false),
    ///     ('Lease', 'Leased equipment', '#f59e0b', 'installation-type', true, 3, 'system', 'lease', false),
    ///     ('Rental', 'Rental equipment', '#8b5cf6', 'installation-type', true, 4, 'system', 'rental', false);
    /// </remarks>
    public static class InstallationTypeConstants
    {
        public const string LookupType = "installation-type";
        
        // Common installation type values
        public const string Internal = "internal";
        public const string External = "external";
        public const string Lease = "lease";
        public const string Rental = "rental";
    }
}
