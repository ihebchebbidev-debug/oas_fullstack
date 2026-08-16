-- =====================================================================
-- OAS · 007 — Audit immuable, offline, notifications, pieces jointes
-- =====================================================================

create table if not exists public.audit_log (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    uuid references public.tenants(id) on delete cascade,
  entity_table text not null,
  entity_id    uuid,
  action       public.audit_action not null,
  actor_id     uuid,
  actor_role   public.app_role,
  before       jsonb,
  after        jsonb,
  reason       text,
  occurred_at  timestamptz not null default now(),
  ip           inet,
  user_agent   text
);

create index if not exists idx_audit_entity on public.audit_log(entity_table, entity_id, occurred_at desc);
create index if not exists idx_audit_tenant on public.audit_log(tenant_id, occurred_at desc);

grant select on public.audit_log to authenticated;
grant all on public.audit_log to service_role;
alter table public.audit_log enable row level security;

drop policy if exists audit_select on public.audit_log;
create policy audit_select on public.audit_log
  for select to authenticated
  using (
    tenant_id = public.current_tenant_id()
    and public.has_any_role(auth.uid(), array['admin','director','process_engineer']::public.app_role[])
  );
-- aucune policy insert/update/delete : remplissage par trigger (security definer)

-- ---------------------------------------------------------------------
-- Traçabilite du rejeu hors-ligne (diagnostic)
-- ---------------------------------------------------------------------
create table if not exists public.sync_receipts (
  id              uuid primary key default gen_random_uuid(),
  tenant_id       uuid not null references public.tenants(id) on delete cascade,
  client_event_id uuid not null,
  device_id       text,
  entity          text not null,
  occurred_at     timestamptz not null,
  received_at     timestamptz not null default now(),
  latency_sec     int generated always as
                    (extract(epoch from (received_at - occurred_at))::int) stored,
  attempts        int not null default 1,
  status          text not null default 'ok' check (status in ('ok','duplicate','rejected')),
  error           text
);

create index if not exists idx_sync_client_event on public.sync_receipts(client_event_id);

create table if not exists public.device_tokens (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    uuid not null references public.tenants(id) on delete cascade,
  user_id      uuid not null references public.profiles(id) on delete cascade,
  platform     text not null check (platform in ('android','ios','web')),
  token        text not null unique,
  app_version  text,
  os_version   text,
  last_seen_at timestamptz not null default now(),
  created_at   timestamptz not null default now()
);

create table if not exists public.attachments (
  id           uuid primary key default gen_random_uuid(),
  tenant_id    uuid not null references public.tenants(id) on delete cascade,
  bucket       text not null default 'oas',
  path         text not null,
  mime_type    text,
  size_bytes   bigint,
  entity_table text,
  entity_id    uuid,
  uploaded_by  uuid references public.profiles(id),
  created_at   timestamptz not null default now()
);

create table if not exists public.imports (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   uuid not null references public.tenants(id) on delete cascade,
  kind        text not null,
  file_path   text,
  rows_total  int not null default 0,
  rows_ok     int not null default 0,
  rows_error  int not null default 0,
  report      jsonb not null default '{}'::jsonb,
  imported_by uuid references public.profiles(id),
  created_at  timestamptz not null default now()
);

select public.apply_tenant_rls('sync_receipts', 'admin,process_engineer');
select public.apply_tenant_rls('attachments',   'operator,team_lead,quality,maintenance');
select public.apply_tenant_rls('imports',       'process_engineer,admin');

-- device_tokens : chacun gere les siens
grant select, insert, update, delete on public.device_tokens to authenticated;
grant all on public.device_tokens to service_role;
alter table public.device_tokens enable row level security;

drop policy if exists device_tokens_own on public.device_tokens;
create policy device_tokens_own on public.device_tokens
  for all to authenticated
  using (tenant_id = public.current_tenant_id() and user_id = auth.uid())
  with check (tenant_id = public.current_tenant_id() and user_id = auth.uid());
