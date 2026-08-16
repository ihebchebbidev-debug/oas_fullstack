using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Shifts.Models;

namespace MyApi.Modules.OAS.Shifts.Data;

public class OasShiftTemplateConfiguration : IEntityTypeConfiguration<OasShiftTemplate>
{
    public void Configure(EntityTypeBuilder<OasShiftTemplate> b)
    {
        b.ToTable("oas_shift_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.SiteId).HasColumnName("site_id");
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.StartTime).HasColumnName("start_time");
        b.Property(x => x.EndTime).HasColumnName("end_time");
        b.Property(x => x.CrossesMidnight).HasColumnName("crosses_midnight");
        b.Property(x => x.BreakMinutes).HasColumnName("break_minutes");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.SiteId, x.Code, x.Name }).IsUnique();
    }
}

public class OasShiftCalendarEntryConfiguration : IEntityTypeConfiguration<OasShiftCalendarEntry>
{
    public void Configure(EntityTypeBuilder<OasShiftCalendarEntry> b)
    {
        b.ToTable("oas_shift_calendar");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.SiteId).HasColumnName("site_id");
        b.Property(x => x.ShiftTemplateId).HasColumnName("shift_template_id");
        b.Property(x => x.WorkDate).HasColumnName("work_date");
        b.Property(x => x.IsWorkingDay).HasColumnName("is_working_day");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.SiteId, x.ShiftTemplateId, x.WorkDate }).IsUnique();
    }
}

public class OasShiftSignoffConfiguration : IEntityTypeConfiguration<OasShiftSignoff>
{
    public void Configure(EntityTypeBuilder<OasShiftSignoff> b)
    {
        b.ToTable("oas_shift_signoffs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ShiftTemplateId).HasColumnName("shift_template_id");
        b.Property(x => x.WorkDate).HasColumnName("work_date");
        b.Property(x => x.SignedBy).HasColumnName("signed_by");
        b.Property(x => x.Note).HasColumnName("note");
        b.Property(x => x.SignedAt).HasColumnName("signed_at");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.ShiftTemplateId, x.WorkDate, x.SignedBy }).IsUnique();
    }
}
