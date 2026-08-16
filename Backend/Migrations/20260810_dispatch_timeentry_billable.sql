-- Dispatch time entries accepted a "billable" flag through the API but had nowhere
-- to store it, so the technician's choice was silently dropped and every dispatch
-- time entry was billed unconditionally. Add the column (default true so existing
-- rows keep their current, effectively-billable behaviour).
ALTER TABLE "TimeEntries" ADD COLUMN IF NOT EXISTS "Billable" boolean NOT NULL DEFAULT true;
