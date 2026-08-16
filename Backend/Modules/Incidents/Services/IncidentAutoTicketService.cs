using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyApi.Infrastructure;
using MyApi.Modules.SupportTickets.DTOs;
using MyApi.Modules.SupportTickets.Models;

namespace MyApi.Modules.Incidents.Services
{
    public class IncidentAutoTicketService : IIncidentAutoTicketService
    {
        private const int MaxAutoTicketsPerDay = 50;
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly ILogger<IncidentAutoTicketService> _logger;

        private static readonly HashSet<string> AlwaysTicketTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "app_crash",
            "react_boundary",
            "unhandled_rejection",
            "window_error",
            "chunk_load_error",
            "sync_failure",
            "backend_health",
            "security_violation",
            "logger_error",
        };

        public IncidentAutoTicketService(
            ITenantDbContextFactory dbFactory,
            ILogger<IncidentAutoTicketService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<AutoIncidentResultDto> ProcessAsync(AutoIncidentReportDto dto, string tenant)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
            {
                return Skipped(null, "empty_message");
            }

            if (!ShouldCreateTicket(dto))
            {
                return Skipped(null, "policy");
            }

            var fingerprint = !string.IsNullOrWhiteSpace(dto.Fingerprint)
                ? dto.Fingerprint.Trim()
                : ComputeFingerprint(dto);

            await using var db = _dbFactory.CreateDbContext(tenant);

            var todayStart = DateTime.UtcNow.Date;
            var todayAutoCount = await db.SupportTickets
                .CountAsync(t => t.Source == "auto" && t.CreatedAt >= todayStart);
            if (todayAutoCount >= MaxAutoTicketsPerDay)
            {
                _logger.LogWarning("Auto-ticket daily limit reached for tenant {Tenant}", tenant);
                return Skipped(fingerprint, "daily_limit");
            }

            var existing = await db.SupportTickets
                .Where(t =>
                    t.ErrorFingerprint == fingerprint &&
                    (t.Status == "open" || t.Status == "in_progress" || t.Status == "resolved"))
                .OrderByDescending(t => t.LastOccurredAt ?? t.CreatedAt)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                if (existing.Status == "resolved")
                {
                    existing.Status = "open";
                }

                existing.OccurrenceCount += 1;
                existing.LastOccurredAt = DateTime.UtcNow;
                if (dto.SystemLogId.HasValue && !existing.SystemLogId.HasValue)
                {
                    existing.SystemLogId = dto.SystemLogId;
                }

                // Escalate urgency as incidents repeat
                if (existing.OccurrenceCount >= 10)
                    existing.Urgency = "critical";
                else if (existing.OccurrenceCount >= 5 && existing.Urgency is "low" or "medium")
                    existing.Urgency = "high";

                var commentText = BuildOccurrenceComment(dto, existing.OccurrenceCount);
                db.Set<SupportTicketComment>().Add(new SupportTicketComment
                {
                    SupportTicketId = existing.Id,
                    Author = "Auto-Incident",
                    AuthorEmail = dto.UserEmail,
                    Text = commentText,
                    IsInternal = true,
                    CreatedAt = DateTime.UtcNow,
                });

                await db.SaveChangesAsync();

                return new AutoIncidentResultDto
                {
                    TicketId = existing.Id,
                    Created = false,
                    Skipped = false,
                    OccurrenceCount = existing.OccurrenceCount,
                    Fingerprint = fingerprint,
                };
            }

            var urgency = MapUrgency(dto);
            var module = NormalizeModule(dto.Module, dto.CurrentPage);
            var title = BuildTitle(dto, module);
            var description = BuildDescription(dto, fingerprint, module);

            var ticket = new SupportTicket
            {
                Title = Truncate(title, 300),
                Description = description,
                Urgency = urgency,
                Category = "bug",
                CurrentPage = Truncate(dto.CurrentPage, 500),
                RelatedUrl = Truncate(dto.RelatedUrl, 1000),
                Tenant = tenant,
                UserEmail = dto.UserEmail,
                Status = "open",
                Source = "auto",
                ErrorFingerprint = fingerprint,
                SystemLogId = dto.SystemLogId,
                OccurrenceCount = 1,
                LastOccurredAt = DateTime.UtcNow,
                IncidentType = Truncate(dto.IncidentType, 50),
                Module = Truncate(module, 100),
                CreatedAt = DateTime.UtcNow,
            };

            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync();

