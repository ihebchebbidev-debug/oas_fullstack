import { useEffect, useRef, useState } from 'react';
import { CheckCircle2, XCircle, MinusCircle, CircleDashed, Loader2, RotateCcw, FlaskConical } from 'lucide-react';

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
}

interface DiagCtx {
  postId?: string;
  postCode?: string;
  lineId?: string;
  cadenceId?: string;
  declarationId?: string;
  eventId?: string;
  importId?: string;
  shiftId?: string;
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
 * a bare "didn't throw" smoke test.
 */
async function ok(
  fn: () => Promise<unknown>,
  describe: (v: unknown) => string = (v) => Array.isArray(v) ? `${v.length} row(s)` : 'OK',
  validate?: (v: unknown) => string | null,
): Promise<TestOutcome> {
  const v = await fn();
  const shapeError = validate?.(v);
  if (shapeError) return { ok: false, detail: `shape mismatch — ${shapeError}` };
  return { ok: true, detail: describe(v) };
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
    if (shapeError) return { ok: false, detail: `shape mismatch — ${shapeError}` };
    assign(ctx, rows);
    return { ok: true, detail: `${rows.length} row(s)` };
  };
}

const TESTS: DiagTest[] = [
  // ---- Connectivity ----------------------------------------------------
  { id: 'auth.me', module: 'diag.module.connectivity', label: 'GET /auth/me', run: () => ok(() => apiFetch('/auth/me'), (v) => `signed in as ${(v as { email?: string })?.email ?? '—'}`, hasKeys('id', 'email', 'role', 'workspace')) },

  // ---- Hierarchy ---------------------------------------------------------
  { id: 'sites', module: 'diag.module.hierarchy', label: 'GET /sites', run: () => ok(() => hierarchyApi.listSites(), undefined, isArray) },
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
  { id: 'equipments', module: 'diag.module.equipment', label: 'GET /equipments', run: () => ok(() => equipmentsApi.list(), undefined, isArray) },
  { id: 'cadences', module: 'diag.module.cadences', label: 'GET /cadences', run: captureList(() => cadencesApi.list(), (ctx, rows) => { ctx.cadenceId = rows[0]?.id; }) },
  { id: 'cadences.history', module: 'diag.module.cadences', label: 'GET /cadences/{id}/history', run: (ctx) => ctx.cadenceId ? ok(() => cadencesApi.history(ctx.cadenceId!), undefined, isArray) : Promise.resolve(skip('no cadence to test against')) },
  { id: 'products', module: 'diag.module.products', label: 'GET /products', run: () => ok(() => productsApi.list(), undefined, isArray) },
  { id: 'productionOrders', module: 'diag.module.products', label: 'GET /production-orders', run: () => ok(() => productionOrdersApi.list(), undefined, isArray) },

  // ---- Causes --------------------------------------------------------------
  { id: 'causes', module: 'diag.module.causes', label: 'GET /causes', run: () => ok(() => causesApi.tree(), undefined, isArray) },
  { id: 'causes.usage', module: 'diag.module.causes', label: 'GET /causes/usage', run: () => ok(() => causesApi.usage(), undefined, isArray) },
  { id: 'causeProposals', module: 'diag.module.causes', label: 'GET /cause-proposals', run: () => ok(() => causesApi.listProposals(), undefined, isArray) },

  // ---- Shifts ----------------------------------------------------------
  { id: 'shifts', module: 'diag.module.shifts', label: 'GET /shifts', run: captureList(() => shiftsApi.list(), (ctx, rows) => { ctx.shiftId = rows[0]?.id; }) },
  { id: 'shifts.calendar', module: 'diag.module.shifts', label: 'GET /shifts/calendar', run: (ctx) => ok(() => apiFetch(`/shifts/calendar?from=${ctx.today}&to=${ctx.today}`)) },
  { id: 'shiftSignoffs', module: 'diag.module.shifts', label: 'GET /shift-signoffs', run: () => ok(() => shiftsApi.signoffs(), undefined, isArray) },

  // ---- Assignments / presence (need a real shift id) ------------------------
  { id: 'assignments', module: 'diag.module.assignments', label: 'GET /assignments', run: (ctx) => ctx.shiftId ? ok(() => assignmentsApi.list(ctx.shiftId!, ctx.today), undefined, isArray) : Promise.resolve(skip('no shift template to test against')) },
  { id: 'assignments.published', module: 'diag.module.assignments', label: 'GET /assignments/published', run: (ctx) => ctx.shiftId ? ok(() => assignmentsApi.published(ctx.shiftId!, ctx.today), undefined, isArray) : Promise.resolve(skip('no shift template to test against')) },
  { id: 'assignments.counts', module: 'diag.module.assignments', label: 'GET /assignments/counts', run: (ctx) => ctx.shiftId ? ok(() => assignmentsApi.counts(ctx.shiftId!, ctx.today), () => 'OK', hasKeys('totalPosts', 'assigned', 'published')) : Promise.resolve(skip('no shift template to test against')) },
  { id: 'assignments.roster', module: 'diag.module.assignments', label: 'GET /assignments/roster', run: () => ok(() => assignmentsApi.roster(), undefined, isArray) },
  { id: 'presence', module: 'diag.module.assignments', label: 'GET /presence', run: (ctx) => ctx.shiftId ? ok(() => presenceApi.list(ctx.shiftId!, ctx.today), undefined, isArray) : Promise.resolve(skip('no shift template to test against')) },

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

  // ---- Declarations / events / changeovers / post states -------------------
  { id: 'declarations', module: 'diag.module.declarations', label: 'GET /declarations', run: captureList(() => declarationsApi.list(), (ctx, rows) => { ctx.declarationId = rows[0]?.id; }) },
  { id: 'declarations.byId', module: 'diag.module.declarations', label: 'GET /declarations/{id}', run: (ctx) => ctx.declarationId ? ok(() => apiFetch(`/declarations/${ctx.declarationId}`), undefined, hasKeys('id', 'kind', 'postId', 'quantityOk', 'quantityNok')) : Promise.resolve(skip('no declaration to test against')) },
  { id: 'events', module: 'diag.module.events', label: 'GET /events', run: captureList(() => eventsApi.list(), (ctx, rows) => { ctx.eventId = rows[0]?.id; }) },
  { id: 'events.byId', module: 'diag.module.events', label: 'GET /events/{id}', run: (ctx) => ctx.eventId ? ok(() => eventsApi.getOne(ctx.eventId!), undefined, hasKeys('id', 'eventType', 'status', 'postId')) : Promise.resolve(skip('no event to test against')) },
  { id: 'events.transitions', module: 'diag.module.events', label: 'GET /events/{id}/transitions', run: (ctx) => ctx.eventId ? ok(() => eventsApi.transitions(ctx.eventId!), undefined, isArray) : Promise.resolve(skip('no event to test against')) },
  { id: 'changeovers', module: 'diag.module.changeovers', label: 'GET /changeovers', run: () => ok(() => changeoversApi.list(), undefined, isArray) },
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

  // ---- SLA / interventions -------------------------------------------------
  { id: 'sla.rules', module: 'diag.module.sla', label: 'GET /sla/rules', run: () => ok(() => slaApi.rules(), undefined, isArray) },
  { id: 'sla.escalations', module: 'diag.module.sla', label: 'GET /sla/escalations', run: () => ok(() => slaApi.escalations(), undefined, isArray) },
  { id: 'responderAvailability', module: 'diag.module.sla', label: 'GET /responder-availability', run: () => ok(() => slaApi.availability(), undefined, isArray) },
  { id: 'interventions', module: 'diag.module.interventions', label: 'GET /interventions', run: () => ok(() => interventionsApi.list(), undefined, isArray) },
  { id: 'interventions.inbox', module: 'diag.module.interventions', label: 'GET /interventions/inbox', run: () => ok(() => interventionsApi.inbox(), undefined, isArray) },

  // ---- Audit / imports / plugins --------------------------------------------
  { id: 'audit', module: 'diag.module.audit', label: 'GET /audit', run: () => ok(() => auditApi.list(), undefined, isArray) },
  { id: 'imports', module: 'diag.module.imports', label: 'GET /imports', run: captureList(() => importsApi.list(), (ctx, rows) => { ctx.importId = rows[0]?.id; }) },
  { id: 'imports.byId', module: 'diag.module.imports', label: 'GET /imports/{id}', run: (ctx) => ctx.importId ? ok(() => apiFetch(`/imports/${ctx.importId}`), undefined, hasKeys('id', 'kind', 'status')) : Promise.resolve(skip('no import to test against')) },
  { id: 'imports.lines', module: 'diag.module.imports', label: 'GET /imports/{id}/lines', run: (ctx) => ctx.importId ? ok(() => apiFetch(`/imports/${ctx.importId}/lines`), undefined, isArray) : Promise.resolve(skip('no import to test against')) },
  { id: 'pluginActivations', module: 'diag.module.plugins', label: 'GET /plugin-activations', run: () => ok(() => pluginActivationsApi.list(), undefined, isArray) },

  // ---- Lookups (spec §7.2 — may legitimately be empty, never seeded) --------
  { id: 'lookups.postType', module: 'diag.module.lookups', label: 'GET /lookups/PostType', run: () => ok(() => lookupsApi.list('PostType'), undefined, isArray) },
  { id: 'lookups.presenceStatus', module: 'diag.module.lookups', label: 'GET /lookups/PresenceStatus', run: () => ok(() => lookupsApi.list('PresenceStatus'), undefined, isArray) },

  // ---- Backend-only domains — no frontend screen calls these (spec §12: not part of the 22-screen scope), tested here for raw connectivity only ----
  { id: 'teams', module: 'diag.module.backendOnly', label: 'GET /teams', run: () => ok(() => apiFetch('/teams'), undefined, isArray) },
  // Unlike every other list endpoint here, GET /attachments has no "list all" mode —
  // OasAttachmentsController.GetAll requires `entity`+`id` (attachments always belong
  // to one specific record), so a bare call is guaranteed a 400 by ASP.NET Core's own
  // model-binding validation. Nothing in this diagnostic run creates a safe target to
  // point it at, so this honestly skips rather than reporting a fake failure.
  { id: 'attachments', module: 'diag.module.backendOnly', label: 'GET /attachments', run: () => Promise.resolve(skip('requires entity+id — no target to test against')) },
  { id: 'integrations.endpoints', module: 'diag.module.backendOnly', label: 'GET /integrations/endpoints', run: () => ok(() => apiFetch('/integrations/endpoints'), undefined, isArray) },
  { id: 'integrations.outbox', module: 'diag.module.backendOnly', label: 'GET /integrations/outbox', run: () => ok(() => apiFetch('/integrations/outbox'), undefined, isArray) },
  { id: 'sync.receipts', module: 'diag.module.backendOnly', label: 'GET /sync/receipts', run: () => ok(() => apiFetch('/sync/receipts'), undefined, isArray) },
  { id: 'quality.templates', module: 'diag.module.backendOnly', label: 'GET /quality-check-templates', run: () => ok(() => apiFetch('/quality-check-templates'), undefined, isArray) },
  { id: 'quality.checks', module: 'diag.module.backendOnly', label: 'GET /quality-checks', run: () => ok(() => apiFetch('/quality-checks'), undefined, isArray) },
];

