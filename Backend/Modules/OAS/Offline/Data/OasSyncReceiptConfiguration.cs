using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Offline.Models;

namespace MyApi.Modules.OAS.Offline.Data;

public class OasSyncReceiptConfiguration : IEntityTypeConfiguration<OasSyncReceipt>
{
    public void Configure(EntityTypeBuilder<OasSyncReceipt> b)
    {
        b.ToTable("oas_sync_receipts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ClientEventId).HasColumnName("client_event_id");
        b.Property(x => x.DeviceId).HasColumnName("device_id");
        b.Property(x => x.Entity).HasColumnName("entity").IsRequired();
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        // latency_sec is a Postgres GENERATED ALWAYS AS column — no CLR
        // property maps to it at all, so EF never attempts to write it.
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.Error).HasColumnName("error");
        b.HasIndex(x => x.ClientEventId);
    }
}
