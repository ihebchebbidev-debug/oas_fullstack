using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Assignments.DTOs;

public class OasAssignmentDto
{
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public string? Note { get; set; }
    public bool Published { get; set; }
}

public class OasAssignmentRequestDto
{
    [Required] public Guid UserId { get; set; }
    [Required] public Guid ShiftTemplateId { get; set; }
    [Required] public DateOnly WorkDate { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public string? Note { get; set; }
}

public class OasAssignmentCountsDto { public int TotalPosts { get; set; } public int Assigned { get; set; } public int Published { get; set; } }
public class OasRosterEntryDto { public Guid UserId { get; set; } public string? DisplayName { get; set; } public bool IsActive { get; set; } }

public class OasPresenceDto
{
    public Guid UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? Reason { get; set; }
}
public class OasPresenceRequestDto
{
    [Required] public DateOnly WorkDate { get; set; }
    [Required] public Guid ShiftTemplateId { get; set; }
    [Required] public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
public class OasPresenceConfirmRequestDto
{
    [Required] public DateOnly WorkDate { get; set; }
    [Required] public Guid ShiftTemplateId { get; set; }
}
