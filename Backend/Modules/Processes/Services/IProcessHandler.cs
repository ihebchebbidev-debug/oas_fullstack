using MyApi.Modules.Processes.DTOs;

namespace MyApi.Modules.Processes.Services
{
    /// <summary>
    /// Contract every scheduled admin process implements. Registered per-key in
    /// <see cref="ProcessHandlerRegistry"/> and invoked by the scheduler or the
    /// manual "Run now" endpoint. Handlers must be idempotent and self-contained
    /// (open their own DB scope via the injected IServiceProvider).
    /// </summary>
    public interface IProcessHandler
    {
        /// <summary>Unique process key (e.g. "admin.retry-failed-emails").</summary>
        string Key { get; }

        Task<RunNowResult> ExecuteAsync(string configJson, CancellationToken ct);
    }

    public class ProcessHandlerRegistry
    {
        private readonly Dictionary<string, IProcessHandler> _byKey;

        public ProcessHandlerRegistry(IEnumerable<IProcessHandler> handlers)
        {
            _byKey = handlers.ToDictionary(h => h.Key, h => h, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGet(string key, out IProcessHandler handler) => _byKey.TryGetValue(key, out handler!);
        public IEnumerable<string> Keys => _byKey.Keys;
    }
}
