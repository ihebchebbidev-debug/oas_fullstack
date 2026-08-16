using System.Collections.Generic;

namespace MyApi.Modules.RetenueSource.Constants
{
    /// <summary>
    /// Official IdTypeOperation table from DGI / RiTEJ cahier de charges (v1.0).
    /// Each code carries a default rate; when null, the code accepts variable rates
    /// and the caller must provide one (or the operation is exoneré).
    /// Codes follow the pattern "RSn_NNNNNN" where n is the family group.
    /// </summary>
    public static class TejOperationCodes
    {
        public record OperationCode(string Code, string LabelFr, decimal? DefaultRate, string Family);

        public static readonly IReadOnlyList<OperationCode> All = new List<OperationCode>
        {
            // RS1 — Honoraires & services (taux usuels)
            new("RS1_000001", "Honoraires - personnes morales / loi commune", 3m,  "Honoraires"),
            new("RS1_000002", "Honoraires - personnes physiques", 10m, "Honoraires"),
            new("RS1_000003", "Honoraires - non résidents", 15m, "Honoraires"),
            new("RS1_000004", "Commissions, courtages, vacations", 15m, "Honoraires"),
            new("RS1_000005", "Loyers", 10m, "Loyers"),
            new("RS1_000006", "Loyers - non résidents", 15m, "Loyers"),

            // RS2 — Marchés / fournitures (sur paiements > seuil)
            new("RS2_000001", "Montants >= 1000 TND TTC", 1m,  "Marchés"),
            new("RS2_000002", "Marchés conclus avec l'État / collectivités",            1.5m, "Marchés"),
            new("RS2_000003", "Acquisitions soumises au taux réduit (0,5%)",            0.5m, "Marchés"),

            // RS3 — Redevances, intérêts, plus-values
            new("RS3_000001", "Redevances - non résidents", 15m, "Redevances"),
            new("RS3_000002", "Intérêts des prêts payés à l'étranger", 20m, "Intérêts"),
            new("RS3_000003", "Intérêts servis aux établissements bancaires non résidents", 5m, "Intérêts"),
            new("RS3_000004", "Plus-values immobilières", 2.5m, "Plus-values"),

            // RS4 — Jeux, lots, prix
            new("RS4_000001", "Lots de loterie / jeux", 25m, "Jeux"),

            // RS5 — Salaires (déclarés via TEJ-S, ici pour cohérence)
            new("RS5_000001", "Salaires & assimilés (barème IRPP)", null, "Salaires"),

            // RS6 — Opérations exonérées par convention bilatérale
            new("RS6_000001", "Exonération par convention de non-double imposition", 0m, "Exonérations"),
        };

        public static OperationCode? Get(string? code) =>
            code is null ? null : System.Linq.Enumerable.FirstOrDefault(All, x => x.Code == code);

        /// <summary>Map legacy rate codes (10/05/03/20) to the closest official operation code.</summary>
        public static string LegacyToOperationCode(string? legacyCode) => legacyCode switch
        {
            "10" => "RS1_000002",  // 10% honoraires PP
            "05" => "RS2_000003",  // 0.5% — rate-matched (was RS3_000003 @5%, a real mismatch)
            "03" => "RS1_000001",  // 3% honoraires PM
            "20" => "RS3_000002",  // 20% intérêts
            "P1" => "RS2_000002",  // 1.5%
            "P2" => "RS3_000003",  // 5%
            "P3" => "RS1_000002",  // 10%
            "P4" => "RS1_000003",  // 15%
            "P5" => "RS4_000001",  // 25%
            _    => "RS1_000001",
        };
    }
}
