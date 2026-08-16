using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.HR.DTOs;
using MyApi.Modules.HR.Filters;
using MyApi.Modules.HR.Services;

namespace MyApi.Modules.HR.Controllers
{
    [ApiController]
    [Authorize]
    [HrExceptionFilter]
    [Route("api/hr")]
    public class HrController : ControllerBase
    {
        private readonly IHrService _hr;

        public HrController(IHrService hr) { _hr = hr; }

        // ---- Employees ----
        [HttpGet("employees")]
        public async Task<IActionResult> GetEmployees() => Ok(new { success = true, data = await _hr.GetEmployeesAsync() });

        [HttpGet("employees/{id:int}")]
        public async Task<IActionResult> GetEmployee(int id) => Ok(new { success = true, data = await _hr.GetEmployeeDetailAsync(id) });

        [HttpPut("employees/{userId:int}/salary-config")]
        public async Task<IActionResult> UpsertSalaryConfig(int userId, [FromBody] UpsertSalaryConfigDto dto)
            => Ok(new { success = true, data = await _hr.UpsertSalaryConfigAsync(userId, dto, GetActorId()) });

        [HttpGet("employees/{userId:int}/salary-history")]
        public async Task<IActionResult> GetSalaryHistory(int userId)
            => Ok(new { success = true, data = await _hr.GetSalaryHistoryAsync(userId) });

        // ---- Leaves ----
        [HttpGet("leaves/balances")]
        public async Task<IActionResult> GetLeaveBalances([FromQuery] int year)
            => Ok(new { success = true, data = await _hr.GetLeaveBalancesAsync(year) });

        [HttpPut("leaves/balances/{userId:int}")]
        public async Task<IActionResult> SetLeaveAllowance(int userId, [FromBody] SetLeaveAllowanceDto dto)
            => Ok(new { success = true, data = await _hr.SetLeaveAllowanceAsync(userId, dto) });

        // ---- Attendance ----
        [HttpGet("attendance")]
        public async Task<IActionResult> GetAttendance([FromQuery] int year, [FromQuery] int month, [FromQuery] int? userId)
            => Ok(new { success = true, data = await _hr.GetAttendanceAsync(year, month, userId) });

