using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.OAS.Shifts.DTOs;

public class OasShiftTemplateDto
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool CrossesMidnight { get; set; }
    public int BreakMinutes { get; set; }
    public bool IsActive { get; set; }
    public int OpeningMinutes { get; set; }
}

public class OasShiftTemplateRequestDto
{
    [Required] public Guid SiteId { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public TimeOnly StartTime { get; set; }
    [Required] public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
}

public class OasShiftCalendarEntryDto
{
    public Guid ShiftTemplateId { get; set; }
    public DateOnly WorkDate { get; set; }
    public bool IsWorkingDay { get; set; }
}

public class OasShiftSignoffDto
{
    public Guid Id { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid SignedBy { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset SignedAt { get; set; }
}
public class OasShiftSignoffRequestDto
{
    [Required] public Guid ShiftTemplateId { get; set; }
    [Required] public DateOnly WorkDate { get; set; }
    public string? Note { get; set; }
}
