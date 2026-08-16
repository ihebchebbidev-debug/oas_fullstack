using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Causes.Models;

namespace MyApi.Modules.OAS.Causes.Data;

public class OasCauseConfiguration : IEntityTypeConfiguration<OasCause>
{
    public void Configure(EntityTypeBuilder<OasCause> b)
    {
        b.ToTable("oas_causes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ParentId).HasColumnName("parent_id");
        b.Property(x => x.Domain).HasColumnName("domain");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.LabelFr).HasColumnName("label_fr").IsRequired();
        b.Property(x => x.LabelAr).HasColumnName("label_ar").IsRequired();
        b.Property(x => x.Icon).HasColumnName("icon");
        b.Property(x => x.EventType).HasColumnName("event_type");
        b.Property(x => x.DefaultCriticality).HasColumnName("default_criticality");
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.Domain, x.Code }).IsUnique();
        b.HasIndex(x => x.ParentId);
    }
}

public class OasCauseProposalConfiguration : IEntityTypeConfiguration<OasCauseProposal>
{
    public void Configure(EntityTypeBuilder<OasCauseProposal> b)
    {
        b.ToTable("oas_cause_proposals");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Domain).HasColumnName("domain");
        b.Property(x => x.LabelFr).HasColumnName("label_fr").IsRequired();
        b.Property(x => x.LabelAr).HasColumnName("label_ar");
        b.Property(x => x.ProposedBy).HasColumnName("proposed_by");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        b.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        b.Property(x => x.ResultingCauseId).HasColumnName("resulting_cause_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}
