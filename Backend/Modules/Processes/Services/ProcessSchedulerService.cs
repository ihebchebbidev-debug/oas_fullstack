using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Processes.Models;

namespace MyApi.Modules.Processes.Services
{
    /// <summary>
    /// Ticks every minute, finds ProcessSchedules whose NextRunAt is due, and
    /// executes their handler with retry + exponential backoff.
    ///
    /// A run either:
    ///   - succeeds → schedule's NextRunAt is bumped by IntervalMinutes,
    ///   - fails and attempts &lt; MaxRetries → NextRetryAt = now + Backoff*2^(attempt-1),
    ///   - fails and attempts ≥ MaxRetries → schedule is marked with a BlockReason, the
    ///     retry ladder resets and the process cools down until its next normal slot
    ///     (min 15 minutes). It is deliberately NEVER auto-paused: a transient outage
    ///     must not silently stop automation until an admin notices.
    ///
    /// Concurrency: every execution is guarded by a Postgres advisory lock keyed
    /// on the process Key, so a scheduler tick + a manual "Run now" (or two app
    /// instances) can never run the same handler twice at the same time.
    /// </summary>
    public class ProcessSchedulerService : BackgroundService
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

        /// <summary>Maximum wall-clock time one handler execution may take before it is cancelled.</summary>
        private static readonly TimeSpan HandlerTimeout = TimeSpan.FromMinutes(10);

        /// <summary>ProcessSchedule.BlockReason is capped at 500 chars in the schema.</summary>
        private static string? Truncate(string? value, int max = 500)
            => value != null && value.Length > max ? value.Substring(0, max - 1) + "…" : value;
        private readonly IServiceProvider _sp;
        private readonly ILogger<ProcessSchedulerService> _logger;

        public ProcessSchedulerService(IServiceProvider sp, ILogger<ProcessSchedulerService> logger)
        {
            _sp = sp; _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("⚙️  ProcessSchedulerService started — tick every {Interval}", TickInterval);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            // Close out runs that were still 'running' when the process died — otherwise
            // the UI shows a phantom "running" pill and history keeps an open row forever.
            try { await ReconcileStaleRunsAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "ProcessSchedulerService stale-run reconcile failed"); }

