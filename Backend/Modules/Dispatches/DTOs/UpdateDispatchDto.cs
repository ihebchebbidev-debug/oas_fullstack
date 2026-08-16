using System;
using System.Collections.Generic;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class UpdateDispatchDto
    {
        public List<string>? AssignedTechnicianIds { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }
        public string? Priority { get; set; }
        public string? Notes { get; set; }
        public List<string>? RequiredSkills { get; set; }
    }
}
