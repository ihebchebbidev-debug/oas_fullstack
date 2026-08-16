using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.Shared.Data.Configurations
{
    /// <summary>
    /// Base interface for entity configurations
    /// </summary>
    public interface IEntityConfiguration
    {
        void Configure(ModelBuilder modelBuilder);
    }
}
