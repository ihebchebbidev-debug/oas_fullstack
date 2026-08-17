-- =====================================================================
-- OAS · COMPLETE SETUP — everything from 001_schema.sql, 002_indexes.sql,
-- 003_triggers.sql and 004_seed.sql, consolidated into one file so the
-- whole *oas database can be provisioned in a single run.
--
-- FIX vs. the original 001_schema.sql: that file created `oas_users`
-- LAST (as "table 18" of §5.2), but ~29 foreign keys on ~20 EARLIER
-- tables (oas_teams, oas_assignments, oas_post_sessions, oas_declarations,
-- oas_events, ...) reference `oas_users(id)`. Run top-to-bottom against a
-- truly empty database, the original file fails at the very first one
-- (`oas_teams.lead_user_id`) with "relation oas_users does not exist".
-- This file creates `oas_users` right after the hierarchy tables
-- (sites/zones/lines/posts) instead, before anything that references it.
-- Nothing else was reordered or changed — every statement below is
-- byte-for-byte the same as its source file, just relocated where noted.
--
-- Safe to re-run: every statement is IF NOT EXISTS / OR REPLACE, and the
-- migration-tracking inserts at the end of each section are idempotent —
-- running this file twice, or running the original 001..004 files
-- afterward, is a no-op the second time.
--
-- Run once per *oas database:
--   psql -h <host> -U <user> -d <oas_db> -f COMPLETE_SETUP.sql
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
  is_deleted  boolean not null default false,
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
  is_deleted  boolean not null default false,
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
  is_deleted  boolean not null default false,
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
  is_deleted    boolean not null default false,
  created_at    timestamptz not null default now(),
  updated_at    timestamptz not null default now(),
  unique (tenant_id, code)
);

-- ---- Users (§5.2 #18) — RELOCATED HERE (see header) --------------------
-- Originally listed last in 001_schema.sql; moved up so every table below
-- that references oas_users(id) can actually be created.
-- (already mapped by EF in Lot 1 — Backend/Modules/OAS/ShopFloorAuth/Models/OasUser.cs)
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
  is_deleted      boolean not null default false,
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
  is_deleted  boolean not null default false,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, reference)
);

