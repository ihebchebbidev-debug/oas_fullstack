using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyApi.Modules.HR.DTOs;

namespace MyApi.Modules.HR.Services
{
    public interface IHrService
    {
        // ---- Employees & Salary ----
        Task<List<object>> GetEmployeesAsync();
        Task<object> GetEmployeeDetailAsync(int userId);
        Task<HrEmployeeSalaryConfigDto> UpsertSalaryConfigAsync(int userId, UpsertSalaryConfigDto dto, int actorUserId);
        Task<List<HrSalaryHistoryDto>> GetSalaryHistoryAsync(int userId);

        // ---- Leaves (HR is source of truth; Planning consumes via API) ----
        Task<List<HrLeaveBalanceDto>> GetLeaveBalancesAsync(int year);
        Task<List<HrLeaveBalanceDto>> SetLeaveAllowanceAsync(int userId, SetLeaveAllowanceDto dto);

        // ---- Attendance ----
        Task<List<HrAttendanceDto>> GetAttendanceAsync(int year, int month, int? userId = null);
        Task<HrAttendanceDto> UpsertAttendanceAsync(UpsertHrAttendanceDto dto, int actorUserId);
        Task DeleteAttendanceAsync(int id, int actorUserId);
        Task<HrAttendanceSettingsDto> GetAttendanceSettingsAsync();
        Task<HrAttendanceSettingsDto> UpsertAttendanceSettingsAsync(UpsertHrAttendanceSettingsDto dto, int actorUserId);
        Task<HrAttendanceImportResultDto> ImportAttendanceAsync(List<ImportHrAttendanceRowDto> rows, int actorUserId);

        // ---- Payroll ----
        Task<HrPayrollRunDto> GeneratePayrollRunAsync(CreatePayrollRunDto dto, int createdByUserId);
        Task<List<HrPayrollRunDto>> ListPayrollRunsAsync(int year);
        Task<HrPayrollRunDto> GetPayrollRunAsync(int id);
        Task<HrPayrollRunDto> ConfirmPayrollRunAsync(int id, int actorUserId);
        Task<HrPayrollRunDto> MarkPayrollRunPaidAsync(int id, int actorUserId);
        Task<object> GetPayslipAsync(int entryId);

        // ---- Departments ----
        Task<List<HrDepartmentDto>> GetDepartmentsAsync();
        Task<HrDepartmentDto> CreateDepartmentAsync(UpsertDepartmentDto dto);
        Task<HrDepartmentDto> UpdateDepartmentAsync(int id, UpsertDepartmentDto dto);
        Task DeleteDepartmentAsync(int id);

        // ---- Bonuses & Costs ----
        Task<List<HrBonusCostDto>> GetBonusCostsAsync(int? userId, int? year, int? month, string? kind);
        Task<HrBonusCostDto> CreateBonusCostAsync(UpsertBonusCostDto dto, int actorUserId);
        Task<HrBonusCostDto> UpdateBonusCostAsync(int id, UpsertBonusCostDto dto, int actorUserId);
        Task DeleteBonusCostAsync(int id, int actorUserId);

        // ---- CNSS Rates ----
        Task<List<HrCnssRateDto>> GetCnssRatesAsync();
        Task<HrCnssRateDto> GetActiveCnssRateAsync();
        Task<HrCnssRateDto> UpsertCnssRateAsync(UpsertCnssRateDto dto, int actorUserId);

        // ---- Public Holidays ----
        Task<List<HrPublicHolidayDto>> GetPublicHolidaysAsync(int? year);
        Task<HrPublicHolidayDto> CreatePublicHolidayAsync(UpsertPublicHolidayDto dto);
        Task<HrPublicHolidayDto> UpdatePublicHolidayAsync(int id, UpsertPublicHolidayDto dto);
        Task DeletePublicHolidayAsync(int id);

        // ---- Employee Documents ----
        Task<List<HrEmployeeDocumentDto>> GetEmployeeDocumentsAsync(int userId);
        Task<HrEmployeeDocumentDto> CreateEmployeeDocumentAsync(UpsertEmployeeDocumentDto dto, int actorUserId);
        Task DeleteEmployeeDocumentAsync(int id);

        // ---- Audit Log ----
        Task<List<HrAuditLogDto>> GetAuditLogAsync(int? userId, int take = 100);

