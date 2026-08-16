using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class CreateDispatchFromInstallationDto
    {
        [Required]
        public int InstallationId { get; set; }

        [Required]
        public string InstallationName { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<int> JobIds { get; set; } = new();

        [Required]
        [MinLength(1)]
        public List<string> AssignedTechnicianIds { get; set; } = new();

        [Required]
        public DateTime ScheduledDate { get; set; }

        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }

        public string Priority { get; set; } = "medium";
        public string? Notes { get; set; }
        public string? SiteAddress { get; set; }
        public int? ContactId { get; set; }
        public int? ServiceOrderId { get; set; }
    }
}
