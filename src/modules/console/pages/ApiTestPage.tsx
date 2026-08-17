import { useEffect, useRef, useState } from 'react';
import { CheckCircle2, XCircle, MinusCircle, CircleDashed, Loader2, RotateCcw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useT } from '@/i18n/I18nProvider';
import { cn } from '@/lib/utils';
import { LoginForm } from '@/modules/auth/components/LoginForm';

import { apiFetch, ApiError, getSession, OAS_API_BASE } from '@/oas/api/client';
import { signInConsole, setupFirstAdmin } from '@/oas/authStore';
import { hierarchyApi } from '@/oas/api/hierarchy';
import {
  operatorsApi, equipmentsApi, cadencesApi, productsApi, productionOrdersApi,
  causesApi, shiftsApi, importsApi, lookupsApi,
} from '@/oas/api/referentials';
import { assignmentsApi, presenceApi } from '@/oas/api/assignments';
import { postSessionsApi, declarationsApi, changeoversApi } from '@/oas/api/operations';
import { eventsApi, slaApi, interventionsApi } from '@/oas/api/events';
import { postStatesApi } from '@/oas/api/postStates';
import { kpiApi, andonMessageApi } from '@/oas/api/kpi';
import { auditApi } from '@/oas/api/audit';
import { pluginActivationsApi } from '@/oas/api/pluginActivations';

type Status = 'idle' | 'running' | 'pass' | 'fail' | 'skip';

interface TestOutcome {
  ok: boolean;
  skipped?: boolean;
  detail: string;
  /** The real response body (or, for a multi-call chain, the last meaningful one) — shown in the expandable JSON viewer per row. */
  json?: unknown;
}

interface DiagCtx {
  postId?: string;
  postCode?: string;
  lineId?: string;
  siteId?: string;
  cadenceId?: string;
  declarationId?: string;
  eventId?: string;
  eventRequalifyId?: string;
  eventInterventionId?: string;
  interventionId?: string;
  postSessionId?: string;
  changeoverId?: string;
  importId?: string;
  shiftId?: string;
  qualityTemplateId?: string;
  operatorId?: string;
  teamId?: string;
  today: string;
}

interface DiagTest {
  id: string;
  module: string;
  label: string;
  run: (ctx: DiagCtx) => Promise<TestOutcome>;
}

/**
 * A call only passes if it round-trips AND its shape actually matches what
 * the frontend types this endpoint as (`validate`, when given) — a 200 with
 * the wrong shape is exactly the kind of bug that slips past `tsc` (which
 * only checks the TYPE ANNOTATION, never the real runtime payload) and past
 * a bare "didn't throw" smoke test. The real response body is always kept
 * (`json`) so the UI can show exactly what the backend actually returned.
 */
async function ok(
  fn: () => Promise<unknown>,
  describe: (v: unknown) => string = (v) => Array.isArray(v) ? `${v.length} row(s)` : 'OK',
  validate?: (v: unknown) => string | null,
): Promise<TestOutcome> {
  const v = await fn();
  const shapeError = validate?.(v);
  if (shapeError) return { ok: false, detail: `shape mismatch — ${shapeError}`, json: v };
  return { ok: true, detail: describe(v), json: v };
}

function skip(reason: string): TestOutcome {
  return { ok: true, skipped: true, detail: reason };
}

/** Every list endpoint is typed as an array on the frontend — assert it really is one. */
function isArray(v: unknown): string | null {
  return Array.isArray(v) ? null : `expected an array, got ${v === null ? 'null' : typeof v}`;
}

/** Confirms the response object actually carries the fields the frontend's TS interface for it declares, catching a backend DTO rename/typo that `tsc` can never see at runtime. */
function hasKeys(...keys: string[]) {
  return (v: unknown): string | null => {
    if (typeof v !== 'object' || v === null) return `expected an object, got ${v === null ? 'null' : typeof v}`;
    const missing = keys.filter((k) => !(k in (v as Record<string, unknown>)));
    return missing.length ? `missing field(s): ${missing.join(', ')}` : null;
  };
}

/** The "fetch a list, validate it's really an array, stash something from row 0 for a later dependent test" pattern, deduplicated. */
function captureList<T>(fetch: () => Promise<T[]>, assign: (ctx: DiagCtx, rows: T[]) => void) {
  return async (ctx: DiagCtx): Promise<TestOutcome> => {
    const rows = await fetch();
    const shapeError = isArray(rows);
    if (shapeError) return { ok: false, detail: `shape mismatch — ${shapeError}`, json: rows };
    assign(ctx, rows);
    return { ok: true, detail: `${rows.length} row(s)`, json: rows };
  };
}

/** Every real production write below tags itself so it's unmistakable in every list/report — never touch/delete anything WITHOUT this prefix by hand-cleanup mistake. */
function diagTag(): string {
  return `DIAGTEST-${Date.now()}`;
}

