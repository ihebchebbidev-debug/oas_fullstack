namespace MyApi.Modules.Roles.DTOs
{
    public class PermissionDto
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool Granted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class RolePermissionsDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public List<PermissionDto> Permissions { get; set; } = new();
    }

    public class SetPermissionRequest
    {
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool Granted { get; set; }
    }

    public class UpdateRolePermissionsRequest
    {
        public List<SetPermissionRequest> Permissions { get; set; } = new();
    }

    public class UserPermissionsDto
    {
        public int UserId { get; set; }
        public List<string> Permissions { get; set; } = new(); // Format: "module:action"
    }

    public class CheckPermissionRequest
    {
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }

    public class CheckPermissionResponse
    {
        public bool HasPermission { get; set; }
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}
