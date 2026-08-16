using System;
using System.Collections.Generic;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class DispatchStatisticsDto
    {
        public int TotalDispatches { get; set; }
        public int CompletedDispatches { get; set; }
        public int PendingDispatches { get; set; }
        public int InProgressDispatches { get; set; }
        public int CancelledDispatches { get; set; }
        public double AverageCompletionTime { get; set; }
        public double TotalTimeSpent { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalMaterialsCost { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal CompletionRate { get; set; }
        public double AverageDuration { get; set; }
        public int TotalTechnicians { get; set; }
        public int HighPriorityCount { get; set; }
        public int MediumPriorityCount { get; set; }
        public int LowPriorityCount { get; set; }
        public DateTime GeneratedAt { get; set; }
        
        // For grouping
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public Dictionary<string, int> ByPriority { get; set; } = new();
    }

    public class StatisticsQueryParams
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? TechnicianId { get; set; }
        public string? Status { get; set; }
    }
}