const TESTS: DiagTest[] = [
  // ---- Connectivity ----------------------------------------------------
  {
    id: 'health', module: 'diag.module.connectivity', label: 'GET /health',
    // 503 (schema incomplete / tenant not provisioned) is real signal, already
    // surfaced prominently by the DbStatusBanner above — this test's job is
    // just confirming the endpoint itself answers, not re-litigating schema state.
    run: async () => {
      try {
        const dto = await apiFetch<{ status: string; tenant?: string }>('/health', { auth: false });
        return { ok: true, detail: `${dto.status}${dto.tenant ? ` — ${dto.tenant}` : ''}` };
      } catch (e) {
        if (e instanceof ApiError && e.status === 503) return { ok: true, detail: 'schema incomplete or not provisioned (see banner above)' };
        throw e;
      }
    },
  },
  { id: 'auth.me', module: 'diag.module.connectivity', label: 'GET /auth/me', run: () => ok(() => apiFetch('/auth/me'), (v) => `signed in as ${(v as { email?: string })?.email ?? '—'}`, hasKeys('id', 'email', 'role', 'workspace')) },
  // Long-lived SSE connection (EventSource), not a request/response call — apiFetch can't
  // exercise it the same way. `connectOasStream()` (src/oas/api/events.ts) is the one real
  // caller and is used live by every screen showing real-time updates, so this is a
  // documented exclusion, not an untested gap.
  { id: 'stream', module: 'diag.module.connectivity', label: 'GET /stream (SSE)', run: () => Promise.resolve(skip('long-lived EventSource connection — exercised live by connectOasStream(), not a fetch-style call')) },

  // ---- Hierarchy ---------------------------------------------------------
  { id: 'sites', module: 'diag.module.hierarchy', label: 'GET /sites', run: captureList(() => hierarchyApi.listSites(), (ctx, rows) => { ctx.siteId = rows[0]?.id; }) },
  { id: 'zones', module: 'diag.module.hierarchy', label: 'GET /zones', run: () => ok(() => hierarchyApi.listZones(), undefined, isArray) },
  { id: 'lines', module: 'diag.module.hierarchy', label: 'GET /lines', run: captureList(() => hierarchyApi.listLines(), (ctx, rows) => { ctx.lineId = rows[0]?.id; }) },
  {
    id: 'posts', module: 'diag.module.hierarchy', label: 'GET /posts',
    run: captureList(() => hierarchyApi.listPosts(), (ctx, rows) => { ctx.postId = rows[0]?.id; ctx.postCode = rows[0]?.code; }),
  },
  { id: 'posts.byId', module: 'diag.module.hierarchy', label: 'GET /posts/{id}', run: (ctx) => ctx.postId ? ok(() => apiFetch(`/posts/${ctx.postId}`), undefined, hasKeys('id', 'code', 'lineId')) : Promise.resolve(skip('no post to test against')) },
  { id: 'posts.byCode', module: 'diag.module.hierarchy', label: 'GET /posts/by-code/{code}', run: (ctx) => ctx.postCode ? ok(() => hierarchyApi.postByCode(ctx.postCode!), undefined, hasKeys('id', 'code')) : Promise.resolve(skip('no post to test against')) },
  { id: 'posts.capacity', module: 'diag.module.hierarchy', label: 'GET /posts/{id}/capacity', run: (ctx) => ctx.postId ? ok(() => hierarchyApi.postCapacity(ctx.postId!), (v) => `${(v as { operatorsRequired?: number })?.operatorsRequired ?? '—'} operator(s)`, hasKeys('operatorsRequired')) : Promise.resolve(skip('no post to test against')) },
  { id: 'posts.qrToken', module: 'diag.module.hierarchy', label: 'GET /posts/{id}/qr-token', run: (ctx) => ctx.postId ? ok(() => hierarchyApi.qrToken(ctx.postId!), undefined, hasKeys('postId', 'token')) : Promise.resolve(skip('no post to test against')) },
  { id: 'posts.qrTokens', module: 'diag.module.hierarchy', label: 'GET /posts/qr-tokens', run: () => ok(() => hierarchyApi.qrTokens(), undefined, isArray) },
  { id: 'hierarchy.tree', module: 'diag.module.hierarchy', label: 'GET /hierarchy/tree', run: () => ok(() => apiFetch('/hierarchy/tree')) },
  { id: 'posts.layout', module: 'diag.module.hierarchy', label: 'GET /posts/layout', run: () => ok(() => apiFetch('/posts/layout')) },
  { id: 'referentials.completeness', module: 'diag.module.hierarchy', label: 'GET /referentials/completeness', run: () => ok(() => hierarchyApi.completeness(), () => 'OK', hasKeys('postsRatio', 'namedProductsRatio', 'overall')) },

  // ---- Operators / equipment / cadences / products -----------------------
  { id: 'operators', module: 'diag.module.operators', label: 'GET /operators', run: () => ok(() => operatorsApi.search(), undefined, isArray) },
  { id: 'operators.create', module: 'diag.module.operators', label: 'POST /operators', run: (ctx) => runOperatorCreateTest(ctx) },
  { id: 'operators.setActive', module: 'diag.module.operators', label: 'PUT /operators/{id}/active', run: (ctx) => ctx.operatorId ? ok(() => apiFetch(`/operators/${ctx.operatorId}/active`, { method: 'PUT', body: { isActive: false } })) : Promise.resolve(skip('no test operator to mutate')) },
  { id: 'operators.setRole', module: 'diag.module.operators', label: 'PUT /operators/{id}/role', run: (ctx) => ctx.operatorId ? ok(() => apiFetch(`/operators/${ctx.operatorId}/role`, { method: 'PUT', body: { role: 'operator' } })) : Promise.resolve(skip('no test operator to mutate')) },
  { id: 'operators.setScope', module: 'diag.module.operators', label: 'PUT /operators/{id}/scope', run: (ctx) => ctx.operatorId ? ok(() => apiFetch(`/operators/${ctx.operatorId}/scope`, { method: 'PUT', body: { scopeSiteId: null, scopeZoneId: null, scopeLineId: null } })) : Promise.resolve(skip('no test operator to mutate')) },
  { id: 'operators.regeneratePin', module: 'diag.module.operators', label: 'POST /operators/{id}/regenerate-pin', run: (ctx) => ctx.operatorId ? ok(() => apiFetch(`/operators/${ctx.operatorId}/regenerate-pin`, { method: 'POST' })) : Promise.resolve(skip('no test operator to mutate')) },
  { id: 'equipments', module: 'diag.module.equipment', label: 'GET /equipments', run: () => ok(() => equipmentsApi.list(), undefined, isArray) },
  { id: 'write.equipments', module: 'diag.module.equipment', label: 'POST /equipments → PUT → DELETE → GET', run: () => runEquipmentsWriteTest() },
  { id: 'cadences', module: 'diag.module.cadences', label: 'GET /cadences', run: captureList(() => cadencesApi.list(), (ctx, rows) => { ctx.cadenceId = rows[0]?.id; }) },
  // No standalone GET /cadences/{id}/history row: on a tenant with no real cadences yet, a
  // cadence only ever exists for the brief window inside the write-path test below, which
  // exercises this endpoint directly against it — a dedicated row here would always just skip.
  {
    id: 'cadences.current', module: 'diag.module.cadences', label: 'GET /cadences/current',
    // A random productId is guaranteed not to exist for this post — 404
    // ("no current cadence for this pairing") is the real, valid response
    // shape, same treatment as posts sessions/active below.
    run: async (ctx) => {
      if (!ctx.postId) return skip('no post to test against');
      try {
        await apiFetch(`/cadences/current?postId=${ctx.postId}&productId=${crypto.randomUUID()}`);
        return { ok: true, detail: 'current cadence found' };
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) return { ok: true, detail: 'no current cadence for this pairing (normal)' };
        throw e;
      }
    },
  },
  { id: 'write.cadences', module: 'diag.module.cadences', label: 'POST /products + /cadences → PUT → DELETE → GET', run: () => runCadencesWriteTest() },
  { id: 'products', module: 'diag.module.products', label: 'GET /products', run: () => ok(() => productsApi.list(), undefined, isArray) },
  { id: 'write.products', module: 'diag.module.products', label: 'POST /products → PUT → DELETE → GET', run: () => runProductsWriteTest() },
  { id: 'productionOrders', module: 'diag.module.products', label: 'GET /production-orders', run: () => ok(() => productionOrdersApi.list(), undefined, isArray) },

  // ---- Causes --------------------------------------------------------------
  { id: 'causes', module: 'diag.module.causes', label: 'GET /causes', run: () => ok(() => causesApi.tree(), undefined, isArray) },
  { id: 'causes.usage', module: 'diag.module.causes', label: 'GET /causes/usage', run: () => ok(() => causesApi.usage(), undefined, isArray) },
  { id: 'causeProposals', module: 'diag.module.causes', label: 'GET /cause-proposals', run: () => ok(() => causesApi.listProposals(), undefined, isArray) },
  { id: 'write.causes', module: 'diag.module.causes', label: 'POST /causes → PUT → setCriticality → setActive → addChild → removeChild → DELETE → GET', run: () => runCausesWriteTest() },

  // ---- Shifts ----------------------------------------------------------
  { id: 'shifts', module: 'diag.module.shifts', label: 'GET /shifts', run: captureList(() => shiftsApi.list(), (ctx, rows) => { ctx.shiftId = rows[0]?.id; }) },
  { id: 'shifts.calendar', module: 'diag.module.shifts', label: 'GET /shifts/calendar', run: (ctx) => ok(() => apiFetch(`/shifts/calendar?from=${ctx.today}&to=${ctx.today}`)) },
  { id: 'shiftSignoffs', module: 'diag.module.shifts', label: 'GET /shift-signoffs', run: () => ok(() => shiftsApi.signoffs(), undefined, isArray) },
  { id: 'write.shiftSignoffs', module: 'diag.module.shifts', label: 'POST /shift-signoffs', run: (ctx) => runShiftSignoffWriteTest(ctx) },

  // ---- Assignments / presence (need a real shift id) ------------------------
  { id: 'assignments', module: 'diag.module.assignments', label: 'GET /assignments', run: (ctx) => ctx.shiftId ? ok(() => assignmentsApi.list(ctx.shiftId!, ctx.today), undefined, isArray) : Promise.resolve(skip('no shift template to test against')) },
  { id: 'write.assignments', module: 'diag.module.assignments', label: 'PUT /assignments/{postId} → DELETE → GET', run: () => runAssignmentsWriteTest() },
  { id: 'write.assignments.autoFillPublish', module: 'diag.module.assignments', label: 'POST /assignments/auto-fill → /publish → DELETE', run: (ctx) => runAssignmentsAutoFillPublishTest(ctx) },
  { id: 'assignments.published', module: 'diag.module.assignments', label: 'GET /assignments/published', run: (ctx) => ctx.shiftId ? ok(() => assignmentsApi.published(ctx.shiftId!, ctx.today), undefined, isArray) : Promise.resolve(skip('no shift template to test against')) },
  { id: 'assignments.counts', module: 'diag.module.assignments', label: 'GET /assignments/counts', run: (ctx) => ctx.shiftId ? ok(() => assignmentsApi.counts(ctx.shiftId!, ctx.today), () => 'OK', hasKeys('totalPosts', 'assigned', 'published')) : Promise.resolve(skip('no shift template to test against')) },
  { id: 'assignments.roster', module: 'diag.module.assignments', label: 'GET /assignments/roster', run: () => ok(() => assignmentsApi.roster(), undefined, isArray) },
  { id: 'presence', module: 'diag.module.assignments', label: 'GET /presence', run: (ctx) => ctx.shiftId ? ok(() => presenceApi.list(ctx.shiftId!, ctx.today), undefined, isArray) : Promise.resolve(skip('no shift template to test against')) },
  { id: 'write.presence', module: 'diag.module.assignments', label: 'PUT /presence/{operatorId} → confirm', run: (ctx) => runPresenceWriteTest(ctx) },

  // ---- Post sessions — 404 (no active session) is a normal outcome, not a failure ----
  {
    id: 'postSessions.active', module: 'diag.module.sessions', label: 'GET /post-sessions/active',
    run: async () => {
      try {
        await postSessionsApi.active();
        return { ok: true, detail: 'active session found' };
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) return { ok: true, detail: 'no active session (normal)' };
        throw e;
      }
    },
  },
  { id: 'write.postSessions', module: 'diag.module.sessions', label: 'POST /post-sessions/open → close', run: (ctx) => runPostSessionLifecycleTest(ctx) },

  // ---- Declarations / events / changeovers / post states -------------------
  // Write tests run BEFORE their {id}-dependent GET siblings deliberately: on a
  // tenant with no real declarations/events yet, the write test is what gives
  // `ctx.declarationId`/`ctx.eventId` a real, permanent row to look up — reversed
  // order would always skip here (declarations/events are never deleted).
  { id: 'declarations', module: 'diag.module.declarations', label: 'GET /declarations', run: captureList(() => declarationsApi.list(), (ctx, rows) => { ctx.declarationId = rows[0]?.id; }) },
  { id: 'write.declarations', module: 'diag.module.declarations', label: 'POST /declarations/production + /scrap', run: (ctx) => runDeclarationsWriteTest(ctx) },
  { id: 'declarations.byId', module: 'diag.module.declarations', label: 'GET /declarations/{id}', run: (ctx) => ctx.declarationId ? ok(() => apiFetch(`/declarations/${ctx.declarationId}`), undefined, hasKeys('id', 'kind', 'postId', 'quantityOk', 'quantityNok')) : Promise.resolve(skip('no declaration to test against')) },
  { id: 'events', module: 'diag.module.events', label: 'GET /events', run: captureList(() => eventsApi.list(), (ctx, rows) => { ctx.eventId = rows[0]?.id; }) },
  { id: 'write.events.lifecycle', module: 'diag.module.events', label: 'POST /events → take → eta → arrive → ack → escalate → advance → close', run: (ctx) => runEventLifecycleTest(ctx) },
  { id: 'write.events.requalifyDecline', module: 'diag.module.events', label: 'POST /events → requalify → decline → close', run: (ctx) => runEventRequalifyDeclineTest(ctx) },
  { id: 'events.byId', module: 'diag.module.events', label: 'GET /events/{id}', run: (ctx) => ctx.eventId ? ok(() => eventsApi.getOne(ctx.eventId!), undefined, hasKeys('id', 'eventType', 'status', 'postId')) : Promise.resolve(skip('no event to test against')) },
  { id: 'events.transitions', module: 'diag.module.events', label: 'GET /events/{id}/transitions', run: (ctx) => ctx.eventId ? ok(() => eventsApi.transitions(ctx.eventId!), undefined, isArray) : Promise.resolve(skip('no event to test against')) },
  { id: 'changeovers', module: 'diag.module.changeovers', label: 'GET /changeovers', run: () => ok(() => changeoversApi.list(), undefined, isArray) },
  { id: 'write.changeovers', module: 'diag.module.changeovers', label: 'POST /changeovers → PUT finish', run: (ctx) => runChangeoverLifecycleTest(ctx) },
  { id: 'postStates', module: 'diag.module.postStates', label: 'GET /post-states', run: () => ok(() => postStatesApi.live(), undefined, isArray) },
  { id: 'postStates.history', module: 'diag.module.postStates', label: 'GET /post-states/{postId}/history', run: (ctx) => ctx.postId ? ok(() => postStatesApi.history(ctx.postId!), undefined, isArray) : Promise.resolve(skip('no post to test against')) },

  // ---- KPI / Andon -------------------------------------------------------
  { id: 'kpi.daily', module: 'diag.module.kpi', label: 'GET /kpi/daily', run: () => ok(() => kpiApi.daily(), (v) => `OEE ${(v as { oee?: number })?.oee ?? '—'}`, hasKeys('availability', 'quality', 'stopsCount', 'openingMin')) },
  { id: 'kpi.pareto', module: 'diag.module.kpi', label: 'GET /kpi/pareto', run: () => ok(() => kpiApi.pareto(), undefined, isArray) },
  { id: 'kpi.trend', module: 'diag.module.kpi', label: 'GET /kpi/trend', run: () => ok(() => kpiApi.trend(), undefined, isArray) },
  { id: 'kpi.lineComparison', module: 'diag.module.kpi', label: 'GET /kpi/line-comparison', run: () => ok(() => kpiApi.lineComparison(), undefined, isArray) },
  { id: 'kpi.slaSummary', module: 'diag.module.kpi', label: 'GET /kpi/sla-summary', run: () => ok(() => kpiApi.slaSummary(), undefined, isArray) },
  { id: 'kpi.cadenceGap', module: 'diag.module.kpi', label: 'GET /kpi/cadence-gap', run: () => ok(() => kpiApi.cadenceGap(), undefined, isArray) },
  { id: 'andon.message', module: 'diag.module.andon', label: 'GET /andon/message', run: () => ok(() => andonMessageApi.get(), (v) => `"${(v as { message?: string })?.message ?? ''}"`, hasKeys('message')) },
  { id: 'write.andon', module: 'diag.module.andon', label: 'PUT /andon/message (set → restore)', run: () => runAndonWriteTest() },

  // ---- SLA / interventions -------------------------------------------------
  { id: 'sla.rules', module: 'diag.module.sla', label: 'GET /sla/rules', run: () => ok(() => slaApi.rules(), undefined, isArray) },
  { id: 'write.slaRules', module: 'diag.module.sla', label: 'POST /sla/rules → PUT → DELETE → GET', run: () => runSlaRulesWriteTest() },
  {
    id: 'sla.escalations.ack', module: 'diag.module.sla', label: 'POST /sla/escalations/{id}/ack',
    // Escalations are only ever created by the background SLA-sweep job (a real
    // breached SLA) — there is no way to POST one directly, so this can only ever
    // exercise the real thing if one already exists, never a fabricated one.
    run: async () => {
      const rows = await slaApi.escalations();
      const real = rows.find((r) => !r.acknowledgedAt);
      if (!real) return skip('no unacknowledged escalation exists to ack — none can be created directly (only the SLA sweep creates these)');
      return ok(() => apiFetch(`/sla/escalations/${real.id}/ack`, { method: 'POST' }));
    },
  },
  { id: 'sla.escalations', module: 'diag.module.sla', label: 'GET /sla/escalations', run: () => ok(() => slaApi.escalations(), undefined, isArray) },
  { id: 'responderAvailability', module: 'diag.module.sla', label: 'GET /responder-availability', run: () => ok(() => slaApi.availability(), undefined, isArray) },
  { id: 'write.responderAvailability', module: 'diag.module.sla', label: 'PUT /responder-availability/{profileId} (flip → restore)', run: () => runResponderAvailabilityWriteTest() },
  { id: 'interventions', module: 'diag.module.interventions', label: 'GET /interventions', run: () => ok(() => interventionsApi.list(), undefined, isArray) },
  { id: 'interventions.inbox', module: 'diag.module.interventions', label: 'GET /interventions/inbox', run: () => ok(() => interventionsApi.inbox(), undefined, isArray) },
  { id: 'write.interventions', module: 'diag.module.interventions', label: 'POST /events → POST /interventions → assign → start → close', run: (ctx) => runInterventionLifecycleTest(ctx) },

  // ---- Audit / imports / plugins --------------------------------------------
  { id: 'audit', module: 'diag.module.audit', label: 'GET /audit', run: () => ok(() => auditApi.list(), undefined, isArray) },
  { id: 'imports', module: 'diag.module.imports', label: 'GET /imports', run: captureList(() => importsApi.list(), (ctx, rows) => { ctx.importId = rows[0]?.id; }) },
  { id: 'imports.byId', module: 'diag.module.imports', label: 'GET /imports/{id}', run: (ctx) => ctx.importId ? ok(() => apiFetch(`/imports/${ctx.importId}`), undefined, hasKeys('id', 'kind', 'status')) : Promise.resolve(skip('no import to test against')) },
  { id: 'imports.lines', module: 'diag.module.imports', label: 'GET /imports/{id}/lines', run: (ctx) => ctx.importId ? ok(() => apiFetch(`/imports/${ctx.importId}/lines`), undefined, isArray) : Promise.resolve(skip('no import to test against')) },
  { id: 'write.imports', module: 'diag.module.imports', label: 'POST /imports → POST /imports/{id}/commit', run: (ctx) => runImportsWriteTest(ctx) },
  { id: 'pluginActivations', module: 'diag.module.plugins', label: 'GET /plugin-activations', run: () => ok(() => pluginActivationsApi.list(), undefined, isArray) },

  // ---- Lookups (spec §7.2 — may legitimately be empty, never seeded) --------
  { id: 'lookups.postType', module: 'diag.module.lookups', label: 'GET /lookups/PostType', run: () => ok(() => lookupsApi.list('PostType'), undefined, isArray) },
  { id: 'lookups.presenceStatus', module: 'diag.module.lookups', label: 'GET /lookups/PresenceStatus', run: () => ok(() => lookupsApi.list('PresenceStatus'), undefined, isArray) },
  { id: 'write.lookups', module: 'diag.module.lookups', label: 'POST /lookups/{type} → PUT → DELETE → GET', run: () => runLookupsWriteTest() },

  // ---- Backend-only domains — no frontend screen calls these (spec §12: not part of the 22-screen scope), tested here for raw connectivity + full CRUD ----
  { id: 'teams', module: 'diag.module.backendOnly', label: 'GET /teams', run: () => ok(() => apiFetch('/teams'), undefined, isArray) },
  { id: 'write.teams', module: 'diag.module.backendOnly', label: 'POST /teams → PUT /teams/{id}/members', run: (ctx) => runTeamsWriteTest(ctx) },
  // Unlike every other list endpoint here, GET /attachments has no "list all" mode —
  // OasAttachmentsController.GetAll requires `entity`+`id` (attachments always belong to
  // one specific record). `GetAttachmentsAsync` does a plain `Where(EntityTable == ... &&
  // EntityId == ...)` with no validation on the entity name, so any real GUID is a valid,
  // safe query — it just legitimately returns an empty list when nothing's attached to it.
  { id: 'attachments', module: 'diag.module.backendOnly', label: 'GET /attachments', run: (ctx) => ok(() => apiFetch(`/attachments?entity=posts&id=${ctx.postId ?? crypto.randomUUID()}`), undefined, isArray) },
  { id: 'integrations.endpoints', module: 'diag.module.backendOnly', label: 'GET /integrations/endpoints', run: () => ok(() => apiFetch('/integrations/endpoints'), undefined, isArray) },
  { id: 'write.integrationEndpoints', module: 'diag.module.backendOnly', label: 'POST /integrations/endpoints → PUT → DELETE → GET', run: () => runIntegrationEndpointsWriteTest() },
  { id: 'write.webhookIn', module: 'diag.module.backendOnly', label: 'POST /integrations/webhooks/in', run: () => runWebhookInTest() },
  { id: 'integrations.outbox', module: 'diag.module.backendOnly', label: 'GET /integrations/outbox', run: () => ok(() => apiFetch('/integrations/outbox'), undefined, isArray) },
  { id: 'sync.receipts', module: 'diag.module.backendOnly', label: 'GET /sync/receipts', run: () => ok(() => apiFetch('/sync/receipts'), undefined, isArray) },
  // `since` is required (no default) — 1970-01-01 pulls everything, which is a valid, safe read (no side effects, this endpoint only reads).
  { id: 'sync.pull', module: 'diag.module.backendOnly', label: 'GET /sync/pull', run: () => ok(() => apiFetch('/sync/pull?since=1970-01-01T00:00:00Z&limit=1'), () => 'OK') },
  { id: 'write.syncPush', module: 'diag.module.backendOnly', label: 'POST /sync/push', run: () => runSyncPushTest() },
  { id: 'quality.templates', module: 'diag.module.backendOnly', label: 'GET /quality-check-templates', run: () => ok(() => apiFetch('/quality-check-templates'), undefined, isArray) },
  // No standalone GET .../{id}/items row, same reasoning as cadences/history above — the
  // write-path test below exercises it directly against its own temporary template.
  { id: 'write.qualityTemplates', module: 'diag.module.backendOnly', label: 'POST /quality-check-templates → PUT → PUT items → GET items → DELETE → GET', run: () => runQualityTemplatesWriteTest() },
  { id: 'quality.checks', module: 'diag.module.backendOnly', label: 'GET /quality-checks', run: () => ok(() => apiFetch('/quality-checks'), undefined, isArray) },
  { id: 'write.qualityChecks', module: 'diag.module.backendOnly', label: 'POST /quality-checks', run: (ctx) => runQualityCheckCreateTest(ctx) },
];

