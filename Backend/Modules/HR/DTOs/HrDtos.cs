using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.HR.DTOs
{
    // -------- Salary Config (existing) --------
    public class UpsertSalaryConfigDto
    {
        public decimal? GrossSalary { get; set; }
        public bool? IsHeadOfFamily { get; set; }
        public int? ChildrenCount { get; set; }
        public decimal? CustomDeductions { get; set; }
        public string? BankAccount { get; set; }
        public string? CnssNumber { get; set; }
        public DateTime? HireDate { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? EmploymentType { get; set; }
        public string? ContractType { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public string? Cin { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? MaritalStatus { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        // Optional reason for salary change (logged into history)
        public string? SalaryChangeReason { get; set; }
    }

    public class HrEmployeeSalaryConfigDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public decimal GrossSalary { get; set; }
        public bool IsHeadOfFamily { get; set; }
        public int ChildrenCount { get; set; }
        public decimal? CustomDeductions { get; set; }
        public string? BankAccount { get; set; }
        public string? CnssNumber { get; set; }
        public DateTime? HireDate { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string EmploymentType { get; set; } = "full_time";
        public string? ContractType { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public string? Cin { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? MaritalStatus { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
    }

    // -------- Leaves (existing) --------
    public class HrLeaveBalanceDto
    {
        public int UserId { get; set; }
        public string LeaveType { get; set; } = "annual";
        public decimal AnnualAllowance { get; set; }
        public decimal Used { get; set; }
        public decimal Remaining { get; set; }
        public decimal Pending { get; set; }
    }

    public class SetLeaveAllowanceDto
    {
        [Required] public int Year { get; set; }
        [Required] public string LeaveType { get; set; } = "annual";
        [Required] public decimal AnnualAllowance { get; set; }
    }

    // -------- Attendance (Round 2) --------
    public class HrAttendanceDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int BreakMinutes { get; set; }
        public decimal TotalHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public string Status { get; set; } = "present";
        public string? Notes { get; set; }
        public string Source { get; set; } = "manual";
    }

    public class UpsertHrAttendanceDto
    {
        /// <summary>Existing row id when editing. 0/absent means "create or match by (user, day)".</summary>
        public int Id { get; set; }
        [Required] public int UserId { get; set; }
        [Required] public DateTime Date { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int BreakMinutes { get; set; }
        public decimal? TotalHours { get; set; }
        public decimal? OvertimeHours { get; set; }
        public string Status { get; set; } = "present";
        public string? Notes { get; set; }
        public string Source { get; set; } = "manual";
    }

    public class HrAttendanceSettingsDto
    {
        public int Id { get; set; }
        public List<int> WorkDays { get; set; } = new();
        public decimal StandardHoursPerDay { get; set; }
        public decimal OvertimeThresholdHours { get; set; }
        public decimal OvertimeMultiplier { get; set; }
        public int LateThresholdMinutes { get; set; }
        public string RoundingMethod { get; set; } = "15min";
        public string CalculationMethod { get; set; } = "actual_hours";
    }

    public class UpsertHrAttendanceSettingsDto
    {
        public List<int> WorkDays { get; set; } = new();
        [Required] public decimal StandardHoursPerDay { get; set; }
        [Required] public decimal OvertimeThresholdHours { get; set; }
        [Required] public decimal OvertimeMultiplier { get; set; }
        public int LateThresholdMinutes { get; set; }
        public string RoundingMethod { get; set; } = "15min";
        public string CalculationMethod { get; set; } = "actual_hours";
    }

    public class ImportHrAttendanceRowDto
    {
        [Required] public int UserId { get; set; }
        [Required] public DateTime Date { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int BreakMinutes { get; set; }
        public decimal? TotalHours { get; set; }
        public decimal? OvertimeHours { get; set; }
        public string Status { get; set; } = "present";
        public string? Notes { get; set; }
        public string Source { get; set; } = "csv_import";
    }

    public class HrAttendanceImportResultDto
    {
        public int Imported { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
    }

    // -------- Payroll (extended with allowances/bonuses + employer CNSS) --------
    public class CreatePayrollRunDto
    {
        [Required] public int Month { get; set; }
        [Required] public int Year { get; set; }
    }

    public class HrPayrollEntryDto
    {
        public int Id { get; set; }
        public int PayrollRunId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal GrossSalary { get; set; }
        public decimal Allowances { get; set; }
        public decimal Bonuses { get; set; }
        public decimal Cnss { get; set; }
        public decimal EmployerCnss { get; set; }
        public decimal TaxableGross { get; set; }
        public decimal Abattement { get; set; }
        public decimal TaxableBase { get; set; }
        public decimal Irpp { get; set; }
        public decimal Css { get; set; }
        public decimal NetSalary { get; set; }
        public decimal WorkedDays { get; set; }
        public decimal TotalHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal LeaveDays { get; set; }
        public object? Details { get; set; }
    }

    public class HrPayrollRunDto
    {
        public int Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string Status { get; set; } = "draft";
        public List<HrPayrollEntryDto> Entries { get; set; } = new();
        public decimal TotalGross { get; set; }
        public decimal TotalNet { get; set; }
        public decimal TotalCnss { get; set; }
        public decimal TotalEmployerCnss { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }

    // -------- Departments (existing) --------
    public class HrDepartmentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int? ParentId { get; set; }
        public int? ManagerId { get; set; }
        public string? Description { get; set; }
        public int? Position { get; set; }
    }

    public class UpsertDepartmentDto
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public int? ParentId { get; set; }
        public int? ManagerId { get; set; }
        public string? Description { get; set; }
        public int? Position { get; set; }
    }

    // -------- Bonuses & Costs (NEW) --------
    public class HrBonusCostDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string Kind { get; set; } = "bonus";
        public string? Category { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Frequency { get; set; } = "monthly";
        public int Year { get; set; }
        public int Month { get; set; }
        public bool AffectsPayroll { get; set; }
        public bool SubjectToCnss { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpsertBonusCostDto
    {
        [Required] public int UserId { get; set; }
        public string Kind { get; set; } = "bonus";
        public string? Category { get; set; }
        [Required] public string Label { get; set; } = string.Empty;
        [Required] public decimal Amount { get; set; }
        public string Frequency { get; set; } = "monthly";
        [Required] public int Year { get; set; }
        [Required] public int Month { get; set; }
        public bool AffectsPayroll { get; set; } = true;
        public bool SubjectToCnss { get; set; } = false;
        public string? Notes { get; set; }
    }

    // -------- CNSS Rates (NEW) --------
    public class HrCnssRateDto
    {
        public int Id { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public decimal EmployeeRate { get; set; }
        public decimal EmployerRate { get; set; }
        public decimal CssRate { get; set; }
        public decimal SalaryCeiling { get; set; }
        public decimal AbattementHeadOfFamily { get; set; }
        public decimal AbattementPerChild { get; set; }
        public List<IrppBracketDto> IrppBrackets { get; set; } = new();
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
    }

    public class IrppBracketDto
    {
        public decimal From { get; set; }
        public decimal? To { get; set; }
        public decimal Rate { get; set; }
    }

    public class UpsertCnssRateDto
    {
        [Required] public DateTime EffectiveFrom { get; set; }
        [Required] public decimal EmployeeRate { get; set; }
        [Required] public decimal EmployerRate { get; set; }
        [Required] public decimal CssRate { get; set; }
        public decimal SalaryCeiling { get; set; } = 0m;
        [Required] public decimal AbattementHeadOfFamily { get; set; }
        [Required] public decimal AbattementPerChild { get; set; }
        public List<IrppBracketDto> IrppBrackets { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
    }

    // -------- Public Holidays (NEW) --------
    public class HrPublicHolidayDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "civil";
        public bool IsRecurring { get; set; }
    }

    public class UpsertPublicHolidayDto
    {
        [Required] public DateTime Date { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "civil";
        public bool IsRecurring { get; set; } = false;
    }

    // -------- Employee Documents (NEW) --------
    public class HrEmployeeDocumentDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DocType { get; set; } = "other";
        public string Title { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public long? FileSize { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpsertEmployeeDocumentDto
    {
        [Required] public int UserId { get; set; }
        public string DocType { get; set; } = "other";
        [Required] public string Title { get; set; } = string.Empty;
        [Required] public string FileUrl { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public long? FileSize { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    // -------- Audit Log (NEW) --------
    public class HrAuditLogDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public object? Payload { get; set; }
        public int? ActorUserId { get; set; }
        public string? ActorName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // -------- Salary History (NEW) --------
    public class HrSalaryHistoryDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public decimal? PreviousGross { get; set; }
        public decimal NewGross { get; set; }
        public string Currency { get; set; } = "TND";
        public DateTime EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public int? ChangedBy { get; set; }
    }

    // -------- Reports (NEW) --------
    public class HrReportPeriodDto
    {
        [Required] public int Year { get; set; }
        public int? Month { get; set; }
    }

    public class HrEmployeeCostDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public decimal Gross { get; set; }
        public decimal Bonuses { get; set; }
        public decimal Allowances { get; set; }
        public decimal EmployerCnss { get; set; }
        public decimal TotalCost { get; set; }
        public decimal YtdGross { get; set; }
        public decimal YtdBonuses { get; set; }
        public decimal YtdEmployerCnss { get; set; }
        public decimal YtdTotalCost { get; set; }
    }

    public class HrCnssMonthlyDeclarationDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalSalarySubject { get; set; }
        public decimal TotalEmployeeCnss { get; set; }
        public decimal TotalEmployerCnss { get; set; }
        public decimal TotalCss { get; set; }
        public List<HrCnssEmployeeLineDto> Lines { get; set; } = new();
    }

    public class HrCnssEmployeeLineDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? CnssNumber { get; set; }
        public decimal SalarySubject { get; set; }
        public decimal EmployeeCnss { get; set; }
        public decimal EmployerCnss { get; set; }
        public decimal Css { get; set; }
    }

    // -------- Active Leaves for Planning calendar (Round 1) --------
    public class HrActiveLeaveDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string LeaveType { get; set; } = "annual";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "approved";
    }

    // -------- Contract expiry alerts (Round 1) --------
    public class HrContractExpiryDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? ContractType { get; set; }
        public DateTime ContractEndDate { get; set; }
        public int DaysUntilExpiry { get; set; }
    }
}