-- Self-heal for databases already provisioned before `is_deleted` was added
-- to these 6 tables above: `create table if not exists` is a no-op against
-- an existing table, so it alone would never retroactively add a missing
-- column. OasDbContext (Backend/Modules/OAS/Common/OasDbContext.cs) applies
-- a global `WHERE is_deleted = false` filter to every query against
-- OasSite/OasZone/OasLine/OasPost/OasEquipment/OasProduct (they all
-- implement IOasSoftDeletable) — without this column present, literally
-- every read against these 6 tables fails with "column is_deleted does not
-- exist". Re-running this file against an already-provisioned database now
-- repairs that, matching the "safe to re-run" promise at the top of this file.
alter table public.oas_sites      add column if not exists is_deleted boolean not null default false;
alter table public.oas_zones      add column if not exists is_deleted boolean not null default false;
alter table public.oas_lines      add column if not exists is_deleted boolean not null default false;
alter table public.oas_posts      add column if not exists is_deleted boolean not null default false;
alter table public.oas_equipments add column if not exists is_deleted boolean not null default false;
alter table public.oas_products   add column if not exists is_deleted boolean not null default false;

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
-- §5.2 — 18 tables created new for OAS (oas_users, #18, already placed above)
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


-- =====================================================================
-- OAS · 002 — Indexes (spec §5.0: idx_oas_* naming, all internal to the
-- OAS module).
-- =====================================================================

-- ---- Hierarchy ----------------------------------------------------------
create index if not exists idx_oas_zones_site on public.oas_zones(tenant_id, site_id);
create index if not exists idx_oas_lines_zone on public.oas_lines(tenant_id, zone_id);
create index if not exists idx_oas_posts_line on public.oas_posts(tenant_id, line_id);
create index if not exists idx_oas_posts_qr on public.oas_posts(qr_token);
create index if not exists idx_oas_equipments_post on public.oas_equipments(tenant_id, post_id);
create index if not exists idx_oas_orders_line_status on public.oas_production_orders(tenant_id, line_id, status);
create index if not exists idx_oas_routing_versions_lookup on public.oas_routing_versions(tenant_id, product_id, post_id, version desc);

-- ---- Shifts / assignments / sessions -------------------------------------
create index if not exists idx_oas_shift_calendar_date on public.oas_shift_calendar(tenant_id, work_date);
create index if not exists idx_oas_assignments_user_date on public.oas_assignments(tenant_id, user_id, work_date);
create index if not exists idx_oas_assignments_post_date on public.oas_assignments(tenant_id, post_id, work_date);
create index if not exists idx_oas_assignments_published on public.oas_assignments(tenant_id, work_date, shift_template_id) where published_at is not null;

-- One open session per post, and per user (spec §6.1 Sessions de poste).
create unique index if not exists uq_oas_open_session_post
  on public.oas_post_sessions(tenant_id, post_id) where ended_at is null;
create unique index if not exists uq_oas_open_session_user
  on public.oas_post_sessions(tenant_id, user_id) where ended_at is null;
create index if not exists idx_oas_sessions_post_started
  on public.oas_post_sessions(tenant_id, post_id, started_at desc);

create index if not exists idx_oas_presence_lookup on public.oas_presence_entries(tenant_id, work_date, shift_template_id);

-- ---- Declarations / events ------------------------------------------------
create index if not exists idx_oas_decl_post_time on public.oas_declarations(tenant_id, post_id, occurred_at desc);
create index if not exists idx_oas_decl_session on public.oas_declarations(post_session_id);
create index if not exists idx_oas_decl_order on public.oas_declarations(tenant_id, production_order_id);
create index if not exists idx_oas_decl_kind_time on public.oas_declarations(tenant_id, kind, occurred_at desc);

create index if not exists idx_oas_events_open
  on public.oas_events(tenant_id, status) where status not in ('closed','cancelled');
create index if not exists idx_oas_events_post_time on public.oas_events(tenant_id, post_id, declared_at desc);
create index if not exists idx_oas_events_type_time on public.oas_events(tenant_id, event_type, declared_at desc);
create index if not exists idx_oas_events_sla
  on public.oas_events(tenant_id, sla_due_at) where sla_breached = false and status not in ('closed','cancelled','resolved');
create index if not exists idx_oas_events_equipment on public.oas_events(tenant_id, equipment_id, declared_at desc);
create index if not exists idx_oas_events_assignee on public.oas_events(tenant_id, assignee_id) where status not in ('closed','cancelled');

-- One open "blocking" event per post (spec: technical/quality/material stop).
create unique index if not exists uq_oas_open_blocking_event
  on public.oas_events(tenant_id, post_id)
  where status not in ('closed','cancelled')
    and event_type in ('technical_stop','quality_stop','material_wait');

create index if not exists idx_oas_transitions_event on public.oas_event_transitions(event_id, occurred_at);
create index if not exists idx_oas_notif_recipient on public.oas_event_notifications(tenant_id, recipient_user_id, sent_at desc);
create index if not exists idx_oas_notif_event on public.oas_event_notifications(event_id);

-- One open changeover per post (backs GET /changeovers?postId=&status=open, spec v13).
create unique index if not exists uq_oas_open_changeover
  on public.oas_changeovers(tenant_id, post_id) where ended_at is null;
create index if not exists idx_oas_changeover_post on public.oas_changeovers(tenant_id, post_id, started_at desc);

create index if not exists idx_oas_qc_post_time on public.oas_quality_checks(tenant_id, post_id, occurred_at desc);
create index if not exists idx_oas_qc_template_items on public.oas_quality_check_template_items(template_id, sort_order);

-- ---- Post states / KPI -----------------------------------------------------
create index if not exists idx_oas_post_states_tenant on public.oas_post_states(tenant_id, state);
create index if not exists idx_oas_state_hist_post on public.oas_post_state_history(tenant_id, post_id, started_at desc);
create unique index if not exists uq_oas_state_hist_open on public.oas_post_state_history(post_id) where ended_at is null;

create unique index if not exists uq_oas_kpi_daily
  on public.oas_kpi_daily(tenant_id, scope_type, scope_id, work_date,
                          coalesce(shift_template_id, '00000000-0000-0000-0000-000000000000'::uuid));
create index if not exists idx_oas_kpi_daily_date on public.oas_kpi_daily(tenant_id, work_date desc);

-- ---- Causes / SLA / escalations / interventions ----------------------------
create index if not exists idx_oas_causes_parent on public.oas_causes(tenant_id, parent_id);
create index if not exists idx_oas_causes_usage on public.oas_declarations(scrap_cause_id);
create index if not exists idx_oas_cause_proposals_status on public.oas_cause_proposals(tenant_id, status);
create index if not exists idx_oas_sla_rules_lookup on public.oas_sla_rules(tenant_id, event_type, is_active, priority desc);
create index if not exists idx_oas_escalations_event on public.oas_escalations(event_id, triggered_at desc);
create index if not exists idx_oas_interventions_event on public.oas_interventions(event_id);
create index if not exists idx_oas_interventions_assignee on public.oas_interventions(tenant_id, assignee_id, status);

-- ---- Audit / offline / imports / integrations -------------------------------
create index if not exists idx_oas_audit_entity on public.oas_audit_log(entity_table, entity_id, occurred_at desc);
create index if not exists idx_oas_audit_tenant on public.oas_audit_log(tenant_id, occurred_at desc);
create index if not exists idx_oas_sync_client_event on public.oas_sync_receipts(client_event_id);
create index if not exists idx_oas_import_lines_import on public.oas_import_lines(import_id, row_number);
create index if not exists idx_oas_integration_outbox_status on public.oas_integration_outbox(tenant_id, status, created_at);

-- ---- Andon / layout / lookups -----------------------------------------------
create index if not exists idx_oas_andon_messages_line on public.oas_andon_messages(tenant_id, line_id);
create index if not exists idx_oas_post_layouts_lookup on public.oas_post_layouts(tenant_id, post_id, layout_key);
create index if not exists idx_oas_lookup_values_type on public.oas_lookup_values(tenant_id, type, sort_order);

-- ---- Users ------------------------------------------------------------------
create index if not exists idx_oas_users_source on public.oas_users(tenant_id, source_user_id, source_tenant_id);

insert into public.oas_schema_migrations (filename) values ('002_indexes.sql')
  on conflict (filename) do nothing;


-- =====================================================================
-- OAS · 003 — Triggers: immutability, SLA, derived post state, audit
-- (spec §3: "logique métier critique conservée dans les triggers Postgres
-- ... rejoués sous leur nom oas_*, jamais réécrits en C#")
--
-- De-Supabased vs. db/migrations/008: `auth.uid()` was Supabase's
-- session-bound current-user function, which doesn't exist here. Actor
-- identity is instead read from a Postgres session-local setting,
-- `oas.actor_id`, which OasDbContext sets via `SET LOCAL` at the start of
-- every transaction that should be attributed to a specific user (see
-- Backend/Modules/OAS/Common/OasDbContext.cs). If unset, triggers fall
-- back to NULL rather than failing — a job/service-initiated write (e.g.
-- the SLA sweep) has no human actor.
-- =====================================================================

create or replace function public.oas_current_actor_id()
returns uuid language sql stable as $$
  select nullif(current_setting('oas.actor_id', true), '')::uuid
$$;

-- 1) declarations : APPEND-ONLY (IATF audit trail) --------------------------
create or replace function public.oas_declarations_immutable()
returns trigger language plpgsql as $$
begin
  if tg_op = 'DELETE' then
    raise exception 'oas_declarations est append-only : suppression interdite (audit IATF)';
  end if;
  if new.id is distinct from old.id
     or new.quantity_ok is distinct from old.quantity_ok
     or new.quantity_nok is distinct from old.quantity_nok
     or new.occurred_at is distinct from old.occurred_at then
    raise exception 'oas_declarations est append-only : créez une correction (corrects_id)';
  end if;
  return new;
end $$;

drop trigger if exists trg_oas_decl_immutable on public.oas_declarations;
create trigger trg_oas_decl_immutable before update or delete on public.oas_declarations
  for each row execute function public.oas_declarations_immutable();

create or replace function public.oas_declarations_mark_corrected()
returns trigger language plpgsql set search_path = public as $$
begin
  if new.corrects_id is not null then
    update public.oas_declarations set is_corrected = true where id = new.corrects_id;
  end if;
  return new;
end $$;

drop trigger if exists trg_oas_decl_correction on public.oas_declarations;
create trigger trg_oas_decl_correction after insert on public.oas_declarations
  for each row execute function public.oas_declarations_mark_corrected();

-- 2) production order progress -----------------------------------------------
create or replace function public.oas_declarations_order_progress()
returns trigger language plpgsql set search_path = public as $$
begin
  if new.production_order_id is not null and new.corrects_id is null then
    update public.oas_production_orders
       set quantity_produced = quantity_produced + new.quantity_ok,
           quantity_scrapped = quantity_scrapped
                               + case when new.kind = 'scrap' then new.quantity_nok else 0 end,
           status = case when status = 'planned' then 'in_progress'::public.oas_order_status else status end,
           updated_at = now()
     where id = new.production_order_id;
  end if;
  return new;
