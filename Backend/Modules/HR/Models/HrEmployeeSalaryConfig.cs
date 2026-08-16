using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.HR.Models
{
    [ModuleScope("hr")]
    [Table("hr_employee_salary_configs")]
    public class HrEmployeeSalaryConfig : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("gross_salary")]
        public decimal GrossSalary { get; set; }

        [Column("is_head_of_family")]
        public bool IsHeadOfFamily { get; set; }

        [Column("children_count")]
        public int ChildrenCount { get; set; }

        [Column("custom_deductions")]
        public decimal? CustomDeductions { get; set; }

        [Column("bank_account")]
        [MaxLength(100)]
        public string? BankAccount { get; set; }

        [Column("cnss_number")]
        [MaxLength(100)]
        public string? CnssNumber { get; set; }

        [Column("hire_date")]
        public DateTime? HireDate { get; set; }

        [Column("department")]
        [MaxLength(100)]
        public string? Department { get; set; }

        [Column("position")]
        [MaxLength(100)]
        public string? Position { get; set; }

        [Column("employment_type")]
        [MaxLength(50)]
        public string EmploymentType { get; set; } = "full_time";

        // ---- Contract tracking (Round 1) ----
        [Column("contract_type")]
        [MaxLength(20)]
        public string? ContractType { get; set; } // CDI | CDD | Stage

        [Column("contract_end_date")]
        public DateTime? ContractEndDate { get; set; }

        [Column("cin")]
        [MaxLength(50)]
        public string? Cin { get; set; }

        [Column("birth_date")]
        public DateTime? BirthDate { get; set; }

        [Column("marital_status")]
        [MaxLength(20)]
        public string? MaritalStatus { get; set; }

        [Column("address_line1")]
        [MaxLength(200)]
        public string? AddressLine1 { get; set; }

        [Column("address_line2")]
        [MaxLength(200)]
        public string? AddressLine2 { get; set; }

        [Column("city")]
        [MaxLength(100)]
        public string? City { get; set; }

        [Column("postal_code")]
        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [Column("emergency_contact_name")]
        [MaxLength(200)]
        public string? EmergencyContactName { get; set; }

        [Column("emergency_contact_phone")]
        [MaxLength(30)]
        public string? EmergencyContactPhone { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
