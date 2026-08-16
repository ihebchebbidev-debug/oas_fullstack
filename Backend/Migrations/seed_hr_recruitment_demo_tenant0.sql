-- =====================================================================
-- HR Recruitment demo data — TenantId = 0
-- Generates 20 job openings + applicants + interviews + notes.
-- Idempotent: re-running deletes previously seeded demo rows first
-- (identified by the [DEMO] marker in the opening description).
-- Tables: hr_job_openings, hr_applicants, hr_interviews, hr_applicant_notes
-- =====================================================================

DO $$
DECLARE
    v_tenant           int  := 0;
    v_opening_id       int;
    v_applicant_id     int;
    v_dept_ids         int[];
    v_dept             int;
    v_titles           text[] := ARRAY[
        'Développeur Full Stack .NET/React','Ingénieur DevOps','Technicien Support N2',
        'Chef de Projet IT','Comptable Senior','Responsable RH','Commercial B2B',
        'Designer UI/UX','Data Analyst','Administrateur Réseau','Ingénieur QA',
        'Développeur Mobile Flutter','Assistant(e) Administratif(ve)','Responsable Achats',
        'Technicien de Maintenance','Architecte Cloud','Community Manager',
        'Ingénieur Sécurité SI','Contrôleur de Gestion','Développeur Backend Node.js'
    ];
    v_locations        text[] := ARRAY['Tunis','Sfax','Sousse','Ariana','Remote','Nabeul','Bizerte','Monastir'];
    v_contracts        text[] := ARRAY['CDI','CDD','Stage','Freelance'];
    v_seniority        text[] := ARRAY['junior','mid','senior','lead'];
    v_statuses         text[] := ARRAY['open','open','open','open','draft','on_hold','closed','filled'];
    v_stages           text[] := ARRAY['applied','applied','screening','screening','interview','interview','offer','hired','rejected','withdrawn'];
    v_sources          text[] := ARRAY['linkedin','referral','website','other'];
    v_first            text[] := ARRAY['Ahmed','Sarra','Mohamed','Ines','Yassine','Rania','Karim','Emna','Bilel','Nour','Skander','Mariem','Hamza','Salma','Oussama','Dorra','Firas','Amira','Wassim','Ghada'];
    v_last             text[] := ARRAY['Ben Ali','Trabelsi','Gharbi','Jelassi','Mansouri','Bouazizi','Chaabane','Hamdi','Khelifi','Zouari','Sassi','Ayari','Mejri','Rekik','Bouzid','Nasri','Farhat','Belhaj','Karray','Dridi'];
    v_kinds            text[] := ARRAY['phone','technical','hr','onsite','final'];
    v_int_status       text[] := ARRAY['scheduled','done','done','cancelled','no_show'];
    v_recos            text[] := ARRAY['hire','no_hire','maybe','next_round'];
    i                  int;
    j                  int;
    k                  int;
    v_napp             int;
    v_nint             int;
    v_stage            text;
    v_status           text;
    v_opened           timestamp;
    v_applied          timestamp;
    v_salary_min       numeric(14,3);
