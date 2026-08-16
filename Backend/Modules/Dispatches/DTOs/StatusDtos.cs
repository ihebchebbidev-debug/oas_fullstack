using System;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class UpdateDispatchStatusDto
    {
        public string Status { get; set; } = null!;
        public string? Notes { get; set; }
    }

    public class StartDispatchDto
    {
        public DateTime ActualStartTime { get; set; }
        public string? Notes { get; set; }
    }

    public class CompleteDispatchDto
    {
        public DateTime ActualEndTime { get; set; }
        public int CompletionPercentage { get; set; }
        public string? Notes { get; set; }
    }

    public class CancelDispatchDto
    {
        public string Reason { get; set; } = null!;
        public string? Notes { get; set; }
    }
}
