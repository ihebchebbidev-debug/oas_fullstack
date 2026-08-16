-- =====================================================================
-- OAS · 005 — Declarations (append-only) + moteur d'evenements
-- =====================================================================

-- ---------------------------------------------------------------------
-- declarations : production / rebut / retouche
-- APPEND-ONLY : aucune modification, une correction cree une nouvelle ligne
-- ---------------------------------------------------------------------
create table if not exists public.declarations (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           uuid not null references public.tenants(id) on delete cascade,
  client_event_id     uuid not null unique,
  kind                public.declaration_kind not null,
  post_session_id     uuid references public.post_sessions(id) on delete set null,
  post_id             uuid not null references public.posts(id),
  line_id             uuid references public.lines(id),
  user_id             uuid not null references public.profiles(id),
  production_order_id uuid references public.production_orders(id),
  product_id          uuid references public.products(id),
  quantity_ok         numeric(12,2) not null default 0,
  quantity_nok        numeric(12,2) not null default 0,
  scrap_cause_id      uuid references public.causes(id),
  photo_path          text,
  note                text,
  occurred_at         timestamptz not null,   -- horodatage A LA SAISIE (offline)
  received_at         timestamptz not null default now(),
  corrects_id         uuid references public.declarations(id),
  is_corrected        boolean not null default false,
  correction_reason   text,
  created_by          uuid not null references public.profiles(id),
  check (quantity_ok >= 0 and quantity_nok >= 0)
);

create index if not exists idx_decl_post_time on public.declarations(tenant_id, post_id, occurred_at desc);
create index if not exists idx_decl_order     on public.declarations(tenant_id, production_order_id);
create index if not exists idx_decl_kind_time on public.declarations(tenant_id, kind, occurred_at desc);
create index if not exists idx_decl_session   on public.declarations(post_session_id);

-- ---------------------------------------------------------------------
-- events : le moteur d'evenements
-- declared -> notified -> acknowledged -> on_site -> resolved -> closed
-- ---------------------------------------------------------------------
create table if not exists public.events (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           uuid not null references public.tenants(id) on delete cascade,
  client_event_id     uuid not null unique,
  event_type          public.event_type not null,
  status              public.event_status not null default 'declared',

  post_id             uuid not null references public.posts(id),
  line_id             uuid references public.lines(id),
  zone_id             uuid references public.zones(id),
  site_id             uuid references public.sites(id),

  post_session_id     uuid references public.post_sessions(id) on delete set null,
  production_order_id uuid references public.production_orders(id),
  product_id          uuid references public.products(id),
  equipment_id        uuid references public.equipments(id),

  cause_id            uuid references public.causes(id),        -- motif declare
  root_cause_id       uuid references public.causes(id),        -- cause racine (cloture)
  criticality         public.criticality not null default 'medium',

  declared_by         uuid not null references public.profiles(id),
  declared_at         timestamptz not null,
  notified_at         timestamptz,
  acknowledged_at     timestamptz,
  acknowledged_by     uuid references public.profiles(id),
  eta_minutes         int,
  on_site_at          timestamptz,
  resolved_at         timestamptz,
  resolved_by         uuid references public.profiles(id),
  closure_type        public.closure_type,
  closure_note        text,
  closed_at           timestamptz,
  closed_by           uuid references public.profiles(id),
  cancelled_at        timestamptz,
  cancel_reason       text,

  sla_minutes         int not null default 10,
  sla_due_at          timestamptz,
  sla_breached        boolean not null default false,
  escalation_level    public.escalation_level not null default 'none',

  duration_sec        int,   -- declared -> closed
  response_sec        int,   -- declared -> on_site
  repair_sec          int,   -- on_site  -> resolved

  note                text,
  received_at         timestamptz not null default now(),
  updated_at          timestamptz not null default now()
);

create index if not exists idx_events_open
  on public.events(tenant_id, status)
  where status not in ('closed','cancelled');
create index if not exists idx_events_post_time on public.events(tenant_id, post_id, declared_at desc);
create index if not exists idx_events_type_time on public.events(tenant_id, event_type, declared_at desc);
create index if not exists idx_events_sla
  on public.events(tenant_id, sla_due_at)
  where sla_breached = false and status not in ('closed','cancelled','resolved');
create index if not exists idx_events_equipment on public.events(tenant_id, equipment_id, declared_at desc);

-- un seul evenement bloquant ouvert par poste
create unique index if not exists uq_open_blocking_event
  on public.events(tenant_id, post_id)
  where status not in ('closed','cancelled')
    and event_type in ('technical_stop','quality_stop','material_wait');

-- ---------------------------------------------------------------------
-- Journal immuable des transitions
-- ---------------------------------------------------------------------
create table if not exists public.event_transitions (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   uuid not null references public.tenants(id) on delete cascade,
  event_id    uuid not null references public.events(id) on delete cascade,
  from_status public.event_status,
  to_status   public.event_status not null,
  actor_id    uuid references public.profiles(id),
  actor_role  public.app_role,
  payload     jsonb not null default '{}'::jsonb,
  occurred_at timestamptz not null default now(),
  received_at timestamptz not null default now()
);

create index if not exists idx_transitions_event on public.event_transitions(event_id, occurred_at);

-- ---------------------------------------------------------------------
-- Notifications envoyees pour un evenement
-- ---------------------------------------------------------------------
create table if not exists public.event_notifications (
  id                uuid primary key default gen_random_uuid(),
  tenant_id         uuid not null references public.tenants(id) on delete cascade,
  event_id          uuid not null references public.events(id) on delete cascade,
  recipient_user_id uuid references public.profiles(id) on delete cascade,
  recipient_role    public.app_role,
  channel           public.notify_channel not null default 'push',
  escalation_level  public.escalation_level not null default 'none',
  sent_at           timestamptz not null default now(),
  delivered_at      timestamptz,
  read_at           timestamptz,
  responded_at      timestamptz,
  response          public.notify_response,
  eta_minutes       int
);