end $$;

drop trigger if exists trg_oas_decl_order_progress on public.oas_declarations;
create trigger trg_oas_decl_order_progress after insert on public.oas_declarations
  for each row execute function public.oas_declarations_order_progress();

-- 3) SLA applied at declaration time -------------------------------------------
create or replace function public.oas_events_apply_sla()
returns trigger language plpgsql set search_path = public as $$
declare r record; p record;
begin
  select l.id as line_id, z.id as zone_id, s.id as site_id
    into p
    from public.oas_posts po
    join public.oas_lines l on l.id = po.line_id
    join public.oas_zones z on z.id = l.zone_id
    join public.oas_sites s on s.id = z.site_id
   where po.id = new.post_id;

  new.line_id := coalesce(new.line_id, p.line_id);
  new.zone_id := coalesce(new.zone_id, p.zone_id);
  new.site_id := coalesce(new.site_id, p.site_id);

  -- oas_sla_rules (event_type/criticality/line, spec §5.2 #8) takes
  -- precedence; oas_routing_rules (reused, role-targeted) is a fallback
  -- for tenants that haven't configured oas_sla_rules yet.
  select target_min into r
    from public.oas_sla_rules
   where tenant_id = new.tenant_id
     and is_active
     and event_type = new.event_type
     and (criticality is null or criticality = new.criticality)
     and (line_id is null or line_id = new.line_id)
   order by priority desc
   limit 1;

  if found then
    new.sla_minutes := r.target_min;
  else
    select sla_minutes into r
      from public.oas_routing_rules
     where tenant_id = new.tenant_id
       and is_active
       and event_type = new.event_type
       and (cause_id is null or cause_id = new.cause_id)
       and (zone_id  is null or zone_id  = new.zone_id)
       and (line_id  is null or line_id  = new.line_id)
     order by priority desc
     limit 1;
    if found then
      new.sla_minutes := r.sla_minutes;
    end if;
  end if;

  new.sla_due_at := new.declared_at + make_interval(mins => new.sla_minutes);
  return new;
end $$;

drop trigger if exists trg_oas_events_sla on public.oas_events;
create trigger trg_oas_events_sla before insert on public.oas_events
  for each row execute function public.oas_events_apply_sla();

-- 4) durations + transition journal --------------------------------------------
create or replace function public.oas_events_track_status()
returns trigger language plpgsql set search_path = public as $$
begin
  if new.on_site_at is not null and old.on_site_at is null then
    new.response_sec := extract(epoch from (new.on_site_at - new.declared_at))::int;
  end if;
  if new.resolved_at is not null and old.resolved_at is null and new.on_site_at is not null then
    new.repair_sec := extract(epoch from (new.resolved_at - new.on_site_at))::int;
  end if;
  if new.closed_at is not null and old.closed_at is null then
    new.duration_sec := extract(epoch from (new.closed_at - new.declared_at))::int;
  end if;
  new.updated_at := now();
  return new;
