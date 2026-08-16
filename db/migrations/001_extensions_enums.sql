-- =====================================================================
-- OAS · Digital Production Assistant
-- 001 — Extensions, types énumérés
-- =====================================================================

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------
-- Rôles applicatifs (stockés dans public.user_roles, JAMAIS sur profiles)
-- ---------------------------------------------------------------------
do $$ begin
  create type public.app_role as enum (
    'operator',          -- P1 Opérateur
    'team_lead',         -- P2 Chef d'équipe
    'maintenance',       -- P5 Technicien maintenance
    'quality',           -- P6 Technicien qualité
    'prod_manager',      -- P3 Responsable production
    'director',          -- P4 Directeur
    'process_engineer',  -- P7 Ingénieur process / méthodes
    'admin'              -- P8 Admin RH / paie (AUCUN accès KPI)
  );
exception when duplicate_object then null; end $$;

-- ---------------------------------------------------------------------
-- Les 6 états machine (dérivés, jamais posés par le client)
-- ---------------------------------------------------------------------
do $$ begin
  create type public.post_state as enum (
    'production',      -- vert
    'material_wait',   -- jaune
    'changeover',      -- orange
    'technical_stop',  -- rouge
    'quality_stop',    -- bleu
    'unassigned'       -- gris
  );
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.event_type as enum (
    'technical_stop','quality_stop','material_wait','changeover','other_stop'
  );
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.event_status as enum (
    'declared','notified','acknowledged','on_site','resolved','closed','cancelled'
  );
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.closure_type as enum ('resolved','palliative','no_fault','cancelled');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.escalation_level as enum ('none','level_1','level_2');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.declaration_kind as enum ('production','scrap','rework');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.shift_code as enum ('morning','afternoon','night','custom');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.criticality as enum ('low','medium','high','critical');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.cause_domain as enum ('stop','scrap','root_cause');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.order_status as enum ('planned','in_progress','done','cancelled');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.check_type as enum ('first_part','in_process','final');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.check_result as enum ('ok','rework','scrap');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.notify_channel as enum ('push','sms','in_app','email');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.notify_response as enum ('coming','busy','delegated');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.audit_action as enum ('insert','update','delete','correct');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.oee_mode as enum ('full','lite');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.scope_type as enum ('post','line','zone','site');
exception when duplicate_object then null; end $$;
