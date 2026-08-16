using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Infrastructure;
using MyApi.Modules.SupportTickets.DTOs;
using MyApi.Modules.SupportTickets.Models;

namespace MyApi.Modules.SupportTickets.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupportTicketsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly ILogger<SupportTicketsController> _logger;

        public SupportTicketsController(
            ITenantDbContextFactory dbFactory,
            ILogger<SupportTicketsController> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        private string GetTenant() => TenantResolution.Resolve(HttpContext);

        /// <summary>
        /// POST /api/SupportTickets — Create a new support ticket (anonymous).
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<SupportTicketResponseDto>> Create([FromForm] CreateSupportTicketDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { error = "Title is required" });
            if (string.IsNullOrWhiteSpace(dto.Description))
                return BadRequest(new { error = "Description is required" });

            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var ticket = new SupportTicket
            {
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                Urgency = dto.Urgency,
                Category = dto.Category,
                CurrentPage = dto.CurrentPage,
                RelatedUrl = dto.RelatedUrl,
                Tenant = tenant,
                UserEmail = dto.UserEmail,
                Status = "open",
                CreatedAt = DateTime.UtcNow
            };

            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync();

            // Handle file uploads
            if (dto.Attachments != null && dto.Attachments.Count > 0)
            {
                var uploadDir = Path.Combine("wwwroot", "uploads", "support-tickets", ticket.Id.ToString());
                Directory.CreateDirectory(uploadDir);

                foreach (var file in dto.Attachments.Take(5))
                {
                    if (file.Length == 0) continue;

                    var safeFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(uploadDir, safeFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var attachment = new SupportTicketAttachment
                    {
                        SupportTicketId = ticket.Id,
                        FileName = file.FileName,
                        FilePath = $"/uploads/support-tickets/{ticket.Id}/{safeFileName}",
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        UploadedAt = DateTime.UtcNow
                    };

                    db.SupportTicketAttachments.Add(attachment);
                }

                await db.SaveChangesAsync();
            }

            var created = await BuildResponseDto(db, ticket.Id);

            _logger.LogInformation("Support ticket #{TicketId} created by {Email} on tenant {Tenant}",
                created!.Id, created.UserEmail ?? "anonymous", tenant);

            return Created($"/api/SupportTickets/{created.Id}", created);
        }

        /// <summary>
        /// GET /api/SupportTickets — List all support tickets (admin).
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<List<SupportTicketResponseDto>>> GetAll()
        {
            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var tickets = await db.SupportTickets
                .Include(t => t.Attachments)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new SupportTicketResponseDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Urgency = t.Urgency,
                    Category = t.Category,
                    CurrentPage = t.CurrentPage,
                    RelatedUrl = t.RelatedUrl,
                    Tenant = t.Tenant,
                    UserEmail = t.UserEmail,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    Source = t.Source,
                    ErrorFingerprint = t.ErrorFingerprint,
                    SystemLogId = t.SystemLogId,
                    OccurrenceCount = t.OccurrenceCount,
                    LastOccurredAt = t.LastOccurredAt,
                    IncidentType = t.IncidentType,
                    Module = t.Module,
                    Attachments = t.Attachments.Select(a => new SupportTicketAttachmentDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FilePath = a.FilePath,
                        FileSize = a.FileSize,
                        ContentType = a.ContentType
                    }).ToList()
                })
                .ToListAsync();

            return Ok(tickets);
        }

        /// <summary>
        /// GET /api/SupportTickets/{id} — Get a single ticket.
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<ActionResult<SupportTicketResponseDto>> GetById(int id)
        {
            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var ticket = await BuildResponseDto(db, id);
            if (ticket == null) return NotFound(new { error = $"Ticket {id} not found" });

            return Ok(ticket);
        }

        /// <summary>
        /// PATCH /api/SupportTickets/{id}/status — Update ticket status.
        /// </summary>
        [HttpPatch("{id:int}/status")]
        [Authorize]
        public async Task<ActionResult<SupportTicketResponseDto>> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            var validStatuses = new[] { "open", "in_progress", "resolved", "closed" };
            if (string.IsNullOrWhiteSpace(dto.Status) || !validStatuses.Contains(dto.Status.ToLower()))
                return BadRequest(new { error = $"Invalid status. Must be one of: {string.Join(", ", validStatuses)}" });

            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound(new { error = $"Ticket {id} not found" });

            ticket.Status = dto.Status.ToLower();
            await db.SaveChangesAsync();

            var response = await BuildResponseDto(db, id);
            return Ok(response);
        }

        private static async Task<SupportTicketResponseDto?> BuildResponseDto(ApplicationDbContext db, int ticketId)
        {
            return await db.SupportTickets
                .Include(t => t.Attachments)
                .Where(t => t.Id == ticketId)
                .Select(t => new SupportTicketResponseDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Urgency = t.Urgency,
                    Category = t.Category,
                    CurrentPage = t.CurrentPage,
                    RelatedUrl = t.RelatedUrl,
                    Tenant = t.Tenant,
                    UserEmail = t.UserEmail,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    Source = t.Source,
                    ErrorFingerprint = t.ErrorFingerprint,
                    SystemLogId = t.SystemLogId,
                    OccurrenceCount = t.OccurrenceCount,
                    LastOccurredAt = t.LastOccurredAt,
                    IncidentType = t.IncidentType,
                    Module = t.Module,
                    Attachments = t.Attachments.Select(a => new SupportTicketAttachmentDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FilePath = a.FilePath,
                        FileSize = a.FileSize,
                        ContentType = a.ContentType
                    }).ToList()
                })
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// GET /api/SupportTickets/{id}/comments — Get comments for a ticket
        /// </summary>
        [HttpGet("{id:int}/comments")]
        [Authorize]
        public async Task<ActionResult<List<SupportTicketCommentDto>>> GetComments(int id)
        {
            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var comments = await db.Set<SupportTicketComment>()
                .Where(c => c.SupportTicketId == id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new SupportTicketCommentDto
                {
                    Id = c.Id,
                    TicketId = c.SupportTicketId,
                    Author = c.Author,
                    AuthorEmail = c.AuthorEmail,
                    Text = c.Text,
                    IsInternal = c.IsInternal,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            return Ok(comments);
        }

        /// <summary>
        /// POST /api/SupportTickets/{id}/comments — Add a comment to a ticket
        /// </summary>
        [HttpPost("{id:int}/comments")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<SupportTicketCommentDto>> AddComment(int id, [FromForm] CreateCommentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest(new { error = "Text is required" });

            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound(new { error = "Ticket not found" });

            var author = User?.Identity?.Name ?? User?.FindFirst("email")?.Value ?? "System";

            var comment = new SupportTicketComment
            {
                SupportTicketId = id,
                Author = string.IsNullOrWhiteSpace(author) ? "System" : author,
                AuthorEmail = User?.FindFirst("email")?.Value,
                Text = dto.Text.Trim(),
                IsInternal = dto.IsInternal,
                CreatedAt = DateTime.UtcNow
            };

            db.Add(comment);
            await db.SaveChangesAsync();

            var createdAttachments = new List<SupportTicketAttachmentDto>();

            if (dto.Attachments != null && dto.Attachments.Count > 0)
            {
                var uploadDir = Path.Combine("wwwroot", "uploads", "support-tickets", ticket.Id.ToString(), "comments", comment.Id.ToString());
                Directory.CreateDirectory(uploadDir);

                foreach (var file in dto.Attachments.Take(5))
                {
                    if (file.Length == 0) continue;

                    var safeFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(uploadDir, safeFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var attachment = new SupportTicketAttachment
                    {
                        SupportTicketId = ticket.Id,
                        FileName = file.FileName,
                        FilePath = $"/uploads/support-tickets/{ticket.Id}/comments/{comment.Id}/{safeFileName}",
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        UploadedAt = DateTime.UtcNow
                    };

                    db.SupportTicketAttachments.Add(attachment);
                    await db.SaveChangesAsync();

                    createdAttachments.Add(new SupportTicketAttachmentDto
                    {
                        Id = attachment.Id,
                        FileName = attachment.FileName,
                        FilePath = attachment.FilePath,
                        FileSize = attachment.FileSize,
                        ContentType = attachment.ContentType
                    });
                }
            }

            var outDto = new SupportTicketCommentDto
            {
                Id = comment.Id,
                TicketId = comment.SupportTicketId,
                Author = comment.Author,
                AuthorEmail = comment.AuthorEmail,
                Text = comment.Text,
                IsInternal = comment.IsInternal,
                CreatedAt = comment.CreatedAt,
                Attachments = createdAttachments
            };

            return Created($"/api/SupportTickets/{id}/comments/{comment.Id}", outDto);
        }

        /// <summary>
        /// GET /api/SupportTickets/{id}/links — Get links for a ticket
        /// </summary>
        [HttpGet("{id:int}/links")]
        [Authorize]
        public async Task<ActionResult<List<SupportTicketLinkDto>>> GetLinks(int id)
        {
            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var links = await db.Set<SupportTicketLink>()
                .Where(l => l.SourceTicketId == id)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync();

            var outList = new List<SupportTicketLinkDto>();
            foreach (var l in links)
            {
                var target = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == l.TargetTicketId);
                outList.Add(new SupportTicketLinkDto
                {
                    Id = l.Id,
                    SourceTicketId = l.SourceTicketId,
                    TargetTicketId = l.TargetTicketId,
                    LinkType = l.LinkType,
                    TargetTicketTitle = target?.Title,
                    TargetTicketStatus = target?.Status
                });
            }

            return Ok(outList);
        }

        /// <summary>
        /// POST /api/SupportTickets/{id}/links — Add a link from source (id) to target
        /// </summary>
        [HttpPost("{id:int}/links")]
        [Authorize]
        public async Task<ActionResult<SupportTicketLinkDto>> AddLink(int id, [FromBody] CreateLinkDto dto)
        {
            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var source = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
            var target = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == dto.TargetTicketId);
            if (source == null || target == null) return NotFound(new { error = "Source or target ticket not found" });

            if (id == dto.TargetTicketId) return BadRequest(new { error = "Cannot link ticket to itself" });

            var exists = await db.Set<SupportTicketLink>().AnyAsync(x => x.SourceTicketId == id && x.TargetTicketId == dto.TargetTicketId && x.LinkType == dto.LinkType);
            if (exists) return Conflict(new { error = "Link already exists" });

            var link = new SupportTicketLink
            {
                SourceTicketId = id,
                TargetTicketId = dto.TargetTicketId,
                LinkType = dto.LinkType ?? "related",
                CreatedAt = DateTime.UtcNow
            };

            db.Add(link);
            await db.SaveChangesAsync();

            var outDto = new SupportTicketLinkDto
            {
                Id = link.Id,
                SourceTicketId = link.SourceTicketId,
                TargetTicketId = link.TargetTicketId,
                LinkType = link.LinkType,
                TargetTicketTitle = target.Title,
                TargetTicketStatus = target.Status
            };

            return Created($"/api/SupportTickets/{id}/links/{link.Id}", outDto);
        }

        /// <summary>
        /// DELETE /api/SupportTickets/{id}/links/{linkId} — Remove a link
        /// </summary>
        [HttpDelete("{id:int}/links/{linkId:int}")]
        [Authorize]
        public async Task<IActionResult> RemoveLink(int id, int linkId)
        {
            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            var link = await db.Set<SupportTicketLink>().FirstOrDefaultAsync(l => l.Id == linkId && l.SourceTicketId == id);
            if (link == null) return NotFound(new { error = "Link not found" });

            db.Remove(link);
            await db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// GET /api/SupportTickets/search?q= — Search tickets by title/description/id
        /// </summary>
        [HttpGet("search")]
        [Authorize]
        public async Task<ActionResult<List<SupportTicketResponseDto>>> Search([FromQuery] string q)
        {
            var tenant = GetTenant();
            await using var db = _dbFactory.CreateDbContext(tenant);

            if (string.IsNullOrWhiteSpace(q)) return Ok(new List<SupportTicketResponseDto>());

            var lowered = q.ToLower();
            var tickets = await db.SupportTickets
                .Where(t => t.Tenant == tenant && (
                    t.Title.ToLower().Contains(lowered) ||
                    t.Description.ToLower().Contains(lowered) ||
                    EF.Functions.Like(t.Id.ToString(), $"%{q}%")
                ))
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .Select(t => new SupportTicketResponseDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Urgency = t.Urgency,
                    Category = t.Category,
                    CurrentPage = t.CurrentPage,
                    RelatedUrl = t.RelatedUrl,
                    Tenant = t.Tenant,
                    UserEmail = t.UserEmail,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    Source = t.Source,
                    ErrorFingerprint = t.ErrorFingerprint,
                    SystemLogId = t.SystemLogId,
                    OccurrenceCount = t.OccurrenceCount,
                    LastOccurredAt = t.LastOccurredAt,
                    IncidentType = t.IncidentType,
                    Module = t.Module,
                    Attachments = t.Attachments.Select(a => new SupportTicketAttachmentDto {
                        Id = a.Id,
                        FileName = a.FileName,
                        FilePath = a.FilePath,
                        FileSize = a.FileSize,
                        ContentType = a.ContentType
                    }).ToList()
                })
                .ToListAsync();

            return Ok(tickets);
        }
    }
}
