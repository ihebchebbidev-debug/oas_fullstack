using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.ExternalEndpoints.Models;

namespace MyApi.Modules.ExternalEndpoints.Database
{
    public class ExternalEndpointConfiguration : IEntityTypeConfiguration<ExternalEndpoint>
    {
        public void Configure(EntityTypeBuilder<ExternalEndpoint> builder)
        {
            builder.HasIndex(e => new { e.TenantId, e.Slug }).IsUnique().HasFilter("\"IsDeleted\" = false");
            builder.HasMany(e => e.Logs).WithOne(l => l.Endpoint).HasForeignKey(l => l.EndpointId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ExternalEndpointLogConfiguration : IEntityTypeConfiguration<ExternalEndpointLog>
    {
        public void Configure(EntityTypeBuilder<ExternalEndpointLog> builder)
        {
            builder.HasIndex(l => l.EndpointId);
            builder.HasIndex(l => l.ReceivedAt);
            // Hot path: list logs for an endpoint, newest first.
            builder.HasIndex(l => new { l.EndpointId, l.ReceivedAt })
                .HasDatabaseName("ix_ExternalEndpointLogs_EndpointId_ReceivedAt");
        }
    }

    public class WebhookForwardJobConfiguration : IEntityTypeConfiguration<WebhookForwardJob>
    {
        public void Configure(EntityTypeBuilder<WebhookForwardJob> builder)
        {
            // Hot path: worker queries `WHERE Status = 'pending' AND NextAttemptAt <= now`.
            builder.HasIndex(j => new { j.Status, j.NextAttemptAt });
            builder.HasIndex(j => j.EndpointId);
            builder.HasIndex(j => j.TenantId);
        }
    }
}
