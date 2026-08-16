using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Numbering.Models;

namespace MyApi.Data.SeedData
{
    public class NumberingSeedData
    {
        public void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NumberingSettings>().HasData(
                new NumberingSettings
                {
                    Id = 1,
                    EntityName = "Offer",
                    IsEnabled = false,
                    Template = "OFR-{YEAR}-{SEQ:6}",
                    Strategy = "atomic_counter",
                    ResetFrequency = "yearly",
                    StartValue = 1,
                    Padding = 6,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new NumberingSettings
                {
                    Id = 2,
                    EntityName = "Sale",
                    IsEnabled = false,
                    Template = "SALE-{DATE:yyyyMMdd}-{GUID:5}",
                    Strategy = "guid",
                    ResetFrequency = "never",
                    StartValue = 1,
                    Padding = 5,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new NumberingSettings
                {
                    Id = 3,
                    EntityName = "ServiceOrder",
                    IsEnabled = false,
                    Template = "SO-{DATE:yyyyMMdd}-{GUID:6}",
                    Strategy = "guid",
                    ResetFrequency = "never",
                    StartValue = 1,
                    Padding = 6,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new NumberingSettings
                {
                    Id = 4,
                    EntityName = "Dispatch",
                    IsEnabled = false,
                    Template = "DISP-{TS:yyyyMMddHHmmss}",
                    Strategy = "timestamp_random",
                    ResetFrequency = "never",
                    StartValue = 1,
                    Padding = 6,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new NumberingSettings
                {
                    Id = 5,
                    EntityName = "Deal",
                    IsEnabled = false,
                    Template = "DEAL-{YEAR}-{SEQ:5}",
                    Strategy = "atomic_counter",
                    ResetFrequency = "yearly",
                    StartValue = 1,
                    Padding = 5,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}