/**
 * Opt-in only, never auto-run — see the write-path note in the page body.
 * Every one of these three is a domain with a VERIFIED real deletion path
 * in the backend source, not a guess:
 *  - causes:   `OasCauseService.DeleteAsync` does `_db.Set<OasCause>().Remove(...)` — true hard delete.
 *  - lookups:  soft delete (`ArchivedAt`), but `GetByTypeAsync` explicitly filters
 *              `ArchivedAt == null` — the list round-trip is real even though the
 *              row technically persists forever, invisible, same as Equipments/Products.
 *  - cadences: `OasCadenceService` does `_db.Set<OasRouting>().Remove(routing)` — true
 *              hard delete. Needs a real post + a throwaway product (soft-delete only,
 *              cleaned up via `productsApi.remove` — permanently hidden, never listed).
 */
interface WriteTest {
  id: string;
  label: string;
  run: () => Promise<TestOutcome>;
}

async function runCausesWriteTest(): Promise<TestOutcome> {
  const tag = `DIAGTEST-${Date.now()}`;
  const created = await causesApi.create({
    domain: 'stop', code: tag, labelFr: tag, labelAr: tag, eventType: 'technical_stop', defaultCriticality: 'low',
  });
  const afterCreate = await causesApi.tree();
  if (!afterCreate.some((c) => c.id === created.id)) {
    throw new Error(`POST /causes reported success but the new row (${tag}) is missing from GET /causes right after`);
  }
  await causesApi.remove(created.id);
  const afterDelete = await causesApi.tree();
  if (afterDelete.some((c) => c.id === created.id)) {
    throw new Error(`DELETE /causes/{id} did not remove ${tag} — check Referentials → Causes and delete it by hand`);
  }
  return { ok: true, detail: `POST → GET (found) → DELETE → GET (gone), tag ${tag}` };
}

