using System;

namespace MyApi.Modules.ModuleRequests.DTOs
{
    /// <summary>
    /// Payload sent by the tenant UI when a user requests activation or
    /// deactivation of a module (plugin) on their subscription.
    /// </summary>
    public class ModuleRequestDto
    {
        /// <summary>"activate" or "deactivate".</summary>
        public string Action { get; set; } = "activate";

        /// <summary>Plugin code, e.g. PL0033SYSTEM.</summary>
        public string ModuleCode { get; set; } = "";

        /// <summary>Module key, e.g. "projects".</summary>
        public string ModuleKey { get; set; } = "";

        /// <summary>Localized module display name shown to the user.</summary>
        public string ModuleName { get; set; } = "";

        /// <summary>Whether the module is currently enabled for this tenant.</summary>
        public bool CurrentlyEnabled { get; set; }

        /// <summary>Optional free-text justification from the user.</summary>
        public string? Reason { get; set; }

        /// <summary>Full origin the request was made from, e.g. https://krossier.flowentra.app</summary>
        public string? AppUrl { get; set; }

        /// <summary>Tenant slug detected in the frontend (krossier, demo, test, dev...).</summary>
        public string? TenantSlug { get; set; }

        /// <summary>Requesting user's email (fallback when not present in the JWT).</summary>
        public string? UserEmail { get; set; }

        /// <summary>Requesting user's display name.</summary>
        public string? UserName { get; set; }

        /// <summary>Client local time as ISO string (for support context).</summary>
        public string? ClientTime { get; set; }

        /// <summary>Client IANA timezone, e.g. Europe/Paris.</summary>
        public string? TimeZone { get; set; }
    }

    public class ModuleRequestResultDto
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? SentTo { get; set; }
        public DateTime RequestedAtUtc { get; set; }
    }
}