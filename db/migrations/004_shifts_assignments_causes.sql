-- =====================================================================
-- OAS · 004 — Equipes, shifts, affectations, sessions, causes, routage
-- =====================================================================

create table if not exists public.shift_templates (
  id               uuid primary key default gen_random_uuid(),
  tenant_id        uuid not null references public.tenants(id) on delete cascade,
  site_id          uuid not null references public.sites(id) on delete cascade,
  code             public.shift_code not null,
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

create table if not exists public.shift_calendar (
  id                 uuid primary key default gen_random_uuid(),
  tenant_id          uuid not null references public.tenants(id) on delete cascade,
  site_id            uuid not null references public.sites(id) on delete cascade,
  shift_template_id  uuid not null references public.shift_templates(id) on delete cascade,
  work_date          date not null,
  is_working_day     boolean not null default true,
  created_at         timestamptz not null default now(),
  unique (tenant_id, site_id, shift_template_id, work_date)
);

create index if not exists idx_shift_calendar_date on public.shift_calendar(tenant_id, work_date);

create table if not exists public.teams (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    uuid not null references public.tenants(id) on delete cascade,
  site_id      uuid not null references public.sites(id) on delete cascade,
  code         text not null,
  name         text not null,
  lead_user_id uuid references public.profiles(id) on delete set null,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.team_members (
  id         uuid primary key default gen_random_uuid(),
  tenant_id  uuid not null references public.tenants(id) on delete cascade,
  team_id    uuid not null references public.teams(id) on delete cascade,
  user_id    uuid not null references public.profiles(id) on delete cascade,
  valid_from date not null default current_date,
  valid_to   date,
  created_at timestamptz not null default now(),
  unique (team_id, user_id, valid_from)
);

-- ---------------------------------------------------------------------
-- Affectation operateur -> poste pour un shift (faite par le chef d'equipe)
-- ---------------------------------------------------------------------
create table if not exists public.assignments (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            uuid not null references public.tenants(id) on delete cascade,
  work_date            date not null,
  shift_template_id    uuid not null references public.shift_templates(id),
  post_id              uuid not null references public.posts(id) on delete cascade,
  user_id              uuid not null references public.profiles(id) on delete cascade,
  production_order_id  uuid references public.production_orders(id) on delete set null,
  assigned_by          uuid references public.profiles(id),
  note                 text,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now(),
  unique (tenant_id, work_date, shift_template_id, post_id, user_id)
);

create index if not exists idx_assignments_user_date on public.assignments(tenant_id, user_id, work_date);
create index if not exists idx_assignments_post_date on public.assignments(tenant_id, post_id, work_date);

-- ---------------------------------------------------------------------
-- Prise de poste reelle (scan QR -> fin de poste)
-- ---------------------------------------------------------------------
create table if not exists public.post_sessions (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           uuid not null references public.tenants(id) on delete cascade,
  client_event_id     uuid not null unique,
  post_id             uuid not null references public.posts(id) on delete cascade,
  user_id             uuid not null references public.profiles(id) on delete cascade,
  assignment_id       uuid references public.assignments(id) on delete set null,
  production_order_id uuid references public.production_orders(id) on delete set null,
  shift_template_id   uuid references public.shift_templates(id),
  started_at          timestamptz not null,
  ended_at            timestamptz,
  started_via         text not null default 'qr'
                        check (started_via in ('qr','manual','biometric','pin')),
  received_at         timestamptz not null default now(),
  created_at          timestamptz not null default now()
);

-- une seule session ouverte par poste, et une seule par operateur
create unique index if not exists uq_open_session_post
  on public.post_sessions(tenant_id, post_id) where ended_at is null;
create unique index if not exists uq_open_session_user
  on public.post_sessions(tenant_id, user_id) where ended_at is null;
create index if not exists idx_sessions_post_started
  on public.post_sessions(tenant_id, post_id, started_at desc);

-- ---------------------------------------------------------------------
-- Arbre de causes (motifs d'arret, causes de rebut, causes racines)
-- ---------------------------------------------------------------------
create table if not exists public.causes (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            uuid not null references public.tenants(id) on delete cascade,
  parent_id            uuid references public.causes(id) on delete cascade,
  domain               public.cause_domain not null,
  code                 text not null,
  label_fr             text not null,
  label_ar             text not null,
  icon                 text,
  event_type           public.event_type,
  default_criticality  public.criticality not null default 'medium',
  sort_order           int not null default 0,
  is_active            boolean not null default true,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now(),
  unique (tenant_id, domain, code)
);

-- ---------------------------------------------------------------------
-- Regles de routage : qui est notifie, sous quel SLA, avec quelle escalade
-- ---------------------------------------------------------------------
create table if not exists public.routing_rules (
  id                   uuid primary key default gen_random_uuid(),
  tenant_id            uuid not null references public.tenants(id) on delete cascade,
  event_type           public.event_type not null,
  cause_id             uuid references public.causes(id) on delete cascade,
  zone_id              uuid references public.zones(id) on delete cascade,
  line_id              uuid references public.lines(id) on delete cascade,
  target_role          public.app_role not null,
  target_team_id       uuid references public.teams(id) on delete set null,
  sla_minutes          int not null default 10,
  escalate_1_after_min int,
  escalate_1_role      public.app_role,
  escalate_2_after_min int,
  escalate_2_role      public.app_role,
  priority             int not null default 0,
  is_active            boolean not null default true,
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now()
);

create index if not exists idx_routing_rules_lookup
  on public.routing_rules(tenant_id, event_type, is_active, priority desc);

-- ---------------------------------------------------------------------
-- Grants + RLS
-- ---------------------------------------------------------------------
select public.apply_tenant_rls('shift_templates', 'admin,process_engineer');
select public.apply_tenant_rls('shift_calendar',  'admin,team_lead,prod_manager');
select public.apply_tenant_rls('teams',           'admin,prod_manager');
select public.apply_tenant_rls('team_members',    'admin,prod_manager,team_lead');
select public.apply_tenant_rls('assignments',     'team_lead,prod_manager');
select public.apply_tenant_rls('causes',          'process_engineer,admin');
select public.apply_tenant_rls('routing_rules',   'process_engineer,prod_manager,admin');

-- post_sessions : l'operateur cree SA session
grant select, insert, update on public.post_sessions to authenticated;
grant all on public.post_sessions to service_role;
alter table public.post_sessions enable row level security;

drop policy if exists sessions_select on public.post_sessions;
create policy sessions_select on public.post_sessions
  for select to authenticated
  using (tenant_id = public.current_tenant_id());

drop policy if exists sessions_insert_self on public.post_sessions;
create policy sessions_insert_self on public.post_sessions
  for insert to authenticated
  with check (tenant_id = public.current_tenant_id() and user_id = auth.uid());

drop policy if exists sessions_update on public.post_sessions;
create policy sessions_update on public.post_sessions
  for update to authenticated
  using (
    tenant_id = public.current_tenant_id()
    and (user_id = auth.uid()
         or public.has_any_role(auth.uid(),
              array['team_lead','prod_manager']::public.app_role[]))
  )
  with check (tenant_id = public.current_tenant_id());

do $$
declare t text;
begin
  foreach t in array array['shift_templates','teams','assignments','causes','routing_rules']
  loop
    execute format('drop trigger if exists trg_%s_updated on public.%I', t, t);
    execute format('create trigger trg_%s_updated before update on public.%I
                    for each row execute function public.set_updated_at()', t, t);
  end loop;
end $$;
