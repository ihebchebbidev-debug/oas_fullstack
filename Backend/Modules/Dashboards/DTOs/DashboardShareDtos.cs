namespace MyApi.Modules.Dashboards.DTOs
{
    public class SharedDashboardInfoDto
    {
        public string ShareToken { get; set; } = string.Empty;
        public string PublicUrl { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class PublicDashboardDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public object? Widgets { get; set; }
        public object? GridSettings { get; set; }
        public object? DataSnapshot { get; set; }
        public DateTime? SnapshotAt { get; set; }
    }

    public class ShareDashboardRequest
    {
        public Dictionary<string, object>? DataSnapshot { get; set; }
    }

    public class DashboardDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? TemplateKey { get; set; }
        public bool IsDefault { get; set; }
        public bool IsShared { get; set; }
        public object? SharedWithRoles { get; set; }
        public int CreatedBy { get; set; }
        public object Widgets { get; set; } = new object[] { };
        public object? GridSettings { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class CreateDashboardDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? TemplateKey { get; set; }
        public bool? IsShared { get; set; }
        public object? SharedWithRoles { get; set; }
        public object Widgets { get; set; } = new object[] { };
        public object? GridSettings { get; set; }
    }

    public class UpdateDashboardDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsShared { get; set; }
        public object? SharedWithRoles { get; set; }
        public object? Widgets { get; set; }
        public object? GridSettings { get; set; }
    }

    public class DuplicateDashboardDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
