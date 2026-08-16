-- =====================================================================
-- OAS · 008 — Triggers : etat derive, SLA, durees, audit, append-only
-- =====================================================================

-- 1) declarations : APPEND-ONLY -------------------------------------------------
create or replace function public.declarations_immutable()
returns trigger language plpgsql as $$
begin
  if tg_op = 'DELETE' then
    raise exception 'declarations est append-only : suppression interdite (audit IATF)';
  end if;
  -- seul le marquage "corrigee" est autorise
  if new.id is distinct from old.id
     or new.quantity_ok is distinct from old.quantity_ok
     or new.quantity_nok is distinct from old.quantity_nok
     or new.occurred_at is distinct from old.occurred_at then
    raise exception 'declarations est append-only : creez une correction (corrects_id)';
  end if;
  return new;
end $$;

drop trigger if exists trg_decl_immutable on public.declarations;
create trigger trg_decl_immutable before update or delete on public.declarations
  for each row execute function public.declarations_immutable();

-- marque l'originale comme corrigee
create or replace function public.declarations_mark_corrected()
returns trigger language plpgsql security definer set search_path = public as $$
begin
  if new.corrects_id is not null then
    update public.declarations set is_corrected = true where id = new.corrects_id;
  end if;
  return new;
end $$;

drop trigger if exists trg_decl_correction on public.declarations;
create trigger trg_decl_correction after insert on public.declarations
  for each row execute function public.declarations_mark_corrected();

-- 2) avancement des OF ----------------------------------------------------------
create or replace function public.declarations_order_progress()
returns trigger language plpgsql security definer set search_path = public as $$
begin
  if new.production_order_id is not null and new.corrects_id is null then
    update public.production_orders
       set quantity_produced = quantity_produced + new.quantity_ok,
           quantity_scrapped = quantity_scrapped
                               + case when new.kind = 'scrap' then new.quantity_nok else 0 end,
           status = case when status = 'planned' then 'in_progress' else status end,
           updated_at = now()
     where id = new.production_order_id;
  end if;
  return new;
end $$;

drop trigger if exists trg_decl_order_progress on public.declarations;
create trigger trg_decl_order_progress after insert on public.declarations
  for each row execute function public.declarations_order_progress();

-- 3) SLA a la declaration -------------------------------------------------------
create or replace function public.events_apply_sla()
returns trigger language plpgsql security definer set search_path = public as $$
declare r record; p record;
begin
  select l.id as line_id, z.id as zone_id, s.id as site_id
    into p
    from public.posts po
    join public.lines l on l.id = po.line_id
    join public.zones z on z.id = l.zone_id
    join public.sites s on s.id = z.site_id
   where po.id = new.post_id;

  new.line_id := coalesce(new.line_id, p.line_id);
  new.zone_id := coalesce(new.zone_id, p.zone_id);
  new.site_id := coalesce(new.site_id, p.site_id);

  select * into r
    from public.routing_rules rr
   where rr.tenant_id = new.tenant_id
     and rr.is_active
     and rr.event_type = new.event_type
     and (rr.cause_id is null or rr.cause_id = new.cause_id)
     and (rr.zone_id  is null or rr.zone_id  = new.zone_id)
     and (rr.line_id  is null or rr.line_id  = new.line_id)
   order by rr.priority desc
   limit 1;

  if found then
    new.sla_minutes := r.sla_minutes;
  end if;

  new.sla_due_at := new.declared_at + make_interval(mins => new.sla_minutes);
  return new;
end $$;

drop trigger if exists trg_events_sla on public.events;
create trigger trg_events_sla before insert on public.events
  for each row execute function public.events_apply_sla();

-- 4) durees + journal de transition ---------------------------------------------
create or replace function public.events_track_status()
returns trigger language plpgsql security definer set search_path = public as $$
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

drop trigger if exists trg_events_durations on public.events;
create trigger trg_events_durations before update on public.events
  for each row execute function public.events_track_status();

create or replace function public.events_log_transition()
returns trigger language plpgsql security definer set search_path = public as $$
begin
  if tg_op = 'INSERT' then
    insert into public.event_transitions(tenant_id, event_id, from_status, to_status, actor_id, occurred_at)
    values (new.tenant_id, new.id, null, new.status, new.declared_by, new.declared_at);
  elsif new.status is distinct from old.status then
    insert into public.event_transitions(tenant_id, event_id, from_status, to_status, actor_id, occurred_at)
    values (new.tenant_id, new.id, old.status, new.status, auth.uid(), now());
  end if;
  return new;
end $$;

drop trigger if exists trg_events_transition on public.events;
create trigger trg_events_transition after insert or update on public.events
  for each row execute function public.events_log_transition();