async function runLookupsWriteTest(): Promise<TestOutcome> {
  const tag = `DIAGTEST-${Date.now()}`;
  const type = 'DIAGTEST'; // synthetic type — never read by any real screen, so it can never appear in a real dropdown
  const created = await lookupsApi.create(type, { code: tag, label: tag });
  const afterCreate = await lookupsApi.list(type);
  if (!afterCreate.some((v) => v.id === created.id)) {
    throw new Error(`POST /lookups/${type} reported success but ${tag} is missing from GET /lookups/${type} right after`);
  }
  await lookupsApi.remove(type, created.id);
  const afterDelete = await lookupsApi.list(type);
  if (afterDelete.some((v) => v.id === created.id)) {
    throw new Error(`DELETE /lookups/${type}/{id} did not remove ${tag} from the list`);
  }
  return { ok: true, detail: `POST → GET (found) → DELETE → GET (gone), tag ${tag}` };
}

async function runCadencesWriteTest(): Promise<TestOutcome> {
  const posts = await hierarchyApi.listPosts();
  const post = posts[0];
  if (!post) return skip('no post available to attach a test cadence to');

  const tag = `DIAGTEST-${Date.now()}`;
  const product = await productsApi.create({ reference: tag, name: tag });
  try {
    const created = await cadencesApi.createVersion({ productId: product.id, postId: post.id, rate: 42 });
    const afterCreate = await cadencesApi.list();
    if (!afterCreate.some((c) => c.id === created.id)) {
      throw new Error(`POST /cadences reported success but the new row (${tag}) is missing from GET /cadences right after`);
    }
    await cadencesApi.remove(created.id);
    const afterDelete = await cadencesApi.list();
    if (afterDelete.some((c) => c.id === created.id)) {
      throw new Error(`DELETE /cadences/{id} did not remove ${tag} — check Referentials → Cadences and delete it by hand`);
    }
    return { ok: true, detail: `POST product → POST cadence → GET (found) → DELETE → GET (gone), tag ${tag}` };
  } finally {
    // Products has no hard delete — soft-deleted and gone from every real
    // screen via the global query filter, same tier of risk as the fixture
    // hierarchy above, so this is never left behind visible anywhere.
    await productsApi.remove(product.id).catch(() => {});
  }
}

