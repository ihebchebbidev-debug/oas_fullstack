using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Skills.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Skills.Data.Configurations
{
    public class SkillConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Skill>(entity =>
            {
                entity.ToTable("Skills");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });
        }
    }
}
