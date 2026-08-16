using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Common.Models;

namespace MyApi.Modules.OAS.Common.Data;

public class OasPluginActivationConfiguration : IEntityTypeConfiguration<OasPluginActivation>
{
    public void Configure(EntityTypeBuilder<OasPluginActivation> b)
    {
        b.ToTable("oas_plugin_activations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.PluginCode).HasColumnName("plugin_code").HasMaxLength(32).IsRequired();
        b.Property(x => x.Enabled).HasColumnName("enabled");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(255);
        b.HasIndex(x => new { x.TenantId, x.PluginCode }).IsUnique();
    }
}
