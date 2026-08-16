using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Shifts.DTOs;
using MyApi.Modules.OAS.Shifts.Models;

namespace MyApi.Modules.OAS.Shifts.Services;

public interface IOasShiftService
{
    Task<IReadOnlyList<OasShiftTemplateDto>> GetTemplatesAsync(int tenantId);
    Task<(bool success, string? error, OasShiftTemplateDto? dto)> CreateTemplateAsync(int tenantId, OasShiftTemplateRequestDto request);
    Task<(bool success, string? error)> UpdateTemplateAsync(int tenantId, Guid id, OasShiftTemplateRequestDto request);
    Task<bool> DeleteTemplateAsync(int tenantId, Guid id);

    Task<IReadOnlyList<OasShiftCalendarEntryDto>> GetCalendarAsync(int tenantId, DateOnly from, DateOnly to);
    Task PutCalendarAsync(int tenantId, Guid siteId, IReadOnlyList<OasShiftCalendarEntryDto> entries);

    Task<OasShiftSignoffDto> CreateSignoffAsync(int tenantId, Guid signedBy, OasShiftSignoffRequestDto request);
    Task<IReadOnlyList<OasShiftSignoffDto>> GetSignoffsAsync(int tenantId, Guid? shiftTemplateId, DateOnly? date);
}

public class OasShiftService : IOasShiftService
{
    private readonly OasDbContext _db;
    public OasShiftService(OasDbContext db) => _db = db;

    public async Task<IReadOnlyList<OasShiftTemplateDto>> GetTemplatesAsync(int tenantId)
    {
        var rows = await _db.Set<OasShiftTemplate>().OrderBy(s => s.StartTime).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<(bool success, string? error, OasShiftTemplateDto? dto)> CreateTemplateAsync(int tenantId, OasShiftTemplateRequestDto request)
    {
        // v15: a zero-length shift (start == end) must be rejected outright,
        // not silently treated as a 24h shift by the midnight-wraparound
        // math downstream (Kpi module's opening-minutes formula).
        if (request.StartTime == request.EndTime) return (false, "start_and_end_must_differ", null);
        if (!Enum.TryParse<OasShiftCode>(request.Code, true, out var code)) code = OasShiftCode.custom;

        var template = new OasShiftTemplate
        {
            TenantId = tenantId, SiteId = request.SiteId, Code = code, Name = request.Name,
            StartTime = request.StartTime, EndTime = request.EndTime,
            CrossesMidnight = request.EndTime < request.StartTime,
            BreakMinutes = request.BreakMinutes,
        };
        _db.Set<OasShiftTemplate>().Add(template);
        await _db.SaveChangesAsync();
        return (true, null, ToDto(template));
    }

    public async Task<(bool success, string? error)> UpdateTemplateAsync(int tenantId, Guid id, OasShiftTemplateRequestDto request)
    {
        if (request.StartTime == request.EndTime) return (false, "start_and_end_must_differ");
        var template = await _db.Set<OasShiftTemplate>().FindAsync(id);
        if (template is null) return (false, "not_found");

        template.SiteId = request.SiteId; template.Name = request.Name;
        template.StartTime = request.StartTime; template.EndTime = request.EndTime;
        template.CrossesMidnight = request.EndTime < request.StartTime;
        template.BreakMinutes = request.BreakMinutes;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> DeleteTemplateAsync(int tenantId, Guid id)
    {
        var template = await _db.Set<OasShiftTemplate>().FindAsync(id);
        if (template is null) return false;
        _db.Set<OasShiftTemplate>().Remove(template);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<OasShiftCalendarEntryDto>> GetCalendarAsync(int tenantId, DateOnly from, DateOnly to)
    {
        var rows = await _db.Set<OasShiftCalendarEntry>()
            .Where(e => e.WorkDate >= from && e.WorkDate <= to)
            .OrderBy(e => e.WorkDate)
            .ToListAsync();
        return rows.Select(e => new OasShiftCalendarEntryDto { ShiftTemplateId = e.ShiftTemplateId, WorkDate = e.WorkDate, IsWorkingDay = e.IsWorkingDay }).ToList();
    }

    public async Task PutCalendarAsync(int tenantId, Guid siteId, IReadOnlyList<OasShiftCalendarEntryDto> entries)
    {
        foreach (var entry in entries)
        {
            var existing = await _db.Set<OasShiftCalendarEntry>()
                .FirstOrDefaultAsync(e => e.SiteId == siteId && e.ShiftTemplateId == entry.ShiftTemplateId && e.WorkDate == entry.WorkDate);
            if (existing is null)
            {
                _db.Set<OasShiftCalendarEntry>().Add(new OasShiftCalendarEntry
                {
                    TenantId = tenantId, SiteId = siteId, ShiftTemplateId = entry.ShiftTemplateId,
                    WorkDate = entry.WorkDate, IsWorkingDay = entry.IsWorkingDay,
                });
            }
            else
            {
                existing.IsWorkingDay = entry.IsWorkingDay;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task<OasShiftSignoffDto> CreateSignoffAsync(int tenantId, Guid signedBy, OasShiftSignoffRequestDto request)
    {
        var signoff = new OasShiftSignoff
        {
            TenantId = tenantId, ShiftTemplateId = request.ShiftTemplateId, WorkDate = request.WorkDate,
            SignedBy = signedBy, Note = request.Note,
        };
        _db.Set<OasShiftSignoff>().Add(signoff);
        await _db.SaveChangesAsync();
        return ToSignoffDto(signoff);
    }

    public async Task<IReadOnlyList<OasShiftSignoffDto>> GetSignoffsAsync(int tenantId, Guid? shiftTemplateId, DateOnly? date)
    {
        var q = _db.Set<OasShiftSignoff>().AsQueryable();
        if (shiftTemplateId is not null) q = q.Where(s => s.ShiftTemplateId == shiftTemplateId);
        if (date is not null) q = q.Where(s => s.WorkDate == date);
        var rows = await q.OrderByDescending(s => s.SignedAt).ToListAsync();
        return rows.Select(ToSignoffDto).ToList();
    }

    private static OasShiftTemplateDto ToDto(OasShiftTemplate s)
    {
        var span = (s.EndTime.ToTimeSpan() - s.StartTime.ToTimeSpan());
        if (span < TimeSpan.Zero) span += TimeSpan.FromHours(24);
        var openingMin = (int)span.TotalMinutes - s.BreakMinutes;

        return new OasShiftTemplateDto
        {
            Id = s.Id, SiteId = s.SiteId, Code = s.Code.ToString(), Name = s.Name,
            StartTime = s.StartTime, EndTime = s.EndTime, CrossesMidnight = s.CrossesMidnight,
            BreakMinutes = s.BreakMinutes, IsActive = s.IsActive, OpeningMinutes = Math.Max(0, openingMin),
        };
    }

    private static OasShiftSignoffDto ToSignoffDto(OasShiftSignoff s) => new()
    {
        Id = s.Id, ShiftTemplateId = s.ShiftTemplateId, WorkDate = s.WorkDate, SignedBy = s.SignedBy, Note = s.Note, SignedAt = s.SignedAt,
    };
}