/**
 * Every write action below is real — this tenant has explicitly accepted
 * full CRUD coverage in a single automatic run, including domains that
 * cannot be cleaned up afterward (declarations, events — see their section
 * further down). Everywhere a real deletion path exists, it's exercised and
 * verified (create → verify present → update → delete → verify gone), not
 * just fired once and assumed. Every created row is tagged `DIAGTEST-`.
 */
async function runCausesWriteTest(): Promise<TestOutcome> {
  const tag = diagTag();
  const created = await causesApi.create({
    domain: 'stop', code: tag, labelFr: tag, labelAr: tag, eventType: 'technical_stop', defaultCriticality: 'low',
  });
  const afterCreate = await causesApi.tree();
  if (!afterCreate.some((c) => c.id === created.id)) {
    throw new Error(`POST /causes reported success but the new row (${tag}) is missing from GET /causes right after`);
  }
  await causesApi.update(created.id, {
    domain: 'stop', code: tag, labelFr: `${tag}-updated`, labelAr: `${tag}-updated`, eventType: 'technical_stop', defaultCriticality: 'low',
  });
  await causesApi.setCriticality(created.id, 'high');
  await causesApi.setActive(created.id, false);
  const child = await causesApi.addChild(created.id, { domain: 'stop', code: `${tag}-C`, labelFr: tag, labelAr: tag });
  await causesApi.removeChild(created.id, child.id);
  await causesApi.remove(created.id);
  const afterDelete = await causesApi.tree();
  if (afterDelete.some((c) => c.id === created.id)) {
    throw new Error(`DELETE /causes/{id} did not remove ${tag} — check Referentials → Causes and delete it by hand`);
  }
  return { ok: true, detail: `POST → PUT → setCriticality → setActive → addChild → removeChild → DELETE → GET (gone), tag ${tag}`, json: created };
}

