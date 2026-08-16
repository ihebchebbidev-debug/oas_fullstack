using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Teams.Models;

namespace MyApi.Modules.OAS.Teams.Data;

public class OasTeamConfiguration : IEntityTypeConfiguration<OasTeam>
{
    public void Configure(EntityTypeBuilder<OasTeam> b)
    {
        b.ToTable("oas_teams");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.SiteId).HasColumnName("site_id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.LeadUserId).HasColumnName("lead_user_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class OasTeamMemberConfiguration : IEntityTypeConfiguration<OasTeamMember>
{
    public void Configure(EntityTypeBuilder<OasTeamMember> b)
    {
        b.ToTable("oas_team_members");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.TeamId).HasColumnName("team_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.ValidFrom).HasColumnName("valid_from");
        b.Property(x => x.ValidTo).HasColumnName("valid_to");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.TeamId, x.UserId, x.ValidFrom }).IsUnique();
    }
}
