using System;

namespace MyApi.Modules.WebsiteBuilder.Services
{
    /// <summary>
    /// Wave 2 — thrown when an optimistic concurrency check fails (e.g. the
    /// caller's ExpectedUpdatedAt no longer matches the row in the DB).
    /// Controllers map this to HTTP 409 Conflict.
    /// </summary>
    public class WBConcurrencyException : Exception
    {
        public DateTime? CurrentUpdatedAt { get; }
        public WBConcurrencyException(string message, DateTime? currentUpdatedAt = null)
            : base(message)
        {
            CurrentUpdatedAt = currentUpdatedAt;
        }
    }

    /// <summary>
    /// Wave 2 — thrown when an attempt is made to create/update an entity
    /// whose (TenantId, Slug) or (TenantId, SiteId, Slug) collides with an
    /// existing non-deleted row. Controllers map this to HTTP 409 Conflict.
    /// </summary>
    public class WBSlugConflictException : Exception
    {
        public WBSlugConflictException(string message) : base(message) { }
    }
}
