using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Articles.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Articles.Data.Configurations
{
    public class ArticleNoteConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ArticleNote>(entity =>
            {
                entity.ToTable("ArticleNotes");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ArticleId).IsRequired();
                entity.Property(e => e.Note).IsRequired();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("NOW()");
                entity.Property(e => e.CreatedBy).HasMaxLength(100);

                entity.HasIndex(e => e.ArticleId);
                entity.HasIndex(e => e.CreatedDate);
            });
        }
    }
}