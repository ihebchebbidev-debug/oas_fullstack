using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Teams.Models;

public class OasTeam : OasEntityBase
{
    public Guid SiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? LeadUserId { get; set; }
}

public class OasTeamMember : OasEntityBase
{
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? ValidTo { get; set; }
}
