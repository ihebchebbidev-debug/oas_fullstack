using System;
using System.Collections.Generic;

namespace MyApi.Modules.HR.DTOs
{
    // ---- Goals ----
    public class HrGoalDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int? CycleId { get; set; }
        public string? CycleName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = "smart";
        public decimal Weight { get; set; }
        public string? TargetValue { get; set; }
        public int Progress { get; set; }
        public string Status { get; set; } = "not_started";
        public DateTime? DueDate { get; set; }
        public decimal? Score { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpsertHrGoalDto
    {
        public int UserId { get; set; }
        public int? CycleId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public decimal? Weight { get; set; }
        public string? TargetValue { get; set; }
        public int? Progress { get; set; }
        public string? Status { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? Score { get; set; }
    }

    // ---- Review cycles ----
    public class HrReviewCycleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Frequency { get; set; } = "annual";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string Status { get; set; } = "draft";
        public bool SelfAssessmentRequired { get; set; }
        public int ReviewsCount { get; set; }
        public int CompletedReviewsCount { get; set; }
    }

    public class UpsertHrReviewCycleDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Frequency { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string? Status { get; set; }
        public bool? SelfAssessmentRequired { get; set; }
    }

    // ---- Reviews ----
    public class HrPerformanceReviewDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int CycleId { get; set; }
        public string? CycleName { get; set; }
        public int? ReviewerUserId { get; set; }
        public string? ReviewerName { get; set; }
        public string Status { get; set; } = "pending";
        public string? SelfAssessment { get; set; }
        public DateTime? SelfAssessmentSubmittedAt { get; set; }
        public string? ManagerComments { get; set; }
        public decimal? OverallScore { get; set; }
        public string? Rating { get; set; }
        public string? Strengths { get; set; }
        public string? Improvements { get; set; }
        public string? DevelopmentPlan { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public List<HrGoalDto> Goals { get; set; } = new();
    }

    public class UpsertHrPerformanceReviewDto
    {
        public int UserId { get; set; }
        public int CycleId { get; set; }
        public int? ReviewerUserId { get; set; }
        public string? Status { get; set; }
        public string? SelfAssessment { get; set; }
        public string? ManagerComments { get; set; }
        public decimal? OverallScore { get; set; }
        public string? Rating { get; set; }
        public string? Strengths { get; set; }
        public string? Improvements { get; set; }
        public string? DevelopmentPlan { get; set; }
    }
}