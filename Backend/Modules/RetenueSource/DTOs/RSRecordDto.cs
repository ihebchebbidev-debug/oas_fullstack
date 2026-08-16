using System;
using System.Collections.Generic;

namespace MyApi.Modules.RetenueSource.DTOs
{
    // ─── Response DTOs ───

    public class RSRecordDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string? EntityNumber { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal InvoiceAmount { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal RSAmount { get; set; }
        public string RSTypeCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierTaxId { get; set; } = string.Empty;
        public string? SupplierAddress { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public string PayerTaxId { get; set; } = string.Empty;
        public string? PayerAddress { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool TEJExported { get; set; }
        public string? TEJFileName { get; set; }
        public string? Notes { get; set; }
        
        // ─── CRITICAL COMPLIANCE FIELDS ───
        public DateTime? DeclarationDeadline { get; set; }
        public bool IsOverdue { get; set; }
        public int DaysLate { get; set; }
        public decimal PenaltyAmount { get; set; }
        
        // ─── MEDIUM PRIORITY COMPLIANCE FIELDS ───
        public string? SupplierType { get; set; }  // individual, company, non_resident
        public bool IsExemptByTreaty { get; set; }
        public string? TreatyCode { get; set; }
        public string? TEJAcceptanceNumber { get; set; }
        public string TEJTransmissionStatus { get; set; } = "pending"; // pending, accepted, rejected

        // ─── TEJ / RiTEJ (DGI cahier de charges) ───
        public string? OperationCode { get; set; }
        public string? Cnpc { get; set; }
        public bool PriseEnCharge { get; set; }
        public int? AnneeFacturation { get; set; }
        public string? RefCertifChezDeclarant { get; set; }
        public string? RsTvaCode { get; set; }
        public decimal? RsTvaTaux { get; set; }
        public decimal RsTvaAmount { get; set; }
        public decimal MontantNetServi { get; set; }
        public string? BeneficiaireCategorie { get; set; }
        public bool BeneficiaireIsResident { get; set; } = true;
        public short? BeneficiaireIdType { get; set; }
        public DateTime? BeneficiaireDateNaissance { get; set; }
        public string BeneficiairePaysCode { get; set; } = "TN";
        public short Acte { get; set; } = 0;

        // ─── AUDIT TRAIL ───
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }
    }

    // ─── Create Request ───

    public class CreateRSRecordDto
    {
        public string EntityType { get; set; } = string.Empty; // "offer" or "sale"
        public int EntityId { get; set; }
        public string? EntityNumber { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal InvoiceAmount { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public string RSTypeCode { get; set; } = "10";
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierTaxId { get; set; } = string.Empty;
        public string? SupplierAddress { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public string PayerTaxId { get; set; } = string.Empty;
        public string? PayerAddress { get; set; }
        public string? Notes { get; set; }
        
        // ─── MEDIUM PRIORITY COMPLIANCE FIELDS ───
        public string? SupplierType { get; set; }  // individual, company, non_resident
        public bool IsExemptByTreaty { get; set; } = false;
        public string? TreatyCode { get; set; }

        // ─── TEJ / RiTEJ ───
        public string? OperationCode { get; set; }
        public string? Cnpc { get; set; }
        public bool PriseEnCharge { get; set; } = false;
        public int? AnneeFacturation { get; set; }
        public string? RefCertifChezDeclarant { get; set; }
        public string? RsTvaCode { get; set; }
        public decimal? RsTvaTaux { get; set; }
        public decimal RsTvaAmount { get; set; } = 0m;
        public string? BeneficiaireCategorie { get; set; }
        public bool BeneficiaireIsResident { get; set; } = true;
        public short? BeneficiaireIdType { get; set; }
        public DateTime? BeneficiaireDateNaissance { get; set; }
        public string BeneficiairePaysCode { get; set; } = "TN";
        public short Acte { get; set; } = 0;
    }

    // ─── Update Request ───

    public class UpdateRSRecordDto
    {
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? RSTypeCode { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierTaxId { get; set; }
        public string? SupplierAddress { get; set; }
        public string? PayerName { get; set; }
        public string? PayerTaxId { get; set; }
        public string? PayerAddress { get; set; }
        public string? Notes { get; set; }
        public string? Status { get; set; }
        
        // ─── COMPLIANCE FIELDS (Updateable) ───
        public string? SupplierType { get; set; }
        public bool? IsExemptByTreaty { get; set; }
        public string? TreatyCode { get; set; }
        public string? TEJAcceptanceNumber { get; set; }
        public string? TEJTransmissionStatus { get; set; }
    }

    // ─── TEJ Export Request ───

    public class TEJExportRequestDto
    {
        public int Month { get; set; }
        public int Year { get; set; }

        /// <summary>
        /// Optional scope. When set, only RS records attached to this entity are
        /// exported and marked as exported. When null the whole period is exported.
        /// </summary>
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }

        /// <summary>
        /// Declarant info (payer company). If not provided, uses defaults from first RS record.
        /// </summary>
        public TEJDeclarantDto? Declarant { get; set; }
    }

    public class TEJDeclarantDto
    {
        public string Name { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        /// <summary>PM (legal entity) or PP (individual) — declared as CategorieContribuable.</summary>
        public string? Categorie { get; set; } = "PM";
    }

    // ─── TEJ Export Response ───

    public class TEJExportResponseDto
    {
        public int LogId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public decimal TotalRSAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public int? DocumentId { get; set; }
    }

    /// <summary>
    /// Result of building the TEJ XML for a single supplier invoice on demand.
    /// Ok=false → Missing lists the user-actionable "please fill X" items.
    /// Ok=true  → Xml + FileName are ready to download.
    /// </summary>
    public class TejInvoiceXmlResult
    {
        public bool Ok { get; set; }
        public List<string> Missing { get; set; } = new();
        public string? Xml { get; set; }
        public string? FileName { get; set; }
    }

    // ─── TEJ Export Log Response ───

    public class TEJExportLogDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime ExportDate { get; set; }
        public string ExportedBy { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public int RecordCount { get; set; }
        public decimal TotalRSAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public int? DocumentId { get; set; }
    }

    // ─── Paginated Response ───

    public class PaginatedRSResponse
    {
        public List<RSRecordDto> Records { get; set; } = new();
        public RSPaginationInfo Pagination { get; set; } = new();
    }

    public class RSPaginationInfo
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }

    // ─── RS Stats ───

    public class RSStatsDto
    {
        public int TotalRecords { get; set; }
        public int PendingRecords { get; set; }
        public int ExportedRecords { get; set; }
        public decimal TotalRSAmount { get; set; }
        public decimal TotalAmountPaid { get; set; }
    }

    // ─── RS Calculation Response ───

    public class RSCalculationDto
    {
        public decimal AmountPaid { get; set; }
        public string RSTypeCode { get; set; } = string.Empty;
        public decimal RSRate { get; set; }
        public decimal RSAmount { get; set; }
        public decimal NetPayment { get; set; }
    }
}
