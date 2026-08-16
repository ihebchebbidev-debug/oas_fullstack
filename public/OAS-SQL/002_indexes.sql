-- =====================================================================
-- OAS · 002 — Indexes (spec §5.0: idx_oas_* naming, all internal to the
-- OAS module). Run after 001_schema.sql.
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
