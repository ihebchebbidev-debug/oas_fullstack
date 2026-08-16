using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Installations.DTOs
{
    public class WarrantyDto
    {
        public bool HasWarranty { get; set; }
        public string? WarrantyFrom { get; set; }
        public string? WarrantyTo { get; set; }
        public string? WarrantyProvider { get; set; }
        public string? WarrantyType { get; set; }
    }

    public class InstallationDto
    {
        public int Id { get; set; }
        public string InstallationNumber { get; set; } = string.Empty;
        public int ContactId { get; set; }
        public string SiteAddress { get; set; } = string.Empty;
        public string InstallationType { get; set; } = string.Empty;
        public DateTime InstallationDate { get; set; }
        public string Status { get; set; } = "active";
        public DateTime? WarrantyExpiry { get; set; }
        public string? Notes { get; set; }
        
        // New fields
        public string? Name { get; set; }
        public string? Model { get; set; }
        public string? Manufacturer { get; set; }
        public string? SerialNumber { get; set; }
        public string? Matricule { get; set; }
        public string? Category { get; set; }
        public string? Type { get; set; }
        public WarrantyDto? Warranty { get; set; }
        
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public List<MaintenanceHistoryDto> MaintenanceHistories { get; set; } = new List<MaintenanceHistoryDto>();
    }

    public class CreateInstallationDto
    {
        public int ContactId { get; set; }

        // Frontend sends these new fields
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? Model { get; set; }

        [MaxLength(200)]
        public string? Manufacturer { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        [MaxLength(100)]
        public string? Matricule { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "active";

        public WarrantyDto? Warranty { get; set; }

        // Legacy fields (still supported)
        [MaxLength(500)]
        public string? SiteAddress { get; set; }

        [MaxLength(100)]
        public string? InstallationType { get; set; }

        public DateTime? InstallationDate { get; set; }

        public DateTime? WarrantyExpiry { get; set; }

        public string? Notes { get; set; }
    }

    public class UpdateInstallationDto
    {
        public int? ContactId { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? Model { get; set; }

        [MaxLength(200)]
        public string? Manufacturer { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        [MaxLength(100)]
        public string? Matricule { get; set; }

        [MaxLength(500)]
        public string? SiteAddress { get; set; }

        [MaxLength(100)]
        public string? InstallationType { get; set; }

        public DateTime? InstallationDate { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; }

        public DateTime? WarrantyExpiry { get; set; }

        public WarrantyDto? Warranty { get; set; }

        public string? Notes { get; set; }
    }

    public class MaintenanceHistoryDto
    {
        public int Id { get; set; }
        public int InstallationId { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public string MaintenanceType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public decimal? Cost { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateMaintenanceHistoryDto
    {
        public DateTime MaintenanceDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string MaintenanceType { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        public decimal? Cost { get; set; }

        public DateTime? NextMaintenanceDate { get; set; }
    }

    public class PaginatedInstallationResponse
    {
        public List<InstallationDto> Installations { get; set; } = new List<InstallationDto>();
        public PaginationInfo Pagination { get; set; } = new PaginationInfo();
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    // =====================================================
    // Bulk Import DTOs - Supports up to 10,000+ records
    // =====================================================

    public class BulkImportInstallationRequestDto
    {
        public List<CreateInstallationDto> Installations { get; set; } = new();
        
        /// <summary>
        /// If true, skip installations with duplicate serial number instead of failing
        /// </summary>
        public bool SkipDuplicates { get; set; } = true;
        
        /// <summary>
        /// If true, update existing installations with matching serial number
        /// </summary>
        public bool UpdateExisting { get; set; } = false;
    }

    public class BulkImportInstallationResultDto
    {
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<InstallationDto> ImportedInstallations { get; set; } = new();
    }
}
