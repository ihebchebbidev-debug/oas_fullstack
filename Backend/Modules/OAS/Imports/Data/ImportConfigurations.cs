using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApi.Modules.OAS.Imports.Models;

namespace MyApi.Modules.OAS.Imports.Data;

public class OasImportConfiguration : IEntityTypeConfiguration<OasImport>
{
    public void Configure(EntityTypeBuilder<OasImport> b)
    {
        b.ToTable("oas_imports");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Kind).HasColumnName("kind").IsRequired();
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.FilePath).HasColumnName("file_path");
        b.Property(x => x.RowsTotal).HasColumnName("rows_total");
        b.Property(x => x.RowsOk).HasColumnName("rows_ok");
        b.Property(x => x.RowsError).HasColumnName("rows_error");
        b.Property(x => x.Report).HasColumnName("report").HasColumnType("jsonb");
        b.Property(x => x.ImportedBy).HasColumnName("imported_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CommittedAt).HasColumnName("committed_at");
        b.Ignore(x => x.UpdatedAt);
    }
}

public class OasImportLineConfiguration : IEntityTypeConfiguration<OasImportLine>
{
    public void Configure(EntityTypeBuilder<OasImportLine> b)
    {
        b.ToTable("oas_import_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ImportId).HasColumnName("import_id");
        b.Property(x => x.RowNumber).HasColumnName("row_number");
        b.Property(x => x.Raw).HasColumnName("raw").HasColumnType("jsonb");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.Error).HasColumnName("error");
        b.Ignore(x => x.CreatedAt);
        b.Ignore(x => x.UpdatedAt);
        b.HasIndex(x => new { x.ImportId, x.RowNumber });
    }
}
