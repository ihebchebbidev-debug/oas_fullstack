-- Allow unlimited dispatches per job.
-- Legacy partial unique indexes restricted a job to a single active dispatch,
-- causing 23505 "duplicate key value violates unique constraint
-- UX_DispatchJobs_Job_Active" or "UX_Dispatches_JobId_Active" when planning again.
DROP INDEX IF EXISTS "UX_DispatchJobs_Job_Active";
DROP INDEX IF EXISTS "UX_Dispatches_JobId_Active";
