using System.Collections.Concurrent;

namespace MyApi.Modules.Processes.Services
{
    /// <summary>
    /// Tracks in-flight process executions so operators can cooperatively cancel
    /// a running handler via the "Stop" button in the Processes UI.
    ///
    /// A key is registered when <see cref="ProcessSchedulerService.ExecuteOnceAsync"/>
    /// starts a run and removed when it finishes. <see cref="RequestStop"/> triggers
    /// the linked CancellationTokenSource — handlers respect the token via EF Core's
    /// async APIs and abort at the next await point.
    ///
    /// Singleton: process-wide state, must survive across scoped HTTP requests
    /// and the hosted scheduler service.
    /// </summary>
    public class RunningProcessRegistry
    {
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _byKey =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns a linked CancellationTokenSource for the given key and registers
        /// it so <see cref="RequestStop"/> can cancel it. Call <see cref="Unregister"/>
        /// (via disposing the returned handle) when the run finishes.
        /// </summary>
        public Registration Register(string key, CancellationToken outer)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
            // Replace any stale entry — advisory lock guarantees only one active
            // run per key, so an existing entry can only be a leaked prior one.
            if (_byKey.TryRemove(key, out var stale))
            {
                try { stale.Dispose(); } catch { /* best effort */ }
            }
            _byKey[key] = cts;
            return new Registration(this, key, cts);
        }

        /// <summary>Signals cancellation to the in-flight run for this key, if any.</summary>
        public bool RequestStop(string key)
        {
            if (_byKey.TryGetValue(key, out var cts))
            {
                try { cts.Cancel(); return true; } catch { return false; }
            }
            return false;
        }

        public bool IsRunning(string key) => _byKey.ContainsKey(key);

        internal void Unregister(string key, CancellationTokenSource cts)
        {
            if (_byKey.TryGetValue(key, out var current) && ReferenceEquals(current, cts))
                _byKey.TryRemove(key, out _);
            try { cts.Dispose(); } catch { /* best effort */ }
        }

        public sealed class Registration : IDisposable
        {
            private readonly RunningProcessRegistry _owner;
            private readonly string _key;
            private readonly CancellationTokenSource _cts;
            public CancellationToken Token => _cts.Token;
            internal Registration(RunningProcessRegistry owner, string key, CancellationTokenSource cts)
            { _owner = owner; _key = key; _cts = cts; }
            public void Dispose() => _owner.Unregister(_key, _cts);
        }
    }
}