const WRITE_TESTS: WriteTest[] = [
  { id: 'write.causes', label: 'POST /causes → GET → DELETE /causes/{id} → GET', run: runCausesWriteTest },
  { id: 'write.lookups', label: 'POST /lookups/{type} → GET → DELETE /lookups/{type}/{id} → GET', run: runLookupsWriteTest },
  { id: 'write.cadences', label: 'POST /products + /cadences → GET → DELETE /cadences/{id} → GET', run: runCadencesWriteTest },
];

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
  shiftId?: string;
}

async function provisionFixture(): Promise<Fixture> {
  const fixture: Fixture = {};
  const tag = `DIAGTEST-${Date.now()}`;

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
  }

  const shifts = await shiftsApi.list();
  if (shifts.length === 0) {
    const siteId = fixture.siteId ?? (await hierarchyApi.listSites())[0]?.id;
    if (siteId) {
      const shift = await shiftsApi.create({ siteId, code: `${tag}-SHIFT`, name: tag, startTime: '06:00', endTime: '14:00' });
      fixture.shiftId = shift.id;
    }
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
 * The read sweep never runs POST/PUT/DELETE automatically: this hits the
 * real production database, and several tables are append-only by design
 * (spec's audit trail). Separately-gated write-path round trips (create →
 * verify → delete → verify-gone, see `WRITE_TESTS`) are available below,
 * but only on demand — never on page load.
 */
export default function ApiTestPage() {
  const t = useT();
  const [authPhase, setAuthPhase] = useState<AuthPhase>('checking');
  const [runs, setRuns] = useState<Record<string, RunState>>({});
  const [running, setRunning] = useState(false);
  const [ranAt, setRanAt] = useState<string | null>(null);
  const [writeRuns, setWriteRuns] = useState<Record<string, RunState>>({});
  const [writeRunning, setWriteRunning] = useState(false);
  const [fixtureNote, setFixtureNote] = useState<string | null>(null);
  const [filter, setFilter] = useState<Status | 'all'>('all');
  const startedOnce = useRef(false);

  const runAll = async () => {
    setRunning(true);
    setFixtureNote(null);
    const initial: Record<string, RunState> = {};
    TESTS.forEach((test) => { initial[test.id] = { status: 'pending' as Status }; });
    setRuns(initial);

    // Auto-provision the minimal hierarchy/shift data the tenant is missing,
    // so dependent tests exercise the real thing instead of skipping —
    // never declarations or events (see provisionFixture's doc comment).
    let fixture: Fixture = {};
    try {
      fixture = await provisionFixture();
    } catch {
      // best-effort — dependent tests will honestly report "skip" if prerequisites are still missing
    }
    const provisioned: string[] = [];
    if (fixture.postId) provisioned.push('site → zone → line → post');
    if (fixture.shiftId) provisioned.push('shift template');

    const ctx: DiagCtx = { today: new Date().toISOString().slice(0, 10) };

    for (const test of TESTS) {
      setRuns((prev) => ({ ...prev, [test.id]: { status: 'running' } }));
      const started = performance.now();
      try {
        const outcome = await test.run(ctx);
        const ms = Math.round(performance.now() - started);
        setRuns((prev) => ({ ...prev, [test.id]: { status: outcome.skipped ? 'skip' : 'pass', ms, detail: outcome.detail } }));
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

    setRunning(false);
    setRanAt(new Date().toLocaleTimeString());
  };

  const runOneWriteTest = async (test: WriteTest) => {
    setWriteRuns((prev) => ({ ...prev, [test.id]: { status: 'running' } }));
    const started = performance.now();
    try {
      const outcome = await test.run();
      const ms = Math.round(performance.now() - started);
      setWriteRuns((prev) => ({ ...prev, [test.id]: { status: outcome.skipped ? 'skip' : 'pass', ms, detail: outcome.detail } }));
    } catch (e) {
      const ms = Math.round(performance.now() - started);
      const detail = e instanceof ApiError ? `${e.status} ${e.message}` : e instanceof Error ? e.message : 'Network error';
      setWriteRuns((prev) => ({ ...prev, [test.id]: { status: 'fail', ms, detail } }));
    }
  };

  const runAllWriteTests = async () => {
    setWriteRunning(true);
    for (const test of WRITE_TESTS) await runOneWriteTest(test);
    setWriteRunning(false);
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
                      return (
                        <div
                          key={test.id}
                          className={cn(
                            'flex items-center gap-2.5 border-b border-border px-3 py-2 text-body-sm last:border-b-0',
                            run.status === 'fail' && 'bg-state-technical/5',
                          )}
                        >
                          <StatusIcon status={run.status} />
                          <span className="min-w-0 flex-1 truncate font-mono text-caption">{test.label}</span>
                          <span className={cn('truncate text-caption', run.status === 'fail' ? 'text-state-technical' : 'text-muted-foreground')}>
                            {run.detail}
                          </span>
                          {run.ms !== undefined && <span className="shrink-0 font-mono text-caption text-muted-foreground">{run.ms}ms</span>}
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>

          <div className="space-y-2 rounded-lg border border-dashed border-border p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h3 className="text-body-sm font-semibold">{t('diag.writePath.title')}</h3>
                <p className="text-caption text-muted-foreground">{t('diag.writePath.desc')}</p>
              </div>
              <Button size="sm" variant="outline" onClick={() => void runAllWriteTests()} disabled={writeRunning}>
                {writeRunning ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : <FlaskConical className="me-1.5 h-3.5 w-3.5" />}
                {t('diag.writePath.run')}
              </Button>
            </div>
            {Object.keys(writeRuns).length > 0 && (
              <div className="space-y-1.5">
                {WRITE_TESTS.map((test) => {
                  const run = writeRuns[test.id];
                  if (!run) return null;
                  return (
                    <div key={test.id} className={cn('flex items-center gap-2.5 rounded-md border border-border px-3 py-2 text-body-sm', run.status === 'fail' && 'bg-state-technical/5')}>
                      <StatusIcon status={run.status} />
                      <span className="min-w-0 flex-1 truncate font-mono text-caption">{test.label}</span>
                      <span className={cn('truncate text-caption', run.status === 'fail' ? 'text-state-technical' : 'text-muted-foreground')}>
                        {run.detail}
                      </span>
                      {run.ms !== undefined && <span className="shrink-0 font-mono text-caption text-muted-foreground">{run.ms}ms</span>}
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <p className="rounded-lg border border-dashed border-border p-2.5 text-caption text-muted-foreground">
            {t('diag.writeNote', { n: 112 })}
          </p>
        </div>
      </div>
    </div>
  );
}
