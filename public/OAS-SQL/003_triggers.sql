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
--
-- Run after 001_schema.sql and 002_indexes.sql.
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