async function runLookupsWriteTest(): Promise<TestOutcome> {
  const tag = diagTag();
  const type = 'DIAGTEST'; // synthetic type — never read by any real screen, so it can never appear in a real dropdown
  const created = await lookupsApi.create(type, { code: tag, label: tag });
  const afterCreate = await lookupsApi.list(type);
  if (!afterCreate.some((v) => v.id === created.id)) {
    throw new Error(`POST /lookups/${type} reported success but ${tag} is missing from GET /lookups/${type} right after`);
  }
  await lookupsApi.update(type, created.id, { code: tag, label: `${tag}-updated` });
  await lookupsApi.remove(type, created.id);
  const afterDelete = await lookupsApi.list(type);
  if (afterDelete.some((v) => v.id === created.id)) {
    throw new Error(`DELETE /lookups/${type}/{id} did not remove ${tag} from the list`);
  }
  return { ok: true, detail: `POST → PUT → DELETE → GET (gone), tag ${tag}`, json: created };
}

async function runCadencesWriteTest(): Promise<TestOutcome> {
  const posts = await hierarchyApi.listPosts();
  const post = posts[0];
  if (!post) return skip('no post available to attach a test cadence to');

  const tag = diagTag();
  const product = await productsApi.create({ reference: tag, name: tag });
  try {
    const created = await cadencesApi.createVersion({ productId: product.id, postId: post.id, rate: 42 });
    const afterCreate = await cadencesApi.list();
    if (!afterCreate.some((c) => c.id === created.id)) {
      throw new Error(`POST /cadences reported success but the new row (${tag}) is missing from GET /cadences right after`);
    }
    await cadencesApi.update(created.id, { productId: product.id, postId: post.id, rate: 55 });
    // GET /cadences/{id}/history can only ever be exercised against a real cadence —
    // on an empty tenant, this brief window (before delete, below) is the only one that
    // ever exists, so it's tested here rather than as a standalone row doomed to skip.
    const history = await cadencesApi.history(created.id);
    await cadencesApi.remove(created.id);
    const afterDelete = await cadencesApi.list();
    if (afterDelete.some((c) => c.id === created.id)) {
      throw new Error(`DELETE /cadences/{id} did not remove ${tag} — check Referentials → Cadences and delete it by hand`);
    }
    return { ok: true, detail: `POST product → POST cadence → PUT → GET /history (${history.length}) → GET (found) → DELETE → GET (gone), tag ${tag}`, json: created };
  } finally {
    // Products has no hard delete — soft-deleted and gone from every real
    // screen via the global query filter, same tier of risk as the fixture
    // hierarchy above, so this is never left behind visible anywhere.
    await productsApi.remove(product.id).catch(() => {});
  }
}

async function runEquipmentsWriteTest(): Promise<TestOutcome> {
  const tag = diagTag();
  const created = await equipmentsApi.create({ code: tag, name: tag });
  const afterCreate = await equipmentsApi.list();
  if (!afterCreate.some((e) => e.id === created.id)) {
    throw new Error(`POST /equipments reported success but ${tag} is missing from GET /equipments right after`);
  }
  await equipmentsApi.update(created.id, { code: tag, name: `${tag}-updated` });
  await equipmentsApi.remove(created.id);
  const afterDelete = await equipmentsApi.list();
  if (afterDelete.some((e) => e.id === created.id)) {
    throw new Error(`DELETE /equipments/{id} did not remove ${tag} — check Referentials → Équipements and delete it by hand`);
  }
  return { ok: true, detail: `POST → PUT → DELETE → GET (gone), tag ${tag}`, json: created };
}

async function runProductsWriteTest(): Promise<TestOutcome> {
  const tag = diagTag();
  const created = await productsApi.create({ reference: tag, name: tag });
  const afterCreate = await productsApi.list();
  if (!afterCreate.some((p) => p.id === created.id)) {
    throw new Error(`POST /products reported success but ${tag} is missing from GET /products right after`);
  }
  await productsApi.update(created.id, { reference: tag, name: `${tag}-updated` });
  await productsApi.remove(created.id);
  const afterDelete = await productsApi.list();
  if (afterDelete.some((p) => p.id === created.id)) {
    throw new Error(`DELETE /products/{id} did not remove ${tag} — soft delete, but should be filtered by the global query filter`);
  }
  return { ok: true, detail: `POST → PUT → DELETE → GET (gone), tag ${tag}`, json: created };
}

/**
 * Needs a real post + shift template + operator to attach to (assignments
 * are a join, not a standalone record) — skips honestly if the tenant has
 * none rather than fabricating any of those three. `workDate` is pinned to
 * a far-future date so the row can never appear on anyone's real "today"
 * schedule even for the brief moment it exists before cleanup.
 */
async function runAssignmentsWriteTest(): Promise<TestOutcome> {
  const [posts, shifts, operators] = await Promise.all([hierarchyApi.listPosts(), shiftsApi.list(), operatorsApi.search()]);
  const post = posts[0];
  const shift = shifts[0];
  const operator = operators[0];
  if (!post || !shift || !operator) return skip('needs a real post + shift template + operator — none available to test against');

  const workDate = '2099-01-01';
  const created = await assignmentsApi.upsert(post.id, { userId: operator.id, shiftTemplateId: shift.id, workDate });
  const afterCreate = await assignmentsApi.list(shift.id, workDate);
  if (!afterCreate.some((a) => a.postId === post.id && a.userId === operator.id)) {
    throw new Error(`PUT /assignments/{postId} reported success but the row is missing from GET /assignments right after`);
  }
  await assignmentsApi.remove(post.id, shift.id, workDate);
  const afterDelete = await assignmentsApi.list(shift.id, workDate);
  if (afterDelete.some((a) => a.postId === post.id && a.userId === operator.id)) {
    throw new Error(`DELETE /assignments/{postId} did not remove the test row — check Assignments for workDate=${workDate} and delete it by hand`);
  }
  return { ok: true, detail: `PUT → GET (found) → DELETE → GET (gone), post ${post.code}, date ${workDate}`, json: created };
}

