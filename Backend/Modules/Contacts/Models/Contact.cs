using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Contacts.Models
{
    [ModuleScope("contacts")]
    [Table("Contacts")]
    public class Contact : ITenantEntity, MyApi.Modules.Shared.Models.ISoftDeletable
    {
        public int TenantId { get; set; }
        
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] [MaxLength(100)] public string FirstName { get; set; } = string.Empty;
        [Required] [MaxLength(100)] public string LastName { get; set; } = string.Empty;
        [MaxLength(255)] [EmailAddress] public string? Email { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(200)] public string? Company { get; set; }
        [MaxLength(100)] public string? Position { get; set; }
        [MaxLength(500)] public string? Address { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        [MaxLength(100)] public string? Country { get; set; }
        [MaxLength(20)]  public string? PostalCode { get; set; }
        [Column(TypeName = "text")] public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [Required] [MaxLength(100)] public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        [MaxLength(100)] public string? ModifiedBy { get; set; }

        [Required] [MaxLength(255)] public string Name { get; set; } = string.Empty;
        [MaxLength(50)] public string? Status { get; set; } = "active";
        [MaxLength(50)] public string? Type { get; set; } = "individual";
        [MaxLength(500)] public string? Avatar { get; set; }
        public bool Favorite { get; set; } = false;
        public DateTime? LastContactDate { get; set; }
        public bool IsDeleted { get; set; } = false;

        // ── Fiscal identification ──
        [MaxLength(50)]  public string? Cin { get; set; }
        [MaxLength(100)] public string? MatriculeFiscale { get; set; }

        // ── Geolocation ──
        [Column("Latitude",  TypeName = "decimal(10,7)")] public decimal? Latitude { get; set; }
        [Column("Longitude", TypeName = "decimal(10,7)")] public decimal? Longitude { get; set; }
        [Column("HasLocation")] public int HasLocation { get; set; } = 0;

        // ── TEJ / RiTEJ compliance fields (DGI cahier de charges) ──
        /// <summary>
        /// Marks THIS contact as the tenant's own company — the TEJ/RiTEJ declarant
        /// (the withholder). Without an explicit marker the declarant used to be
        /// guessed as "first company contact with a Matricule Fiscal", which can
        /// resolve to a B2B customer and declare the wrong entity to the DGI.
        /// </summary>
        [Column("IsTejDeclarant")]
        public bool IsTejDeclarant { get; set; } = false;

        /// <summary>PM (Personne Morale) or PP (Personne Physique). Drives TEJ CategorieContribuable.</summary>
        [Column("CategorieContribuable")] [MaxLength(2)]
        public string? CategorieContribuable { get; set; }

        /// <summary>Tunisian resident? Maps to TEJ Resident (1/0).</summary>
        [Column("IsResident")]
        public bool IsResident { get; set; } = true;

        /// <summary>TEJ TypeIdentifiant: 1=MF, 2=CIN, 3=Passeport, 4=CarteSejour, 5=Autre.</summary>
        [Column("IdTaxpayerType")]
        public short? IdTaxpayerType { get; set; }

        /// <summary>Required for PP non-residents in some operation codes.</summary>
        [Column("DateNaissance")]
        public DateTime? DateNaissance { get; set; }

        /// <summary>ISO-3 country code (TN by default).</summary>
        [Column("PaysCode")] [MaxLength(3)]
        public string PaysCode { get; set; } = "TN";

        /// <summary>Used when IdTaxpayerType = 5 (Autre).</summary>
        [Column("AutreIdentifiantFiscal")] [MaxLength(50)]
        public string? AutreIdentifiantFiscal { get; set; }

        // Navigation
        public virtual ICollection<ContactNote> ContactNotes { get; set; } = new List<ContactNote>();
        public virtual ICollection<ContactTagAssignment> TagAssignments { get; set; } = new List<ContactTagAssignment>();
        public virtual ICollection<ContactUserGroupAssignment> UserGroupAssignments { get; set; } = new List<ContactUserGroupAssignment>();

    }
}
