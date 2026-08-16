using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.ShopFloorAuth.Models;

namespace MyApi.Modules.OAS.ShopFloorAuth.Data;

public class OasUserConfiguration : IEntityTypeConfiguration<OasUser>
{
    public void Configure(EntityTypeBuilder<OasUser> b)
    {
        b.ToTable("oas_users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.SourceUserId).HasColumnName("source_user_id");
        b.Property(x => x.SourceTenantId).HasColumnName("source_tenant_id");
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.Property(x => x.EmployeeCode).HasColumnName("employee_code").HasMaxLength(50);
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        b.Property(x => x.Pin).HasColumnName("pin").HasMaxLength(20);
        b.Property(x => x.QrToken).HasColumnName("qr_token").HasMaxLength(255);
        // Native Postgres enums (oas_app_role, oas_workspace) — mapped via
        // NpgsqlDataSourceBuilder in OasNpgsqlEnums, NOT HasConversion<string>:
        // the column is a real enum type, not text, so converting to string
        // here would send a text parameter Postgres can't implicitly cast.
        b.Property(x => x.Role).HasColumnName("role");
        b.Property(x => x.Workspace).HasColumnName("workspace");
        b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(255);
        b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
        b.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
        b.Property(x => x.ScopeSiteId).HasColumnName("scope_site_id");
        b.Property(x => x.ScopeZoneId).HasColumnName("scope_zone_id");
        b.Property(x => x.ScopeLineId).HasColumnName("scope_line_id");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.IsInterim).HasColumnName("is_interim");
        b.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts");
        b.Property(x => x.LockedUntil).HasColumnName("locked_until");
        b.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
        b.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        b.Property(x => x.RefreshToken).HasColumnName("refresh_token");
        b.Property(x => x.RefreshTokenExpiresAt).HasColumnName("refresh_token_expires_at");

        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.EmployeeCode }).IsUnique().HasFilter("employee_code IS NOT NULL");
        b.HasIndex(x => new { x.TenantId, x.QrToken }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.SourceUserId, x.SourceTenantId });
    }
}
