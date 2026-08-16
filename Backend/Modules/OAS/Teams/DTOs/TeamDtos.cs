using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Teams.DTOs;

public class OasTeamDto
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? LeadUserId { get; set; }
    public List<Guid> MemberIds { get; set; } = new();
}
public class OasTeamRequestDto
{
    [Required] public Guid SiteId { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public Guid? LeadUserId { get; set; }
}
public class OasTeamMembersRequestDto { public List<Guid> MemberIds { get; set; } = new(); }