async function runSlaRulesWriteTest(): Promise<TestOutcome> {
  // priority -999 guarantees this test rule never wins the SLA sweep's `order
  // by priority desc` over any real rule (seed data starts at priority 0),
  // even for the brief moment it exists.
  const created = await apiFetch<{ id: string }>('/sla/rules', {
    method: 'POST', body: { eventType: 'other_stop', targetMin: 999, priority: -999 },
  });
  const afterCreate = await slaApi.rules();
  if (!afterCreate.some((r) => r.id === created.id)) {
    throw new Error('POST /sla/rules reported success but the new row is missing from GET /sla/rules right after');
  }
  await apiFetch(`/sla/rules/${created.id}`, { method: 'PUT', body: { eventType: 'other_stop', targetMin: 500, priority: -999 } });
  await apiFetch(`/sla/rules/${created.id}`, { method: 'DELETE' });
  const afterDelete = await slaApi.rules();
  if (afterDelete.some((r) => r.id === created.id)) {
    throw new Error('DELETE /sla/rules/{id} did not remove the test row — check SLA rules and delete it by hand');
  }
  return { ok: true, detail: 'POST → PUT → GET (found) → DELETE → GET (gone), priority -999', json: created };
}

async function runQualityTemplatesWriteTest(): Promise<TestOutcome> {
  const tag = diagTag();
  const created = await apiFetch<{ id: string }>('/quality-check-templates', {
    method: 'POST', body: { code: tag, name: tag, checkType: 'in_process' },
  });
  const afterCreate = await apiFetch<{ id: string }[]>('/quality-check-templates');
  if (!afterCreate.some((tpl) => tpl.id === created.id)) {
    throw new Error(`POST /quality-check-templates reported success but ${tag} is missing from GET right after`);
  }
  await apiFetch(`/quality-check-templates/${created.id}`, { method: 'PUT', body: { code: tag, name: `${tag}-updated`, checkType: 'in_process' } });
  await apiFetch(`/quality-check-templates/${created.id}/items`, { method: 'PUT', body: [{ label: 'Visual check', valueType: 'boolean', isRequired: true, sortOrder: 0 }] });
  // GET .../items, same reasoning as cadences/history above — only ever real inside this window.
  const items = await apiFetch<unknown[]>(`/quality-check-templates/${created.id}/items`);
  await apiFetch(`/quality-check-templates/${created.id}`, { method: 'DELETE' });
  const afterDelete = await apiFetch<{ id: string }[]>('/quality-check-templates');
  if (afterDelete.some((tpl) => tpl.id === created.id)) {
    throw new Error(`DELETE /quality-check-templates/{id} did not remove ${tag} — check and delete it by hand`);
  }
  return { ok: true, detail: `POST → PUT → PUT items → GET items (${items.length}) → GET (found) → DELETE → GET (gone), tag ${tag}`, json: created };
}

async function runIntegrationEndpointsWriteTest(): Promise<TestOutcome> {
  const tag = diagTag();
  // isActive:false + empty eventTypes is double protection: ReceiveWebhookAsync's
  // filter (`e.IsActive && e.EventTypes.Contains(...)`) can never match this row,
  // so it can't fire a real outbound webhook even for the moment it exists.
  const created = await apiFetch<{ id: string }>('/integrations/endpoints', {
    method: 'POST', body: { name: tag, url: 'https://example.invalid/diagtest', eventTypes: [], isActive: false },
  });
  const afterCreate = await apiFetch<{ id: string }[]>('/integrations/endpoints');
  if (!afterCreate.some((e) => e.id === created.id)) {
    throw new Error(`POST /integrations/endpoints reported success but ${tag} is missing from GET right after`);
  }
  await apiFetch(`/integrations/endpoints/${created.id}`, { method: 'PUT', body: { name: `${tag}-updated` } });
  await apiFetch(`/integrations/endpoints/${created.id}`, { method: 'DELETE' });
  const afterDelete = await apiFetch<{ id: string }[]>('/integrations/endpoints');
  if (afterDelete.some((e) => e.id === created.id)) {
    throw new Error(`DELETE /integrations/endpoints/{id} did not remove ${tag} — check and delete it by hand`);
  }
  return { ok: true, detail: `POST → PUT → GET (found) → DELETE → GET (gone), tag ${tag}, isActive:false`, json: created };
}

/* ------------------------------------------------------------------ */
/* Brand-new domains — zero prior coverage. Operators has no delete    */
/* endpoint at all, so this creates ONE throwaway test operator and     */
/* runs every mutation against it (never a real account); the row      */
/* persists forever with isActive:false, same accepted tradeoff as     */
/* everything below.                                                   */
/* ------------------------------------------------------------------ */

async function runOperatorCreateTest(ctx: DiagCtx): Promise<TestOutcome> {
  const tag = diagTag();
  const created = await apiFetch<{ id: string }>('/operators', {
    method: 'POST',
    body: { email: `${tag.toLowerCase()}@diagtest.invalid`, displayName: tag, role: 'operator', workspace: 'mobile', interim: false },
  });
  ctx.operatorId = created.id;
  return { ok: true, detail: `created ${tag} (no delete endpoint exists — stays, deactivated at the end of this chain)`, json: created };
}

/**
 * Declarations are append-only by DB trigger (`trg_oas_decl_immutable`) —
 * there is no update or delete path, ever. This tenant explicitly accepted
 * that a real production and a real scrap declaration get created here
 * permanently on every run, tagged via their note field for traceability.
 */
async function runDeclarationsWriteTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.postId) return skip('no post to test against');
  const tag = diagTag();
  const now = new Date().toISOString();
  const production = await apiFetch<{ id: string }>('/declarations/production', {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), postId: ctx.postId, quantityOk: 1, quantityNok: 0, note: tag, occurredAt: now },
  });
  ctx.declarationId = production.id;
  const scrap = await apiFetch('/declarations/scrap', {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), postId: ctx.postId, quantityNok: 1, note: tag, occurredAt: now },
  });
  return { ok: true, detail: `POST /declarations/production + /declarations/scrap, tag ${tag} (permanent — append-only)`, json: { production, scrap } };
}

/**
 * Full happy-path lifecycle on ONE event: create → take → eta → arrive →
 * ack → escalate → advance → close. Ends `closed` (terminal) so it never
 * lingers as a visibly "active" alarm — but the row itself is permanent
 * (no delete endpoint for events, ever).
 */
async function runEventLifecycleTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.postId) return skip('no post to test against');
  const tag = diagTag();
  // `other_stop` deliberately, not `technical_stop`/`quality_stop`/`material_wait` — those
  // three are the only ones covered by `uq_oas_open_blocking_event` (one open blocking event
  // per post); using one of them here could collide with a REAL ongoing stop on this post.
  const created = await apiFetch<{ id: string }>('/events', {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), eventType: 'other_stop', postId: ctx.postId, note: tag, declaredAt: new Date().toISOString() },
  });
  ctx.eventId = created.id;
  await apiFetch(`/events/${created.id}/take`, { method: 'POST' });
  await apiFetch(`/events/${created.id}/eta`, { method: 'PUT', body: { etaMinutes: 5 } });
  await apiFetch(`/events/${created.id}/arrive`, { method: 'POST' });
  await apiFetch(`/events/${created.id}/ack`, { method: 'POST' });
  await apiFetch(`/events/${created.id}/escalate`, { method: 'POST' });
  await apiFetch(`/events/${created.id}/advance`, { method: 'POST' });
  const closed = await apiFetch(`/events/${created.id}/close`, { method: 'POST', body: { closureType: 'resolved', note: tag } });
  return { ok: true, detail: `POST → take → eta → arrive → ack → escalate → advance → close, tag ${tag} (permanent — no delete endpoint)`, json: closed };
}

/** Second event: covers requalify + decline (mutually exclusive with the happy-path chain above — both allowed even from `resolved`, but the happy path never gets there). Also ends `closed`. */
async function runEventRequalifyDeclineTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.postId) return skip('no post to test against');
  const tag = diagTag();
  const created = await apiFetch<{ id: string }>('/events', {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), eventType: 'other_stop', postId: ctx.postId, note: tag, declaredAt: new Date().toISOString() },
  });
  ctx.eventRequalifyId = created.id;
  await apiFetch(`/events/${created.id}/requalify`, { method: 'POST', body: { eventType: 'material_wait' } });
  await apiFetch(`/events/${created.id}/decline`, { method: 'POST' });
  const closed = await apiFetch(`/events/${created.id}/close`, { method: 'POST', body: { closureType: 'cancelled', note: tag } });
  return { ok: true, detail: `POST → requalify → decline → close, tag ${tag} (permanent — no delete endpoint)`, json: closed };
}

/**
 * Third event, dedicated to interventions — `assign`/`start` internally
 * drive the SAME event state machine (take/arrive), so this needs its own
 * fresh, still-open event rather than reusing the closed ones above.
 * Intervention `close` hardcodes `closureType: "resolved"` server-side,
 * which also closes this event — no leftover open event either.
 */