            _logger.LogInformation(
                "Auto ticket #{TicketId} created for {IncidentType} on {Page} (tenant {Tenant})",
                ticket.Id, dto.IncidentType, dto.CurrentPage ?? "unknown", tenant);

            return new AutoIncidentResultDto
            {
                TicketId = ticket.Id,
                Created = true,
                Skipped = false,
                OccurrenceCount = 1,
                Fingerprint = fingerprint,
            };
        }

        private static bool ShouldCreateTicket(AutoIncidentReportDto dto)
        {
            var type = dto.IncidentType?.Trim() ?? string.Empty;
            var count = dto.ClientOccurrenceCount ?? 1;

            if (AlwaysTicketTypes.Contains(type))
            {
                return true;
            }

            if (dto.HttpStatus is >= 500)
            {
                return true;
            }

            if (type is "api_error" or "mutation_error" or "query_error")
            {
                if (dto.HttpStatus is >= 500) return true;
                if (dto.HttpStatus is >= 400 and < 500) return count >= 3;
                return count >= 1;
            }

            if (type == "network_error")
            {
                return count >= 1;
            }

            if (type == "console_error")
            {
                return count >= 2;
            }

            return false;
        }

        private static string ComputeFingerprint(AutoIncidentReportDto dto)
        {
            var stackTop = ExtractStackTop(dto.Stack);
            var endpoint = NormalizeEndpoint(dto.Endpoint);
            var route = NormalizeRoute(dto.CurrentPage);
            var raw = string.Join('|', new[]
            {
                dto.IncidentType ?? "",
                NormalizeMessage(dto.Message),
                dto.HttpStatus?.ToString() ?? "",
                dto.HttpMethod ?? "",
                endpoint,
                route,
                stackTop,
            });

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string NormalizeMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "";
            var trimmed = message.Trim();
            if (trimmed.Length > 200) trimmed = trimmed[..200];
            return trimmed;
        }

        private static string NormalizeEndpoint(string? endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return "";
            var path = endpoint.Split('?')[0];
            return System.Text.RegularExpressions.Regex.Replace(path, @"/\d+", "/:id");
        }

        private static string NormalizeRoute(string? page)
        {
            if (string.IsNullOrWhiteSpace(page)) return "";
            return System.Text.RegularExpressions.Regex.Replace(page, @"/\d+", "/:id");
        }

        private static string ExtractStackTop(string? stack)
        {
            if (string.IsNullOrWhiteSpace(stack)) return "";
            var line = stack.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.Contains("at ") && !l.Contains("node_modules"));
            return line?.Trim() ?? stack.Split('\n').FirstOrDefault()?.Trim() ?? "";
        }

        private static string MapUrgency(AutoIncidentReportDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.Severity))
            {
                return dto.Severity.ToLowerInvariant() switch
                {
                    "critical" => "critical",
                    "high" => "high",
                    "medium" => "medium",
                    "low" => "low",
                    _ => "medium",
                };
            }

            return dto.IncidentType?.ToLowerInvariant() switch
            {
                "app_crash" or "react_boundary" or "backend_health" => "critical",
                "chunk_load_error" or "sync_failure" or "security_violation" => "high",
                "unhandled_rejection" or "window_error" or "logger_error" => "high",
                _ when dto.ClientOccurrenceCount is >= 10 => "critical",
                _ when dto.ClientOccurrenceCount is >= 5 => "high",
                _ when dto.HttpStatus is >= 500 => "high",
                _ => "medium",
            };
        }

        private static string NormalizeModule(string? module, string? currentPage)
        {
            if (!string.IsNullOrWhiteSpace(module)) return module.Trim();
            if (string.IsNullOrWhiteSpace(currentPage)) return "app";
            var parts = currentPage.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Equals("dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }
            return parts.Length > 0 ? parts[0] : "app";
        }

        private static string BuildTitle(AutoIncidentReportDto dto, string module)
        {
            var msg = NormalizeMessage(dto.Message);
            if (msg.Length > 80) msg = msg[..80] + "…";
            var typeLabel = dto.IncidentType?.Replace('_', ' ') ?? "incident";
            return $"[Auto] {module}: {typeLabel} — {msg}";
        }

        private static string BuildDescription(AutoIncidentReportDto dto, string fingerprint, string module)
        {
            var lines = new List<string>
            {
                "Automatically detected incident",
                "",
                $"Type: {dto.IncidentType}",
                $"Module: {module}",
                $"Message: {dto.Message}",
                $"Severity: {MapUrgency(dto)}",
                $"Fingerprint: {fingerprint}",
            };

            if (!string.IsNullOrWhiteSpace(dto.ReferenceId))
                lines.Add($"Reference ID: {dto.ReferenceId}");
            if (!string.IsNullOrWhiteSpace(dto.CurrentPage))
                lines.Add($"Page: {dto.CurrentPage}");
            if (!string.IsNullOrWhiteSpace(dto.RelatedUrl))
                lines.Add($"URL: {dto.RelatedUrl}");
            if (dto.HttpStatus.HasValue)
                lines.Add($"HTTP: {dto.HttpMethod ?? "GET"} {dto.Endpoint} → {dto.HttpStatus}");
            if (!string.IsNullOrWhiteSpace(dto.UserEmail))
                lines.Add($"User: {dto.UserName ?? dto.UserEmail} ({dto.UserEmail})");
            if (!string.IsNullOrWhiteSpace(dto.UserId))
                lines.Add($"User ID: {dto.UserId}");
            if (!string.IsNullOrWhiteSpace(dto.EntityType))
                lines.Add($"Entity: {dto.EntityType} {dto.EntityId}".Trim());
            if (!string.IsNullOrWhiteSpace(dto.UserAgent))
                lines.Add($"User Agent: {dto.UserAgent}");
            if (!string.IsNullOrWhiteSpace(dto.Stack))
            {
                lines.Add("");
                lines.Add("Stack trace:");
                lines.Add(dto.Stack.Length > 4000 ? dto.Stack[..4000] + "…" : dto.Stack);
            }
            if (!string.IsNullOrWhiteSpace(dto.ComponentStack))
            {
                lines.Add("");
                lines.Add("Component stack:");
                lines.Add(dto.ComponentStack.Length > 2000 ? dto.ComponentStack[..2000] + "…" : dto.ComponentStack);
            }
            if (!string.IsNullOrWhiteSpace(dto.Details))
            {
                lines.Add("");
                lines.Add("Additional details:");
                lines.Add(dto.Details.Length > 2000 ? dto.Details[..2000] + "…" : dto.Details);
            }

            lines.Add("");
            lines.Add($"Detected at: {DateTime.UtcNow:O}");

            return string.Join('\n', lines);
        }

        private static string BuildOccurrenceComment(AutoIncidentReportDto dto, int count)
        {
            var parts = new List<string> { $"Occurrence #{count} detected at {DateTime.UtcNow:O}" };
            if (!string.IsNullOrWhiteSpace(dto.CurrentPage))
                parts.Add($"Page: {dto.CurrentPage}");
            if (!string.IsNullOrWhiteSpace(dto.Message))
                parts.Add($"Message: {NormalizeMessage(dto.Message)}");
            if (dto.HttpStatus.HasValue)
                parts.Add($"HTTP {dto.HttpStatus}: {dto.HttpMethod} {dto.Endpoint}");
            if (!string.IsNullOrWhiteSpace(dto.ReferenceId))
                parts.Add($"Reference: {dto.ReferenceId}");
            return string.Join("\n", parts);
        }

        private static string Truncate(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed[..max];
        }

        private static AutoIncidentResultDto Skipped(string? fingerprint, string reason) =>
            new()
            {
                Skipped = true,
                SkipReason = reason,
                Fingerprint = fingerprint,
            };
    }
}
