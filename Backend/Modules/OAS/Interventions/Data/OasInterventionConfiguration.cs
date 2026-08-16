using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Interventions.Models;

namespace MyApi.Modules.OAS.Interventions.Data;

public class OasInterventionConfiguration : IEntityTypeConfiguration<OasIntervention>
{
    public void Configure(EntityTypeBuilder<OasIntervention> b)
    {
        b.ToTable("oas_interventions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.EventId).HasColumnName("event_id");
        b.Property(x => x.AssigneeId).HasColumnName("assignee_id");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.AssignedAt).HasColumnName("assigned_at");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.ClosedAt).HasColumnName("closed_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => x.EventId);
        b.HasIndex(x => new { x.TenantId, x.AssigneeId, x.Status });
    }
}
