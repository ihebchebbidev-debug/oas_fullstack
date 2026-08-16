-- Workflow engine integrity + hot-path indexes.
-- Mirrors the idempotent block executed at startup in Program.cs.
--
--   1. WorkflowProcessedEntities must hold at most one row per
--      (tenant, trigger, entity type, entity id, processed status).
--      WorkflowPollingService checks "already processed?" with a read and then
--      inserts — a classic TOCTOU window. Two concurrent polling passes (or a
--      poll racing a webhook) both pass the read and fire the workflow twice.
--      The unique index turns the insert into the authoritative claim; the
--      service catches the conflict and skips.
--
--   2. Executions/triggers lookups (list by workflow, poll by entity type,
--      cleanup by status) had no supporting index at all.

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WorkflowProcessedEntities') THEN
        -- Collapse pre-existing duplicates (keep the earliest claim)
        DELETE FROM "WorkflowProcessedEntities" a
        USING "WorkflowProcessedEntities" b
        WHERE a."Id" > b."Id"
          AND a."TenantId" = b."TenantId"
          AND a."TriggerId" = b."TriggerId"
          AND a."EntityType" = b."EntityType"
          AND a."EntityId" = b."EntityId"
          AND a."ProcessedStatus" IS NOT DISTINCT FROM b."ProcessedStatus";
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkflowProcessedEntities_Dedupe"
            ON "WorkflowProcessedEntities" ("TenantId", "TriggerId", "EntityType", "EntityId", "ProcessedStatus");
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WorkflowExecutions') THEN
        CREATE INDEX IF NOT EXISTS "IX_WorkflowExecutions_Tenant_Workflow_StartedAt"
            ON "WorkflowExecutions" ("TenantId", "WorkflowId", "StartedAt" DESC);
        CREATE INDEX IF NOT EXISTS "IX_WorkflowExecutions_Tenant_Status"
            ON "WorkflowExecutions" ("TenantId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_WorkflowExecutions_Tenant_Entity"
            ON "WorkflowExecutions" ("TenantId", "TriggerEntityType", "TriggerEntityId");
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WorkflowTriggers') THEN
        CREATE INDEX IF NOT EXISTS "IX_WorkflowTriggers_Tenant_Entity_Active"
            ON "WorkflowTriggers" ("TenantId", "EntityType", "IsActive");
    END IF;
END $$;
