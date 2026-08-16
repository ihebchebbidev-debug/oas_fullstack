using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Integrations.Models;

namespace MyApi.Modules.OAS.Integrations.Data;

public class OasIntegrationEndpointConfiguration : IEntityTypeConfiguration<OasIntegrationEndpoint>
{
    public void Configure(EntityTypeBuilder<OasIntegrationEndpoint> b)
    {
        b.ToTable("oas_integration_endpoints");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Url).HasColumnName("url");
        b.Property(x => x.Secret).HasColumnName("secret");
        b.Property(x => x.EventTypes).HasColumnName("event_types").HasColumnType("text[]");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

public class OasIntegrationOutboxConfiguration : IEntityTypeConfiguration<OasIntegrationOutbox>
{
    public void Configure(EntityTypeBuilder<OasIntegrationOutbox> b)
    {
        b.ToTable("oas_integration_outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.EndpointId).HasColumnName("endpoint_id");
        b.Property(x => x.EventType).HasColumnName("event_type");
        b.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.LastError).HasColumnName("last_error");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}
