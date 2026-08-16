using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Lookups.Models;
using MyApi.Modules.Numbering.Models;

namespace MyApi.Modules.Tenants.Services
{
    /// <summary>
    /// Seeds default data (LookupItems, Currencies, NumberingSettings) for a newly created tenant
    /// by cloning rows from the first/default tenant (TenantId = 0).
    /// </summary>
    public class TenantSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TenantSeeder> _logger;

        // The source tenant whose data we clone
        private const int SourceTenantId = 0;

        public TenantSeeder(ApplicationDbContext context, ILogger<TenantSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Clone all default lookup data from TenantId=0 into the new tenant.
        /// Called right after a new Tenant row is created.
        /// Uses raw SQL to bypass the Global Query Filter (which scopes to active tenant).
        /// </summary>
        public async Task SeedForNewTenantAsync(int newTenantId)
        {
            _logger.LogInformation("🌱 Seeding default data for new tenant {TenantId}...", newTenantId);

            var lookupCount = await CloneLookupItemsAsync(newTenantId);
            var currencyCount = await CloneCurrenciesAsync(newTenantId);
            var numberingCount = await CloneNumberingSettingsAsync(newTenantId);

            _logger.LogInformation(
                "🌱 Tenant {TenantId} seeded: {Lookups} lookup items, {Currencies} currencies, {Numbering} numbering settings",
                newTenantId, lookupCount, currencyCount, numberingCount);
        }

        private async Task<int> CloneLookupItemsAsync(int newTenantId)
        {
            // Use raw SQL to bypass EF Global Query Filter
            var sql = @"
                INSERT INTO ""LookupItems"" (
                    ""TenantId"", ""LookupType"", ""Name"", ""Description"", ""Color"",
                    ""IsActive"", ""SortOrder"", ""CreatedUser"", ""CreatedAt"",
                    ""IsDeleted"", ""IsDefault"", ""IsPaid"", ""Category"", ""Value"", ""DisplayOrder""
                )
                SELECT
                    @p0, ""LookupType"", ""Name"", ""Description"", ""Color"",
                    ""IsActive"", ""SortOrder"", 'system', NOW(),
                    FALSE, ""IsDefault"", ""IsPaid"", ""Category"", ""Value"", ""DisplayOrder""
                FROM ""LookupItems""
                WHERE ""TenantId"" = @p1 AND ""IsDeleted"" = FALSE";

            var count = await _context.Database.ExecuteSqlRawAsync(sql, newTenantId, SourceTenantId);
            _logger.LogDebug("  → Cloned {Count} LookupItems for tenant {TenantId}", count, newTenantId);
            return count;
        }

        private async Task<int> CloneCurrenciesAsync(int newTenantId)
        {
            var sql = @"
                INSERT INTO ""Currencies"" (
                    ""TenantId"", ""Code"", ""Name"", ""Symbol"",
                    ""IsActive"", ""IsDefault"", ""SortOrder"", ""CreatedUser"", ""CreatedAt"", ""IsDeleted""
                )
                SELECT
                    @p0, ""Code"", ""Name"", ""Symbol"",
                    ""IsActive"", ""IsDefault"", ""SortOrder"", 'system', NOW(), FALSE
                FROM ""Currencies""
                WHERE ""TenantId"" = @p1 AND ""IsDeleted"" = FALSE";

            var count = await _context.Database.ExecuteSqlRawAsync(sql, newTenantId, SourceTenantId);
            _logger.LogDebug("  → Cloned {Count} Currencies for tenant {TenantId}", count, newTenantId);
            return count;
        }

        private async Task<int> CloneNumberingSettingsAsync(int newTenantId)
        {
            var sql = @"
                INSERT INTO ""NumberingSettings"" (
                    ""TenantId"", ""entity_name"", ""is_enabled"", ""template"",
                    ""strategy"", ""reset_frequency"", ""start_value"", ""padding"",
                    ""created_at"", ""updated_at""
                )
                SELECT
                    @p0, ""entity_name"", ""is_enabled"", ""template"",
                    ""strategy"", ""reset_frequency"", ""start_value"", ""padding"",
                    NOW(), NOW()
                FROM ""NumberingSettings""
                WHERE ""TenantId"" = @p1";

            var count = await _context.Database.ExecuteSqlRawAsync(sql, newTenantId, SourceTenantId);
            _logger.LogDebug("  → Cloned {Count} NumberingSettings for tenant {TenantId}", count, newTenantId);
            return count;
        }
    }
}
