-- =====================================================================
-- OAS · 002 — Tenants, profils, rôles, fonctions de sécurité
-- =====================================================================

-- ---------------------------------------------------------------------
-- tenants
-- ---------------------------------------------------------------------
create table if not exists public.tenants (
  id              uuid primary key default gen_random_uuid(),
  name            text not null,
  slug            text not null unique,
  locale_default  text not null default 'fr',
  timezone        text not null default 'Africa/Tunis',
  settings        jsonb not null default '{}'::jsonb,
  is_active       boolean not null default true,
  created_at      timestamptz not null default now(),
  updated_at      timestamptz not null default now()
);

grant select on public.tenants to authenticated;
grant all on public.tenants to service_role;
alter table public.tenants enable row level security;

-- ---------------------------------------------------------------------
-- profiles (miroir de auth.users)
-- ---------------------------------------------------------------------
create table if not exists public.profiles (
  id                  uuid primary key,
  tenant_id           uuid not null references public.tenants(id) on delete cascade,
  full_name           text not null,
  employee_code       text,
  phone               text,
  locale              text not null default 'fr',
  pin_hash            text,
  biometric_enrolled  boolean not null default false,
  is_active           boolean not null default true,
  created_at          timestamptz not null default now(),
  updated_at          timestamptz not null default now(),
  unique (tenant_id, employee_code)
);

create index if not exists idx_profiles_tenant on public.profiles(tenant_id);

grant select, insert, update on public.profiles to authenticated;
grant all on public.profiles to service_role;
alter table public.profiles enable row level security;

-- ---------------------------------------------------------------------
-- user_roles : table SEPAREE (anti elevation de privileges)
-- ---------------------------------------------------------------------
create table if not exists public.user_roles (
  id             uuid primary key default gen_random_uuid(),
  user_id        uuid not null references public.profiles(id) on delete cascade,
  tenant_id      uuid not null references public.tenants(id) on delete cascade,
  role           public.app_role not null,
  scope_site_id  uuid,
  scope_zone_id  uuid,
  created_at     timestamptz not null default now(),
  unique (user_id, role, scope_site_id, scope_zone_id)
);

create index if not exists idx_user_roles_user on public.user_roles(user_id);

grant select on public.user_roles to authenticated;
grant all on public.user_roles to service_role;
alter table public.user_roles enable row level security;

-- =====================================================================
-- Fonctions SECURITY DEFINER (evitent la recursion RLS)
-- =====================================================================

create or replace function public.current_tenant_id()
returns uuid
language sql
stable
security definer
set search_path = public
as $$
  select tenant_id from public.profiles where id = auth.uid()
$$;

create or replace function public.has_role(_user_id uuid, _role public.app_role)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1 from public.user_roles
    where user_id = _user_id and role = _role
  )
$$;

create or replace function public.has_any_role(_user_id uuid, _roles public.app_role[])
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1 from public.user_roles
    where user_id = _user_id and role = any(_roles)
  )
$$;

create or replace function public.can_access_scope(_user_id uuid, _site_id uuid, _zone_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1 from public.user_roles ur
    where ur.user_id = _user_id
      and (ur.scope_site_id is null or ur.scope_site_id = _site_id)
      and (ur.scope_zone_id is null or ur.scope_zone_id = _zone_id)
  )
$$;

-- P8 (admin RH) n'a AUCUN acces aux KPI ni aux declarations nominatives
create or replace function public.can_read_kpi(_user_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select public.has_any_role(
    _user_id,
    array['team_lead','prod_manager','director','process_engineer',
          'maintenance','quality','operator']::public.app_role[]
  )
$$;

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end $$;

-- ---------------------------------------------------------------------
-- Helper : applique grants + RLS multi-tenant standard a une table
-- ---------------------------------------------------------------------
create or replace function public.apply_tenant_rls(
  _table text,
  _write_roles text default 'team_lead,prod_manager,director,process_engineer'
)
returns void
language plpgsql
security definer
set search_path = public
as $fn$
declare
  _roles text := replace(_write_roles, ',', ''',''');
begin
  execute format('grant select, insert, update, delete on public.%I to authenticated', _table);
  execute format('grant all on public.%I to service_role', _table);
  execute format('alter table public.%I enable row level security', _table);

  execute format('drop policy if exists tenant_select on public.%I', _table);
  execute format('create policy tenant_select on public.%I for select to authenticated using (tenant_id = public.current_tenant_id())', _table);

  execute format('drop policy if exists tenant_insert on public.%I', _table);
  execute format('create policy tenant_insert on public.%I for insert to authenticated with check (tenant_id = public.current_tenant_id() and public.has_any_role(auth.uid(), array[''%s'']::public.app_role[]))', _table, _roles);

  execute format('drop policy if exists tenant_update on public.%I', _table);
  execute format('create policy tenant_update on public.%I for update to authenticated using (tenant_id = public.current_tenant_id() and public.has_any_role(auth.uid(), array[''%s'']::public.app_role[])) with check (tenant_id = public.current_tenant_id())', _table, _roles);

  execute format('drop policy if exists tenant_delete on public.%I', _table);
  execute format('create policy tenant_delete on public.%I for delete to authenticated using (tenant_id = public.current_tenant_id() and public.has_any_role(auth.uid(), array[''admin'',''process_engineer'']::public.app_role[]))', _table);
end $fn$;

revoke execute on function public.apply_tenant_rls(text, text) from public, anon, authenticated;

-- =====================================================================
-- Policies du socle
-- =====================================================================
drop policy if exists tenant_self_read on public.tenants;
create policy tenant_self_read on public.tenants
  for select to authenticated
  using (id = public.current_tenant_id());

drop policy if exists profiles_read on public.profiles;
create policy profiles_read on public.profiles
  for select to authenticated
  using (
    id = auth.uid()
    or (
      tenant_id = public.current_tenant_id()
      and public.has_any_role(auth.uid(),
        array['team_lead','prod_manager','director','admin','process_engineer']::public.app_role[])
    )
  );

drop policy if exists profiles_update_self on public.profiles;
create policy profiles_update_self on public.profiles
  for update to authenticated
  using (id = auth.uid())
  with check (id = auth.uid());

drop policy if exists profiles_admin_insert on public.profiles;
create policy profiles_admin_insert on public.profiles
  for insert to authenticated
  with check (
    tenant_id = public.current_tenant_id()
    and public.has_role(auth.uid(), 'admin')
  );

drop policy if exists user_roles_read on public.user_roles;
create policy user_roles_read on public.user_roles
  for select to authenticated
  using (
    user_id = auth.uid()
    or (tenant_id = public.current_tenant_id() and public.has_role(auth.uid(), 'admin'))
  );

drop trigger if exists trg_tenants_updated on public.tenants;
create trigger trg_tenants_updated before update on public.tenants
  for each row execute function public.set_updated_at();

drop trigger if exists trg_profiles_updated on public.profiles;
create trigger trg_profiles_updated before update on public.profiles
  for each row execute function public.set_updated_at();