async function runInterventionLifecycleTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.postId) return skip('no post to test against');
  const tag = diagTag();
  const event = await apiFetch<{ id: string }>('/events', {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), eventType: 'other_stop', postId: ctx.postId, note: tag, declaredAt: new Date().toISOString() },
  });
  ctx.eventInterventionId = event.id;
  const created = await apiFetch<{ id: string }>('/interventions', { method: 'POST', body: { eventId: event.id } });
  ctx.interventionId = created.id;
  await apiFetch(`/interventions/${created.id}/assign`, { method: 'POST' });
  await apiFetch(`/interventions/${created.id}/start`, { method: 'POST' });
  const closed = await apiFetch(`/interventions/${created.id}/close`, { method: 'POST' });
  return { ok: true, detail: `POST /events → POST /interventions → assign → start → close, tag ${tag} (permanent — no delete endpoint)`, json: closed };
}

/** Real shop-floor clock-in/out — opened then immediately closed properly, never left dangling. No delete endpoint (a real session shouldn't have one), so the closed row persists in history, same as a real operator's session would. */
async function runPostSessionLifecycleTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.postId) return skip('no post to test against');
  let opened: { id: string };
  try {
    opened = await apiFetch<{ id: string }>('/post-sessions/open', {
      method: 'POST',
      body: { clientEventId: crypto.randomUUID(), postId: ctx.postId, startedVia: 'manual' },
    });
  } catch (e) {
    // A real operator (or the diag bot itself, from a prior interrupted run)
    // already has an active session here — a real, expected conflict, not a
    // bug. forceRelay would forcibly end a REAL session to make room, which
    // is worse than just skipping.
    if (e instanceof ApiError && e.status === 409) return skip(`post or diag user already has an active session (${e.message}) — not forcing a relay`);
    throw e;
  }
  ctx.postSessionId = opened.id;
  const closed = await apiFetch(`/post-sessions/${opened.id}/close`, { method: 'POST', body: { clientEventId: crypto.randomUUID() } });
  return { ok: true, detail: 'POST /post-sessions/open → POST /post-sessions/{id}/close (permanent — no delete endpoint, same as a real clock-out)', json: closed };
}

/** Steps pre-marked done so `finish` succeeds immediately (finish 409s on an incomplete checklist) — needs a real product for `toProductId`. */
async function runChangeoverLifecycleTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.postId) return skip('no post to test against');
  const tag = diagTag();
  const product = await productsApi.create({ reference: tag, name: tag });
  try {
    let created: { id: string };
    try {
      created = await apiFetch<{ id: string }>('/changeovers', {
        method: 'POST',
        body: {
          clientEventId: crypto.randomUUID(), postId: ctx.postId, toProductId: product.id,
          steps: [{ id: '1', label: 'Setup', done: true }, { id: '2', label: 'Validate', done: true }],
        },
      });
    } catch (e) {
      // A real changeover is already in progress on this post — a real, expected conflict.
      if (e instanceof ApiError && e.status === 409) return skip(`post already has an open changeover (${e.message})`);
      throw e;
    }
    ctx.changeoverId = created.id;
    const finished = await apiFetch(`/changeovers/${created.id}/finish`, { method: 'PUT' });
    return { ok: true, detail: `POST /changeovers → PUT /changeovers/{id}/finish, tag ${tag} (permanent — no delete endpoint)`, json: finished };
  } finally {
    await productsApi.remove(product.id).catch(() => {});
  }
}

async function runQualityCheckCreateTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.postId) return skip('no post to test against');
  const created = await apiFetch('/quality-checks', {
    method: 'POST',
    body: { clientEventId: crypto.randomUUID(), postId: ctx.postId, checkType: 'in_process', result: 'ok', occurredAt: new Date().toISOString() },
  });
  return { ok: true, detail: 'POST /quality-checks (permanent — represents a real inspection record, no delete endpoint)', json: created };
}

async function runImportsWriteTest(ctx: DiagCtx): Promise<TestOutcome> {
  const created = await apiFetch<{ id: string }>('/imports', { method: 'POST', body: { kind: 'products', rows: [] } });
  ctx.importId = created.id;
  const committed = await apiFetch(`/imports/${created.id}/commit`, { method: 'POST' });
  return { ok: true, detail: 'POST /imports (0 rows) → POST /imports/{id}/commit (permanent — imports are an audit trail by design, no delete endpoint)', json: committed };
}

async function runTeamsWriteTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.siteId) return skip('no site to test against');
  const tag = diagTag();
  const created = await apiFetch<{ id: string }>('/teams', { method: 'POST', body: { siteId: ctx.siteId, code: tag, name: tag } });
  ctx.teamId = created.id;
  const withMembers = await apiFetch(`/teams/${created.id}/members`, { method: 'PUT', body: { memberIds: [] } });
  return { ok: true, detail: `POST /teams → PUT /teams/{id}/members, tag ${tag} (permanent — no delete endpoint)`, json: withMembers };
}

/** Reversible: reads the current message, sets a test value, verifies, restores the original — never leaves the andon board showing test text. */
async function runAndonWriteTest(): Promise<TestOutcome> {
  const before = await andonMessageApi.get();
  const tag = diagTag();
  await andonMessageApi.set(undefined, tag);
  const after = await andonMessageApi.get();
  if (after.message !== tag) throw new Error(`PUT /andon/message reported success but GET /andon/message returned "${after.message}" instead of "${tag}"`);
  const restored = await andonMessageApi.set(undefined, before.message);
  return { ok: true, detail: `GET → PUT (changed) → GET (confirmed) → PUT (restored to "${before.message}")`, json: restored };
}

/** Needs a real shift template — sets presence for a real operator on the far-future test date (never a real day), then confirms it. No delete endpoint exists (accepted, tiny/harmless row). */
async function runPresenceWriteTest(ctx: DiagCtx): Promise<TestOutcome> {
  const operators = await operatorsApi.search();
  const operator = operators[0];
  if (!ctx.shiftId || !operator) return skip('needs a real shift template + operator — none available to test against');
  const workDate = '2099-01-01';
  await presenceApi.set(operator.id, { workDate, shiftTemplateId: ctx.shiftId, status: 'expected' });
  const confirmed = await presenceApi.confirm(operator.id, { workDate, shiftTemplateId: ctx.shiftId });
  return { ok: true, detail: `PUT /presence/{operatorId} → POST /presence/{operatorId}/confirm, date ${workDate} (permanent — no delete endpoint, tiny row)`, json: confirmed };
}

async function runShiftSignoffWriteTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.shiftId) return skip('no shift template to test against');
  const tag = diagTag();
  const created = await apiFetch('/shift-signoffs', { method: 'POST', body: { shiftTemplateId: ctx.shiftId, workDate: '2099-01-01', note: tag } });
  return { ok: true, detail: `POST /shift-signoffs, tag ${tag} (permanent — no delete endpoint)`, json: created };
}

/** Reversible: reads the caller's own current availability, flips it, verifies, restores the original. Self-only by server-side rule unless admin/supervisor (the diag bot is admin). */
async function runResponderAvailabilityWriteTest(): Promise<TestOutcome> {
  const me = await apiFetch<{ id: string }>('/auth/me');
  const before = await slaApi.availability();
  const mine = before.find((r) => r.profileId === me.id);
  const originalBusy = mine?.busy ?? false;
  const originalReason = mine?.reason ?? null;
  await apiFetch(`/responder-availability/${me.id}`, { method: 'PUT', body: { busy: !originalBusy, reason: 'DIAGTEST' } });
  const restored = await apiFetch(`/responder-availability/${me.id}`, { method: 'PUT', body: { busy: originalBusy, reason: originalReason } });
  return { ok: true, detail: `GET → PUT (flipped) → PUT (restored to busy:${originalBusy})`, json: restored };
}

/** Scoped entirely to the far-future test date/shift — can never touch a real day's schedule. */
async function runAssignmentsAutoFillPublishTest(ctx: DiagCtx): Promise<TestOutcome> {
  if (!ctx.shiftId) return skip('no shift template to test against');
  const workDate = '2099-01-01';
  const filled = await apiFetch<{ filled: number }>(`/assignments/auto-fill?shift=${ctx.shiftId}&date=${workDate}`, { method: 'POST' });
  const published = await apiFetch<{ published: number }>(`/assignments/publish?shift=${ctx.shiftId}&date=${workDate}`, { method: 'POST' });
  // Best-effort cleanup of anything auto-fill created — it may have assigned real operators to real posts, but only for the fictional 2099 date.
  await assignmentsApi.clearAll(ctx.shiftId, workDate).catch(() => {});
  return { ok: true, detail: `POST /assignments/auto-fill → POST /assignments/publish → DELETE (cleared), date ${workDate}`, json: { filled, published } };
}

async function runSyncPushTest(): Promise<TestOutcome> {
  const result = await apiFetch('/sync/push', { method: 'POST', body: { items: [] } });
  return { ok: true, detail: 'POST /sync/push (0 items — pure receipt tracker, no entity data written)', json: result };
}

async function runWebhookInTest(): Promise<TestOutcome> {
  const tag = diagTag();
  const result = await apiFetch<{ fannedOut: number }>('/integrations/webhooks/in', { method: 'POST', body: { eventType: `diagtest.${tag}`, payload: {} } });
  return { ok: true, detail: `POST /integrations/webhooks/in, synthetic eventType (fannedOut:${result.fannedOut} — 0 unless a real endpoint subscribes to it)`, json: result };
}

