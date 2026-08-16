-- =====================================================================
-- OAS · 004 — Seed data (spec §5.0: causes types, SLA parameters)
--
-- Deliberately does NOT seed a default admin account: that would mean
-- hardcoding a password (hash) in a committed SQL file — exactly the
-- anti-pattern this whole build corrects elsewhere (MASTER_LOGIN_PASSWORD,
-- the leaked Neon credential). The first admin is created for real via
-- POST /api/oas/setup (spec §8.2), which refuses once one already exists.
--
-- Idempotent: every insert is ON CONFLICT DO NOTHING, tenant_id = 0
-- (the default company scope, matching the socle's convention — spec
-- §1.2 bis point 7). Run after 001..003.
-- =====================================================================

-- ---- Default SLA targets by event type (spec §7.3: SLA_TARGET_MIN) --------
insert into public.oas_sla_rules (tenant_id, event_type, target_min, priority, is_active)
values
  (0, 'technical_stop', 10, 0, true),
  (0, 'quality_stop',   10, 0, true),
  (0, 'material_wait',  15, 0, true),
  (0, 'changeover',     30, 0, true),
  (0, 'other_stop',     15, 0, true)
on conflict do nothing;

-- ---- Starter cause taxonomy (top-level parents only — operators/admins
-- build out the tree via POST /causes and POST /cause-proposals, Lot 3) ----
insert into public.oas_causes (tenant_id, parent_id, domain, code, label_fr, label_ar, event_type, default_criticality, sort_order)
values
  (0, null, 'stop', 'MECH', 'Panne mécanique', 'عطل ميكانيكي', 'technical_stop', 'high', 10),
  (0, null, 'stop', 'ELEC', 'Panne électrique', 'عطل كهربائي', 'technical_stop', 'high', 20),
  (0, null, 'stop', 'MATL', 'Manque matière', 'نقص المواد', 'material_wait', 'medium', 30),
  (0, null, 'stop', 'CHGO', 'Changement de série', 'تغيير السلسلة', 'changeover', 'low', 40),
  (0, null, 'stop', 'QUAL', 'Arrêt qualité', 'توقف الجودة', 'quality_stop', 'high', 50),
  (0, null, 'scrap', 'DIM',  'Non-conformité dimensionnelle', 'عدم مطابقة الأبعاد', null, 'medium', 10),
  (0, null, 'scrap', 'SURF', 'Défaut de surface/aspect', 'عيب في المظهر', null, 'medium', 20),
  (0, null, 'root_cause', 'WEAR', 'Usure normale', 'تآكل عادي', null, 'low', 10),
  (0, null, 'root_cause', 'MISUSE', 'Erreur opérateur', 'خطأ المشغل', null, 'medium', 20)
on conflict (tenant_id, domain, code) do nothing;

insert into public.oas_schema_migrations (filename) values ('004_seed.sql')
  on conflict (filename) do nothing;
