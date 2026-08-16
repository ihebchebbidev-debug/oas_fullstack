-- The service order status 'scheduled' was a duplicate of 'planned' and has been removed
-- from the status model (src/config/entity-statuses/service-order.config.ts).
-- Migrate any legacy rows so they keep rendering with a known status.
UPDATE "ServiceOrders" SET "Status" = 'planned' WHERE "Status" = 'scheduled';

-- Workflow triggers that fired on the removed status now fire on 'planned'.
UPDATE "WorkflowTriggers" SET "ToStatus" = 'planned'
WHERE "EntityType" = 'service_order' AND "ToStatus" = 'scheduled';

UPDATE "WorkflowTriggers" SET "FromStatus" = 'planned'
WHERE "EntityType" = 'service_order' AND "FromStatus" = 'scheduled';
