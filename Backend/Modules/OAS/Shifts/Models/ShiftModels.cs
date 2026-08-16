using MyApi.Modules.OAS.Common.Data;

namespace MyApi.Modules.OAS.Shifts.Models;

public enum OasShiftCode { morning, afternoon, night, custom }

public class OasShiftTemplate : OasEntityBase
{
    public Guid SiteId { get; set; }
    public OasShiftCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool CrossesMidnight { get; set; }
    public int BreakMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OasShiftCalendarEntry : OasEntityBase
{
    public Guid SiteId { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public DateOnly WorkDate { get; set; }
    public bool IsWorkingDay { get; set; } = true;
}

public class OasShiftSignoff : OasEntityBase
{
    public Guid ShiftTemplateId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid SignedBy { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;
}