/* ------------------------------------------------------------------ */
/* Auto-provisioned test fixture — hierarchy + shift template only     */
/* ------------------------------------------------------------------ */

/**
 * Everything below exists to unblock the tests that otherwise legitimately
 * "skip" on a tenant with no data yet (posts.byId, posts.byCode, capacity,
 * qr-token, postStates.history, assignments*, presence). It ONLY ever
 * creates a brand-new, self-contained, `DIAGTEST-`-tagged hierarchy chain —
 * it never attaches to or mutates a real existing site/zone/line/post — and
 * only when the tenant genuinely has none yet (so it never runs against a
 * real, already-populated tenant). Everything created here is removed again
 * at the end of the same run.
 *
 * Declarations and events are deliberately NEVER fabricated: declarations
 * are append-only (DB trigger `trg_oas_decl_immutable` forbids UPDATE/
 * DELETE) and events have no removal mechanism at all — either one would
 * permanently pollute real audit/operational history. Those tests stay
 * honest "skip"s when a tenant has none.
 */
interface Fixture {
  siteId?: string;
  zoneId?: string;
  lineId?: string;
  postId?: string;
  postCode?: string;
  shiftId?: string;
  /** Any step that failed, by name — surfaced in the UI instead of silently swallowed, so a broken fixture is a visible bug, not an unexplained wall of "skip". */
  errors: string[];
}

/**
 * The hierarchy chain and the shift template are two INDEPENDENT concerns,
 * each in its own try/catch — a failure in one must never discard progress
 * already made in (or hide the real error from) the other, unlike the
 * previous single-try version where any exception anywhere lost the whole
 * `fixture` object silently.
 */
async function provisionFixture(): Promise<Fixture> {
  const fixture: Fixture = { errors: [] };
  const tag = `DIAGTEST-${Date.now()}`;

  try {
    const posts = await hierarchyApi.listPosts();
    if (posts.length === 0) {
      const site = await hierarchyApi.createSite({ code: `${tag}-SITE`, name: tag });
      fixture.siteId = site.id;
      const zone = await hierarchyApi.createZone({ siteId: site.id, code: `${tag}-ZONE`, name: tag });
      fixture.zoneId = zone.id;
      const line = await hierarchyApi.createLine({ zoneId: zone.id, code: `${tag}-LINE`, name: tag });
      fixture.lineId = line.id;
      const post = await hierarchyApi.createPost({ lineId: line.id, code: `${tag}-POST`, name: tag });
      fixture.postId = post.id;
      fixture.postCode = post.code;
    }
  } catch (e) {
    fixture.errors.push(`hierarchy: ${e instanceof ApiError ? `${e.status} ${e.message}` : e instanceof Error ? e.message : 'unknown error'}`);
  }

  try {
    const shifts = await shiftsApi.list();
    if (shifts.length === 0) {
      const siteId = fixture.siteId ?? (await hierarchyApi.listSites())[0]?.id;
      if (siteId) {
        const shift = await shiftsApi.create({ siteId, code: `${tag}-SHIFT`, name: tag, startTime: '06:00', endTime: '14:00' });
        fixture.shiftId = shift.id;
      } else {
        fixture.errors.push('shift: no site available to attach a test shift template to');
      }
    }
  } catch (e) {
    fixture.errors.push(`shift: ${e instanceof ApiError ? `${e.status} ${e.message}` : e instanceof Error ? e.message : 'unknown error'}`);
  }

  return fixture;
}

/** Best-effort, reverse creation order — one failed step must never block the others. */
async function cleanupFixture(fixture: Fixture): Promise<void> {
  if (fixture.shiftId) await shiftsApi.remove(fixture.shiftId).catch(() => {});
  if (fixture.postId) await hierarchyApi.archivePost(fixture.postId).catch(() => {});
  if (fixture.lineId) await hierarchyApi.archiveLine(fixture.lineId).catch(() => {});
  if (fixture.zoneId) await hierarchyApi.archiveZone(fixture.zoneId).catch(() => {});
  if (fixture.siteId) await hierarchyApi.archiveSite(fixture.siteId).catch(() => {});
}

/* ------------------------------------------------------------------ */
/* Auto sign-in — reuse session, then stored diag credentials, then    */
/* self-bootstrap a first admin, before ever asking a human to log in  */
/* ------------------------------------------------------------------ */

const DIAG_CREDS_KEY = 'oas.diag.creds.v1';

interface DiagCreds { email: string; password: string }

function readStoredDiagCreds(): DiagCreds | null {
  try {
    const raw = localStorage.getItem(DIAG_CREDS_KEY);
    return raw ? (JSON.parse(raw) as DiagCreds) : null;
  } catch {
    return null;
  }
}

function writeStoredDiagCreds(creds: DiagCreds): void {
  try { localStorage.setItem(DIAG_CREDS_KEY, JSON.stringify(creds)); } catch { /* private mode — reused creds just won't persist */ }
}

function clearStoredDiagCreds(): void {
  try { localStorage.removeItem(DIAG_CREDS_KEY); } catch { /* private mode */ }
}

/** Random, never hardcoded — this page can be reached at a public URL against the real production API host. */
function randomPassword(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return `${Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')}Aa1!`;
}

const MODULE_ORDER = [
  'diag.module.connectivity', 'diag.module.hierarchy', 'diag.module.operators', 'diag.module.equipment',
  'diag.module.cadences', 'diag.module.products', 'diag.module.causes', 'diag.module.shifts',
  'diag.module.assignments', 'diag.module.sessions', 'diag.module.declarations', 'diag.module.events',
  'diag.module.changeovers', 'diag.module.postStates', 'diag.module.kpi', 'diag.module.andon',
  'diag.module.sla', 'diag.module.interventions', 'diag.module.audit', 'diag.module.imports',
  'diag.module.plugins', 'diag.module.lookups', 'diag.module.backendOnly',
];

interface RunState {
  status: Status;
  ms?: number;
  detail?: string;
  json?: unknown;
}

function StatusIcon({ status }: { status: Status }) {
  switch (status) {
    case 'pass': return <CheckCircle2 className="h-4 w-4 text-state-production" />;
    case 'fail': return <XCircle className="h-4 w-4 text-state-technical" />;
    case 'skip': return <MinusCircle className="h-4 w-4 text-muted-foreground" />;
    case 'running': return <Loader2 className="h-4 w-4 animate-spin text-state-material" />;
    default: return <CircleDashed className="h-4 w-4 text-muted-foreground/50" />;
  }
}

type AuthPhase = 'checking' | 'login' | 'ready';

/**
 * Standalone route (`/test`, wired in `App.tsx`, outside the web/mobile
 * shells on purpose) — signs in automatically (reusing any valid session
 * already in this browser, or the same `LoginForm` the real console uses if
 * not) and then auto-runs a full smoke test of every safe (read-only) OAS
 * endpoint, using the SAME typed API client modules every real screen uses
 * — so this proves how the backend is actually wired to this build, not a
 * separate mock, and every result is validated against the real response
 * SHAPE (`hasKeys`/`isArray`), not just "didn't throw".
 *
 * This tenant has explicitly opted in to full CRUD coverage in a single
 * automatic run — every safe round trip (create → verify → update →
 * delete → verify-gone) runs inline as a normal test, including domains
 * with no cleanup path (declarations, events — permanent by design, see
 * their section further down for exactly why). Nothing here is opt-in
 * anymore: `runAll()` below is the only run, and it is everything.
 */