            // Seed the built-in reliable schedules on boot so processes execute on their
            // own without requiring an admin to first create the row from the UI.
            try { await SeedBuiltInSchedulesAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "ProcessSchedulerService seed failed"); }

            var ticks = 0L;
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await TickAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "ProcessSchedulerService tick failed"); }

                // A boot-only reconcile leaves phantom 'running' rows forever in a
                // long-lived instance (e.g. the app was SIGKILLed mid-run). Re-run the
                // sweep hourly so run history and analytics stay truthful.
                if (++ticks % 60 == 0)
                {
                    try { await ReconcileStaleRunsAsync(stoppingToken); }
                    catch (Exception ex) { _logger.LogError(ex, "ProcessSchedulerService periodic stale-run reconcile failed"); }
                }

                try { await Task.Delay(TickInterval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        /// <summary>
        /// Registered handlers that ship with a default schedule. Every entry here is
        /// end-to-end reliable — no placeholder work, no external dependency the app
        /// can't verify at runtime. Add a new entry only after the handler is proven.
        /// </summary>
        private static readonly (string Key, string Name, int IntervalMinutes)[] BuiltInSchedules =
            new[]
            {
                // System hygiene — daily
                ("admin.purge-system-logs",            "Purge old system logs",             1440),
                ("admin.notifications-purge-read",     "Purge read notifications",          1440),
                ("admin.notifications-purge-stale-unread", "Purge stale unread notifications", 1440),
                ("admin.sync-changes-purge",           "Purge old sync changes",            1440),
                ("admin.sync-receipts-purge",          "Purge old sync receipts",           1440),
                ("admin.webhook-jobs-purge",           "Purge completed webhook jobs",      1440),
                ("admin.external-endpoint-logs-purge", "Purge external endpoint logs",      1440),
                ("admin.calendar-events-purge-past",   "Purge past calendar events",        1440),
                ("admin.dispatch-audit-purge",         "Purge old dispatch audit logs",     1440),
                ("admin.hr-audit-purge",               "Purge old HR audit logs",           1440),
                ("admin.recurring-task-logs-purge",    "Purge old recurring task logs",     1440),
                ("admin.soft-deleted-purge",           "Hard-purge soft-deleted records",   1440),
                ("admin.draft-offers-purge",           "Purge abandoned draft offers",      1440),
                ("admin.draft-invoices-purge",         "Purge abandoned draft invoices",    1440),

                // Business status — hourly
                ("admin.invoices-mark-overdue",              "Mark overdue invoices",              60),
                ("admin.offers-mark-expired",                "Expire past-due offers",             60),
                ("admin.payment-installments-mark-overdue",  "Mark overdue payment installments",  60),
                ("admin.dispatches-mark-missed",             "Mark missed dispatches",             60),
                ("admin.support-tickets-autoclose-resolved", "Auto-close resolved tickets",        360),

                // Email retries — frequent
                ("admin.retry-failed-emails",          "Retry failed outbound emails",       5),
            };

        /// <summary>
        /// Any ProcessRun left in 'running' state (crash / restart mid-run) is closed as
        /// failed so the UI's running-keys endpoint and the history tab tell the truth.
        /// Advisory locks are session-scoped, so they are already gone after a restart.
        /// </summary>
        private async Task ReconcileStaleRunsAsync(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            // Only close runs older than the hard handler timeout. In a multi-instance
            // deployment (protected by advisory locks) a rolling restart on instance B
            // must NOT mark instance A's still-executing run as failed.
            var staleCutoff = now - HandlerTimeout;
            var fixedUp = await db.Set<ProcessRun>()
                .Where(r => r.Status == "running" && r.FinishedAt == null && r.StartedAt < staleCutoff)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(r => r.Status, "failed")
                    .SetProperty(r => r.FinishedAt, now)
                    .SetProperty(r => r.Error, "Interrupted — the application restarted while this run was in progress"), ct);
            if (fixedUp > 0)
                _logger.LogWarning("⚙️  Reconciled {Count} interrupted process run(s) on boot", fixedUp);

            // Hard safety floor for run history. Trimming ProcessRuns used to happen
            // ONLY inside admin.purge-system-logs, so disabling/pausing/blocking that
            // one schedule let the table grow without bound. This sweep is decoupled
            // from any schedule row and keeps a generous 90-day window (well above the
            // 30-day minimum the purge handler enforces), so the two never fight.
            try
            {
                var historyCutoff = now.AddDays(-90);
                var trimmed = await db.Set<ProcessRun>()
                    .Where(r => r.StartedAt < historyCutoff)
                    .ExecuteDeleteAsync(ct);
                if (trimmed > 0)
                    _logger.LogInformation("⚙️  Trimmed {Count} process run(s) older than 90 days", trimmed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚙️  Process run-history safety trim failed");
            }
        }



        private async Task SeedBuiltInSchedulesAsync(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var registry = scope.ServiceProvider.GetRequiredService<ProcessHandlerRegistry>();

            // Adding a process means touching two independent lists: the DI handler
            // registration and BuiltInSchedules below. Forgetting the seed entry used to
            // fail silently (the process simply never self-scheduled), so warn loudly
            // about any drift between the two at boot.
            var seededKeys = BuiltInSchedules.Select(b => b.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var handlerKey in registry.Keys)
            {
                if (!seededKeys.Contains(handlerKey))
                    _logger.LogWarning(
                        "⚙️  Process handler '{Key}' is registered but has no BuiltInSchedules entry — it will never run automatically until an admin saves a schedule for it",
                        handlerKey);
            }

            foreach (var (key, name, interval) in BuiltInSchedules)
            {
                if (!registry.TryGet(key, out _))
                {
                    _logger.LogWarning(
                        "⚙️  Built-in schedule '{Key}' has no registered handler — skipping seed", key);
                    continue;
                }


                var existing = await db.Set<ProcessSchedule>().FirstOrDefaultAsync(s => s.Key == key, ct);
                if (existing == null)
                {
                    db.Set<ProcessSchedule>().Add(new ProcessSchedule
                    {
                        Key = key,
                        Name = name,
                        Enabled = true,
                        Paused = false,
                        IntervalMinutes = interval,
                        NextRunAt = DateTime.UtcNow.AddMinutes(1),
                    });
                    _logger.LogInformation("⚙️  Seeded built-in process schedule '{Key}' (every {Interval} min)", key, interval);
                }
                else if (existing.Enabled && !existing.Paused && existing.NextRunAt == null)
                {
                    // Repair a schedule that lost its NextRunAt (e.g. after a prior block).
                    existing.NextRunAt = DateTime.UtcNow.AddMinutes(1);
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(ct);
        }


        private async Task TickAsync(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var registry = scope.ServiceProvider.GetRequiredService<ProcessHandlerRegistry>();

            var now = DateTime.UtcNow;
            var due = await db.Set<ProcessSchedule>()
                .Where(s => s.Enabled && !s.Paused && s.NextRunAt != null && s.NextRunAt <= now)
                .ToListAsync(ct);

            // Handle "no handler registered" cases synchronously — they only touch
            // the schedule row and never fire user code.
            var runnable = new List<ProcessSchedule>();
            foreach (var s in due)
            {
                if (!registry.TryGet(s.Key, out _))
                {
                    s.LastStatus = "blocked";
                    s.LastRunAt = now;
                    s.BlockReason = "No handler registered for this key";
                    s.NextRunAt = now.AddMinutes(Math.Max(1, s.IntervalMinutes));
                    s.UpdatedAt = now;
                    // Audit it like any other outcome: without a run row the history
                    // panel stays empty forever while the row shows "blocked", and the
                    // operator has no timestamped trace of the missed slots.
                    db.Set<ProcessRun>().Add(new ProcessRun
                    {
                        ProcessKey = s.Key,
                        TriggeredBy = "schedule",
                        Attempt = 1,
                        Status = "blocked",
                        StartedAt = now,
                        FinishedAt = now,
                        DurationMs = 0,
                        ItemsProcessed = 0,
                        BlockReason = "No handler registered for this key",
                    });
                    await db.SaveChangesAsync(ct);
                    _logger.LogWarning(
                        "⚙️  Process '{Key}' is due but has no registered handler — skipping until {NextRun:o}",
                        s.Key, s.NextRunAt);
                    continue;
                }
                runnable.Add(s);
            }

            if (runnable.Count == 0) return;

            // Run due handlers with bounded concurrency. Sequential execution
            // meant one slow handler (up to the 10-minute HandlerTimeout) stalled
            // every other due schedule for the same window. Per-key advisory locks
            // still prevent duplicate execution across instances/triggers.
            //
            // Each handler creates its own DbContext scope (see ProcessDb.Resolve),
            // so we can also give ExecuteOnceAsync its own fresh scope here — the
            // outer `db` is only used for reading the due list.
            await Parallel.ForEachAsync(
                runnable,
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (s, token) =>
                {
                    try
                    {
                        using var runScope = _sp.CreateScope();
                        var runDb = runScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var runRegistry = runScope.ServiceProvider.GetRequiredService<ProcessHandlerRegistry>();
                        var running = runScope.ServiceProvider.GetRequiredService<RunningProcessRegistry>();
                        if (!runRegistry.TryGet(s.Key, out var handler)) return;

                        // Re-load the schedule on the run-scoped context so tracking
                        // stays attached to that connection (advisory lock is per session).
                        var scoped = await runDb.Set<ProcessSchedule>().FirstOrDefaultAsync(x => x.Id == s.Id, token);
                        if (scoped == null) return;
                        var attempt = Math.Max(1, scoped.ConsecutiveFailures + 1);
                        var result = await ExecuteOnceAsync(runDb, scoped, handler, "schedule", attempt, token, running, _logger);
                        // Every scheduled outcome is logged, not just crashes, so
                        // operators can trace successes/blocks in the server log
                        // alongside the persisted ProcessRun row.
                        if (string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation(
                                "⚙️  Process '{Key}' succeeded in {Duration}ms (attempt {Attempt})",
                                s.Key, result.DurationMs, attempt);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "⚙️  Process '{Key}' finished with status '{Status}' in {Duration}ms (attempt {Attempt}): {Detail}",
                                s.Key, result.Status, result.DurationMs, attempt,
                                result.Error ?? result.BlockReason ?? "no detail");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ProcessSchedulerService: handler '{Key}' threw outside ExecuteOnceAsync", s.Key);
                    }
                });
        }


        /// <summary>Stable 64-bit hash for Postgres pg_advisory_lock keys.</summary>
        private static long AdvisoryLockKey(string key)
        {
            // FNV-1a 64-bit — deterministic, no allocations.
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (var c in key)
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
                return (long)hash;
            }
        }

        /// <summary>
        /// A Postgres advisory lock held on its OWN dedicated connection.
        ///
        /// It must NOT ride on the EF DbContext connection: ApplicationDbContext is
        /// configured with EnableRetryOnFailure, and EF's connection resiliency may
        /// transparently reconnect on a transient error during SaveChangesAsync.
        /// Advisory locks are *session*-scoped, so a silent reconnect would drop the
        /// lock while this code still believes it holds it — defeating the "never run
        /// the same handler twice at once" guarantee. A dedicated connection that we
        /// open and close ourselves has a session lifetime we fully control.
        /// </summary>
        private sealed class AdvisoryLock : IAsyncDisposable
        {
            private readonly Npgsql.NpgsqlConnection _conn;
            private readonly long _lockId;

            private AdvisoryLock(Npgsql.NpgsqlConnection conn, long lockId)
            {
                _conn = conn; _lockId = lockId;
            }

            /// <summary>Returns null when the lock is already held by someone else.</summary>
            public static async Task<AdvisoryLock?> TryAcquireAsync(ApplicationDbContext db, long lockId, CancellationToken ct)
            {
                var connectionString = db.Database.GetConnectionString();
                // An unresolvable connection string is NOT contention. Returning null
                // here made every run report "skipped — another execution in progress"
                // forever, with no error anywhere. Fail loudly instead so the run is
                // recorded as failed and the retry ladder / block reason kick in.
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException(
                        "Cannot acquire the process advisory lock: no database connection string is available.");


                var conn = new Npgsql.NpgsqlConnection(connectionString);
                try
                {
                    await conn.OpenAsync(ct);
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT pg_try_advisory_lock(@k)";
                    cmd.Parameters.AddWithValue("@k", lockId);
                    var result = await cmd.ExecuteScalarAsync(ct);
                    if (result is bool b && b) return new AdvisoryLock(conn, lockId);
                }
                catch
                {
                    await conn.DisposeAsync();
                    throw;
                }

                await conn.DisposeAsync();
                return null;
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "SELECT pg_advisory_unlock(@k)";
                    cmd.Parameters.AddWithValue("@k", _lockId);
                    await cmd.ExecuteScalarAsync();
                }
                catch
                {
                    // Unlock failed — closing the connection ends the session, which
                    // releases every advisory lock it held anyway.
                }
                finally
                {
                    await _conn.DisposeAsync();
                }
            }
        }



        /// <summary>
        /// Runs a handler and persists both the ProcessRun row and the schedule state.
        /// Public so ProcessesController can reuse it for "Run now".
        /// </summary>
        public static async Task<DTOs.RunNowResult> ExecuteOnceAsync(
            ApplicationDbContext db,
            ProcessSchedule s,
            IProcessHandler handler,
            string triggeredBy,
            int attempt,
            CancellationToken ct,
            RunningProcessRegistry? running = null,
            // Static method, so it has no instance _logger: callers pass their own
            // logger to keep lock-contention outcomes visible in the server log.
            ILogger? logger = null)
        {
            // Prevent duplicate execution across scheduler ticks, manual "Run now",
            // and multiple app instances all pointing at the same database.
            var lockId = AdvisoryLockKey(s.Key);
            AdvisoryLock? advisoryLock;
            try
            {
                advisoryLock = await AdvisoryLock.TryAcquireAsync(db, lockId, ct);
            }
            catch (Exception lockEx)
            {
                // Could not even attempt the lock (bad/absent connection string, DB
                // unreachable). Record it as a real failure with the real reason —
                // never as a silent "skipped".
                var failAt = DateTime.UtcNow;
                var failReason = Truncate("Could not acquire the execution lock: " + lockEx.Message);
                try
                {
                    db.Set<ProcessRun>().Add(new ProcessRun
                    {
                        ProcessKey = s.Key,
                        TriggeredBy = triggeredBy,
                        Attempt = attempt,
                        Status = "failed",
                        StartedAt = failAt,
                        FinishedAt = failAt,
                        DurationMs = 0,
                        ItemsProcessed = 0,
                        Error = lockEx.Message,
                        BlockReason = failReason,
                    });
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception auditEx)
                {
                    logger?.LogWarning(auditEx, "⚙️  Process '{Key}': failed to persist lock-failure audit row", s.Key);
                }
                logger?.LogError(lockEx, "⚙️  Process '{Key}' could not acquire its execution lock", s.Key);
                return new DTOs.RunNowResult
                {
                    Status = "failed",
                    Error = lockEx.Message,
                    BlockReason = failReason,
                };
            }

            if (advisoryLock == null)
            {
                // Contention is a real outcome, not a silent no-op: persist it so the
                // history explains why a "Run now" click produced nothing, and log it.
                var busyAt = DateTime.UtcNow;
                const string busyReason = "Another execution of this process is already in progress";
                try
                {
                    db.Set<ProcessRun>().Add(new ProcessRun
                    {
                        ProcessKey = s.Key,
                        TriggeredBy = triggeredBy,
                        Attempt = attempt,
                        Status = "skipped",
                        StartedAt = busyAt,
                        FinishedAt = busyAt,
                        DurationMs = 0,
                        ItemsProcessed = 0,
                        BlockReason = busyReason,
                    });
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception auditEx)
                {
                    logger?.LogWarning(auditEx, "⚙️  Process '{Key}': failed to persist lock-contention audit row", s.Key);
                }
                logger?.LogWarning(
                    "⚙️  Process '{Key}' skipped ({Trigger}): {Reason}", s.Key, triggeredBy, busyReason);
                return new DTOs.RunNowResult
                {
                    Status = "skipped",
                    BlockReason = busyReason,
                };
            }

            try
            {
                var run = new ProcessRun
                {
                    ProcessKey = s.Key,
                    TriggeredBy = triggeredBy,
                    Attempt = attempt,
                    Status = "running",
                    StartedAt = DateTime.UtcNow,
                };
                db.Set<ProcessRun>().Add(run);
                await db.SaveChangesAsync(ct);


                var sw = Stopwatch.StartNew();
                DTOs.RunNowResult result;
                // Hard cap: a hung handler must never block the scheduler loop or
                // an HTTP "Run now" request. The registry adds cooperative cancel:
                // an operator clicking Stop cancels the same linked token so the
                // handler aborts at its next await point.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(HandlerTimeout);
                using var stopReg = running?.Register(s.Key, timeoutCts.Token);
                var runToken = stopReg?.Token ?? timeoutCts.Token;
                try
                {
                    result = await handler.ExecuteAsync(s.ConfigJson ?? "{}", runToken);
                    if (string.IsNullOrEmpty(result.Status)) result.Status = "success";
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    result = new DTOs.RunNowResult
                    {
                        Status = "failed",
                        Error = $"Timed out after {HandlerTimeout.TotalMinutes:0} minutes",
                    };
                }
                catch (OperationCanceledException) when (runToken.IsCancellationRequested && !ct.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
                {
                    // Operator-requested stop via RunningProcessRegistry.RequestStop.
                    result = new DTOs.RunNowResult
                    {
                        Status = "cancelled",
                        Error = "Stopped by operator",
                    };
                }
                catch (Exception ex)
                {
                    result = new DTOs.RunNowResult { Status = "failed", Error = ex.Message };
                }
                sw.Stop();


                run.FinishedAt = DateTime.UtcNow;
                run.DurationMs = (int)sw.ElapsedMilliseconds;
                run.Status = result.Status;
                run.Error = result.Error;
                run.BlockReason = Truncate(result.BlockReason); // column is capped at 500 chars
                run.ItemsProcessed = result.ItemsProcessed;
                run.OutputJson = result.Output != null ? System.Text.Json.JsonSerializer.Serialize(result.Output) : null;

                result.DurationMs = run.DurationMs ?? 0;

                // Schedule state transition.
                // "Last run" is a factual audit field: a manual run IS the most
                // recent execution, so it is always recorded. Without this, a
                // manual "Run now" left no trace on the row and the page looked
                // unchanged after a reload, as if nothing had persisted.
                // What manual runs still must NOT touch is the retry ladder
                // (ConsecutiveFailures / NextRunAt / BlockReason) — those belong
                // to the scheduled cadence.
                var isManual = triggeredBy == "manual";
                s.LastRunAt = run.FinishedAt;
                s.LastStatus = result.Status;
                s.UpdatedAt = DateTime.UtcNow;

                if (result.Status == "success" || result.Status == "skipped" || result.Status == "cancelled")
                {
                    if (!isManual)
                    {
                        // A cancelled run is not a failure — the operator stopped it
                        // on purpose. Reset the ladder and reschedule normally so the
                        // process keeps running on its cadence.
                        s.ConsecutiveFailures = 0;
                        // A "skipped" run can still carry a block signal (e.g. a purge
                        // handler that hit a foreign-key constraint). Keep that reason on
                        // the schedule row — the UI's diagnostics card reads block_reason
                        // and would otherwise show a permanently green "Not blocked" for a
                        // process that in fact never deletes anything.
                        s.BlockReason = string.IsNullOrWhiteSpace(result.BlockReason)
                            ? null
                            : Truncate(result.BlockReason);
                        s.NextRunAt = DateTime.UtcNow.AddMinutes(Math.Max(1, s.IntervalMinutes));
                    }
                    else if (result.Status == "success")
                    {
                        // A manual run that actually succeeded proves the previous
                        // block is gone. Clearing it (and the failure counter) keeps
                        // the row from showing a stale "blocked" badge with an old
                        // reason until the next scheduled tick. The cadence fields
                        // (NextRunAt) stay untouched — manual runs never reschedule.
                        s.BlockReason = string.IsNullOrWhiteSpace(result.BlockReason)
                            ? null
                            : Truncate(result.BlockReason);
                        s.ConsecutiveFailures = 0;
                    }
                }
                else if (result.Status == "blocked")
                {
                    if (!isManual)
                    {
                        s.BlockReason = Truncate(result.BlockReason ?? "Handler reported blocked");
                        s.NextRunAt = DateTime.UtcNow.AddMinutes(Math.Max(1, s.IntervalMinutes));
                    }
                }
                else if (!isManual) // failed, scheduled run
                {
                    s.ConsecutiveFailures = s.ConsecutiveFailures + 1;
                    if (attempt < Math.Max(1, s.MaxRetries))
                    {
                        // Clamp the exponent (and the resulting delay) so a long failure
                        // streak can never overflow or push the next run years away.
                        var exponent = Math.Min(attempt - 1, 10);
                        var backoffSec = Math.Min(
                            (long)Math.Max(1, s.RetryBackoffSeconds) * (long)Math.Pow(2, exponent),
                            86_400L);
                        var retryAt = DateTime.UtcNow.AddSeconds(backoffSec);
                        run.NextRetryAt = retryAt;
                        s.NextRunAt = retryAt; // scheduler will pick it up on the next tick past retryAt
                    }
                    else
                    {
                        // Retry ladder exhausted. Processes must KEEP RUNNING — never
                        // self-pause, otherwise a transient outage silently stops
                        // automation until someone notices. Instead: surface the reason,
                        // reset the ladder, and cool down until the next normal slot
                        // (at least 15 minutes) so we don't hot-loop on a hard failure.
                        s.BlockReason = Truncate($"Failed after {attempt} attempts: {result.Error}");
                        s.ConsecutiveFailures = 0;
                        var cooldown = Math.Max(15, Math.Max(1, s.IntervalMinutes));
                        s.NextRunAt = DateTime.UtcNow.AddMinutes(cooldown);
                        run.NextRetryAt = s.NextRunAt;
                    }
                }
                // manual + failed → surfaced to the operator via the response, no schedule mutation.

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception saveEx)
                {
                    // Never leave the run row stuck as 'running'. Persist a minimal
                    // closing update with raw SQL so the UI and history stay accurate.
                    try
                    {
                        var finishedAt = DateTime.UtcNow;
                        await db.Set<ProcessRun>()
                            .Where(r => r.Id == run.Id)
                            .ExecuteUpdateAsync(u => u
                                .SetProperty(r => r.Status, "failed")
                                .SetProperty(r => r.FinishedAt, finishedAt)
                                .SetProperty(r => r.Error, "State persist failed: " + saveEx.Message), ct);
                    }
                    catch { /* best effort — boot reconcile will close it */ }

                    // Also persist the schedule mutations we made above (NextRunAt,
                    // ConsecutiveFailures, BlockReason, LastStatus) via raw SQL so a
                    // tracked-save failure doesn't drop the retry ladder / cooldown.
                    try
                    {
                        var nextRunAt = s.NextRunAt;
                        var lastRunAt = s.LastRunAt;
                        var lastStatus = s.LastStatus;
                        var consecutive = s.ConsecutiveFailures;
                        var blockReason = s.BlockReason;
                        var updatedAt = DateTime.UtcNow;
                        await db.Set<ProcessSchedule>()
                            .Where(x => x.Id == s.Id)
                            .ExecuteUpdateAsync(u => u
                                .SetProperty(x => x.NextRunAt, nextRunAt)
                                .SetProperty(x => x.LastRunAt, lastRunAt)
                                .SetProperty(x => x.LastStatus, lastStatus)
                                .SetProperty(x => x.ConsecutiveFailures, consecutive)
                                .SetProperty(x => x.BlockReason, blockReason)
                                .SetProperty(x => x.UpdatedAt, updatedAt), ct);
                    }
                    catch { /* best effort */ }

                    result.Status = "failed";
                    result.Error ??= saveEx.Message;
                }
                return result;
            }
            finally
            {
                await advisoryLock.DisposeAsync();
            }
        }

    }
}
