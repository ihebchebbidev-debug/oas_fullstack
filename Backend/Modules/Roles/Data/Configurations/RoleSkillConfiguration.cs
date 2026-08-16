using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Roles.Models;
using MyApi.Modules.Skills.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Roles.Data.Configurations
{
    public class RoleSkillConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RoleSkill>(entity =>
            {
                entity.ToTable("RoleSkills");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RoleId, e.SkillId });
                entity.Property(e => e.AssignedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                // Define relationships
                entity.HasOne(rs => rs.Role)
                    .WithMany()
                    .HasForeignKey(rs => rs.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rs => rs.Skill)
                    .WithMany()
                    .HasForeignKey(rs => rs.SkillId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
