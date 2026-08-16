using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Dispatches.DTOs
{
    // Plan a whole service order as ONE dispatch containing all of its jobs.
    // Mirrors CreateDispatchFromInstallationDto but keyed by service order and
    // without the installation-specific fields. JobIds is optional: when empty,
    // the service resolves every non-deleted job on the service order.
    public class CreateDispatchFromServiceOrderDto
    {
        [Required]
        public int ServiceOrderId { get; set; }

        public List<int> JobIds { get; set; } = new();

        public List<string> AssignedTechnicianIds { get; set; } = new();

        [Required]
        public DateTime ScheduledDate { get; set; }

        public TimeSpan? ScheduledStartTime { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }

        public string Priority { get; set; } = "medium";
        public string? Notes { get; set; }
        public string? SiteAddress { get; set; }
        public int? ContactId { get; set; }
    }
}
