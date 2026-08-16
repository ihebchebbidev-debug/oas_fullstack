using System.Text.RegularExpressions;

namespace MyApi.Modules.RetenueSource.Constants
{
    /// <summary>
    /// Matricule Fiscal (MF) validation for Tunisian taxpayers.
    /// Canonical structure: 7 digits + 1 check letter + category letter + taxpayer-type
    /// letter + 3-digit establishment code, e.g. <c>1234567A/P/M/000</c>.
    /// Separators are optional and case-insensitive. A bare 7-digit + letter core
    /// (e.g. <c>1234567A</c>) is also accepted since many records store only that.
    /// The old <c>^\d{10,15}$</c> check both rejected valid MFs (they contain letters)
    /// and accepted arbitrary digit strings.
    /// </summary>
    public static class TunisianTaxId
    {
        private static readonly Regex MfPattern = new(
            @"^\d{7}[A-Z](?:[\/\- ]?[A-Z](?:[\/\- ]?[A-Z])?)?(?:[\/\- ]?\d{3})?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>CIN (national ID card) — exactly 8 digits.</summary>
        private static readonly Regex CinPattern = new(@"^\d{8}$", RegexOptions.Compiled);

        public static string Normalize(string? value) =>
            (value ?? string.Empty).Trim().ToUpperInvariant();

        public static bool IsValidMatriculeFiscal(string? value)
        {
            var v = Normalize(value);
            return v.Length > 0 && MfPattern.IsMatch(v);
        }

        public static bool IsValidCin(string? value) => CinPattern.IsMatch(Normalize(value));

        /// <summary>
        /// A beneficiary identifier is acceptable when it matches the identifier type
        /// declared for it: 1 = Matricule Fiscal, 2 = CIN, 3/4/5 = passport / carte de
        /// séjour / other fiscal id (free-form, only non-empty is enforceable).
        /// </summary>
        public static bool IsValidForIdType(string? value, short idType) => idType switch
        {
            1 => IsValidMatriculeFiscal(value),
            2 => IsValidCin(value),
            _ => !string.IsNullOrWhiteSpace(value),
        };

        public static string DescribeExpectedFormat(short idType) => idType switch
        {
            1 => "Matricule Fiscal (e.g. 1234567A/P/M/000)",
            2 => "CIN (8 digits)",
            _ => "identifier",
        };
    }
}