end $$;

drop trigger if exists trg_oas_events_durations on public.oas_events;
create trigger trg_oas_events_durations before update on public.oas_events
  for each row execute function public.oas_events_track_status();

create or replace function public.oas_events_log_transition()
returns trigger language plpgsql set search_path = public as $$
begin
  if tg_op = 'INSERT' then
    insert into public.oas_event_transitions(tenant_id, event_id, from_status, to_status, actor_id, occurred_at)
    values (new.tenant_id, new.id, null, new.status, new.declared_by, new.declared_at);
  elsif new.status is distinct from old.status then
    insert into public.oas_event_transitions(tenant_id, event_id, from_status, to_status, actor_id, occurred_at)
    values (new.tenant_id, new.id, old.status, new.status, public.oas_current_actor_id(), now());
  end if;
  return new;
end $$;

drop trigger if exists trg_oas_events_transition on public.oas_events;
create trigger trg_oas_events_transition after insert or update on public.oas_events
  for each row execute function public.oas_events_log_transition();

-- 5) DERIVED POST STATE -----------------------------------------------------
-- priority: technical > quality > material > changeover > production > unassigned
create or replace function public.oas_recompute_post_state(_post_id uuid)
returns void language plpgsql set search_path = public as $$
declare
  _tenant int; _state public.oas_post_state := 'unassigned';
  _ev record; _co record; _se record; _prev public.oas_post_state;
