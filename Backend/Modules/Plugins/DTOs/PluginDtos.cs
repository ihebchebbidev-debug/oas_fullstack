using System;
using System.Collections.Generic;

namespace MyApi.Modules.Plugins.DTOs
{
    public class PluginActivationDto
    {
        public string Code { get; set; } = "";
        public bool IsEnabled { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PluginToggleRequest
    {
        public bool IsEnabled { get; set; }
        /// <summary>When true, dependents are switched off / the dependency chain on instead of a 409.</summary>
        public bool Cascade { get; set; }
    }

    public class PluginBulkToggleRequest
    {
        public List<string> Codes { get; set; } = new();
        public bool IsEnabled { get; set; }
        public bool Cascade { get; set; }
    }

    public class PluginStatsDto
    {
        public int Active { get; set; }
        public int Total { get; set; }
    }

    public class PluginConflictDto
    {
        public string Message { get; set; } = "";
        public List<string> BlockingDependents { get; set; } = new();
    }
}