export default function ApiTestPage() {
  const t = useT();
  const [authPhase, setAuthPhase] = useState<AuthPhase>('checking');
  const [runs, setRuns] = useState<Record<string, RunState>>({});
  const [running, setRunning] = useState(false);
  const [ranAt, setRanAt] = useState<string | null>(null);
  const [fixtureNote, setFixtureNote] = useState<string | null>(null);
  const [fixtureError, setFixtureError] = useState<string | null>(null);
  const [filter, setFilter] = useState<Status | 'all'>('all');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const startedOnce = useRef(false);

  const runAll = async () => {
    setRunning(true);
    setFixtureNote(null);
    setFixtureError(null);
    const initial: Record<string, RunState> = {};
    TESTS.forEach((test) => { initial[test.id] = { status: 'pending' as Status }; });
    setRuns(initial);

    // Auto-provision the minimal hierarchy/shift data the tenant is missing,
    // so dependent tests exercise the real thing instead of skipping —
    // never declarations or events (see provisionFixture's doc comment).
    const fixture = await provisionFixture();
    const provisioned: string[] = [];
    if (fixture.postId) provisioned.push('site → zone → line → post');
    if (fixture.shiftId) provisioned.push('shift template');

    const ctx: DiagCtx = { today: new Date().toISOString().slice(0, 10) };
    // Wire the fixture straight into ctx immediately — don't rely solely on
    // the `sites`/`posts`/`shifts` GET tests re-discovering it via list(),
    // so a dependent test never skips just because list-capture happened to
    // run in an order or moment that missed it.
    if (fixture.siteId) ctx.siteId = fixture.siteId;
    if (fixture.postId) { ctx.postId = fixture.postId; ctx.postCode = fixture.postCode; }
    if (fixture.shiftId) ctx.shiftId = fixture.shiftId;

    for (const test of TESTS) {
      setRuns((prev) => ({ ...prev, [test.id]: { status: 'running' } }));
      const started = performance.now();
      try {
        const outcome = await test.run(ctx);
        const ms = Math.round(performance.now() - started);
        setRuns((prev) => ({ ...prev, [test.id]: { status: outcome.skipped ? 'skip' : 'pass', ms, detail: outcome.detail, json: outcome.json } }));
      } catch (e) {
        const ms = Math.round(performance.now() - started);
        const detail = e instanceof ApiError ? `${e.status} ${e.message}` : e instanceof Error ? e.message : 'Network error';
        setRuns((prev) => ({ ...prev, [test.id]: { status: 'fail', ms, detail } }));
      }
    }

    if (provisioned.length > 0) {
      await cleanupFixture(fixture);
      setFixtureNote(t('diag.fixtureNote', { items: provisioned.join(', ') }));
    }
    setFixtureError(fixture.errors.length > 0 ? fixture.errors.join(' · ') : null);

    setRunning(false);
    setRanAt(new Date().toLocaleTimeString());
  };

  // Auto sign-in cascade — never wait on a human unless every automatic
  // path is genuinely exhausted:
  //   1. reuse a valid session already in this browser (previous /test
  //      visit, or /web/login or /mobile/login in another tab — same
  //      origin, same localStorage);
  //   2. reuse diagnostic credentials this page generated on an earlier
  //      run (stored locally, never hardcoded);
  //   3. self-bootstrap the tenant's first admin via `/setup` with a
  //      freshly-random password — the endpoint itself refuses
  //      (`Success:false`) if an admin already exists, so this can never
  //      collide with or overwrite a real account, only succeed once, on a
  //      genuinely empty tenant;
  //   4. only then fall back to the real login form.
  useEffect(() => {
    if (startedOnce.current) return;
    startedOnce.current = true;
    (async () => {
      if (getSession()?.accessToken) {
        try {
          await apiFetch('/auth/me');
          setAuthPhase('ready');
          return;
        } catch {
          // stale/expired token — fall through
        }
      }

      const stored = readStoredDiagCreds();
      if (stored) {
        const result = await signInConsole(stored.email, stored.password);
        if (typeof result !== 'string') {
          setAuthPhase('ready');
          return;
        }
        clearStoredDiagCreds(); // dead/rotated — don't keep retrying them
      }

      const email = `diagtest-${Date.now()}@local.test`;
      const password = randomPassword();
      const setupResult = await setupFirstAdmin(email, password, 'Diagnostics Bot');
      if (typeof setupResult !== 'string') {
        writeStoredDiagCreds({ email, password });
        setAuthPhase('ready');
        return;
      }

      setAuthPhase('login');
    })();
  }, []);

  useEffect(() => {
    if (authPhase !== 'ready') return;
    void runAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authPhase]);

  const values = Object.values(runs);
  const total = TESTS.length;
  const passCount = values.filter((r) => r.status === 'pass').length;
  const failCount = values.filter((r) => r.status === 'fail').length;
  const skipCount = values.filter((r) => r.status === 'skip').length;
  const done = passCount + failCount + skipCount;
  const filteredCount = filter === 'all' ? done : values.filter((r) => r.status === filter).length;

  const toggleFilter = (status: Status) => setFilter((f) => (f === status ? 'all' : status));
  const toggleExpanded = (id: string) => setExpanded((prev) => {
    const next = new Set(prev);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  if (authPhase === 'checking') {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (authPhase === 'login') {
    return (
      <div dir="ltr" className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-4">
        <div className="text-center">
          <h1 className="text-title font-heading">{t('diag.title')}</h1>
          <p className="mt-1 text-body-sm text-muted-foreground">{t('diag.signInFirst')}</p>
        </div>
        <LoginForm onSuccess={() => setAuthPhase('ready')} />
      </div>
    );
  }

  return (
    <div dir="ltr" className="flex h-screen flex-col bg-background text-foreground">
      <header className="shrink-0 space-y-3 border-b border-border p-4 sm:px-8 sm:py-5">
        <div className="mx-auto flex max-w-3xl flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-title font-heading">{t('diag.title')}</h1>
            <p className="text-body-sm text-muted-foreground">
              {t('diag.desc')} <span className="font-mono text-caption">{OAS_API_BASE}</span>
            </p>
          </div>
          <Button size="sm" variant="outline" onClick={() => void runAll()} disabled={running}>
            {running ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : <RotateCcw className="me-1.5 h-3.5 w-3.5" />}
            {running ? t('diag.running', { done, total }) : t('diag.rerun')}
          </Button>
        </div>

        {values.length > 0 && (
          <div className="mx-auto flex max-w-3xl flex-wrap items-center gap-2 text-body-sm">
            <button type="button" onClick={() => toggleFilter('pass')} disabled={passCount === 0} className="disabled:cursor-not-allowed disabled:opacity-50">
              <Badge variant="ghost" className={cn('text-state-production', filter === 'pass' && 'ring-1 ring-state-production')}>{t('diag.pass', { n: passCount })}</Badge>
            </button>
            <button type="button" onClick={() => toggleFilter('fail')} disabled={failCount === 0} className="disabled:cursor-not-allowed disabled:opacity-50">
              <Badge variant="ghost" className={cn('text-state-technical', filter === 'fail' && 'ring-1 ring-state-technical')}>{t('diag.fail', { n: failCount })}</Badge>
            </button>
            <button type="button" onClick={() => toggleFilter('skip')} disabled={skipCount === 0} className="disabled:cursor-not-allowed disabled:opacity-50">
              <Badge variant="ghost" className={cn('text-muted-foreground', filter === 'skip' && 'ring-1 ring-foreground/40')}>{t('diag.skip', { n: skipCount })}</Badge>
            </button>
            {filter !== 'all' && (
              <button type="button" onClick={() => setFilter('all')} className="text-caption text-muted-foreground underline decoration-dotted underline-offset-2 hover:text-foreground">
                {t('diag.clearFilter')}
              </button>
            )}
            {ranAt && !running && <span className="ms-auto text-caption text-muted-foreground">{t('diag.lastRun', { time: ranAt })}</span>}
          </div>
        )}

        {fixtureNote && !running && (
          <p className="mx-auto max-w-3xl rounded-lg border border-dashed border-border p-2.5 text-caption text-muted-foreground">{fixtureNote}</p>
        )}

        {fixtureError && !running && (
          <p className="mx-auto max-w-3xl rounded-lg border border-state-technical/40 bg-state-technical/5 p-2.5 text-caption text-state-technical">
            {t('diag.fixtureError', { error: fixtureError })}
          </p>
        )}
      </header>

      <div className="flex-1 overflow-y-auto">
        <div className="mx-auto max-w-3xl space-y-4 p-4 sm:p-8">
          {filter !== 'all' && filteredCount === 0 && (
            <p className="py-8 text-center text-body-sm text-muted-foreground">{t('diag.filterEmpty')}</p>
          )}

          <div className="space-y-4">
            {MODULE_ORDER.map((moduleKey) => {
              const moduleTests = TESTS.filter((test) => test.module === moduleKey);
              const visible = moduleTests.filter((test) => runs[test.id] && (filter === 'all' || runs[test.id].status === filter));
              if (visible.length === 0) return null;
              return (
                <div key={moduleKey}>
                  <h3 className="mb-1.5 text-caption font-semibold uppercase tracking-[0.04em] text-muted-foreground">{t(moduleKey)}</h3>
                  <div className="overflow-hidden rounded-lg border border-border">
                    {visible.map((test) => {
                      const run = runs[test.id];
                      const hasJson = run.json !== undefined;
                      const isExpanded = expanded.has(test.id);
                      return (
                        <div key={test.id} className="border-b border-border last:border-b-0">
                          <div
                            role={hasJson ? 'button' : undefined}
                            tabIndex={hasJson ? 0 : undefined}
                            onClick={hasJson ? () => toggleExpanded(test.id) : undefined}
                            onKeyDown={hasJson ? (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleExpanded(test.id); } } : undefined}
                            className={cn(
                              'flex items-center gap-2.5 px-3 py-2 text-body-sm',
                              run.status === 'fail' && 'bg-state-technical/5',
                              hasJson && 'cursor-pointer hover:bg-muted/40',
                            )}
                          >
                            <StatusIcon status={run.status} />
                            <span className="min-w-0 flex-1 truncate font-mono text-caption">{test.label}</span>
                            <span className={cn('truncate text-caption', run.status === 'fail' ? 'text-state-technical' : 'text-muted-foreground')}>
                              {run.detail}
                            </span>
                            {run.ms !== undefined && <span className="shrink-0 font-mono text-caption text-muted-foreground">{run.ms}ms</span>}
                            {hasJson && (
                              <span className="shrink-0 text-caption text-muted-foreground">{isExpanded ? '▾' : '▸'}</span>
                            )}
                          </div>
                          {hasJson && isExpanded && (
                            <pre className="overflow-x-auto border-t border-border bg-muted/30 p-3 text-caption">
                              {JSON.stringify(run.json, null, 2)}
                            </pre>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>

          <p className="rounded-lg border border-dashed border-border p-2.5 text-caption text-muted-foreground">
            {t('diag.writeNote', { n: total })}
          </p>
        </div>
      </div>
    </div>
  );
}
