using System.Collections.Concurrent;

namespace MyApi.Modules.Shared.Services
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }

    public interface IInMemoryLogStore
    {
        void AddLog(LogEntry entry);
        IEnumerable<LogEntry> GetLogs(int count = 100, string? level = null);
        void Clear();
        int Count { get; }
    }

    public class InMemoryLogStore : IInMemoryLogStore
    {
        private readonly ConcurrentQueue<LogEntry> _logs = new();
        private const int MaxLogs = 500;

        public void AddLog(LogEntry entry)
        {
            _logs.Enqueue(entry);
            while (_logs.Count > MaxLogs && _logs.TryDequeue(out _)) { }
        }

        public IEnumerable<LogEntry> GetLogs(int count = 100, string? level = null)
        {
            var logs = _logs.ToArray().Reverse();
            if (!string.IsNullOrEmpty(level))
            {
                logs = logs.Where(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
            }
            return logs.Take(count);
        }

        public void Clear()
        {
            while (_logs.TryDequeue(out _)) { }
        }

        public int Count => _logs.Count;
    }
}
