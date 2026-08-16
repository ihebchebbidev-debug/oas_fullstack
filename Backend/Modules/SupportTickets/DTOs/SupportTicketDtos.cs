using Microsoft.AspNetCore.Http;

namespace MyApi.Modules.SupportTickets.DTOs
{
    public class CreateSupportTicketDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Urgency { get; set; }
        public string? Category { get; set; }
        public string? CurrentPage { get; set; }
        public string? RelatedUrl { get; set; }
        public string? UserEmail { get; set; }
        public List<IFormFile>? Attachments { get; set; }
    }

    public class SupportTicketResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Urgency { get; set; }
        public string? Category { get; set; }
        public string? CurrentPage { get; set; }
        public string? RelatedUrl { get; set; }
        public string Tenant { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public string Status { get; set; } = "open";
        public DateTime CreatedAt { get; set; }
        public string Source { get; set; } = "manual";
        public string? ErrorFingerprint { get; set; }
        public int? SystemLogId { get; set; }
        public int OccurrenceCount { get; set; } = 1;
        public DateTime? LastOccurredAt { get; set; }
        public string? IncidentType { get; set; }
        public string? Module { get; set; }
        public List<SupportTicketAttachmentDto> Attachments { get; set; } = new();
    }

    public class AutoIncidentReportDto
    {
        public string IncidentType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Severity { get; set; }
        public string? Module { get; set; }
        public string? CurrentPage { get; set; }
        public string? RelatedUrl { get; set; }
        public string? UserEmail { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Stack { get; set; }
        public string? ComponentStack { get; set; }
        public string? Fingerprint { get; set; }
        public int? HttpStatus { get; set; }
        public string? HttpMethod { get; set; }
        public string? Endpoint { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? ReferenceId { get; set; }
        public int? SystemLogId { get; set; }
        public string? Details { get; set; }
        public string? UserAgent { get; set; }
        public int? ClientOccurrenceCount { get; set; }
    }

    public class AutoIncidentResultDto
    {
        public int? TicketId { get; set; }
        public bool Created { get; set; }
        public bool Skipped { get; set; }
        public string? SkipReason { get; set; }
        public int OccurrenceCount { get; set; }
        public string? Fingerprint { get; set; }
    }

    public class SupportTicketAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public long FileSize { get; set; }
        public string? ContentType { get; set; }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class SupportTicketCommentDto
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public string Author { get; set; } = string.Empty;
        public string? AuthorEmail { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<SupportTicketAttachmentDto>? Attachments { get; set; }
    }

    public class CreateCommentDto
    {
        public string Text { get; set; } = string.Empty;
        public bool IsInternal { get; set; } = false;
        public List<IFormFile>? Attachments { get; set; }
    }

    public class SupportTicketLinkDto
    {
        public int Id { get; set; }
        public int SourceTicketId { get; set; }
        public int TargetTicketId { get; set; }
        public string LinkType { get; set; } = "related";
        public string? TargetTicketTitle { get; set; }
        public string? TargetTicketStatus { get; set; }
    }

    public class CreateLinkDto
    {
        public int TargetTicketId { get; set; }
        public string LinkType { get; set; } = "related";
    }
}
