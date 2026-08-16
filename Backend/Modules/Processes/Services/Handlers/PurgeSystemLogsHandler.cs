using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Processes.DTOs;

namespace MyApi.Modules.Processes.Services.Handlers
{
    /// <summary>
    /// Deletes SystemLogs older than <c>retention_days</c> (default 30).
    /// Also trims ProcessRuns history using the same window.
    /// </summary>
    public class PurgeSystemLogsHandler : IProcessHandler
    {
        public string Key => "admin.purge-system-logs";

        private readonly IServiceProvider _sp;
        public PurgeSystemLogsHandler(IServiceProvider sp) { _sp = sp; }

        public async Task<RunNowResult> ExecuteAsync(string configJson, CancellationToken ct)
        {
            // Defaults + clamps live in ProcessConfigSchemas — the schema also
            // drives the API contract, the frontend labels and the tests.
            int retentionDays = ProcessConfigSchemas.GetInt(Key, configJson, "retention_days");
            int runRetentionDays = ProcessConfigSchemas.GetInt(Key, configJson, "run_retention_days");

            var logCutoff = DateTime.UtcNow.AddDays(-retentionDays);
            // Floor process-run history at 30 days so shortening system-log retention
            // never silently truncates the audit trail the Processes UI depends on.
            var effectiveRunRetention = Math.Max(30, runRetentionDays);
            var runCutoff = DateTime.UtcNow.AddDays(-effectiveRunRetention);

            using var scope = _sp.CreateScope();
            var db = ProcessDb.Resolve(scope); // view-all: purge logs for every tenant

            var logsDeleted = await db.SystemLogs.Where(l => l.Timestamp < logCutoff).ExecuteDeleteAsync(ct);
            var runsDeleted = await db.Set<Models.ProcessRun>()
                .Where(r => r.StartedAt < runCutoff)
                .ExecuteDeleteAsync(ct);

            return new RunNowResult
            {
                Status = "success",
                ItemsProcessed = logsDeleted + runsDeleted,
                Output = new { retention_days = retentionDays, run_retention_days = effectiveRunRetention, logs_deleted = logsDeleted, runs_deleted = runsDeleted },
            };
        }
    }
}