        [HttpPost("attendance")]
        public async Task<IActionResult> UpsertAttendance([FromBody] UpsertHrAttendanceDto dto)
        {
            try
            {
                return Ok(new { success = true, data = await _hr.UpsertAttendanceAsync(dto, GetActorId()) });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, errorCode = ex.Message, field = ex.ParamName });
            }
        }

        [HttpDelete("attendance/{id:int}")]
        public async Task<IActionResult> DeleteAttendance(int id)
        {
            await _hr.DeleteAttendanceAsync(id, GetActorId());
            return Ok(new { success = true });
        }

        [HttpGet("attendance/settings")]
        public async Task<IActionResult> GetAttendanceSettings()
            => Ok(new { success = true, data = await _hr.GetAttendanceSettingsAsync() });

        [HttpPut("attendance/settings")]
        public async Task<IActionResult> UpsertAttendanceSettings([FromBody] UpsertHrAttendanceSettingsDto dto)
            => Ok(new { success = true, data = await _hr.UpsertAttendanceSettingsAsync(dto, GetActorId()) });

        [HttpPost("attendance/import")]
        public async Task<IActionResult> ImportAttendance([FromBody] List<ImportHrAttendanceRowDto> rows)
            => Ok(new { success = true, data = await _hr.ImportAttendanceAsync(rows, GetActorId()) });

        // ---- Payroll ----
        [HttpPost("payroll/run")]
        public async Task<IActionResult> GenerateRun([FromBody] CreatePayrollRunDto dto)
            => Ok(new { success = true, data = await _hr.GeneratePayrollRunAsync(dto, GetActorId()) });

        [HttpGet("payroll/runs")]
        public async Task<IActionResult> ListRuns([FromQuery] int year)
            => Ok(new { success = true, data = await _hr.ListPayrollRunsAsync(year) });

        [HttpGet("payroll/runs/{id:int}")]
        public async Task<IActionResult> GetRun(int id) => Ok(new { success = true, data = await _hr.GetPayrollRunAsync(id) });

        [HttpPut("payroll/runs/{id:int}/confirm")]
        public async Task<IActionResult> ConfirmRun(int id)
        {
            try { return Ok(new { success = true, data = await _hr.ConfirmPayrollRunAsync(id, GetActorId()) }); }
            catch (KeyNotFoundException) { return NotFound(new { success = false, error = "hr.payroll_run_not_found" }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }

        [HttpPut("payroll/runs/{id:int}/pay")]
        public async Task<IActionResult> MarkRunPaid(int id)
        {
            try { return Ok(new { success = true, data = await _hr.MarkPayrollRunPaidAsync(id, GetActorId()) }); }
            catch (KeyNotFoundException) { return NotFound(new { success = false, error = "hr.payroll_run_not_found" }); }
            catch (InvalidOperationException ex) { return BadRequest(new { success = false, error = ex.Message }); }
        }

        [HttpGet("payroll/payslip/{entryId:int}")]
        public async Task<IActionResult> GetPayslip(int entryId) => Ok(new { success = true, data = await _hr.GetPayslipAsync(entryId) });

        // ---- Departments ----
        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments() => Ok(new { success = true, data = await _hr.GetDepartmentsAsync() });

        [HttpPost("departments")]
        public async Task<IActionResult> CreateDepartment([FromBody] UpsertDepartmentDto dto) => Ok(new { success = true, data = await _hr.CreateDepartmentAsync(dto) });

        [HttpPut("departments/{id:int}")]
        public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpsertDepartmentDto dto) => Ok(new { success = true, data = await _hr.UpdateDepartmentAsync(id, dto) });

        [HttpDelete("departments/{id:int}")]
        public async Task<IActionResult> DeleteDepartment(int id) { await _hr.DeleteDepartmentAsync(id); return Ok(new { success = true }); }

        // ---- Bonuses & Costs ----
        [HttpGet("bonuses")]
        public async Task<IActionResult> GetBonuses([FromQuery] int? userId, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? kind)
            => Ok(new { success = true, data = await _hr.GetBonusCostsAsync(userId, year, month, kind) });

        [HttpPost("bonuses")]
        public async Task<IActionResult> CreateBonus([FromBody] UpsertBonusCostDto dto)
        {
            try { return Ok(new { success = true, data = await _hr.CreateBonusCostAsync(dto, GetActorId()) }); }
            catch (ArgumentException ex) { return BadRequest(new { success = false, errorCode = ex.Message, field = ex.ParamName }); }
        }

        [HttpPut("bonuses/{id:int}")]
        public async Task<IActionResult> UpdateBonus(int id, [FromBody] UpsertBonusCostDto dto)
        {
            try { return Ok(new { success = true, data = await _hr.UpdateBonusCostAsync(id, dto, GetActorId()) }); }
            catch (ArgumentException ex) { return BadRequest(new { success = false, errorCode = ex.Message, field = ex.ParamName }); }
        }

        [HttpDelete("bonuses/{id:int}")]
        public async Task<IActionResult> DeleteBonus(int id) { await _hr.DeleteBonusCostAsync(id, GetActorId()); return Ok(new { success = true }); }

        // ---- CNSS ----
        [HttpGet("cnss/rates")]
        public async Task<IActionResult> GetCnssRates() => Ok(new { success = true, data = await _hr.GetCnssRatesAsync() });

        [HttpGet("cnss/rates/active")]
        public async Task<IActionResult> GetActiveCnssRate() => Ok(new { success = true, data = await _hr.GetActiveCnssRateAsync() });

        [HttpPut("cnss/rates")]
        public async Task<IActionResult> UpsertCnssRate([FromBody] UpsertCnssRateDto dto)
            => Ok(new { success = true, data = await _hr.UpsertCnssRateAsync(dto, GetActorId()) });

        [HttpGet("cnss/declaration")]
        public async Task<IActionResult> GetDeclaration([FromQuery] int year, [FromQuery] int month)
            => Ok(new { success = true, data = await _hr.GetCnssDeclarationAsync(year, month) });

        // ---- Public Holidays ----
        [HttpGet("holidays")]
        public async Task<IActionResult> GetHolidays([FromQuery] int? year)
            => Ok(new { success = true, data = await _hr.GetPublicHolidaysAsync(year) });

        [HttpPost("holidays")]
        public async Task<IActionResult> CreateHoliday([FromBody] UpsertPublicHolidayDto dto)
            => Ok(new { success = true, data = await _hr.CreatePublicHolidayAsync(dto) });

        [HttpPut("holidays/{id:int}")]
        public async Task<IActionResult> UpdateHoliday(int id, [FromBody] UpsertPublicHolidayDto dto)
            => Ok(new { success = true, data = await _hr.UpdatePublicHolidayAsync(id, dto) });

        [HttpDelete("holidays/{id:int}")]
        public async Task<IActionResult> DeleteHoliday(int id) { await _hr.DeletePublicHolidayAsync(id); return Ok(new { success = true }); }

        // ---- Documents ----
        [HttpGet("documents/{userId:int}")]
        public async Task<IActionResult> GetDocuments(int userId) => Ok(new { success = true, data = await _hr.GetEmployeeDocumentsAsync(userId) });

        [HttpPost("documents")]
        public async Task<IActionResult> AddDocument([FromBody] UpsertEmployeeDocumentDto dto)
            => Ok(new { success = true, data = await _hr.CreateEmployeeDocumentAsync(dto, GetActorId()) });

        [HttpDelete("documents/{id:int}")]
        public async Task<IActionResult> DeleteDocument(int id) { await _hr.DeleteEmployeeDocumentAsync(id); return Ok(new { success = true }); }

        // ---- Audit log ----
        [HttpGet("audit")]
        public async Task<IActionResult> GetAudit([FromQuery] int? userId, [FromQuery] int take = 100)
            => Ok(new { success = true, data = await _hr.GetAuditLogAsync(userId, take) });

        // ---- Reports ----
        [HttpGet("reports/employee-cost")]
        public async Task<IActionResult> EmployeeCost([FromQuery] int year, [FromQuery] int? month)
            => Ok(new { success = true, data = await _hr.GetEmployeeCostReportAsync(year, month) });

        // ---- Planning integration & contract alerts (Round 1) ----
        [HttpGet("leaves/active")]
        public async Task<IActionResult> GetActiveLeaves([FromQuery] DateTime? date)
            => Ok(new { success = true, data = await _hr.GetActiveLeavesAsync(date ?? DateTime.UtcNow.Date) });

        [HttpGet("contracts/expiring")]
        public async Task<IActionResult> GetExpiringContracts([FromQuery] int withinDays = 60)
            => Ok(new { success = true, data = await _hr.GetExpiringContractsAsync(withinDays) });

        // ====================================================================
        // PERFORMANCE: Goals
        // ====================================================================
        [HttpGet("performance/goals")]
        public async Task<IActionResult> GetGoals([FromQuery] int? userId, [FromQuery] int? cycleId, [FromQuery] string? status)
            => Ok(new { success = true, data = await _hr.GetGoalsAsync(userId, cycleId, status) });

        [HttpPost("performance/goals")]
        public async Task<IActionResult> CreateGoal([FromBody] UpsertHrGoalDto dto)
            => Ok(new { success = true, data = await _hr.CreateGoalAsync(dto, GetActorId()) });

        [HttpPut("performance/goals/{id:int}")]
        public async Task<IActionResult> UpdateGoal(int id, [FromBody] UpsertHrGoalDto dto)
            => Ok(new { success = true, data = await _hr.UpdateGoalAsync(id, dto, GetActorId()) });

        [HttpDelete("performance/goals/{id:int}")]
        public async Task<IActionResult> DeleteGoal(int id) { await _hr.DeleteGoalAsync(id, GetActorId()); return Ok(new { success = true }); }

        // ---- Review cycles ----
        [HttpGet("performance/cycles")]
        public async Task<IActionResult> GetCycles() => Ok(new { success = true, data = await _hr.GetReviewCyclesAsync() });

        [HttpPost("performance/cycles")]
        public async Task<IActionResult> CreateCycle([FromBody] UpsertHrReviewCycleDto dto)
            => Ok(new { success = true, data = await _hr.CreateReviewCycleAsync(dto, GetActorId()) });

        [HttpPut("performance/cycles/{id:int}")]
        public async Task<IActionResult> UpdateCycle(int id, [FromBody] UpsertHrReviewCycleDto dto)
            => Ok(new { success = true, data = await _hr.UpdateReviewCycleAsync(id, dto, GetActorId()) });

        [HttpDelete("performance/cycles/{id:int}")]
        public async Task<IActionResult> DeleteCycle(int id) { await _hr.DeleteReviewCycleAsync(id, GetActorId()); return Ok(new { success = true }); }

        // ---- Reviews ----
        [HttpGet("performance/reviews")]
        public async Task<IActionResult> GetReviews([FromQuery] int? cycleId, [FromQuery] int? userId, [FromQuery] string? status)
            => Ok(new { success = true, data = await _hr.GetReviewsAsync(cycleId, userId, status) });

        [HttpGet("performance/reviews/{id:int}")]
        public async Task<IActionResult> GetReview(int id) => Ok(new { success = true, data = await _hr.GetReviewAsync(id) });

        [HttpPost("performance/reviews")]
        public async Task<IActionResult> CreateReview([FromBody] UpsertHrPerformanceReviewDto dto)
            => Ok(new { success = true, data = await _hr.CreateReviewAsync(dto, GetActorId()) });

        [HttpPut("performance/reviews/{id:int}")]
        public async Task<IActionResult> UpdateReview(int id, [FromBody] UpsertHrPerformanceReviewDto dto)
            => Ok(new { success = true, data = await _hr.UpdateReviewAsync(id, dto, GetActorId()) });

        [HttpDelete("performance/reviews/{id:int}")]
        public async Task<IActionResult> DeleteReview(int id) { await _hr.DeleteReviewAsync(id, GetActorId()); return Ok(new { success = true }); }

        // ====================================================================
        // RECRUITMENT
        // ====================================================================
        [HttpGet("recruitment/dashboard")]
        public async Task<IActionResult> RecruitmentDashboard()
            => Ok(new { success = true, data = await _hr.GetRecruitmentDashboardAsync() });

        // ---- Job openings ----
        [HttpGet("recruitment/openings")]
        public async Task<IActionResult> GetOpenings([FromQuery] string? status)
            => Ok(new { success = true, data = await _hr.GetJobOpeningsAsync(status) });

        [HttpGet("recruitment/openings/{id:int}")]
        public async Task<IActionResult> GetOpening(int id) => Ok(new { success = true, data = await _hr.GetJobOpeningAsync(id) });

        [HttpPost("recruitment/openings")]
        public async Task<IActionResult> CreateOpening([FromBody] UpsertHrJobOpeningDto dto)
            => Ok(new { success = true, data = await _hr.CreateJobOpeningAsync(dto, GetActorId()) });

        [HttpPut("recruitment/openings/{id:int}")]
        public async Task<IActionResult> UpdateOpening(int id, [FromBody] UpsertHrJobOpeningDto dto)
            => Ok(new { success = true, data = await _hr.UpdateJobOpeningAsync(id, dto, GetActorId()) });

        [HttpDelete("recruitment/openings/{id:int}")]
        public async Task<IActionResult> DeleteOpening(int id) { await _hr.DeleteJobOpeningAsync(id, GetActorId()); return Ok(new { success = true }); }

        // ---- Applicants ----
        [HttpGet("recruitment/applicants")]
        public async Task<IActionResult> GetApplicants([FromQuery] int? openingId, [FromQuery] string? stage)
            => Ok(new { success = true, data = await _hr.GetApplicantsAsync(openingId, stage) });

        [HttpGet("recruitment/applicants/{id:int}")]
        public async Task<IActionResult> GetApplicant(int id) => Ok(new { success = true, data = await _hr.GetApplicantAsync(id) });

        [HttpPost("recruitment/applicants")]
        public async Task<IActionResult> CreateApplicant([FromBody] UpsertHrApplicantDto dto)
            => Ok(new { success = true, data = await _hr.CreateApplicantAsync(dto, GetActorId()) });

        [HttpPut("recruitment/applicants/{id:int}")]
        public async Task<IActionResult> UpdateApplicant(int id, [FromBody] UpsertHrApplicantDto dto)
            => Ok(new { success = true, data = await _hr.UpdateApplicantAsync(id, dto, GetActorId()) });

        [HttpPost("recruitment/applicants/{id:int}/move")]
        public async Task<IActionResult> MoveApplicant(int id, [FromBody] MoveApplicantStageDto dto)
            => Ok(new { success = true, data = await _hr.MoveApplicantStageAsync(id, dto, GetActorId()) });

        [HttpDelete("recruitment/applicants/{id:int}")]
        public async Task<IActionResult> DeleteApplicant(int id) { await _hr.DeleteApplicantAsync(id, GetActorId()); return Ok(new { success = true }); }

        // ---- Interviews ----
        [HttpGet("recruitment/interviews")]
        public async Task<IActionResult> GetInterviews([FromQuery] int? applicantId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
            => Ok(new { success = true, data = await _hr.GetInterviewsAsync(applicantId, from, to) });

        [HttpPost("recruitment/interviews")]
        public async Task<IActionResult> CreateInterview([FromBody] UpsertHrInterviewDto dto)
            => Ok(new { success = true, data = await _hr.CreateInterviewAsync(dto, GetActorId()) });

        [HttpPut("recruitment/interviews/{id:int}")]
        public async Task<IActionResult> UpdateInterview(int id, [FromBody] UpsertHrInterviewDto dto)
            => Ok(new { success = true, data = await _hr.UpdateInterviewAsync(id, dto, GetActorId()) });

        [HttpDelete("recruitment/interviews/{id:int}")]
        public async Task<IActionResult> DeleteInterview(int id) { await _hr.DeleteInterviewAsync(id, GetActorId()); return Ok(new { success = true }); }

        // ---- Applicant notes ----
        [HttpGet("recruitment/applicants/{applicantId:int}/notes")]
        public async Task<IActionResult> GetApplicantNotes(int applicantId)
            => Ok(new { success = true, data = await _hr.GetApplicantNotesAsync(applicantId) });

        [HttpPost("recruitment/applicants/{applicantId:int}/notes")]
        public async Task<IActionResult> AddApplicantNote(int applicantId, [FromBody] UpsertHrApplicantNoteDto dto)
        {
            dto.ApplicantId = applicantId;
            return Ok(new { success = true, data = await _hr.AddApplicantNoteAsync(dto, GetActorId()) });
        }

        [HttpDelete("recruitment/notes/{id:int}")]
        public async Task<IActionResult> DeleteNote(int id) { await _hr.DeleteApplicantNoteAsync(id, GetActorId()); return Ok(new { success = true }); }

        private int GetActorId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId") ?? "0";
            return int.TryParse(raw, out var id) ? id : 0;
        }
    }
}