-- 5) ETAT DERIVE DU POSTE -------------------------------------------------------
-- priorite : technical > quality > material > changeover > production > unassigned
create or replace function public.recompute_post_state(_post_id uuid)
returns void language plpgsql security definer set search_path = public as $$
declare
  _tenant uuid; _state public.post_state := 'unassigned';
  _ev record; _co record; _se record; _prev public.post_state;
begin
  select tenant_id into _tenant from public.posts where id = _post_id;
  if _tenant is null then return; end if;

  select * into _se from public.post_sessions
   where post_id = _post_id and ended_at is null limit 1;
  select * into _co from public.changeovers
   where post_id = _post_id and ended_at is null limit 1;
  select * into _ev from public.events
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
                else 'changeover' end::public.post_state;
  elsif _co.id is not null then
    _state := 'changeover';
  elsif _se.id is not null then
    _state := 'production';
  end if;

  select state into _prev from public.post_states where post_id = _post_id;

  insert into public.post_states(post_id, tenant_id, state, since, active_event_id,
                                 active_session_id, active_changeover_id,
                                 current_user_id, current_order_id, updated_at)
  values (_post_id, _tenant, _state, now(), _ev.id, _se.id, _co.id,
          _se.user_id, _se.production_order_id, now())
  on conflict (post_id) do update
    set state                = excluded.state,
        since                = case when public.post_states.state = excluded.state
                                    then public.post_states.since else now() end,
        active_event_id      = excluded.active_event_id,
        active_session_id    = excluded.active_session_id,
        active_changeover_id = excluded.active_changeover_id,
        current_user_id      = excluded.current_user_id,
        current_order_id     = excluded.current_order_id,
        updated_at           = now();

  if _prev is distinct from _state then
    update public.post_state_history
       set ended_at = now(),
           duration_sec = extract(epoch from (now() - started_at))::int
     where post_id = _post_id and ended_at is null;

    insert into public.post_state_history(tenant_id, post_id, state, started_at, event_id, session_id)
    values (_tenant, _post_id, _state, now(), _ev.id, _se.id);
  end if;
end $$;

create or replace function public.trg_recompute_post_state()
returns trigger language plpgsql security definer set search_path = public as $$
begin
  perform public.recompute_post_state(coalesce(new.post_id, old.post_id));
  return coalesce(new, old);
end $$;

drop trigger if exists trg_state_events on public.events;
create trigger trg_state_events after insert or update on public.events
  for each row execute function public.trg_recompute_post_state();

drop trigger if exists trg_state_sessions on public.post_sessions;
create trigger trg_state_sessions after insert or update on public.post_sessions
  for each row execute function public.trg_recompute_post_state();

drop trigger if exists trg_state_changeovers on public.changeovers;
create trigger trg_state_changeovers after insert or update on public.changeovers
  for each row execute function public.trg_recompute_post_state();

-- 6) AUDIT generique ------------------------------------------------------------
create or replace function public.audit_row()
returns trigger language plpgsql security definer set search_path = public as $$
declare _tenant uuid;
begin
  begin
    _tenant := case when tg_op = 'DELETE' then (to_jsonb(old)->>'tenant_id')::uuid
                    else (to_jsonb(new)->>'tenant_id')::uuid end;
  exception when others then _tenant := null; end;

  insert into public.audit_log(tenant_id, entity_table, entity_id, action, actor_id, before, after)
  values (
    _tenant, tg_table_name,
    case when tg_op = 'DELETE' then old.id else new.id end,
    lower(tg_op)::public.audit_action,
    auth.uid(),
    case when tg_op in ('UPDATE','DELETE') then to_jsonb(old) end,
    case when tg_op in ('INSERT','UPDATE') then to_jsonb(new) end
  );
  return coalesce(new, old);
end $$;

do $$
declare t text;
begin
  foreach t in array array['declarations','events','changeovers','quality_checks',
                           'assignments','post_sessions','user_roles','routing_rules',
                           'routings','production_orders','posts']
  loop
    execute format('drop trigger if exists trg_audit_%s on public.%I', t, t);
    execute format('create trigger trg_audit_%s after insert or update or delete on public.%I
                    for each row execute function public.audit_row()', t, t);
  end loop;
end $$;

-- 7) Job SLA : a appeler toutes les minutes (pg_cron ou edge function) ----------
create or replace function public.job_check_sla()
returns int language plpgsql security definer set search_path = public as $$
declare _n int;
begin
  with breached as (
    update public.events
       set sla_breached = true,
           escalation_level = case
             when now() > sla_due_at + make_interval(mins => sla_minutes) then 'level_2'
             else 'level_1' end::public.escalation_level
     where status not in ('closed','cancelled','resolved')
       and sla_due_at < now()
       and (sla_breached = false or escalation_level <> 'level_2')
    returning 1
  )
  select count(*) into _n from breached;
  return _n;
end $$;

revoke execute on function public.job_check_sla() from public, anon, authenticated;
