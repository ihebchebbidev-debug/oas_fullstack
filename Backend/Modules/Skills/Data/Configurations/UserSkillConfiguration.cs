using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Skills.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Skills.Data.Configurations
{
    public class UserSkillConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserSkill>(entity =>
            {
                entity.ToTable("UserSkills");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.SkillId });
                entity.Property(e => e.AssignedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                // Define relationships - properly reference User.UserSkills collection
                entity.HasOne(us => us.User)
                    .WithMany(u => u.UserSkills)
                    .HasForeignKey(us => us.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(us => us.Skill)
                    .WithMany(s => s.UserSkills)
                    .HasForeignKey(us => us.SkillId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
