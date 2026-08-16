using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Declarations.Models;

namespace MyApi.Modules.OAS.Declarations.Data;

public class OasDeclarationConfiguration : IEntityTypeConfiguration<OasDeclaration>
{
    public void Configure(EntityTypeBuilder<OasDeclaration> b)
    {
        b.ToTable("oas_declarations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ClientEventId).HasColumnName("client_event_id");
        b.Property(x => x.Kind).HasColumnName("kind");
        b.Property(x => x.PostSessionId).HasColumnName("post_session_id");
        b.Property(x => x.PostId).HasColumnName("post_id");
        b.Property(x => x.LineId).HasColumnName("line_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.ProductionOrderId).HasColumnName("production_order_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.QuantityOk).HasColumnName("quantity_ok").HasColumnType("numeric(12,2)");
        b.Property(x => x.QuantityNok).HasColumnName("quantity_nok").HasColumnType("numeric(12,2)");
        b.Property(x => x.ScrapCauseId).HasColumnName("scrap_cause_id");
        b.Property(x => x.PhotoPath).HasColumnName("photo_path");
        b.Property(x => x.Note).HasColumnName("note");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        b.Property(x => x.CorrectsId).HasColumnName("corrects_id");
        b.Property(x => x.IsCorrected).HasColumnName("is_corrected");
        b.Property(x => x.CorrectionReason).HasColumnName("correction_reason");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => x.ClientEventId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.PostId, x.OccurredAt });
    }
}