create index if not exists idx_notif_recipient
  on public.event_notifications(tenant_id, recipient_user_id, sent_at desc);
create index if not exists idx_notif_event on public.event_notifications(event_id);

-- ---------------------------------------------------------------------
-- Changements de serie (chronometres jusqu'a la 1ere piece bonne)
-- ---------------------------------------------------------------------
create table if not exists public.changeovers (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           uuid not null references public.tenants(id) on delete cascade,
  client_event_id     uuid not null unique,
  post_id             uuid not null references public.posts(id) on delete cascade,
  from_product_id     uuid references public.products(id),
  to_product_id       uuid not null references public.products(id),
  production_order_id uuid references public.production_orders(id),
  event_id            uuid references public.events(id) on delete set null,
  started_at          timestamptz not null,
  first_good_part_at  timestamptz,
  ended_at            timestamptz,
  duration_sec        int,
  target_min          int,
  started_by          uuid not null references public.profiles(id),
  validated_by        uuid references public.profiles(id),
  received_at         timestamptz not null default now()
);

create unique index if not exists uq_open_changeover
  on public.changeovers(tenant_id, post_id) where ended_at is null;
create index if not exists idx_changeover_post on public.changeovers(tenant_id, post_id, started_at desc);

-- ---------------------------------------------------------------------
-- Controles qualite (dont controle 1ere piece obligatoire apres changement)
-- ---------------------------------------------------------------------
create table if not exists public.quality_checks (
  id                  uuid primary key default gen_random_uuid(),
  tenant_id           uuid not null references public.tenants(id) on delete cascade,
  client_event_id     uuid not null unique,
  post_id             uuid not null references public.posts(id) on delete cascade,
  production_order_id uuid references public.production_orders(id),
  product_id          uuid references public.products(id),
  changeover_id       uuid references public.changeovers(id) on delete set null,
  event_id            uuid references public.events(id) on delete set null,
  check_type          public.check_type not null,
  result              public.check_result not null,
  quantity_checked    numeric(12,2) not null default 1,
  quantity_rejected   numeric(12,2) not null default 0,
  cause_id            uuid references public.causes(id),
  inspector_id        uuid not null references public.profiles(id),
  photo_path          text,
  note                text,
  occurred_at         timestamptz not null,
  received_at         timestamptz not null default now()
);

create index if not exists idx_qc_post_time on public.quality_checks(tenant_id, post_id, occurred_at desc);

-- =====================================================================
-- Grants + RLS
-- =====================================================================

-- declarations : lecture tenant, insert par l'auteur, AUCUN update/delete
grant select, insert on public.declarations to authenticated;
grant all on public.declarations to service_role;
alter table public.declarations enable row level security;

drop policy if exists decl_select on public.declarations;
create policy decl_select on public.declarations
  for select to authenticated
  using (
    tenant_id = public.current_tenant_id()
    and not public.has_role(auth.uid(), 'admin')  -- P8 RH : pas de donnee nominative
  );

drop policy if exists decl_insert on public.declarations;
create policy decl_insert on public.declarations
  for insert to authenticated
  with check (
    tenant_id = public.current_tenant_id()
    and created_by = auth.uid()
  );

-- events
grant select, insert, update on public.events to authenticated;
grant all on public.events to service_role;
alter table public.events enable row level security;

drop policy if exists events_select on public.events;
create policy events_select on public.events
  for select to authenticated
  using (tenant_id = public.current_tenant_id());

drop policy if exists events_insert on public.events;
create policy events_insert on public.events
  for insert to authenticated
  with check (
    tenant_id = public.current_tenant_id()
    and declared_by = auth.uid()
  );

drop policy if exists events_update on public.events;
create policy events_update on public.events
  for update to authenticated
  using (
    tenant_id = public.current_tenant_id()
    and public.has_any_role(auth.uid(),
      array['operator','team_lead','maintenance','quality','prod_manager']::public.app_role[])
  )
  with check (tenant_id = public.current_tenant_id());

-- transitions : insert par trigger, jamais modifiable
grant select on public.event_transitions to authenticated;
grant all on public.event_transitions to service_role;
alter table public.event_transitions enable row level security;

drop policy if exists transitions_select on public.event_transitions;
create policy transitions_select on public.event_transitions
  for select to authenticated
  using (tenant_id = public.current_tenant_id());

-- notifications : chacun voit les siennes, l'encadrement voit tout
grant select, update on public.event_notifications to authenticated;
grant all on public.event_notifications to service_role;
alter table public.event_notifications enable row level security;

drop policy if exists notif_select on public.event_notifications;
create policy notif_select on public.event_notifications
  for select to authenticated
  using (
    tenant_id = public.current_tenant_id()
    and (recipient_user_id = auth.uid()
         or public.has_any_role(auth.uid(),
              array['team_lead','prod_manager','director']::public.app_role[]))
  );

drop policy if exists notif_update_self on public.event_notifications;
create policy notif_update_self on public.event_notifications
  for update to authenticated
  using (tenant_id = public.current_tenant_id() and recipient_user_id = auth.uid())
  with check (tenant_id = public.current_tenant_id());

select public.apply_tenant_rls('changeovers',    'operator,team_lead,prod_manager');
select public.apply_tenant_rls('quality_checks', 'quality,team_lead,prod_manager');
