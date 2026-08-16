using Microsoft.EntityFrameworkCore;
using MyApi.Modules.WebsiteBuilder.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.WebsiteBuilder.Data.Configurations
{
    public class WBSiteConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBSite>(entity =>
            {
                entity.ToTable("WB_Sites");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.Published);
                entity.HasIndex(e => e.IsDeleted);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Name);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.Published).HasDefaultValue(false);
                entity.Property(e => e.ThemeJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
                entity.Property(e => e.LanguagesJson).HasColumnType("jsonb");
            });
        }
    }

    public class WBPageConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBPage>(entity =>
            {
                entity.ToTable("WB_Pages");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.SiteId, e.Slug });
                entity.HasIndex(e => new { e.SiteId, e.SortOrder });
                entity.HasIndex(e => e.IsDeleted);
                entity.HasIndex(e => new { e.SiteId, e.IsHomePage });

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsHomePage).HasDefaultValue(false);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.ComponentsJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
                entity.Property(e => e.SeoJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
                entity.Property(e => e.TranslationsJson).HasColumnType("jsonb");

                entity.HasOne(e => e.Site)
                    .WithMany(s => s.Pages)
                    .HasForeignKey(e => e.SiteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    public class WBPageVersionConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBPageVersion>(entity =>
            {
                entity.ToTable("WB_PageVersions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PageId);
                entity.HasIndex(e => e.SiteId);
                entity.HasIndex(e => new { e.PageId, e.VersionNumber }).IsDescending(false, true);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.VersionNumber).HasDefaultValue(1);
                entity.Property(e => e.ComponentsJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");

                entity.HasOne(e => e.Page)
                    .WithMany(p => p.Versions)
                    .HasForeignKey(e => e.PageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Site)
                    .WithMany()
                    .HasForeignKey(e => e.SiteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    public class WBGlobalBlockConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBGlobalBlock>(entity =>
            {
                entity.ToTable("WB_GlobalBlocks");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsDeleted);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.ComponentJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            });
        }
    }

    public class WBGlobalBlockUsageConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBGlobalBlockUsage>(entity =>
            {
                entity.ToTable("WB_GlobalBlockUsages");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GlobalBlockId);
                entity.HasIndex(e => e.SiteId);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

                entity.HasOne(e => e.GlobalBlock)
                    .WithMany(b => b.Usages)
                    .HasForeignKey(e => e.GlobalBlockId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Site)
                    .WithMany()
                    .HasForeignKey(e => e.SiteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Page)
                    .WithMany()
                    .HasForeignKey(e => e.PageId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }

    public class WBBrandProfileConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBBrandProfile>(entity =>
            {
                entity.ToTable("WB_BrandProfiles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.IsBuiltIn);
                entity.HasIndex(e => e.IsDeleted);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsBuiltIn).HasDefaultValue(false);
                entity.Property(e => e.ThemeJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            });
        }
    }

    public class WBFormSubmissionConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBFormSubmission>(entity =>
            {
                entity.ToTable("WB_FormSubmissions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SiteId);
                entity.HasIndex(e => e.PageId);
                entity.HasIndex(e => e.FormComponentId);
                entity.HasIndex(e => e.SubmittedAt);

                entity.Property(e => e.SubmittedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.DataJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");

                entity.HasOne(e => e.Site)
                    .WithMany(s => s.FormSubmissions)
                    .HasForeignKey(e => e.SiteId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Page)
                    .WithMany()
                    .HasForeignKey(e => e.PageId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }

    public class WBMediaConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBMedia>(entity =>
            {
                entity.ToTable("WB_Media");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SiteId);
                entity.HasIndex(e => e.Folder);
                entity.HasIndex(e => e.ContentType);
                entity.HasIndex(e => e.IsDeleted);
                entity.HasIndex(e => e.UploadedAt);

                entity.Property(e => e.UploadedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.FileSize).HasDefaultValue(0);

                entity.HasOne(e => e.Site)
                    .WithMany(s => s.Media)
                    .HasForeignKey(e => e.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }

    public class WBTemplateConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBTemplate>(entity =>
            {
                entity.ToTable("WB_Templates");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsBuiltIn);
                entity.HasIndex(e => e.IsPremium);
                entity.HasIndex(e => e.IsDeleted);
                entity.HasIndex(e => e.SortOrder);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsBuiltIn).HasDefaultValue(false);
                entity.Property(e => e.IsPremium).HasDefaultValue(false);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.ThemeJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
                entity.Property(e => e.PagesJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            });
        }
    }

    public class WBActivityLogConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WBActivityLog>(entity =>
            {
                entity.ToTable("WB_ActivityLog");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SiteId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.CreatedBy);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

                entity.HasOne(e => e.Site)
                    .WithMany(s => s.ActivityLogs)
                    .HasForeignKey(e => e.SiteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
