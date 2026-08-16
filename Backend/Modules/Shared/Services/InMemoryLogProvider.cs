namespace MyApi.Modules.Shared.Services
{
    public class InMemoryLogProvider : ILoggerProvider
    {
        private readonly IInMemoryLogStore _logStore;

        public InMemoryLogProvider(IInMemoryLogStore logStore)
        {
            _logStore = logStore;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new InMemoryLogger(_logStore, categoryName);
        }

        public void Dispose() { }
    }

    public class InMemoryLogger : ILogger
    {
        private readonly IInMemoryLogStore _logStore;
        private readonly string _categoryName;

        public InMemoryLogger(IInMemoryLogStore logStore, string categoryName)
        {
            _logStore = logStore;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            _logStore.AddLog(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = logLevel.ToString(),
                Category = _categoryName,
                Message = formatter(state, exception),
                Exception = exception?.ToString()
            });
        }
    }
}