BEGIN
    -- ---------- cleanup previous demo seed ----------
    DELETE FROM hr_applicant_notes n
     WHERE n."TenantId" = v_tenant
       AND n.applicant_id IN (
            SELECT a.id FROM hr_applicants a
             JOIN hr_job_openings o ON o.id = a.opening_id AND o."TenantId" = v_tenant
            WHERE a."TenantId" = v_tenant AND o.description LIKE '[DEMO]%');

    DELETE FROM hr_interviews iv
     WHERE iv."TenantId" = v_tenant
       AND iv.applicant_id IN (
            SELECT a.id FROM hr_applicants a
             JOIN hr_job_openings o ON o.id = a.opening_id AND o."TenantId" = v_tenant
            WHERE a."TenantId" = v_tenant AND o.description LIKE '[DEMO]%');

    DELETE FROM hr_applicants a
     USING hr_job_openings o
     WHERE a."TenantId" = v_tenant AND o."TenantId" = v_tenant
       AND o.id = a.opening_id AND o.description LIKE '[DEMO]%';

    DELETE FROM hr_job_openings
     WHERE "TenantId" = v_tenant AND description LIKE '[DEMO]%';

    -- ---------- available departments (optional link) ----------
    SELECT COALESCE(array_agg(id ORDER BY id), ARRAY[]::int[])
      INTO v_dept_ids
      FROM hr_departments
     WHERE "TenantId" = v_tenant AND is_deleted = false;

    -- ---------- 20 job openings ----------
    FOR i IN 1..20 LOOP
        v_status     := v_statuses[1 + ((i * 3) % array_length(v_statuses,1))];
        v_opened     := NOW() - ((90 - i * 3) || ' days')::interval;
        v_salary_min := 1200 + (i % 8) * 450;
        v_dept       := CASE WHEN array_length(v_dept_ids,1) IS NULL THEN NULL
                             ELSE v_dept_ids[1 + (i % array_length(v_dept_ids,1))] END;

        INSERT INTO hr_job_openings (
            "TenantId", title, department_id, location, contract_type, seniority,
            description, requirements, salary_min, salary_max, currency,
            openings_count, status, hiring_manager_user_id, opened_at, closed_at,
            created_at, created_by, updated_at, is_deleted)
        VALUES (
            v_tenant,
            v_titles[i],
            v_dept,
            v_locations[1 + (i % array_length(v_locations,1))],
            v_contracts[1 + (i % 4)],
            v_seniority[1 + (i % 4)],
            '[DEMO] Poste de ' || v_titles[i] || '. Rejoignez une équipe dynamique sur des projets à forte valeur ajoutée.',
            '- 2 à 8 ans d''expérience' || chr(10) ||
            '- Bonne maîtrise du domaine ' || v_titles[i] || chr(10) ||
            '- Français / Anglais professionnel',
            v_salary_min,
            v_salary_min + 900,
            'TND',
            1 + (i % 3),
            v_status,
            NULL,
            CASE WHEN v_status = 'draft' THEN NULL ELSE v_opened END,
            CASE WHEN v_status IN ('closed','filled') THEN v_opened + interval '45 days' ELSE NULL END,
            v_opened, NULL, NOW(), false)
        RETURNING id INTO v_opening_id;

        -- ---------- applicants (2..7 per opening) ----------
        v_napp := 2 + (i % 6);
        FOR j IN 1..v_napp LOOP
            v_stage   := v_stages[1 + ((i + j * 3) % array_length(v_stages,1))];
            v_applied := v_opened + ((j * 2) || ' days')::interval;

            INSERT INTO hr_applicants (
                "TenantId", opening_id, first_name, last_name, email, phone, source,
                resume_url, resume_file_name, stage, rating, expected_salary,
                available_from, rejection_reason, created_at, created_by, updated_at, is_deleted)
            VALUES (
                v_tenant, v_opening_id,
                v_first[1 + ((i + j) % 20)],
                v_last[1 + ((i * 2 + j) % 20)],
                lower(replace(v_first[1 + ((i + j) % 20)], ' ', '')) || '.' ||
                lower(replace(v_last[1 + ((i * 2 + j) % 20)], ' ', '')) || i || j || '@example.tn',
                '+216 ' || (20000000 + ((i * 7919 + j * 131) % 79999999))::text,
                v_sources[1 + ((i + j) % 4)],
                NULL,
                'cv_' || i || '_' || j || '.pdf',
                v_stage,
                CASE WHEN v_stage IN ('applied') THEN NULL ELSE 1 + ((i + j) % 5) END,
                v_salary_min + ((j % 4) * 300),
                (CURRENT_DATE + ((15 + j * 5) || ' days')::interval)::timestamp,
                CASE WHEN v_stage = 'rejected' THEN
                        (ARRAY['Profil junior','Prétentions salariales trop élevées','Compétences techniques insuffisantes','Poste pourvu'])[1 + ((i + j) % 4)]
                     ELSE NULL END,
                v_applied, NULL, NOW(), false)
            RETURNING id INTO v_applicant_id;

            -- ---------- interviews (only for advanced stages) ----------
            IF v_stage IN ('interview','offer','hired','rejected') THEN
                v_nint := 1 + ((i + j) % 3);
                FOR k IN 1..v_nint LOOP
                    INSERT INTO hr_interviews (
                        "TenantId", applicant_id, kind, scheduled_at, duration_minutes,
                        interviewer_user_id, location, meeting_url, status, score,
                        feedback, recommendation, created_at, created_by, updated_at, is_deleted)
                    VALUES (
                        v_tenant, v_applicant_id,
                        v_kinds[1 + ((k + j) % 5)],
                        v_applied + ((k * 4) || ' days')::interval + interval '10 hours',
                        (ARRAY[30,45,60,90])[1 + ((i + k) % 4)],
                        NULL,
                        CASE WHEN (k % 2) = 0 THEN 'Siège - Salle ' || (1 + (k % 4)) ELSE NULL END,
                        CASE WHEN (k % 2) = 1 THEN 'https://meet.example.com/itw-' || v_applicant_id || '-' || k ELSE NULL END,
                        CASE WHEN v_applied + ((k * 4) || ' days')::interval > NOW()
                             THEN 'scheduled'
                             ELSE v_int_status[1 + ((i + k) % 5)] END,
                        CASE WHEN v_applied + ((k * 4) || ' days')::interval > NOW() THEN NULL
                             ELSE 1 + ((i + j + k) % 5) END,
                        CASE WHEN v_applied + ((k * 4) || ' days')::interval > NOW() THEN NULL
                             ELSE 'Entretien mené. Points forts: communication, motivation. À approfondir: aspects techniques.' END,
                        CASE WHEN v_applied + ((k * 4) || ' days')::interval > NOW() THEN NULL
                             ELSE v_recos[1 + ((i + k) % 4)] END,
                        v_applied, NULL, NOW(), false);
                END LOOP;
            END IF;

            -- ---------- notes (1..2 per applicant) ----------
            INSERT INTO hr_applicant_notes ("TenantId", applicant_id, author_user_id, body, created_at)
            VALUES (v_tenant, v_applicant_id, NULL,
                    (ARRAY[
                        'CV reçu via ' || v_sources[1 + ((i + j) % 4)] || ', profil intéressant.',
                        'Premier contact téléphonique effectué, candidat disponible sous 1 mois.',
                        'Bon feeling général, à faire passer au tour suivant.',
                        'Prétentions salariales à négocier.'
                    ])[1 + ((i + j) % 4)],
                    v_applied + interval '1 day');

            IF (j % 2) = 0 THEN
                INSERT INTO hr_applicant_notes ("TenantId", applicant_id, author_user_id, body, created_at)
                VALUES (v_tenant, v_applicant_id, NULL,
                        'Relance envoyée par email, en attente de retour du candidat.',
                        v_applied + interval '4 days');
            END IF;
        END LOOP;
    END LOOP;

    RAISE NOTICE 'HR recruitment demo data seeded for tenant %', v_tenant;
END $$;

-- Quick verification
-- SELECT (SELECT count(*) FROM hr_job_openings   WHERE "TenantId"=0) AS openings,
--        (SELECT count(*) FROM hr_applicants     WHERE "TenantId"=0) AS applicants,
--        (SELECT count(*) FROM hr_interviews     WHERE "TenantId"=0) AS interviews,
--        (SELECT count(*) FROM hr_applicant_notes WHERE "TenantId"=0) AS notes;
