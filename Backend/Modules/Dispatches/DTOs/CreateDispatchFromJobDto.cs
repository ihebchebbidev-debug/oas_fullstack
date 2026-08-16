using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class CreateDispatchFromJobDto
    {
        [Required]
        [MinLength(1)]
        public List<string> AssignedTechnicianIds { get; set; } = new();

        [Required]
        public DateTime ScheduledDate { get; set; }

        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }

        public int? EstimatedTravelTime { get; set; }
        public decimal? EstimatedTravelDistance { get; set; }
        
        public string Priority { get; set; } = "medium";
        public string? Notes { get; set; }
        
        // These come from the job/service order
        public int? ContactId { get; set; }
        public int? ServiceOrderId { get; set; }
        public string? SiteAddress { get; set; }
    }
}