        // ---- Reports ----
        Task<List<HrEmployeeCostDto>> GetEmployeeCostReportAsync(int year, int? month);
        Task<HrCnssMonthlyDeclarationDto> GetCnssDeclarationAsync(int year, int month);

        // ---- Planning integration & contract alerts (Round 1) ----
        Task<List<HrActiveLeaveDto>> GetActiveLeavesAsync(DateTime date);
        Task<List<HrContractExpiryDto>> GetExpiringContractsAsync(int withinDays = 60);

        // ---- Performance: Goals ----
        Task<List<HrGoalDto>> GetGoalsAsync(int? userId, int? cycleId, string? status);
        Task<HrGoalDto> CreateGoalAsync(UpsertHrGoalDto dto, int actorUserId);
        Task<HrGoalDto> UpdateGoalAsync(int id, UpsertHrGoalDto dto, int actorUserId);
        Task DeleteGoalAsync(int id, int actorUserId);

        // ---- Performance: Review cycles ----
        Task<List<HrReviewCycleDto>> GetReviewCyclesAsync();
        Task<HrReviewCycleDto> CreateReviewCycleAsync(UpsertHrReviewCycleDto dto, int actorUserId);
        Task<HrReviewCycleDto> UpdateReviewCycleAsync(int id, UpsertHrReviewCycleDto dto, int actorUserId);
        Task DeleteReviewCycleAsync(int id, int actorUserId);

        // ---- Performance: Reviews ----
        Task<List<HrPerformanceReviewDto>> GetReviewsAsync(int? cycleId, int? userId, string? status);
        Task<HrPerformanceReviewDto> GetReviewAsync(int id);
        Task<HrPerformanceReviewDto> CreateReviewAsync(UpsertHrPerformanceReviewDto dto, int actorUserId);
        Task<HrPerformanceReviewDto> UpdateReviewAsync(int id, UpsertHrPerformanceReviewDto dto, int actorUserId);
        Task DeleteReviewAsync(int id, int actorUserId);

        // ---- Recruitment: Job openings ----
        Task<List<HrJobOpeningDto>> GetJobOpeningsAsync(string? status);
        Task<HrJobOpeningDto> GetJobOpeningAsync(int id);
        Task<HrJobOpeningDto> CreateJobOpeningAsync(UpsertHrJobOpeningDto dto, int actorUserId);
        Task<HrJobOpeningDto> UpdateJobOpeningAsync(int id, UpsertHrJobOpeningDto dto, int actorUserId);
        Task DeleteJobOpeningAsync(int id, int actorUserId);

        // ---- Recruitment: Applicants ----
        Task<List<HrApplicantDto>> GetApplicantsAsync(int? openingId, string? stage);
        Task<HrApplicantDto> GetApplicantAsync(int id);
        Task<HrApplicantDto> CreateApplicantAsync(UpsertHrApplicantDto dto, int actorUserId);
        Task<HrApplicantDto> UpdateApplicantAsync(int id, UpsertHrApplicantDto dto, int actorUserId);
        Task<HrApplicantDto> MoveApplicantStageAsync(int id, MoveApplicantStageDto dto, int actorUserId);
        Task DeleteApplicantAsync(int id, int actorUserId);

        // ---- Recruitment: Interviews ----
        Task<List<HrInterviewDto>> GetInterviewsAsync(int? applicantId, DateTime? from, DateTime? to);
        Task<HrInterviewDto> CreateInterviewAsync(UpsertHrInterviewDto dto, int actorUserId);
        Task<HrInterviewDto> UpdateInterviewAsync(int id, UpsertHrInterviewDto dto, int actorUserId);
        Task DeleteInterviewAsync(int id, int actorUserId);

        // ---- Recruitment: Notes ----
        Task<List<HrApplicantNoteDto>> GetApplicantNotesAsync(int applicantId);
        Task<HrApplicantNoteDto> AddApplicantNoteAsync(UpsertHrApplicantNoteDto dto, int actorUserId);
        Task DeleteApplicantNoteAsync(int id, int actorUserId);

        // ---- Recruitment: Dashboard ----
        Task<RecruitmentDashboardDto> GetRecruitmentDashboardAsync();
    }
}
