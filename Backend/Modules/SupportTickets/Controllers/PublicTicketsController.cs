using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Infrastructure;
using MyApi.Modules.SupportTickets.DTOs;
using MyApi.Modules.SupportTickets.Models;

namespace MyApi.Modules.SupportTickets.Controllers
{
    /// <summary>
    /// Cross-tenant, fully OPEN access to support tickets across every
    /// configured tenant database. No auth, no API key, no headers.
    /// Bypasses X-Tenant routing; fans out via
    /// TenantConnectionResolver.GetConfiguredTenantConnections().
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/public")]
    public class PublicTicketsController : ControllerBase
    {
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly ILogger<PublicTicketsController> _logger;

        public PublicTicketsController(
            ITenantDbContextFactory dbFactory,
            ILogger<PublicTicketsController> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        private static readonly string[] ValidStatuses = { "open", "in_progress", "resolved", "closed" };

        // ─────────────────────────────────────────────────────────────
        // Tenants
        // ─────────────────────────────────────────────────────────────

        [HttpGet("tenants")]
        public ActionResult<List<TenantSummaryDto>> ListTenants()
        {
            var tenants = TenantConnectionResolver.GetConfiguredTenantConnections()
                .Select(t => new TenantSummaryDto { Tenant = t.Tenant, Source = t.Source })
                .ToList();
            return Ok(tenants);
        }

        // ─────────────────────────────────────────────────────────────
        // List tickets across all tenants
        // ─────────────────────────────────────────────────────────────

        [HttpGet("tickets")]
        public async Task<ActionResult<PagedPublicTicketsDto>> ListTickets(
            [FromQuery] string? tenant,
            [FromQuery] string? origin,
            [FromQuery] string? status,
            [FromQuery] string? userEmail,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 500) pageSize = 500;

            var configured = TenantConnectionResolver.GetConfiguredTenantConnections()
                .Select(t => t.Tenant)
                .ToList();

            if (!string.IsNullOrWhiteSpace(tenant))
            {
                var t = tenant.Trim().ToLowerInvariant();
                configured = configured.Where(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var errors = new List<CrossTenantError>();
            var perTenantResults = new List<PublicTicketDto>();

            var tasks = configured.Select(async slug =>
            {
                try
                {
                    await using var db = _dbFactory.CreateDbContext(slug);
                    IQueryable<SupportTicket> q = db.SupportTickets.Include(x => x.Attachments);

                    if (!string.IsNullOrWhiteSpace(status))
                        q = q.Where(x => x.Status == status.ToLower());
                    if (!string.IsNullOrWhiteSpace(origin))
                    {
                        var o = origin.Trim().ToLowerInvariant();
                        q = q.Where(x => (x.Source ?? "manual").ToLower() == o);
                    }
                    if (!string.IsNullOrWhiteSpace(userEmail))
                    {
                        var e = userEmail.Trim().ToLowerInvariant();
                        q = q.Where(x => x.UserEmail != null && x.UserEmail.ToLower() == e);
                    }
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim();
                        q = q.Where(x => EF.Functions.ILike(x.Title, $"%{s}%") ||
                                         EF.Functions.ILike(x.Description, $"%{s}%"));
                    }

                    var rows = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
                    return (slug, rows.Select(r => ToDto(r, slug)).ToList(), (string?)null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Public API: tenant {Tenant} query failed", slug);
                    return (slug, new List<PublicTicketDto>(), (string?)ex.Message);
                }
            });

            var results = await Task.WhenAll(tasks);
            foreach (var (slug, rows, err) in results)
            {
                perTenantResults.AddRange(rows);
                if (err != null) errors.Add(new CrossTenantError { Tenant = slug, Message = err });
            }

            var merged = perTenantResults.OrderByDescending(x => x.CreatedAt).ToList();
            var total = merged.Count;
            var paged = merged.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var counts = new Dictionary<string, int>
            {
                ["manual"] = merged.Count(x => x.Origin.Type == "manual"),
                ["auto"] = merged.Count(x => x.Origin.Type == "auto"),
                ["open"] = merged.Count(x => x.Status == "open"),
                ["in_progress"] = merged.Count(x => x.Status == "in_progress"),
                ["resolved"] = merged.Count(x => x.Status == "resolved"),
                ["closed"] = merged.Count(x => x.Status == "closed"),
            };

            return Ok(new PagedPublicTicketsDto
            {
                Items = paged,
                Total = total,
                Page = page,
                PageSize = pageSize,
                Counts = counts,
                Errors = errors
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Single ticket
        // ─────────────────────────────────────────────────────────────

        [HttpGet("tickets/{tenant}/{id:int}")]
        public async Task<ActionResult<PublicTicketDto>> GetTicket(string tenant, int id)
        {
            if (!ValidateTenant(tenant, out var err)) return err!;
            await using var db = _dbFactory.CreateDbContext(tenant);
            var t = await db.SupportTickets.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound(new { error = $"Ticket {id} not found in tenant '{tenant}'." });
            return Ok(ToDto(t, tenant));
        }

        // ─────────────────────────────────────────────────────────────
        // Update status
        // ─────────────────────────────────────────────────────────────

        [HttpPatch("tickets/{tenant}/{id:int}/status")]
        public async Task<ActionResult<PublicTicketDto>> UpdateStatus(string tenant, int id, [FromBody] UpdateStatusDto dto)
        {
            if (!ValidateTenant(tenant, out var err)) return err!;
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status) || !ValidStatuses.Contains(dto.Status.ToLower()))
                return BadRequest(new { error = $"Invalid status. Must be one of: {string.Join(", ", ValidStatuses)}" });

            await using var db = _dbFactory.CreateDbContext(tenant);
            var t = await db.SupportTickets.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound(new { error = $"Ticket {id} not found in tenant '{tenant}'." });

            t.Status = dto.Status.ToLower();
            await db.SaveChangesAsync();

            _logger.LogInformation("Public API: ticket #{Id} on {Tenant} status → {Status}", id, tenant, t.Status);
            return Ok(ToDto(t, tenant));
        }

        // ─────────────────────────────────────────────────────────────
        // Comments
        // ─────────────────────────────────────────────────────────────

        [HttpGet("tickets/{tenant}/{id:int}/comments")]
        public async Task<ActionResult<List<PublicCommentDto>>> GetComments(string tenant, int id)
        {
            if (!ValidateTenant(tenant, out var err)) return err!;
            await using var db = _dbFactory.CreateDbContext(tenant);

            var exists = await db.SupportTickets.AnyAsync(x => x.Id == id);
            if (!exists) return NotFound(new { error = $"Ticket {id} not found in tenant '{tenant}'." });

            var comments = await db.SupportTicketComments
                .Where(c => c.SupportTicketId == id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new PublicCommentDto
                {
                    Id = c.Id,
                    TicketId = c.SupportTicketId,
                    Tenant = tenant,
                    Author = c.Author,
                    AuthorEmail = c.AuthorEmail,
                    Text = c.Text,
                    IsInternal = c.IsInternal,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            return Ok(comments);
        }

        [HttpPost("tickets/{tenant}/{id:int}/comments")]
        public async Task<ActionResult<PublicCommentDto>> AddComment(string tenant, int id, [FromBody] CreatePublicCommentDto dto)
        {
            if (!ValidateTenant(tenant, out var err)) return err!;
            if (dto == null || string.IsNullOrWhiteSpace(dto.Text))
                return BadRequest(new { error = "Text is required." });

            await using var db = _dbFactory.CreateDbContext(tenant);
            var ticket = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id);
            if (ticket == null) return NotFound(new { error = $"Ticket {id} not found in tenant '{tenant}'." });

            var comment = new SupportTicketComment
            {
                SupportTicketId = id,
                Author = string.IsNullOrWhiteSpace(dto.Author) ? "public-api" : dto.Author.Trim(),
                AuthorEmail = string.IsNullOrWhiteSpace(dto.AuthorEmail) ? null : dto.AuthorEmail.Trim(),
                Text = dto.Text.Trim(),
                IsInternal = dto.IsInternal ?? false,
                CreatedAt = DateTime.UtcNow
            };

            db.SupportTicketComments.Add(comment);
            await db.SaveChangesAsync();

            return Ok(new PublicCommentDto
            {
                Id = comment.Id,
                TicketId = comment.SupportTicketId,
                Tenant = tenant,
                Author = comment.Author,
                AuthorEmail = comment.AuthorEmail,
                Text = comment.Text,
                IsInternal = comment.IsInternal,
                CreatedAt = comment.CreatedAt
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private bool ValidateTenant(string tenant, out ActionResult? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(tenant))
            {
                error = BadRequest(new { error = "Tenant is required." });
                return false;
            }
            var configured = TenantConnectionResolver.GetConfiguredTenantConnections()
                .Any(t => string.Equals(t.Tenant, tenant, StringComparison.OrdinalIgnoreCase));
            if (!configured)
            {
                error = NotFound(new { error = $"Unknown tenant '{tenant}'." });
                return false;
            }
            return true;
        }

        private static PublicTicketDto ToDto(SupportTicket t, string tenantSlug)
        {
            var source = string.IsNullOrWhiteSpace(t.Source) ? "manual" : t.Source.ToLowerInvariant();
            var originType = source == "auto" ? "auto" : "manual";
            var anonymous = string.IsNullOrWhiteSpace(t.UserEmail);

            return new PublicTicketDto
            {
                Id = t.Id,
                Tenant = tenantSlug,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Urgency = t.Urgency,
                Category = t.Category,
                Module = t.Module,
                CurrentPage = t.CurrentPage,
                RelatedUrl = t.RelatedUrl,
                CreatedAt = t.CreatedAt,
                Origin = new TicketOriginDto
                {
                    Type = originType,
                    Source = source,
                    IncidentType = t.IncidentType,
                    ErrorFingerprint = t.ErrorFingerprint,
                    SystemLogId = t.SystemLogId,
                    OccurrenceCount = t.OccurrenceCount,
                    LastOccurredAt = t.LastOccurredAt
                },
                Reporter = new TicketReporterDto
                {
                    Email = t.UserEmail,
                    IsAnonymous = anonymous,
                    IsSystem = originType == "auto" && anonymous
                },
                Attachments = t.Attachments?.Select(a => new SupportTicketAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType
                }).ToList() ?? new List<SupportTicketAttachmentDto>()
            };
        }
    }
}
