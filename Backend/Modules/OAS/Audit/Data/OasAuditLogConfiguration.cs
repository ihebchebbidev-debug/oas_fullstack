using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Audit.Models;

namespace MyApi.Modules.OAS.Audit.Data;

public class OasAuditLogConfiguration : IEntityTypeConfiguration<OasAuditLog>
{
    public void Configure(EntityTypeBuilder<OasAuditLog> b)
    {
        b.ToTable("oas_audit_log");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.EntityTable).HasColumnName("entity_table");
        b.Property(x => x.EntityId).HasColumnName("entity_id");
        b.Property(x => x.Action).HasColumnName("action");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.Before).HasColumnName("before").HasColumnType("jsonb");
        b.Property(x => x.After).HasColumnName("after").HasColumnType("jsonb");
        b.Property(x => x.Reason).HasColumnName("reason");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.HasIndex(x => new { x.EntityTable, x.EntityId, x.OccurredAt });
        b.HasIndex(x => new { x.TenantId, x.OccurredAt });
    }
}
