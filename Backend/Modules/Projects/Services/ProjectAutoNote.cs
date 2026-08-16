using MyApi.Data;
using MyApi.Modules.Projects.Models;

namespace MyApi.Modules.Projects.Services
{
    /// <summary>
    /// Lightweight helper to append "system" notes onto a project's notes timeline
    /// whenever something meaningful happens (offer converted, deal created, link added…).
    /// Kept as a static helper to avoid IProjectService / IOfferService DI cycles.
    /// </summary>
    public static class ProjectAutoNote
    {
        public const string SystemAuthor = "system";

        public static void Add(ApplicationDbContext context, int? projectId, string content, string? userId)
        {
            if (!projectId.HasValue || projectId.Value <= 0) return;
            if (string.IsNullOrWhiteSpace(content)) return;

            var safeContent = content.Length > 4900 ? content.Substring(0, 4900) : content;
            var author = string.IsNullOrWhiteSpace(userId) ? SystemAuthor : userId!;

            context.Set<ProjectNote>().Add(new ProjectNote
            {
                ProjectId = projectId.Value,
                Content = $"[auto] {safeContent}",
                CreatedDate = System.DateTime.UtcNow,
                CreatedBy = author
            });
        }
    }
}
