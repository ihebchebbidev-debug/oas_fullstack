using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Assignments.Models;

namespace MyApi.Modules.OAS.Assignments.Data;

public class OasAssignmentConfiguration : IEntityTypeConfiguration<OasAssignment>
{
    public void Configure(EntityTypeBuilder<OasAssignment> b)
    {
        b.ToTable("oas_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.WorkDate).HasColumnName("work_date");
        b.Property(x => x.ShiftTemplateId).HasColumnName("shift_template_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.ProductionOrderId).HasColumnName("production_order_id");
        b.Property(x => x.AssignedBy).HasColumnName("assigned_by");
        b.Property(x => x.Note).HasColumnName("note");
        b.Property(x => x.PublishedAt).HasColumnName("published_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.WorkDate, x.ShiftTemplateId, x.PostId, x.UserId }).IsUnique();
    }
}

public class OasPresenceEntryConfiguration : IEntityTypeConfiguration<OasPresenceEntry>
{
    public void Configure(EntityTypeBuilder<OasPresenceEntry> b)
    {
        b.ToTable("oas_presence_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.WorkDate).HasColumnName("work_date");
        b.Property(x => x.ShiftTemplateId).HasColumnName("shift_template_id");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        b.Property(x => x.Reason).HasColumnName("reason");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.WorkDate, x.ShiftTemplateId }).IsUnique();
    }
}
