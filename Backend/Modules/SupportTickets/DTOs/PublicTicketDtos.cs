using System;
using System.Collections.Generic;

namespace MyApi.Modules.SupportTickets.DTOs
{
    public class TicketOriginDto
    {
        /// <summary>"manual" (user-submitted) or "auto" (incident monitor).</summary>
        public string Type { get; set; } = "manual";
        public string Source { get; set; } = "manual";
        public string? IncidentType { get; set; }
        public string? ErrorFingerprint { get; set; }
        public int? SystemLogId { get; set; }
        public int OccurrenceCount { get; set; } = 1;
        public DateTime? LastOccurredAt { get; set; }
    }

    public class TicketReporterDto
    {
        public string? Email { get; set; }
        public bool IsAnonymous { get; set; }
        /// <summary>True when the ticket was auto-created and no user email was captured.</summary>
        public bool IsSystem { get; set; }
    }

    public class PublicTicketDto
    {
        public int Id { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "open";
        public string? Urgency { get; set; }
        public string? Category { get; set; }
        public string? Module { get; set; }
        public string? CurrentPage { get; set; }
        public string? RelatedUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public TicketOriginDto Origin { get; set; } = new();
        public TicketReporterDto Reporter { get; set; } = new();

        public List<SupportTicketAttachmentDto> Attachments { get; set; } = new();
    }

    public class CrossTenantError
    {
        public string Tenant { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class PagedPublicTicketsDto
    {
        public List<PublicTicketDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new();
        public List<CrossTenantError> Errors { get; set; } = new();
    }

    public class PublicCommentDto
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? AuthorEmail { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreatePublicCommentDto
    {
        public string Text { get; set; } = string.Empty;
        public bool? IsInternal { get; set; }
        public string? Author { get; set; }
        public string? AuthorEmail { get; set; }
    }

    public class TenantSummaryDto
    {
        public string Tenant { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }
}
