namespace MyApi.Modules.Lookups.Models
{
    /// <summary>
    /// Constants for Installation Category lookup type
    /// </summary>
    public static class InstallationCategoryConstants
    {
        public const string LookupType = "installation-category";
        
        // Common installation categories
        public const string Server = "server";
        public const string Network = "network";
        public const string HVAC = "hvac";
        public const string Security = "security";
        public const string Electrical = "electrical";
        public const string Plumbing = "plumbing";
        public const string Other = "other";
    }
}

/*
SQL Script to seed Installation Categories:
 
INSERT INTO "LookupItems" ("Name", "Description", "Color", "LookupType", "IsActive", "SortOrder", "CreatedUser", "Value", "IsDefault")
VALUES
    ('Server', 'Server equipment installations', '#3b82f6', 'installation-category', true, 1, 'system', 'server', true),
    ('Network', 'Network infrastructure installations', '#10b981', 'installation-category', true, 2, 'system', 'network', false),
    ('HVAC', 'Heating, ventilation, and air conditioning', '#f59e0b', 'installation-category', true, 3, 'system', 'hvac', false),
    ('Security', 'Security system installations', '#ef4444', 'installation-category', true, 4, 'system', 'security', false),
    ('Electrical', 'Electrical system installations', '#8b5cf6', 'installation-category', true, 5, 'system', 'electrical', false),
    ('Plumbing', 'Plumbing system installations', '#06b6d4', 'installation-category', true, 6, 'system', 'plumbing', false),
    ('Other', 'Other installations', '#6b7280', 'installation-category', true, 7, 'system', 'other', false);
*/
