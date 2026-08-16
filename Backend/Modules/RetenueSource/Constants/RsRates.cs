using System.Collections.Generic;

namespace MyApi.Modules.RetenueSource.Constants
{
    /// <summary>
    /// SINGLE SOURCE OF TRUTH for Retenue à la Source rates.
    /// Previously the same table was hardcoded in RSService, SupplierInvoiceService and
    /// (implicitly) TejOperationCodes, which had already drifted for code "05".
    /// Every consumer must resolve rates through this class.
    /// </summary>
    public static class RsRates
    {
        /// <summary>Legacy / short rate codes → percentage rate.</summary>
        public static readonly IReadOnlyDictionary<string, decimal> ByTypeCode =
            new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "10", 10m },
                { "05", 0.5m },
                { "03", 3m },
                { "20", 20m },
                { "P1", 1.5m },
                { "P2", 5m },
                { "P3", 10m },
                { "P4", 15m },
                { "P5", 25m },
            };

        public static bool IsKnownTypeCode(string? typeCode) =>
            !string.IsNullOrWhiteSpace(typeCode) && ByTypeCode.ContainsKey(typeCode);

        public static bool TryGetRate(string? typeCode, out decimal rate)
        {
            rate = 0m;
            return !string.IsNullOrWhiteSpace(typeCode) && ByTypeCode.TryGetValue(typeCode, out rate);
        }

        /// <summary>Rate for a legacy type code, or 0 when unknown.</summary>
        public static decimal GetRate(string? typeCode) =>
            TryGetRate(typeCode, out var r) ? r : 0m;

        /// <summary>
        /// Effective declared rate for a record/invoice: the operation code's official
        /// DGI rate wins when present (it is what <c>IdTypeOperation</c> declares to the
        /// tax authority); the legacy type code is only the fallback.
        /// </summary>
        public static decimal GetEffectiveRate(string? operationCode, string? typeCode)
        {
            var op = TejOperationCodes.Get(operationCode);
            if (op?.DefaultRate is decimal opRate) return opRate;
            return GetRate(typeCode);
        }

        /// <summary>
        /// True when the operation code declared in the XML and the legacy rate code
        /// disagree on the rate — the DGI cross-checks MontantRS against the operation's
        /// rate, so this must never be exported.
        /// </summary>
        public static bool IsRateMismatch(string? operationCode, string? typeCode)
        {
            var op = TejOperationCodes.Get(operationCode);
            if (op?.DefaultRate is not decimal opRate) return false;      // variable-rate operation
            if (!TryGetRate(typeCode, out var legacyRate)) return false;  // nothing to compare against
            return opRate != legacyRate;
        }
    }
}