-- =====================================================================
-- OAS · 006 — Etat derive des postes, historique, KPI
-- =====================================================================

-- ---------------------------------------------------------------------
-- post_states : 1 ligne par poste, etat courant (alimente le plan atelier)
-- ---------------------------------------------------------------------
create table if not exists public.post_states (
  post_id            uuid primary key references public.posts(id) on delete cascade,
  tenant_id          uuid not null references public.tenants(id) on delete cascade,
  state              public.post_state not null default 'unassigned',
  since              timestamptz not null default now(),
  active_event_id    uuid references public.events(id) on delete set null,
  active_session_id  uuid references public.post_sessions(id) on delete set null,
  active_changeover_id uuid references public.changeovers(id) on delete set null,
  current_user_id    uuid references public.profiles(id) on delete set null,
  current_product_id uuid references public.products(id) on delete set null,
  current_order_id   uuid references public.production_orders(id) on delete set null,
  updated_at         timestamptz not null default now()
);

create index if not exists idx_post_states_tenant on public.post_states(tenant_id, state);

-- ---------------------------------------------------------------------
-- Historique des etats : base de calcul de la disponibilite
-- ---------------------------------------------------------------------
create table if not exists public.post_state_history (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    uuid not null references public.tenants(id) on delete cascade,
  post_id      uuid not null references public.posts(id) on delete cascade,
  state        public.post_state not null,
  started_at   timestamptz not null,
  ended_at     timestamptz,
  duration_sec int,
  event_id     uuid references public.events(id) on delete set null,
  session_id   uuid references public.post_sessions(id) on delete set null
);

create index if not exists idx_state_hist_post
  on public.post_state_history(tenant_id, post_id, started_at desc);
create unique index if not exists uq_state_hist_open
  on public.post_state_history(post_id) where ended_at is null;

-- ---------------------------------------------------------------------
-- kpi_daily : agregat recalcule par job
-- TRS-lite : si theoretical_qty est NULL -> performance NULL, oee = dispo x qualite
-- ---------------------------------------------------------------------
create table if not exists public.kpi_daily (
  id                 uuid primary key default gen_random_uuid(),
  tenant_id          uuid not null references public.tenants(id) on delete cascade,
  scope_type         public.scope_type not null,
  scope_id           uuid not null,
  work_date          date not null,
  shift_template_id  uuid references public.shift_templates(id) on delete set null,

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
  oee_mode           public.oee_mode not null default 'lite',

  stops_count        int not null default 0,
  mtbf_sec           int,
  mttr_sec           int,

  computed_at        timestamptz not null default now()
);

create unique index if not exists uq_kpi_daily
  on public.kpi_daily(tenant_id, scope_type, scope_id, work_date,
                      coalesce(shift_template_id, '00000000-0000-0000-0000-000000000000'::uuid));
create index if not exists idx_kpi_daily_date on public.kpi_daily(tenant_id, work_date desc);

-- =====================================================================
-- Grants + RLS
-- =====================================================================
grant select on public.post_states to authenticated;
grant all on public.post_states to service_role;
alter table public.post_states enable row level security;

drop policy if exists post_states_select on public.post_states;
create policy post_states_select on public.post_states
  for select to authenticated
  using (tenant_id = public.current_tenant_id());

grant select on public.post_state_history to authenticated;
grant all on public.post_state_history to service_role;
alter table public.post_state_history enable row level security;

drop policy if exists state_hist_select on public.post_state_history;
create policy state_hist_select on public.post_state_history
  for select to authenticated
  using (tenant_id = public.current_tenant_id());

-- kpi_daily : ecriture service_role uniquement ; P8 (admin RH) exclu en lecture
grant select on public.kpi_daily to authenticated;
grant all on public.kpi_daily to service_role;
alter table public.kpi_daily enable row level security;

drop policy if exists kpi_select on public.kpi_daily;
create policy kpi_select on public.kpi_daily
  for select to authenticated
  using (
    tenant_id = public.current_tenant_id()
    and public.can_read_kpi(auth.uid())
  );

-- =====================================================================
-- Vues
-- =====================================================================

create or replace view public.v_shopfloor_map as
select
  p.id            as post_id,
  p.tenant_id,
  p.code          as post_code,
  p.name          as post_name,
  l.id            as line_id,
  l.code          as line_code,
  l.name          as line_name,
  z.id            as zone_id,
  z.name          as zone_name,
  s.id            as site_id,
  coalesce(ps.state, 'unassigned'::public.post_state) as state,
  ps.since,
  ps.current_user_id,
  pr.reference    as current_reference,
  ps.current_order_id,
  ps.active_event_id,
  e.event_type    as active_event_type,
  e.sla_due_at,
  e.sla_breached,
  p.sort_order,
  l.sort_order    as line_sort_order
from public.posts p
join public.lines l on l.id = p.line_id
join public.zones z on z.id = l.zone_id
join public.sites s on s.id = z.site_id
left join public.post_states ps on ps.post_id = p.id
left join public.products pr on pr.id = ps.current_product_id
left join public.events e on e.id = ps.active_event_id
where p.archived_at is null and p.is_active;

grant select on public.v_shopfloor_map to authenticated;

create or replace view public.v_open_events as
select
  e.*,
  p.code as post_code,
  l.code as line_code,
  c.label_fr as cause_label_fr,
  c.label_ar as cause_label_ar,
  greatest(0, extract(epoch from (e.sla_due_at - now()))::int) as sla_remaining_sec
from public.events e
join public.posts p on p.id = e.post_id
left join public.lines l on l.id = e.line_id
left join public.causes c on c.id = e.cause_id
where e.status not in ('closed','cancelled');

grant select on public.v_open_events to authenticated;

create or replace view public.v_pareto_causes as
select
  e.tenant_id,
  e.event_type,
  e.cause_id,
  c.label_fr,
  c.label_ar,
  date_trunc('day', e.declared_at) as day,
  count(*)                          as occurrences,
  coalesce(sum(e.duration_sec), 0)  as total_downtime_sec
from public.events e
left join public.causes c on c.id = e.cause_id
where e.status = 'closed'
group by 1,2,3,4,5,6;

grant select on public.v_pareto_causes to authenticated;

create or replace view public.v_equipment_reliability as
select
  eq.tenant_id,
  eq.id                        as equipment_id,
  eq.code,
  eq.name,
  count(e.id)                  as failures,
  avg(e.repair_sec)::int       as mttr_sec,
  case when count(e.id) > 1
       then (extract(epoch from (max(e.declared_at) - min(e.declared_at))) / (count(e.id) - 1))::int
  end                          as mtbf_sec
from public.equipments eq
left join public.events e
       on e.equipment_id = eq.id
      and e.event_type = 'technical_stop'
      and e.status = 'closed'
group by 1,2,3,4;

grant select on public.v_equipment_reliability to authenticated;
