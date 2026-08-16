-- =====================================================================
-- HR • Leave balances demo/seed data  —  DEFAULT TENANT (TenantId = 0)
-- =====================================================================
-- What it does
--   1. Ensures tenant 0 has leave-type lookups ("leave-type"):
--      annual, sick, unpaid, maternity, marriage, bereavement.
--   2. Creates one hr_leave_balances row per (active user, year, leave type)
--      for the previous, current and next year.
--   3. Creates realistic user_leaves rows (approved / pending / rejected)
--      so that GetLeaveBalancesAsync computes non-zero Used / Pending
--      (used & pending are DERIVED from user_leaves, not stored).
--   4. Back-fills the stored used/pending/remaining columns to match the
--      derived values, so raw SQL reports agree with the API.
--
-- Idempotent: re-running deletes the previously seeded demo leaves
-- (reason LIKE '[DEMO]%') and upserts balances. Safe to run repeatedly.
--
--   psql -f Backend/Migrations/seed_hr_leave_balances_tenant0.sql
-- =====================================================================

DO $$
DECLARE
    v_tenant   INT  := 0;
    v_year     INT  := EXTRACT(YEAR FROM CURRENT_DATE)::INT;
    v_users    INT;
    v_balances INT;
    v_leaves   INT;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'hr_leave_balances') THEN
        RAISE NOTICE 'hr_leave_balances does not exist — nothing to seed.';
        RETURN;
    END IF;

    -- ------------------------------------------------------------------
    -- 1. Leave-type lookups for tenant 0
    -- ------------------------------------------------------------------
    INSERT INTO "LookupItems" ("TenantId","LookupType","Name","Description","Color",
                               "IsActive","SortOrder","CreatedUser","CreatedAt",
                               "IsDeleted","IsDefault","IsPaid")
    SELECT v_tenant, 'leave-type', t.name, t.descr, t.color,
           TRUE, t.ord, 'system', NOW(), FALSE, (t.ord = 1), t.paid
    FROM (VALUES
            ('annual',      'Congé annuel payé',        '#3b82f6', 1, TRUE),
            ('sick',        'Congé maladie',            '#ef4444', 2, TRUE),
            ('unpaid',      'Congé sans solde',         '#6b7280', 3, FALSE),
            ('maternity',   'Congé maternité',          '#ec4899', 4, TRUE),
            ('marriage',    'Congé mariage',            '#a855f7', 5, TRUE),
            ('bereavement', 'Congé décès',              '#0ea5e9', 6, TRUE)
         ) AS t(name, descr, color, ord, paid)
    WHERE NOT EXISTS (
        SELECT 1 FROM "LookupItems" l
        WHERE l."TenantId" = v_tenant AND l."LookupType" = 'leave-type'
          AND lower(l."Name") = t.name AND l."IsDeleted" = FALSE);

    -- ------------------------------------------------------------------
    -- 2. Balances: every active tenant-0 user × 6 leave types × 3 years
    --    Allowances follow Tunisian practice (annual grows with seniority).
    -- ------------------------------------------------------------------
    CREATE TEMP TABLE _seed_users ON COMMIT DROP AS
    SELECT u."Id" AS user_id,
           (row_number() OVER (ORDER BY u."Id"))::INT AS rn
    FROM "Users" u
    WHERE u."TenantId" = v_tenant
      AND COALESCE(u."IsActive", TRUE) = TRUE
      AND COALESCE(u."IsDeleted", FALSE) = FALSE;

    SELECT count(*) INTO v_users FROM _seed_users;
    IF v_users = 0 THEN
        RAISE NOTICE 'No active users in tenant 0 — nothing to seed.';
        RETURN;
    END IF;

    CREATE TEMP TABLE _seed_types ON COMMIT DROP AS
    SELECT * FROM (VALUES
        ('annual',      21.0),
        ('sick',        15.0),
        ('unpaid',      10.0),
        ('maternity',   30.0),
        ('marriage',     3.0),
        ('bereavement',  3.0)
    ) AS t(leave_type, base_allowance);

    -- Remove previously seeded balances for the 3-year window so allowances
    -- stay deterministic across re-runs (keeps rows outside the window).
    DELETE FROM hr_leave_balances
    WHERE "TenantId" = v_tenant AND year BETWEEN v_year - 1 AND v_year + 1;

    INSERT INTO hr_leave_balances
        ("TenantId", user_id, year, leave_type, annual_allowance, used, pending, remaining, created_at, updated_at)
    SELECT v_tenant,
           u.user_id,
           y.year,
           t.leave_type,
           CASE WHEN t.leave_type = 'annual'
                THEN t.base_allowance + LEAST((u.rn % 5) * 1.0, 4.0)  -- 21..25 j
                ELSE t.base_allowance END,
           0, 0,
           CASE WHEN t.leave_type = 'annual'
                THEN t.base_allowance + LEAST((u.rn % 5) * 1.0, 4.0)
                ELSE t.base_allowance END,
           NOW(), NOW()
    FROM _seed_users u
    CROSS JOIN _seed_types t
    CROSS JOIN (SELECT generate_series(v_year - 1, v_year + 1) AS year) y;

    GET DIAGNOSTICS v_balances = ROW_COUNT;

    -- ------------------------------------------------------------------
    -- 3. Actual leave requests (drive Used / Pending in the API)
    -- ------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'user_leaves') THEN

        DELETE FROM user_leaves
        WHERE "TenantId" = v_tenant AND reason LIKE '[DEMO]%';

        -- a) Approved annual leave: 5-day block for every user (current year)
        INSERT INTO user_leaves ("TenantId", user_id, leave_type, start_date, end_date,
                                 status, reason, approved_by, approved_at, created_at, updated_at)
        SELECT v_tenant, u.user_id, 'annual',
               make_date(v_year, 1 + (u.rn % 9), 3)::timestamptz,
               (make_date(v_year, 1 + (u.rn % 9), 3) + INTERVAL '4 days')::timestamptz,
               'approved', '[DEMO] Congé annuel', u.user_id,
               NOW() - INTERVAL '30 days', NOW(), NOW()
        FROM _seed_users u;

        -- b) Approved sick leave: 2 days, every 2nd user
        INSERT INTO user_leaves ("TenantId", user_id, leave_type, start_date, end_date,
                                 status, reason, approved_by, approved_at, created_at, updated_at)
        SELECT v_tenant, u.user_id, 'sick',
               make_date(v_year, 2 + (u.rn % 8), 12)::timestamptz,
               (make_date(v_year, 2 + (u.rn % 8), 12) + INTERVAL '1 day')::timestamptz,
               'approved', '[DEMO] Arrêt maladie', u.user_id,
               NOW() - INTERVAL '20 days', NOW(), NOW()
        FROM _seed_users u WHERE u.rn % 2 = 0;

        -- c) Pending annual request: 3 days, every 3rd user (future dated)
        INSERT INTO user_leaves ("TenantId", user_id, leave_type, start_date, end_date,
                                 status, reason, created_at, updated_at)
        SELECT v_tenant, u.user_id, 'annual',
               (CURRENT_DATE + ((u.rn % 20) + 10) * INTERVAL '1 day')::timestamptz,
               (CURRENT_DATE + ((u.rn % 20) + 12) * INTERVAL '1 day')::timestamptz,
               'pending', '[DEMO] Demande en attente', NOW(), NOW()
        FROM _seed_users u WHERE u.rn % 3 = 0;

        -- d) Pending unpaid request, every 5th user
        INSERT INTO user_leaves ("TenantId", user_id, leave_type, start_date, end_date,
                                 status, reason, created_at, updated_at)
        SELECT v_tenant, u.user_id, 'unpaid',
               (CURRENT_DATE + ((u.rn % 15) + 20) * INTERVAL '1 day')::timestamptz,
               (CURRENT_DATE + ((u.rn % 15) + 21) * INTERVAL '1 day')::timestamptz,
               'pending', '[DEMO] Congé sans solde', NOW(), NOW()
        FROM _seed_users u WHERE u.rn % 5 = 0;

        -- e) Rejected request (must NOT affect balances), every 7th user
        INSERT INTO user_leaves ("TenantId", user_id, leave_type, start_date, end_date,
                                 status, reason, created_at, updated_at)
        SELECT v_tenant, u.user_id, 'annual',
               make_date(v_year, 12, 20)::timestamptz,
               make_date(v_year, 12, 27)::timestamptz,
               'rejected', '[DEMO] Refusé (pic d''activité)', NOW(), NOW()
        FROM _seed_users u WHERE u.rn % 7 = 0;

        -- f) Last-year history: 10 approved annual days per user
        INSERT INTO user_leaves ("TenantId", user_id, leave_type, start_date, end_date,
                                 status, reason, approved_by, approved_at, created_at, updated_at)
        SELECT v_tenant, u.user_id, 'annual',
               make_date(v_year - 1, 7, 1)::timestamptz,
               make_date(v_year - 1, 7, 10)::timestamptz,
               'approved', '[DEMO] Congé été (N-1)', u.user_id,
               NOW() - INTERVAL '300 days', NOW(), NOW()
        FROM _seed_users u;

        SELECT count(*) INTO v_leaves
        FROM user_leaves WHERE "TenantId" = v_tenant AND reason LIKE '[DEMO]%';

        -- --------------------------------------------------------------
        -- 4. Sync stored used / pending / remaining with derived values
        -- --------------------------------------------------------------
        UPDATE hr_leave_balances b
        SET used      = agg.used,
            pending   = agg.pending,
            remaining = b.annual_allowance - agg.used - agg.pending,
            updated_at = NOW()
        FROM (
            SELECT l."TenantId", l.user_id, l.leave_type,
                   EXTRACT(YEAR FROM l.start_date)::INT AS year,
                   SUM(CASE WHEN l.status = 'approved'
                            THEN (l.end_date::date - l.start_date::date) + 1 ELSE 0 END)::numeric AS used,
                   SUM(CASE WHEN l.status = 'pending'
                            THEN (l.end_date::date - l.start_date::date) + 1 ELSE 0 END)::numeric AS pending
            FROM user_leaves l
            WHERE l."TenantId" = v_tenant
            GROUP BY l."TenantId", l.user_id, l.leave_type, EXTRACT(YEAR FROM l.start_date)
        ) agg
        WHERE b."TenantId" = agg."TenantId"
          AND b.user_id   = agg.user_id
          AND b.leave_type = agg.leave_type
          AND b.year       = agg.year;
    END IF;

    RAISE NOTICE 'HR leave seed done — tenant %, % users, % balance rows (% .. %), % demo leaves.',
        v_tenant, v_users, v_balances, v_year - 1, v_year + 1, COALESCE(v_leaves, 0);
END $$;

-- Quick verification
-- SELECT year, leave_type, count(*), sum(annual_allowance), sum(used), sum(pending), sum(remaining)
-- FROM hr_leave_balances WHERE "TenantId" = 0 GROUP BY 1,2 ORDER BY 1,2;
