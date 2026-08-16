namespace MyApi.Modules.UserGroups.DTOs
{
    public class UserGroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int MemberCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UserGroupMemberDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    public class CreateUserGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateUserGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class AssignUsersToGroupRequest
    {
        public List<int> UserIds { get; set; } = new();
    }
}
