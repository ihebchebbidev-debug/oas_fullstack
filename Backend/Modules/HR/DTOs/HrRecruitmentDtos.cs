using System;
using System.Collections.Generic;

namespace MyApi.Modules.HR.DTOs
{
    // ---- Job openings ----
    public class HrJobOpeningDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string? Location { get; set; }
        public string? ContractType { get; set; }
        public string? Seniority { get; set; }
        public string? Description { get; set; }
        public string? Requirements { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string Currency { get; set; } = "TND";
        public int OpeningsCount { get; set; }
        public string Status { get; set; } = "draft";
        public int? HiringManagerUserId { get; set; }
        public string? HiringManagerName { get; set; }
        public DateTime? OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int ApplicantsCount { get; set; }
        public int HiredCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpsertHrJobOpeningDto
    {
        public string Title { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string? Location { get; set; }
        public string? ContractType { get; set; }
        public string? Seniority { get; set; }
        public string? Description { get; set; }
        public string? Requirements { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? Currency { get; set; }
        public int? OpeningsCount { get; set; }
        public string? Status { get; set; }
        public int? HiringManagerUserId { get; set; }
    }

    // ---- Applicants ----
    public class HrApplicantDto
    {
        public int Id { get; set; }
        public int OpeningId { get; set; }
        public string? OpeningTitle { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Source { get; set; }
        public string? ResumeUrl { get; set; }
        public string? ResumeFileName { get; set; }
        public string Stage { get; set; } = "applied";
        public int? Rating { get; set; }
        public decimal? ExpectedSalary { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public int InterviewsCount { get; set; }
        public int NotesCount { get; set; }
    }

    public class UpsertHrApplicantDto
    {
        public int OpeningId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Source { get; set; }
        public string? ResumeUrl { get; set; }
        public string? ResumeFileName { get; set; }
        public string? Stage { get; set; }
        public int? Rating { get; set; }
        public decimal? ExpectedSalary { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class MoveApplicantStageDto
    {
        public string Stage { get; set; } = "applied";
        public string? RejectionReason { get; set; }
    }

    // ---- Interviews ----
    public class HrInterviewDto
    {
        public int Id { get; set; }
        public int ApplicantId { get; set; }
        public string? ApplicantName { get; set; }
        public string Kind { get; set; } = "phone";
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        public int? InterviewerUserId { get; set; }
        public string? InterviewerName { get; set; }
        public string? Location { get; set; }
        public string? MeetingUrl { get; set; }
        public string Status { get; set; } = "scheduled";
        public int? Score { get; set; }
        public string? Feedback { get; set; }
        public string? Recommendation { get; set; }
    }

    public class UpsertHrInterviewDto
    {
        public int ApplicantId { get; set; }
        public string? Kind { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int? DurationMinutes { get; set; }
        public int? InterviewerUserId { get; set; }
        public string? Location { get; set; }
        public string? MeetingUrl { get; set; }
        public string? Status { get; set; }
        public int? Score { get; set; }
        public string? Feedback { get; set; }
        public string? Recommendation { get; set; }
    }

    // ---- Notes ----
    public class HrApplicantNoteDto
    {
        public int Id { get; set; }
        public int ApplicantId { get; set; }
        public int? AuthorUserId { get; set; }
        public string? AuthorName { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class UpsertHrApplicantNoteDto
    {
        public int ApplicantId { get; set; }
        public string Body { get; set; } = string.Empty;
    }

    public class RecruitmentDashboardDto
    {
        public int OpenPositions { get; set; }
        public int ActiveApplicants { get; set; }
        public int InterviewsThisWeek { get; set; }
        public int OffersOut { get; set; }
        public Dictionary<string, int> ByStage { get; set; } = new();
    }
}