-- =====================================================================
-- OAS · 003 — Referentiel : site > zone > ligne > poste > equipement
--                + produits, gammes, ordres de fabrication
-- =====================================================================

create table if not exists public.sites (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   uuid not null references public.tenants(id) on delete cascade,
  code        text not null,
  name        text not null,
  timezone    text not null default 'Africa/Tunis',
  address     text,
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.zones (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   uuid not null references public.tenants(id) on delete cascade,
  site_id     uuid not null references public.sites(id) on delete cascade,
  code        text not null,
  name        text not null,
  sort_order  int not null default 0,
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.lines (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   uuid not null references public.tenants(id) on delete cascade,
  zone_id     uuid not null references public.zones(id) on delete cascade,
  code        text not null,
  name        text not null,
  sort_order  int not null default 0,
  target_oee  numeric(5,2),
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, code)
);

create table if not exists public.posts (
  id            uuid primary key default gen_random_uuid(),
  tenant_id     uuid not null references public.tenants(id) on delete cascade,
  line_id       uuid not null references public.lines(id) on delete cascade,
  code          text not null,
  name          text not null,
  qr_token      text not null unique,
  qr_rotated_at timestamptz,
  sort_order    int not null default 0,
  is_active     boolean not null default true,
  archived_at   timestamptz,
  created_at    timestamptz not null default now(),
  updated_at    timestamptz not null default now(),
  unique (tenant_id, code)
);

create index if not exists idx_posts_line on public.posts(line_id);
create index if not exists idx_posts_qr on public.posts(qr_token);

create table if not exists public.equipments (
  id             uuid primary key default gen_random_uuid(),
  tenant_id      uuid not null references public.tenants(id) on delete cascade,
  post_id        uuid references public.posts(id) on delete set null,
  code           text not null,
  name           text not null,
  serial_number  text,
  manufacturer   text,
  commissioned_at date,
  criticality    public.criticality not null default 'medium',
  archived_at    timestamptz,
  created_at     timestamptz not null default now(),
  updated_at     timestamptz not null default now(),
  unique (tenant_id, code)
);

-- ---------------------------------------------------------------------
-- Produits / gammes / OF
-- ---------------------------------------------------------------------
create table if not exists public.products (
  id          uuid primary key default gen_random_uuid(),
  tenant_id   uuid not null references public.tenants(id) on delete cascade,
  reference   text not null,
  name        text not null,
  customer    text,
  unit        text not null default 'pcs',
  archived_at timestamptz,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),
  unique (tenant_id, reference)
);

-- cycle_time_sec NULL  =>  mode TRS-lite (PC-3)
create table if not exists public.routings (
  id                        uuid primary key default gen_random_uuid(),
  tenant_id                 uuid not null references public.tenants(id) on delete cascade,
  product_id                uuid not null references public.products(id) on delete cascade,
  post_id                   uuid not null references public.posts(id) on delete cascade,
  cycle_time_sec            numeric(10,3),
  theoretical_rate_per_hour numeric(10,2),
  changeover_target_min     int,
  operators_required        int not null default 1,
  created_at                timestamptz not null default now(),
  updated_at                timestamptz not null default now(),
  unique (tenant_id, product_id, post_id)
);

create table if not exists public.production_orders (
  id                 uuid primary key default gen_random_uuid(),
  tenant_id          uuid not null references public.tenants(id) on delete cascade,
  order_number       text not null,
  product_id         uuid not null references public.products(id),
  line_id            uuid references public.lines(id),
  quantity_planned   numeric(12,2) not null default 0,
  quantity_produced  numeric(12,2) not null default 0,  -- derive
  quantity_scrapped  numeric(12,2) not null default 0,  -- derive
  due_date           date,
  status             public.order_status not null default 'planned',
  priority           int not null default 0,
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  unique (tenant_id, order_number)
);

create index if not exists idx_orders_line_status on public.production_orders(tenant_id, line_id, status);

-- ---------------------------------------------------------------------
-- Grants + RLS
-- ---------------------------------------------------------------------
select public.apply_tenant_rls('sites',             'admin,process_engineer,director');
select public.apply_tenant_rls('zones',             'admin,process_engineer,director');
select public.apply_tenant_rls('lines',             'admin,process_engineer,director');
select public.apply_tenant_rls('posts',             'admin,process_engineer,director');
select public.apply_tenant_rls('equipments',        'admin,process_engineer,maintenance');
select public.apply_tenant_rls('products',          'admin,process_engineer');
select public.apply_tenant_rls('routings',          'admin,process_engineer');
select public.apply_tenant_rls('production_orders', 'process_engineer,prod_manager,team_lead');

do $$
declare t text;
begin
  foreach t in array array['sites','zones','lines','posts','equipments',
                           'products','routings','production_orders']
  loop
    execute format('drop trigger if exists trg_%s_updated on public.%I', t, t);
    execute format('create trigger trg_%s_updated before update on public.%I
                    for each row execute function public.set_updated_at()', t, t);
  end loop;
end $$;
