-- =====================================================================
-- OAS · 001 — Extensions, enums, tables (spec §5.0/§5.1/§5.2)
-- De-Supabased re-emission of db/migrations/001,003-007: every table
-- prefixed oas_, tenant_id changed from `uuid references tenants(id)` to
-- `int not null default 0` (no oas_tenants table — spec §1.2 bis point 7),
-- every FK to `profiles(id)` retargeted to `oas_users(id)`. NO RLS, NO
-- policies, NO `authenticated`/`anon`/`service_role` roles anywhere in
-- this module (spec §5.0) — isolation is the tenant_id filter in
-- OasDbContext + OasScopeFilter, not Postgres RLS.
--
-- Run once per *oas database, in order: 001 → 002 → 003 → 004.
-- Safe to re-run (every statement is IF NOT EXISTS / OR REPLACE).
-- =====================================================================

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------
-- Enums
-- ---------------------------------------------------------------------
do $$ begin
  create type public.oas_app_role as enum ('admin', 'supervisor', 'operator');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_workspace as enum ('web', 'mobile', 'both');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_post_state as enum (
    'production', 'material_wait', 'changeover', 'technical_stop', 'quality_stop', 'unassigned'
  );
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_event_type as enum (
    'technical_stop','quality_stop','material_wait','changeover','other_stop'
  );
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_event_status as enum (
    'declared','notified','acknowledged','on_site','resolved','closed','cancelled'
  );
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_closure_type as enum ('resolved','palliative','no_fault','cancelled');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_escalation_level as enum ('none','level_1','level_2');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_declaration_kind as enum ('production','scrap','rework');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_shift_code as enum ('morning','afternoon','night','custom');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_criticality as enum ('low','medium','high','critical');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_cause_domain as enum ('stop','scrap','root_cause');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_order_status as enum ('planned','in_progress','done','cancelled');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_check_type as enum ('first_part','in_process','final');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_check_result as enum ('ok','rework','scrap');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_notify_channel as enum ('push','sms','in_app','email');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_notify_response as enum ('coming','busy','delegated');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_audit_action as enum ('insert','update','delete','correct');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_oee_mode as enum ('full','lite');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_scope_type as enum ('post','line','zone','site');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oas_import_status as enum ('pending','committed','failed');
exception when duplicate_object then null; end $$;

-- =====================================================================
-- §5.1 — 30 tables re-emitted from db/migrations/003..007
-- =====================================================================

