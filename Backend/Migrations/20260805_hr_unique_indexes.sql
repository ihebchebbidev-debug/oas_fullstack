-- HR module integrity indexes.
-- Mirrors the idempotent block executed at startup in Program.cs.
--   1. hr_attendance must hold at most one row per (tenant, employee, day):
--      duplicates double-count hours/overtime in payroll generation and make
--      HrService.UpsertAttendanceAsync's concurrent-insert recovery a no-op.
--   2. hr_payroll_runs must hold at most one run per (tenant, year, month):
--      the check-then-insert guard in HrService needs a DB constraint to be
--      race-safe.

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'hr_attendance') THEN
        DELETE FROM hr_attendance a
        USING hr_attendance b
        WHERE a.id < b.id
          AND a."TenantId" = b."TenantId"
          AND a.user_id = b.user_id
          AND a.date = b.date;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_hr_attendance_tenant_user_date
            ON hr_attendance ("TenantId", user_id, date);
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'hr_payroll_runs') THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ux_hr_payroll_runs_tenant_year_month
            ON hr_payroll_runs ("TenantId", year, month);
    END IF;
END $$;
