using System;

namespace MyApi.Modules.Numbering.Services
{
    /// <summary>
    /// Centralised GUID-based document number fallback used whenever
    /// <see cref="INumberingService.GetNextAsync"/> is unavailable or throws.
    /// Keeping this in one place avoids prefix / length drift between modules.
    /// </summary>
    public static class NumberingFallback
    {
        public static string Generate(string entity)
        {
            var now = DateTime.UtcNow;
            return entity switch
            {
                "Offer"        => $"OFR-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                "Sale"         => $"SALE-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                "ServiceOrder" => $"SO-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                "Dispatch"     => $"DISP-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                "Deal"         => $"DEAL-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                "Invoice"      => $"INV-{now:yyyy-MM-dd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                _              => $"DOC-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            };
        }
    }
}