-- ---- Hierarchy (003) --------------------------------------------------
create table if not exists public.oas_sites (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  code        text not null,
  name        text not null,
  timezone    text not null default 'Africa/Tunis',
  address     text,
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.oas_zones (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  site_id     uuid not null references public.oas_sites(id) on delete cascade,
  code        text not null,
  name        text not null,
  sort_order  int not null default 0,
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.oas_lines (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  zone_id     uuid not null references public.oas_zones(id) on delete cascade,
  code        text not null,
  name        text not null,
  sort_order  int not null default 0,
  target_oee  numeric(5,2),
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.oas_posts (
  id            uuid primary key default gen_random_uuid(),
  tenant_id     int not null default 0,
  line_id       uuid not null references public.oas_lines(id) on delete cascade,
  code          text not null,
  name          text not null,
  qr_token      text not null unique,
  qr_rotated_at timestamptz,
  sort_order    int not null default 0,
  post_type     text,
  is_critical   boolean not null default false,
  is_active     boolean not null default true,
  archived_at   timestamptz,
  created_at    timestamptz not null default now(),
  updated_at    timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.oas_equipments (
  id              uuid primary key default gen_random_uuid(),
  tenant_id       int not null default 0,
  post_id         uuid references public.oas_posts(id) on delete set null,
  code            text not null,
  name            text not null,
  serial_number   text,
  manufacturer    text,
  commissioned_at date,
  criticality     public.oas_criticality not null default 'medium',
  archived_at     timestamptz,
  created_at      timestamptz not null default now(),
  updated_at      timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.oas_products (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  reference   text not null,
  name        text not null,
  customer    text,
  unit        text not null default 'pcs',
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, reference)
);

-- Current/base cadence per (product, post). Versioned history lives in
-- oas_routing_versions (§5.2 #2) — this row always reflects the latest.
create table if not exists public.oas_routings (
  id                        uuid primary key default gen_random_uuid(),
  tenant_id                 int not null default 0,
  product_id                uuid not null references public.oas_products(id) on delete cascade,
  post_id                   uuid not null references public.oas_posts(id) on delete cascade,
  rate                      numeric(10,2) not null default 60,
  cycle_time_sec            numeric(10,3),
  changeover_target_min     int,
  operators_required        int not null default 1,
  created_at                timestamptz not null default now(),
  updated_at                timestamptz not null default now(),
  unique (tenant_id, product_id, post_id)
);

create table if not exists public.oas_production_orders (
  id                 uuid primary key default gen_random_uuid(),
  tenant_id          int not null default 0,
  order_number       text not null,
  product_id         uuid not null references public.oas_products(id),
  line_id            uuid references public.oas_lines(id),
  quantity_planned   numeric(12,2) not null default 0,
  quantity_produced  numeric(12,2) not null default 0,
  quantity_scrapped  numeric(12,2) not null default 0,
  due_date           date,
  status             public.oas_order_status not null default 'planned',
  priority           int not null default 0,
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  unique (tenant_id, order_number)
);

-- ---- Shifts / teams / assignments / sessions / causes / routing rules (004) ----
create table if not exists public.oas_shift_templates (
  id               uuid primary key default gen_random_uuid(),
  tenant_id        int not null default 0,
  site_id          uuid not null references public.oas_sites(id) on delete cascade,
  code             public.oas_shift_code not null,
  name             text not null,
  start_time       time not null,
  end_time         time not null,
  crosses_midnight boolean not null default false,
  break_minutes    int not null default 0,
  is_active        boolean not null default true,
  created_at       timestamptz not null default now(),
  updated_at       timestamptz not null default now(),
  unique (tenant_id, site_id, code, name)
);

create table if not exists public.oas_shift_calendar (
  id                 uuid primary key default gen_random_uuid(),
  tenant_id          int not null default 0,
  site_id            uuid not null references public.oas_sites(id) on delete cascade,
  shift_template_id  uuid not null references public.oas_shift_templates(id) on delete cascade,
  work_date          date not null,
  is_working_day     boolean not null default true,
  created_at         timestamptz not null default now(),
  unique (tenant_id, site_id, shift_template_id, work_date)
);

create table if not exists public.oas_teams (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  site_id      uuid not null references public.oas_sites(id) on delete cascade,
  code         text not null,
  name         text not null,
  lead_user_id uuid references public.oas_users(id) on delete set null,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.oas_team_members (
  id         uuid primary key default gen_random_uuid(),
  tenant_id  int not null default 0,
  team_id    uuid not null references public.oas_teams(id) on delete cascade,
  user_id    uuid not null references public.oas_users(id) on delete cascade,
  valid_from date not null default current_date,
  valid_to   date,
  created_at timestamptz not null default now(),
  unique (team_id, user_id, valid_from)
);

create table if not exists public.oas_assignments (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            int not null default 0,
  work_date            date not null,
  shift_template_id    uuid not null references public.oas_shift_templates(id),
  post_id              uuid not null references public.oas_posts(id) on delete cascade,
  user_id              uuid not null references public.oas_users(id) on delete cascade,
  production_order_id  uuid references public.oas_production_orders(id) on delete set null,
  assigned_by          uuid references public.oas_users(id),
  note                 text,
  -- v15 decision: publish state is per-post, not a single global flag
  -- (editing one post must not un-publish the whole board).
  published_at         timestamptz,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now(),
  unique (tenant_id, work_date, shift_template_id, post_id, user_id)
);

create table if not exists public.oas_post_sessions (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           int not null default 0,
  client_event_id     uuid not null unique,
  post_id             uuid not null references public.oas_posts(id) on delete cascade,
  user_id             uuid not null references public.oas_users(id) on delete cascade,
  assignment_id       uuid references public.oas_assignments(id) on delete set null,
  production_order_id uuid references public.oas_production_orders(id) on delete set null,
  shift_template_id   uuid references public.oas_shift_templates(id),
  started_at          timestamptz not null,
  ended_at            timestamptz,
  started_via         text not null default 'qr' check (started_via in ('qr','manual','biometric','pin')),
  received_at         timestamptz not null default now(),
  created_at          timestamptz not null default now()
);

create table if not exists public.oas_causes (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            int not null default 0,
  parent_id            uuid references public.oas_causes(id) on delete cascade,
  domain               public.oas_cause_domain not null,
  code                 text not null,
  label_fr             text not null,
  label_ar             text not null,
  icon                 text,
  event_type           public.oas_event_type,
  default_criticality  public.oas_criticality not null default 'medium',
  sort_order           int not null default 0,
  is_active            boolean not null default true,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now(),
  unique (tenant_id, domain, code)
);

create table if not exists public.oas_routing_rules (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            int not null default 0,
  event_type           public.oas_event_type not null,
  cause_id             uuid references public.oas_causes(id) on delete cascade,
  zone_id              uuid references public.oas_zones(id) on delete cascade,
  line_id              uuid references public.oas_lines(id) on delete cascade,
  target_role          public.oas_app_role not null default 'supervisor',
  target_team_id       uuid references public.oas_teams(id) on delete set null,
  sla_minutes          int not null default 10,
  priority             int not null default 0,
  is_active            boolean not null default true,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now()
);

-- ---- Declarations / events (005) --------------------------------------
create table if not exists public.oas_declarations (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           int not null default 0,
  client_event_id     uuid not null unique,
  kind                public.oas_declaration_kind not null,
  post_session_id     uuid references public.oas_post_sessions(id) on delete set null,
  post_id             uuid not null references public.oas_posts(id),
  line_id             uuid references public.oas_lines(id),
  user_id             uuid not null references public.oas_users(id),
  production_order_id uuid references public.oas_production_orders(id),
  product_id          uuid references public.oas_products(id),
  quantity_ok         numeric(12,2) not null default 0,
  quantity_nok        numeric(12,2) not null default 0,
  scrap_cause_id      uuid references public.oas_causes(id),
  photo_path          text,
  note                text,
  occurred_at         timestamptz not null,
  received_at         timestamptz not null default now(),
  corrects_id         uuid references public.oas_declarations(id),
  is_corrected        boolean not null default false,
  correction_reason   text,
  -- v15: server-side authorization for PUT /declarations/{id}/correct is
  -- keyed off THIS column (who the server authenticated as the declarer),
  -- never a client-supplied field.
  created_by          uuid not null references public.oas_users(id),
  check (quantity_ok >= 0 and quantity_nok >= 0)
);

create table if not exists public.oas_events (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           int not null default 0,
  client_event_id     uuid not null unique,
  event_type          public.oas_event_type not null,
  status              public.oas_event_status not null default 'declared',

  post_id             uuid not null references public.oas_posts(id),
  line_id             uuid references public.oas_lines(id),
  zone_id             uuid references public.oas_zones(id),
  site_id             uuid references public.oas_sites(id),

  post_session_id     uuid references public.oas_post_sessions(id) on delete set null,
  production_order_id uuid references public.oas_production_orders(id),
  product_id          uuid references public.oas_products(id),
  equipment_id        uuid references public.oas_equipments(id),

  cause_id            uuid references public.oas_causes(id),
  root_cause_id       uuid references public.oas_causes(id),
  criticality         public.oas_criticality not null default 'medium',

  declared_by         uuid not null references public.oas_users(id),
  declared_at         timestamptz not null,
  notified_at         timestamptz,
  acknowledged_at     timestamptz,
  acknowledged_by     uuid references public.oas_users(id),
  eta_minutes         int,
  on_site_at          timestamptz,
  assignee_id         uuid references public.oas_users(id),
  resolved_at         timestamptz,
  resolved_by         uuid references public.oas_users(id),
  closure_type        public.oas_closure_type,
  closure_note        text,
  closed_at           timestamptz,
  closed_by           uuid references public.oas_users(id),
  cancelled_at        timestamptz,
  cancel_reason       text,

  sla_minutes         int not null default 10,
  sla_due_at          timestamptz,
  sla_breached        boolean not null default false,
  escalation_level    public.oas_escalation_level not null default 'none',

  duration_sec        int,
  response_sec        int,
  repair_sec          int,

  note                text,
  received_at         timestamptz not null default now(),
  updated_at          timestamptz not null default now()
);

create table if not exists public.oas_event_transitions (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  event_id    uuid not null references public.oas_events(id) on delete cascade,
  from_status public.oas_event_status,
  to_status   public.oas_event_status not null,
  actor_id    uuid references public.oas_users(id),
  actor_role  public.oas_app_role,
  payload     jsonb not null default '{}'::jsonb,
  occurred_at timestamptz not null default now(),
  received_at timestamptz not null default now()
);

create table if not exists public.oas_event_notifications (
  id                uuid primary key default gen_random_uuid(),
  tenant_id         int not null default 0,
  event_id          uuid not null references public.oas_events(id) on delete cascade,
  recipient_user_id uuid references public.oas_users(id) on delete cascade,
  recipient_role    public.oas_app_role,
  channel           public.oas_notify_channel not null default 'push',
  escalation_level  public.oas_escalation_level not null default 'none',
  sent_at           timestamptz not null default now(),
  delivered_at      timestamptz,
  read_at           timestamptz,
  responded_at      timestamptz,
  response          public.oas_notify_response,
  eta_minutes       int
);

create table if not exists public.oas_changeovers (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           int not null default 0,
  client_event_id     uuid not null unique,
  post_id             uuid not null references public.oas_posts(id) on delete cascade,
  from_product_id     uuid references public.oas_products(id),
  to_product_id       uuid not null references public.oas_products(id),
  production_order_id uuid references public.oas_production_orders(id),
  event_id            uuid references public.oas_events(id) on delete set null,
  started_at          timestamptz not null,
  first_good_part_at  timestamptz,
  ended_at            timestamptz,
  duration_sec        int,
  target_min          int,
  -- v13: persists the 5-step checklist (ChangeoverPage.tsx:25) so a
  -- navigate-away/app-restart can resume it — absent from the original
  -- migration, added specifically for this. Shape: [{"id":"...","done":true}, ...].
  steps               jsonb not null default '[]'::jsonb,
  started_by          uuid not null references public.oas_users(id),
  validated_by        uuid references public.oas_users(id),
  received_at         timestamptz not null default now()
);

create table if not exists public.oas_quality_checks (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           int not null default 0,
  client_event_id     uuid not null unique,
  post_id             uuid not null references public.oas_posts(id) on delete cascade,
  production_order_id uuid references public.oas_production_orders(id),
  product_id          uuid references public.oas_products(id),
  changeover_id       uuid references public.oas_changeovers(id) on delete set null,
  event_id            uuid references public.oas_events(id) on delete set null,
  template_id         uuid,
  check_type          public.oas_check_type not null,
  result              public.oas_check_result not null,
  quantity_checked    numeric(12,2) not null default 1,
  quantity_rejected   numeric(12,2) not null default 0,
  cause_id            uuid references public.oas_causes(id),
  inspector_id        uuid not null references public.oas_users(id),
  photo_path          text,
  note                text,
  occurred_at         timestamptz not null,
  received_at         timestamptz not null default now()
);

-- ---- Post states / KPI (006) -------------------------------------------
create table if not exists public.oas_post_states (
  post_id               uuid primary key references public.oas_posts(id) on delete cascade,
  tenant_id             int not null default 0,
  state                 public.oas_post_state not null default 'unassigned',
  since                 timestamptz not null default now(),
  active_event_id       uuid references public.oas_events(id) on delete set null,
  active_session_id     uuid references public.oas_post_sessions(id) on delete set null,
  active_changeover_id  uuid references public.oas_changeovers(id) on delete set null,
  current_user_id       uuid references public.oas_users(id) on delete set null,
  current_product_id    uuid references public.oas_products(id) on delete set null,
  current_order_id      uuid references public.oas_production_orders(id) on delete set null,
  updated_at            timestamptz not null default now()
);

create table if not exists public.oas_post_state_history (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  post_id      uuid not null references public.oas_posts(id) on delete cascade,
  state        public.oas_post_state not null,
  started_at   timestamptz not null,
  ended_at     timestamptz,
  duration_sec int,
  event_id     uuid references public.oas_events(id) on delete set null,
  session_id   uuid references public.oas_post_sessions(id) on delete set null
);

create table if not exists public.oas_kpi_daily (
  id                 uuid primary key default gen_random_uuid(),
  tenant_id          int not null default 0,
  scope_type         public.oas_scope_type not null,
  scope_id           uuid not null,
  work_date          date not null,
  shift_template_id  uuid references public.oas_shift_templates(id) on delete set null,

  planned_time_sec   int not null default 0,
  run_time_sec       int not null default 0,
  downtime_sec       int not null default 0,
  changeover_sec     int not null default 0,

  qty_ok             numeric(12,2) not null default 0,
  qty_nok            numeric(12,2) not null default 0,
  qty_scrap          numeric(12,2) not null default 0,
  theoretical_qty    numeric(12,2),

  availability       numeric(5,2),
  performance        numeric(5,2),
  quality            numeric(5,2),
  oee                numeric(5,2),
  oee_mode           public.oas_oee_mode not null default 'lite',
  cadence_known      boolean not null default true,

  stops_count        int not null default 0,
  mtbf_sec           int,
  mttr_sec           int,

  computed_at        timestamptz not null default now()
);

-- ---- Audit / offline (007) ----------------------------------------------
create table if not exists public.oas_audit_log (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  entity_table text not null,
  entity_id    uuid,
  action       public.oas_audit_action not null,
  actor_id     uuid,
  actor_role   public.oas_app_role,
  before       jsonb,
  after        jsonb,
  reason       text,
  occurred_at  timestamptz not null default now(),
  ip           inet,
  user_agent   text
);

create table if not exists public.oas_sync_receipts (
  id              uuid primary key default gen_random_uuid(),
  tenant_id       int not null default 0,
  client_event_id uuid not null,
  device_id       text,
  entity          text not null,
  occurred_at     timestamptz not null,
  received_at     timestamptz not null default now(),
  latency_sec     int generated always as (extract(epoch from (received_at - occurred_at))::int) stored,
  attempts        int not null default 1,
  status          text not null default 'ok' check (status in ('ok','duplicate','rejected')),
  error           text
);

create table if not exists public.oas_device_tokens (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  user_id      uuid not null references public.oas_users(id) on delete cascade,
  platform     text not null check (platform in ('android','ios','web')),
  token        text not null unique,
  app_version  text,
  os_version   text,
  last_seen_at timestamptz not null default now(),
  created_at   timestamptz not null default now()
);

create table if not exists public.oas_attachments (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  bucket       text not null default 'oas',
  path         text not null,
  mime_type    text,
  size_bytes   bigint,
  entity_table text,
  entity_id    uuid,
  uploaded_by  uuid references public.oas_users(id),
  created_at   timestamptz not null default now()
);

create table if not exists public.oas_imports (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  kind         text not null,
  status       public.oas_import_status not null default 'pending',
  file_path    text,
  rows_total   int not null default 0,
  rows_ok      int not null default 0,
  rows_error   int not null default 0,
  report       jsonb not null default '{}'::jsonb,
  imported_by  uuid references public.oas_users(id),
  created_at   timestamptz not null default now(),
  committed_at timestamptz
);

-- =====================================================================
-- §5.2 — 18 tables created new for OAS
-- =====================================================================

-- 1
create table if not exists public.oas_cause_proposals (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  domain       public.oas_cause_domain not null,
  label_fr     text not null,
  label_ar     text,
  proposed_by  uuid not null references public.oas_users(id),
  status       text not null default 'pending' check (status in ('pending','accepted','rejected')),
  reviewed_by  uuid references public.oas_users(id),
  reviewed_at  timestamptz,
  resulting_cause_id uuid references public.oas_causes(id) on delete set null,
  created_at   timestamptz not null default now()
);

-- 2
create table if not exists public.oas_routing_versions (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  product_id   uuid not null references public.oas_products(id) on delete cascade,
  post_id      uuid not null references public.oas_posts(id) on delete cascade,
  rate         numeric(10,2) not null,
  version      int not null default 1,
  since        timestamptz not null default now(),
  created_by   uuid references public.oas_users(id),
  created_at   timestamptz not null default now(),
  unique (tenant_id, product_id, post_id, version)
);

-- 3
create table if not exists public.oas_shift_signoffs (
  id                uuid primary key default gen_random_uuid(),
  tenant_id         int not null default 0,
  shift_template_id uuid not null references public.oas_shift_templates(id),
  work_date         date not null,
  signed_by         uuid not null references public.oas_users(id),
  note              text,
  signed_at         timestamptz not null default now(),
  unique (tenant_id, shift_template_id, work_date, signed_by)
);

-- 4
create table if not exists public.oas_presence_entries (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  user_id      uuid not null references public.oas_users(id) on delete cascade,
  work_date    date not null,
  shift_template_id uuid not null references public.oas_shift_templates(id),
  status       text not null default 'expected' check (status in ('expected','confirmed','absent')),
  confirmed_at timestamptz,
  reason       text,
  created_at   timestamptz not null default now(),
  unique (tenant_id, user_id, work_date, shift_template_id)
);

-- 5
create table if not exists public.oas_interventions (
  id            uuid primary key default gen_random_uuid(),
  tenant_id     int not null default 0,
  event_id      uuid not null references public.oas_events(id) on delete cascade,
  assignee_id   uuid references public.oas_users(id),
  status        text not null default 'open' check (status in ('open','in_progress','closed')),
  assigned_at   timestamptz,
  started_at    timestamptz,
  closed_at     timestamptz,
  created_at    timestamptz not null default now()
);

-- 6
create table if not exists public.oas_quality_check_templates (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  code        text not null,
  name        text not null,
  check_type  public.oas_check_type not null,
  is_active   boolean not null default true,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, code)
);

-- 7
create table if not exists public.oas_quality_check_template_items (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  template_id  uuid not null references public.oas_quality_check_templates(id) on delete cascade,
  label        text not null,
  value_type   text not null default 'boolean' check (value_type in ('boolean','numeric','text')),
  min_value    numeric(12,4),
  max_value    numeric(12,4),
  is_required  boolean not null default true,
  sort_order   int not null default 0
);

-- 8
create table if not exists public.oas_sla_rules (
  id            uuid primary key default gen_random_uuid(),
  tenant_id     int not null default 0,
  event_type    public.oas_event_type not null,
  criticality   public.oas_criticality,
  line_id       uuid references public.oas_lines(id) on delete cascade,
  target_min    int not null default 10,
  priority      int not null default 0,
  is_active     boolean not null default true,
  created_at    timestamptz not null default now(),
  updated_at    timestamptz not null default now()
);

-- 9
create table if not exists public.oas_escalations (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  event_id     uuid not null references public.oas_events(id) on delete cascade,
  level        public.oas_escalation_level not null,
  triggered_at timestamptz not null default now(),
  reason       text,
  acknowledged_at timestamptz,
  acknowledged_by uuid references public.oas_users(id)
);

-- 10
create table if not exists public.oas_import_lines (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  import_id   uuid not null references public.oas_imports(id) on delete cascade,
  row_number  int not null,
  raw         jsonb not null default '{}'::jsonb,
  status      text not null default 'pending' check (status in ('pending','ok','error')),
  error       text
);

-- 11
create table if not exists public.oas_integration_endpoints (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    int not null default 0,
  name         text not null,
  url          text not null,
  secret       text,
  event_types  text[] not null default '{}',
  is_active    boolean not null default true,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now()
);

-- 12
create table if not exists public.oas_integration_outbox (
  id            uuid primary key default gen_random_uuid(),
  tenant_id     int not null default 0,
  endpoint_id   uuid not null references public.oas_integration_endpoints(id) on delete cascade,
  event_type    text not null,
  payload       jsonb not null,
  status        text not null default 'pending' check (status in ('pending','sent','failed')),
  attempts      int not null default 0,
  last_error    text,
  created_at    timestamptz not null default now(),
  sent_at       timestamptz
);

-- 13
create table if not exists public.oas_lookup_values (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  type        text not null,
  code        text not null,
  label       text not null,
  color       text,
  sort_order  int not null default 0,
  is_default  boolean not null default false,
  archived_at timestamptz,
  unique (tenant_id, type, code)
);

-- 14
create table if not exists public.oas_post_layouts (
  id         uuid primary key default gen_random_uuid(),
  tenant_id  int not null default 0,
  post_id    uuid not null references public.oas_posts(id) on delete cascade,
  layout_key text not null default 'default',
  sort_order int not null default 0,
  col_span   int not null default 1,
  row_span   int not null default 1,
  x          numeric(10,2),
  y          numeric(10,2),
  unique (tenant_id, post_id, layout_key)
);

-- 15
create table if not exists public.oas_andon_messages (
  id         uuid primary key default gen_random_uuid(),
  tenant_id  int not null default 0,
  line_id    uuid references public.oas_lines(id) on delete cascade, -- null = site-wide (v13)
  message    text not null default '',
  updated_by uuid references public.oas_users(id),
  updated_at timestamptz not null default now()
);

-- 16 (already mapped by EF in Lot 1 — Backend/Modules/OAS/Common/Models/OasPluginActivation.cs)
create table if not exists public.oas_plugin_activations (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  plugin_code text not null,
  enabled     boolean not null default true,
  updated_at  timestamptz not null default now(),
  updated_by  text,
  unique (tenant_id, plugin_code)
);

-- 17
create table if not exists public.oas_responder_availability (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   int not null default 0,
  profile_id  uuid not null references public.oas_users(id) on delete cascade,
  busy        boolean not null default false,
  since       timestamptz not null default now(),
  reason      text,
  unique (tenant_id, profile_id)
);

-- 18 (already mapped by EF in Lot 1 — Backend/Modules/OAS/ShopFloorAuth/Models/OasUser.cs)
create table if not exists public.oas_users (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            int not null default 0,
  source_user_id       int,
  source_tenant_id     int,
  email                varchar(255) not null,
  employee_code        varchar(50),
  password_hash        varchar(255),
  pin                  varchar(20),
  qr_token             varchar(255),
  role                 public.oas_app_role not null default 'operator',
  workspace            public.oas_workspace not null default 'mobile',
  display_name         varchar(255),
  phone                varchar(50),
  avatar_url           text,
  scope_site_id        uuid references public.oas_sites(id),
  scope_zone_id        uuid references public.oas_zones(id),
  scope_line_id        uuid references public.oas_lines(id),
  is_active            boolean not null default true,
  is_interim           boolean not null default false,
  failed_login_attempts int not null default 0,
  locked_until         timestamptz,
  last_login_at        timestamptz,
  last_synced_at       timestamptz,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now(),
  is_deleted           boolean not null default false,
  refresh_token        text,
  refresh_token_expires_at timestamptz,
  unique (tenant_id, email),
  unique (tenant_id, qr_token)
);
create unique index if not exists uq_oas_users_employee_code
  on public.oas_users(tenant_id, employee_code) where employee_code is not null;

-- ---------------------------------------------------------------------
-- Migration tracking (informational only — spec §3, §5.0: the app never
-- runs these files automatically, this table just records that an
-- operator did).
-- ---------------------------------------------------------------------
create table if not exists public.oas_schema_migrations (
  filename    text primary key,
  applied_at  timestamptz not null default now()
);

insert into public.oas_schema_migrations (filename) values ('001_schema.sql')
  on conflict (filename) do nothing;