begin
  select tenant_id into _tenant from public.oas_posts where id = _post_id;
  if _tenant is null then return; end if;

  select * into _se from public.oas_post_sessions
   where post_id = _post_id and ended_at is null limit 1;
  select * into _co from public.oas_changeovers
   where post_id = _post_id and ended_at is null limit 1;
  select * into _ev from public.oas_events
   where post_id = _post_id and status not in ('closed','cancelled')
   order by case event_type
              when 'technical_stop' then 1 when 'quality_stop' then 2
              when 'material_wait'  then 3 else 4 end
   limit 1;

  if _ev.id is not null then
    _state := case _ev.event_type
                when 'technical_stop' then 'technical_stop'
                when 'quality_stop'   then 'quality_stop'
                when 'material_wait'  then 'material_wait'
                else 'changeover' end::public.oas_post_state;
  elsif _co.id is not null then
    _state := 'changeover';
  elsif _se.id is not null then
    _state := 'production';
  end if;

  select state into _prev from public.oas_post_states where post_id = _post_id;

  insert into public.oas_post_states(post_id, tenant_id, state, since, active_event_id,
                                     active_session_id, active_changeover_id,
                                     current_user_id, current_order_id, updated_at)
  values (_post_id, _tenant, _state, now(), _ev.id, _se.id, _co.id,
          _se.user_id, _se.production_order_id, now())
  on conflict (post_id) do update
    set state                = excluded.state,
        since                = case when public.oas_post_states.state = excluded.state
                                    then public.oas_post_states.since else now() end,
        active_event_id      = excluded.active_event_id,
        active_session_id    = excluded.active_session_id,
        active_changeover_id = excluded.active_changeover_id,
        current_user_id      = excluded.current_user_id,
        current_order_id     = excluded.current_order_id,
        updated_at           = now();

  if _prev is distinct from _state then
    update public.oas_post_state_history
       set ended_at = now(),
           duration_sec = extract(epoch from (now() - started_at))::int
     where post_id = _post_id and ended_at is null;

    insert into public.oas_post_state_history(tenant_id, post_id, state, started_at, event_id, session_id)
    values (_tenant, _post_id, _state, now(), _ev.id, _se.id);
  end if;
end $$;

create or replace function public.oas_trg_recompute_post_state()
returns trigger language plpgsql set search_path = public as $$
begin
  perform public.oas_recompute_post_state(coalesce(new.post_id, old.post_id));
  return coalesce(new, old);
end $$;

drop trigger if exists trg_oas_state_events on public.oas_events;
create trigger trg_oas_state_events after insert or update on public.oas_events
  for each row execute function public.oas_trg_recompute_post_state();

drop trigger if exists trg_oas_state_sessions on public.oas_post_sessions;
create trigger trg_oas_state_sessions after insert or update on public.oas_post_sessions
  for each row execute function public.oas_trg_recompute_post_state();

