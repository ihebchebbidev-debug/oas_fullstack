# Processes module — verified table map (20 processes)

Extracted directly from the handler source, not from docs. Run
`Backend/Migrations/Processes_Required_Schema_Verify.sql` — PART 2 must return zero rows.

## Module-owned tables (created by this module)

| Table | Purpose |
|---|---|
| `ProcessSchedules` | one row per process key: enabled/paused, interval, retry ladder, config, next/last run, last status, consecutive failures, block reason |
| `ProcessRuns` | one row per execution attempt (schedule / manual / retry) — this is the History tab |

Everything else below is owned by its own module migration; Processes only reads/updates/deletes.

## Process → table map

| # | Process key | Tables touched | Key columns |
|---|---|---|---|
| 1 | `admin.invoices-mark-overdue` | `Invoices` | Status, DueDate, AmountPaid, GrandTotal, IsDeleted, UpdatedAt |
| 2 | `admin.offers-mark-expired` | `Offers` | Status, ValidUntil, IsDeleted, UpdatedAt |
| 3 | `admin.dispatches-mark-missed` | `Dispatches` | Status, ScheduledDate, ActualStartTime, IsDeleted, ModifiedDate |
| 4 | `admin.payment-installments-mark-overdue` | `payment_plan_installments` | status, due_date *(snake_case)* |
| 5 | `admin.support-tickets-autoclose-resolved` | `SupportTickets` | Status, LastOccurredAt, CreatedAt |
| 6 | `admin.draft-offers-purge` | `Offers` (+ FK children) | Status, UpdatedAt/ModifiedDate/CreatedDate |
| 7 | `admin.draft-invoices-purge` | `Invoices` (+ `InvoiceLines`, `InvoiceActivities` via FK) | Status, UpdatedAt/CreatedAt |
| 8 | `admin.notifications-purge-read` | `Notifications` | IsRead, CreatedAt |
| 9 | `admin.notifications-purge-stale-unread` | `Notifications` | IsRead, CreatedAt |
| 10 | `admin.calendar-events-purge-past` | `calendar_events` | "End", "Status" *(snake table, PascalCase cols)* |
| 11 | `admin.sync-changes-purge` | `sync_changes` | ChangedAt |
| 12 | `admin.sync-receipts-purge` | `sync_operation_receipts` | CreatedAt |
| 13 | `admin.webhook-jobs-purge` | `WebhookForwardJobs` | Status, CompletedAt |
| 14 | `admin.external-endpoint-logs-purge` | `ExternalEndpoints`, `ExternalEndpointLogs` | IsDeleted, LogRetentionDays / EndpointId, ReceivedAt |
| 15 | `admin.dispatch-audit-purge` | `DispatchAuditLogs` | CreatedAt |
| 16 | `admin.hr-audit-purge` | `hr_audit_logs` | created_at *(snake_case)* |
| 17 | `admin.soft-deleted-purge` | `Invoices`, `Offers`, `Deals`, `Sales`, `Articles`, `Dispatches`, `ServiceOrders` | IsDeleted, DeletedAt on each |
| 18 | `admin.recurring-task-logs-purge` | `RecurringTaskLogs` | GeneratedDate |
| 19 | `admin.purge-system-logs` | `SystemLogs`, `ProcessRuns` | Timestamp / StartedAt |
| 20 | `admin.retry-failed-emails` | `OutboundEmailLogs`, `ConnectedEmailAccounts` | Status, Attempts, MaxAttempts, NextRetryAt, LastAttemptAt, LastError, AccountId, UserId, TenantId, CreatedAt |

## Cross-cutting requirements

- **`SystemLogs`** — every run outcome (success = info, failed/blocked/skipped = warning) is written here by the scheduler, on top of the `ProcessRuns` row. Required for all 20.
- **Tenant scope** — handlers set the view-all sentinel (`SetTenantId(-1)`) and mutate via `ExecuteUpdateAsync`/`ExecuteDeleteAsync`, so tables need no per-tenant setup, but tenant-scoped tables must have their `TenantId` column present.
- **Advisory locks** — the scheduler takes a Postgres advisory lock on a dedicated connection; no table needed, but the DB user must be allowed `pg_try_advisory_lock`.
- **Grants** — if role separation (`app_user`) is enabled, PART 3 of the verify script grants SELECT/INSERT/UPDATE/DELETE on all 22 tables above.

## Adding a new process (checklist)

1. Add an `IProcessHandler` implementation + register it in `Program.cs`.
2. Add a `BuiltInSchedules` entry with the same key.
3. Add the key to `REAL_HANDLER_KEYS` in `src/modules/system/services/processesService.ts` and to `PROCESSES` in `processesCatalog.ts`.
4. Add `items.<key>.name` / `.description` to `processes.en.json` and `processes.fr.json`.
5. Add the new table/column rows to PART 2 of the verify script.

The catalog integrity test (`processesCatalog.test.ts`) fails loudly if steps 2–4 are missed.
