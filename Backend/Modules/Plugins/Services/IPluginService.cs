using System.Collections.Generic;
using System.Threading.Tasks;
using MyApi.Modules.Plugins.DTOs;

namespace MyApi.Modules.Plugins.Services
{
    public interface IPluginService
    {
        Task<List<PluginActivationDto>> GetActivationsAsync();
        Task<PluginActivationDto> SetActivationAsync(string code, bool isEnabled, int? userId);
        Task<PluginActivationDto> SetActivationAsync(string code, bool isEnabled, int? userId, bool cascade);
        Task<List<PluginActivationDto>> BulkSetAsync(List<string> codes, bool isEnabled, int? userId);
        Task<List<PluginActivationDto>> BulkSetAsync(List<string> codes, bool isEnabled, int? userId, bool cascade);
        Task<PluginStatsDto> GetStatsAsync();
    }

    public class PluginCoreLockedException : System.Exception
    {
        public string Code { get; }
        public PluginCoreLockedException(string code) : base($"Plugin '{code}' is core and cannot be disabled.") => Code = code;
    }

    public class PluginDependencyConflictException : System.Exception
    {
        public string Code { get; }
        public List<string> BlockingDependents { get; }
        public PluginDependencyConflictException(string code, List<string> dependents)
            : base($"Plugin '{code}' is required by other enabled plugins.")
        {
            Code = code;
            BlockingDependents = dependents;
        }
    }

    public class PluginUnknownException : System.Exception
    {
        public string Code { get; }
        public PluginUnknownException(string code) : base($"Unknown plugin code '{code}'.") => Code = code;
    }
}