drop trigger if exists trg_oas_state_changeovers on public.oas_changeovers;
create trigger trg_oas_state_changeovers after insert or update on public.oas_changeovers
  for each row execute function public.oas_trg_recompute_post_state();

-- 6) generic audit (spec: "logAudit n'a pas d'endpoint d'écriture" — the
-- trigger is the ONLY writer of oas_audit_log) -------------------------------
create or replace function public.oas_audit_row()
returns trigger language plpgsql set search_path = public as $$
declare _tenant int;
begin
  begin
    _tenant := case when tg_op = 'DELETE' then (to_jsonb(old)->>'tenant_id')::int
                    else (to_jsonb(new)->>'tenant_id')::int end;
  exception when others then _tenant := null; end;

  insert into public.oas_audit_log(tenant_id, entity_table, entity_id, action, actor_id, before, after)
  values (
    _tenant, tg_table_name,
    case when tg_op = 'DELETE' then old.id else new.id end,
    lower(tg_op)::public.oas_audit_action,
    public.oas_current_actor_id(),
    case when tg_op in ('UPDATE','DELETE') then to_jsonb(old) end,
    case when tg_op in ('INSERT','UPDATE') then to_jsonb(new) end
  );
  return coalesce(new, old);
end $$;

-- Every table an admin/supervisor might reasonably need to trace,
-- including oas_plugin_activations (spec decision v12: plugin toggles
-- must be audited, which activationStore.ts never did on the client).
do $$
declare t text;
begin
  foreach t in array array[
    'oas_declarations','oas_events','oas_changeovers','oas_quality_checks',
    'oas_assignments','oas_post_sessions','oas_routing_rules','oas_routings',
    'oas_production_orders','oas_posts','oas_users','oas_plugin_activations',
    'oas_sla_rules','oas_causes'
  ]
  loop
    execute format('drop trigger if exists trg_oas_audit_%s on public.%I', t, t);
    execute format('create trigger trg_oas_audit_%s after insert or update or delete on public.%I
                    for each row execute function public.oas_audit_row()', t, t);
  end loop;
end $$;

-- 7) SLA sweep — called every 30s by EscalationSweepHostedService (spec
-- §6.3), NOT pg_cron (no scheduler assumption on the target Postgres
-- host). Forces sequential escalation levels (v15: an event skipping
-- straight to level_2 without passing level_1 is a bug the backend must
-- not replicate) and never regresses an already-escalated level.
-- Returns one row per event whose escalation_level actually changed this
-- sweep, so EscalationSweepHostedService knows exactly what to broadcast
-- on GET /stream and log into oas_escalations — not just a count.
create or replace function public.oas_job_check_sla()
returns table(event_id uuid, tenant_id int, new_level public.oas_escalation_level)
language plpgsql set search_path = public as $$
begin
  return query
  with breached as (
    update public.oas_events e
       set sla_breached = true,
           escalation_level = case
             when e.escalation_level = 'level_1' and now() > e.sla_due_at + make_interval(mins => e.sla_minutes)
               then 'level_2'
             when e.escalation_level = 'none'
               then 'level_1'
             else e.escalation_level
           end::public.oas_escalation_level
     where e.status not in ('closed','cancelled','resolved')
       and e.sla_due_at < now()
       and e.escalation_level <> (case
             when e.escalation_level = 'level_1' and now() > e.sla_due_at + make_interval(mins => e.sla_minutes)
               then 'level_2'
             when e.escalation_level = 'none'
               then 'level_1'
             else e.escalation_level
           end::public.oas_escalation_level)
    returning e.id, e.tenant_id, e.escalation_level
  )
  select id, tenant_id, escalation_level from breached;
end $$;

insert into public.oas_schema_migrations (filename) values ('003_triggers.sql')
  on conflict (filename) do nothing;


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
-- §1.2 bis point 7).
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

-- =====================================================================
-- Done. 48 OAS tables (30 §5.1 + 18 §5.2) + oas_schema_migrations, 20
-- enums, all indexes, all triggers/functions, seed data. Verify with:
--   select filename, applied_at from public.oas_schema_migrations order by applied_at;
-- should show all 4 rows (001_schema.sql, 002_indexes.sql,
-- 003_triggers.sql, 004_seed.sql).
-- =====================================================================
