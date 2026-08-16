using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApi.Modules.Tenants.Models
{
    [Table("Tenants")]
    public class Tenant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int MainAdminUserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? CompanyLogoUrl { get; set; }

        [MaxLength(500)]
        public string? CompanyWebsite { get; set; }

        [MaxLength(50)]
        public string? CompanyPhone { get; set; }

        public string? CompanyAddress { get; set; }

        [MaxLength(2)]
        public string? CompanyCountry { get; set; }

        [MaxLength(100)]
        public string? Industry { get; set; }

        // ─── Per-company report / footer identity ───
        // Each tenant carries its OWN contact, address, legal and bank details.

        [MaxLength(255)]
        public string? CompanyEmail { get; set; }

        [MaxLength(255)]
        public string? CompanyTagline { get; set; }

        [MaxLength(120)]
        public string? CompanyCity { get; set; }

        [MaxLength(30)]
        public string? CompanyPostalCode { get; set; }

        [MaxLength(120)]
        public string? CompanyState { get; set; }

        [MaxLength(80)]
        public string? TaxId { get; set; }

        [MaxLength(80)]
        public string? RegistrationNumber { get; set; }

        [MaxLength(80)]
        public string? ShareCapital { get; set; }

        [MaxLength(160)]
        public string? BankName { get; set; }

        [MaxLength(80)]
        public string? BankAccount { get; set; }

        [MaxLength(40)]
        public string? BankSwift { get; set; }

        public string? ReportFooterMessage { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
