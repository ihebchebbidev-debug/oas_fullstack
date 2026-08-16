using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.RetenueSource.Models
{
    [Table("RSRecords")]
    public class RSRecord : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>"offer" | "sale" | "supplier_invoice"</summary>
        [Required] [Column("EntityType")] [MaxLength(20)]
        public string EntityType { get; set; } = string.Empty;

        [Required] [Column("EntityId")]
        public int EntityId { get; set; }

        [Column("EntityNumber")] [MaxLength(50)]
        public string? EntityNumber { get; set; }

        [Required] [Column("InvoiceNumber")] [MaxLength(100)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required] [Column("InvoiceDate")] public DateTime InvoiceDate { get; set; }
        [Required] [Column("InvoiceAmount", TypeName = "decimal(15,2)")] public decimal InvoiceAmount { get; set; }
        [Required] [Column("PaymentDate")] public DateTime PaymentDate { get; set; }
        [Required] [Column("AmountPaid", TypeName = "decimal(15,2)")] public decimal AmountPaid { get; set; }
        [Required] [Column("RSAmount", TypeName = "decimal(15,2)")] public decimal RSAmount { get; set; }

        /// <summary>Legacy rate code (10/05/03/20). Kept for backward compatibility — TEJ now uses <see cref="OperationCode"/>.</summary>
        [Required] [Column("RSTypeCode")] [MaxLength(10)]
        public string RSTypeCode { get; set; } = "10";

        [Required] [Column("SupplierName")] [MaxLength(255)] public string SupplierName { get; set; } = string.Empty;
        [Required] [Column("SupplierTaxId")] [MaxLength(50)] public string SupplierTaxId { get; set; } = string.Empty;
        [Column("SupplierAddress")] [MaxLength(500)] public string? SupplierAddress { get; set; }

        [Required] [Column("PayerName")] [MaxLength(255)] public string PayerName { get; set; } = string.Empty;
        [Required] [Column("PayerTaxId")] [MaxLength(50)] public string PayerTaxId { get; set; } = string.Empty;
        [Column("PayerAddress")] [MaxLength(500)] public string? PayerAddress { get; set; }

        [Required] [Column("Status")] [MaxLength(20)]
        public string Status { get; set; } = "pending";

        [Column("TEJExported")] public bool TEJExported { get; set; } = false;
        [Column("TEJFileName")] [MaxLength(255)] public string? TEJFileName { get; set; }

        /// <summary>
        /// Soft delete. RS records are fiscal-declaration data and must never be
        /// physically removed (audit trail), unlike the previous hard delete.
        /// </summary>
        [Column("IsDeleted")] public bool IsDeleted { get; set; } = false;
        [Column("DeletedAt")] public DateTime? DeletedAt { get; set; }
        [Column("DeletedBy")] [MaxLength(100)] public string? DeletedBy { get; set; }

        /// <summary>Deposit sequence for the month's TEJ file (0 = initial, 1..n = rectifications).</summary>
        [Column("DepotSequence")] public int DepotSequence { get; set; } = 0;

        // ── Compliance ──
        [Column("DeclarationDeadline")] public DateTime? DeclarationDeadline { get; set; }
        [Column("IsOverdue")] public bool IsOverdue { get; set; } = false;
        [Column("DaysLate")] public int DaysLate { get; set; } = 0;
        [Column("PenaltyAmount", TypeName = "decimal(15,2)")] public decimal PenaltyAmount { get; set; } = 0m;
        [Column("SupplierType")] [MaxLength(20)] public string? SupplierType { get; set; }
        [Column("IsExemptByTreaty")] public bool IsExemptByTreaty { get; set; } = false;
        [Column("TreatyCode")] [MaxLength(20)] public string? TreatyCode { get; set; }
        [Column("TEJAcceptanceNumber")] [MaxLength(255)] public string? TEJAcceptanceNumber { get; set; }
        [Column("TEJTransmissionStatus")] [MaxLength(20)] public string TEJTransmissionStatus { get; set; } = "pending";
        [Column("Notes")] [MaxLength(1000)] public string? Notes { get; set; }

        // ── TEJ / RiTEJ Compliance (DGI cahier de charges v1.0) ──
        /// <summary>IdTypeOperation per official table (e.g. "RS1_000001").</summary>
        [Column("OperationCode")] [MaxLength(20)] public string? OperationCode { get; set; }

        /// <summary>CNPC if applicable.</summary>
        [Column("Cnpc")] [MaxLength(20)] public string? Cnpc { get; set; }

        /// <summary>Prise en charge flag (declarant supports the RS instead of withholding from the beneficiary).</summary>
        [Column("PriseEnCharge")] public bool PriseEnCharge { get; set; } = false;

        [Column("AnneeFacturation")] public int? AnneeFacturation { get; set; }
        [Column("RefCertifChezDeclarant")] [MaxLength(50)] public string? RefCertifChezDeclarant { get; set; }

        // RS-TVA (separate withholding on VAT, when applicable)
        [Column("RsTvaCode")] [MaxLength(20)] public string? RsTvaCode { get; set; }
        [Column("RsTvaTaux", TypeName = "decimal(5,2)")] public decimal? RsTvaTaux { get; set; }
        [Column("RsTvaAmount", TypeName = "decimal(15,2)")] public decimal RsTvaAmount { get; set; } = 0m;

        /// <summary>Computed: AmountPaid - RSAmount - RsTvaAmount.</summary>
        [Column("MontantNetServi", TypeName = "decimal(15,2)")] public decimal MontantNetServi { get; set; } = 0m;

        /// <summary>
        /// Real tax-exclusive (hors taxe) amount of the invoice, pro-rated to the declared
        /// basis. Declared as TEJ <c>MontantHT</c> — previously AmountPaid (a TTC figure
        /// already net of RS) was written there, misstating the base to the DGI.
        /// </summary>
        [Column("MontantHT", TypeName = "decimal(15,2)")] public decimal MontantHT { get; set; } = 0m;

        /// <summary>
        /// Real VAT of the invoice, pro-rated to the declared basis. Declared as TEJ
        /// <c>MontantTVA</c>. Distinct from <see cref="RsTvaAmount"/> (VAT *withheld*),
        /// which is declared as <c>MontantRSTVA</c>.
        /// </summary>
        [Column("MontantTvaFacture", TypeName = "decimal(15,2)")] public decimal MontantTvaFacture { get; set; } = 0m;

        // Beneficiary snapshot (TEJ Beneficiaire block)
        [Column("BeneficiaireCategorie")] [MaxLength(2)] public string? BeneficiaireCategorie { get; set; }
        [Column("BeneficiaireIsResident")] public bool BeneficiaireIsResident { get; set; } = true;
        [Column("BeneficiaireIdType")] public short? BeneficiaireIdType { get; set; }
        [Column("BeneficiaireDateNaissance")] public DateTime? BeneficiaireDateNaissance { get; set; }
        [Column("BeneficiairePaysCode")] [MaxLength(3)] public string BeneficiairePaysCode { get; set; } = "TN";

        /// <summary>0=AjouterCertificats, 1=ModifierCertificats, 2=AnnulerCertificats.</summary>
        [Column("Acte")] public short Acte { get; set; } = 0;

        [Required] [Column("CreatedAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Required] [Column("CreatedBy")] [MaxLength(100)] public string CreatedBy { get; set; } = string.Empty;
        [Column("ModifiedAt")] public DateTime? ModifiedAt { get; set; }
        [Column("ModifiedBy")] [MaxLength(100)] public string? ModifiedBy { get; set; }
    }
}
