/**
 * Operator session store — QR scan-and-declare flow, backed by the real
 * `/api/oas/post-sessions`, `/api/oas/declarations` and `/api/oas/changeovers`
 * endpoints (spec §6.1). Declarations/stops stay session-scoped locally (a
 * product decision made explicitly for this bascule — mobile history is not
 * re-fetched from the server) but every create/close attempts real
 * persistence immediately; a failed attempt is retried by `flushPending()`,
 * replacing the old `setTimeout` + `Math.random() < 0.2` network simulation
 * with real `fetch` failures.
 */

import { useEffect, useState, useSyncExternalStore } from 'react';
import { kindToState, type EventKind } from '@/oas/demo';
import { getRefState } from '@/oas/refStore';
import { getHierarchyState } from '@/oas/hierarchyStore';
import { currentShiftId, currentWorkDate } from '@/oas/assignmentStore';
import { presenceApi } from '@/oas/api/assignments';
import { getAuth } from '@/oas/authStore';
import { declarationsApi, postSessionsApi } from '@/oas/api/operations';
import { productionOrdersApi, type OasProductionOrderDto } from '@/oas/api/referentials';
import { ApiError } from '@/oas/api/client';

const KIND_TO_EVENT_TYPE: Record<EventKind, string> = {
  technical: 'technical_stop', quality: 'quality_stop', material: 'material_wait', changeover: 'changeover',
};

export type Shift = 'morning' | 'afternoon' | 'night';

/** Morning 06h–14h · afternoon 14h–22h · night 22h–06h. */
function getShift(date: Date = new Date()): Shift {
  const h = date.getHours();
  if (h >= 6 && h < 14) return 'morning';
  if (h >= 14 && h < 22) return 'afternoon';
  return 'night';
}

export interface OperatorSession {
  /** Real `oas_post_sessions` id — needed for relay/close. */
  id: string;
  postId: string;
  postCode: string;
  lineKey: string;
  order: string;
  ref: string;
  /** parts per hour */
  cadence: number;
  /** planned quantity for the work order */
  targetQty: number;
  operator: string;
  startedAt: string;
  /** last time production was declared (or session start) */
  lastDeclaredAt: string;
  /** BL-019 — operator confirmed presence at the assigned post. */
  presenceConfirmedAt?: string | null;
  /** Shift the session was opened in — derived from the clock, not declared. */
  shift: Shift;
}

export interface DeclarationEntry {
  clientEventId: string;
  /** The server's real primary key, captured once the declaration is first persisted — required for `PUT /declarations/{id}/correct`, which looks up by this, never by `clientEventId`. */
  serverId?: string;
  kind: 'production' | 'scrap' | 'rework';
  postCode: string;
  quantityOk: number;
  quantityNok: number;
  scrapCause: string | null;
  occurredAt: string;
  synced: boolean;
  /** BL-045 — set when the entry was corrected inside the 10-min window. */
  correctedAt?: string;
  correctionReason?: string;
  /** Who corrected it (operator himself, or a team leader from the console). */
  correctedBy?: string;
  /** Value before the correction, kept visible — a correction is never a deletion. */
  originalQty?: number;
  /** BL-025 — entry recorded while a stop was running (deferred reading). */
  deferredStopId?: string;
}

export interface StopEntry {
  clientEventId: string;
  postCode: string;
  eventType: string;
  causeCode: string;
  causeLabelKey: string;
  declaredAt: string;
  closedAt: string | null;
  synced: boolean;
  /** BL-008 — free text when the operator signals a missing cause. */
  note?: string;
}

export interface SessionState {
  session: OperatorSession | null;
  declarations: DeclarationEntry[];
  stops: StopEntry[];
}

const KEY = 'oas.operator.session.v1';
const EMPTY: SessionState = { session: null, declarations: [], stops: [] };

function read(): SessionState {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return EMPTY;
    return { ...EMPTY, ...(JSON.parse(raw) as Partial<SessionState>) };
  } catch {
    return EMPTY;
  }
}

let state: SessionState = typeof window === 'undefined' ? EMPTY : read();
const listeners = new Set<() => void>();

function commit(next: SessionState) {
  state = next;
  try {
    localStorage.setItem(KEY, JSON.stringify(next));
  } catch {
    /* storage full / private mode — keep memory state */
  }
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

/** Non-reactive read — for imperative callers (guided demo, scripts). */
export function getSessionState(): SessionState {
  return state;
}

/**
 * A session persisted in localStorage might no longer be valid server-side
 * (closed from another device, expired, the shift ended and
 * `OasSessionWatchdogHostedService` auto-closed it) — verify once per app
 * load against `GET /post-sessions/active` (spec §1.5 OperatorHome) rather
 * than trusting the local cache blindly forever.
 */
let sessionChecked = false;
function ensureSessionValid() {
  if (sessionChecked || !state.session) return;
  sessionChecked = true;
  const localId = state.session.id;
  void postSessionsApi.active()
    .then((dto) => {
      if (dto.id !== localId) {
        // A different (or no, on 404 below) session is active server-side — the
        // local one is stale; drop it so the operator re-scans rather than
        // silently declaring against a session the server no longer recognizes.
        commit(EMPTY);
      }
    })
    .catch(() => commit(EMPTY));
}

export function useSessionState(): SessionState {
  ensureSessionValid();
  return useSyncExternalStore(subscribe, () => state, () => EMPTY);
}

const uid = () =>
  (globalThis.crypto?.randomUUID?.() ??
    `${Date.now()}-${Math.random().toString(16).slice(2)}`);

/* ------------------------------------------------------------------ */
/* QR payloads                                                         */
/* ------------------------------------------------------------------ */

/**
 * Resolves a scanned/typed sticker payload — plain code, `OAS:PO-104`,
 * `oas://post/PO-104`, a URL, or a rotated server-issued QR token — against
 * the real backend (`POST /post-sessions/scan`, spec §6.1). This is
 * deliberately a server round-trip rather than local regex matching: post
 * QR badges carry a token that's generated and rotated server-side
 * (`GET/POST /posts/{id}/qr-token[/rotate]`), so a client-only parser can
 * never recognize a real printed badge, only a manually-typed post code.
 */
/** Throws on a network/server failure — the caller distinguishes that (retry, don't blame the badge) from a genuinely-unrecognized code (`null`, blame the badge). */
export async function scanCode(raw: string): Promise<string | null> {
  const value = raw.trim();
  if (!value) return null;
  const result = await postSessionsApi.scan(value);
  return result.resolved && result.postCode ? result.postCode : null;
}

export interface ResolvedPost { code: string; lineKey: string }

/** Resolves a scanned/typed post code against the real hierarchy — `lineKey` is the real line name. */
export function findPost(code: string): ResolvedPost | undefined {
  const post = resolveHierarchyPost(code);
  if (!post) return undefined;
  const lineName = getHierarchyState().lines.find((l) => l.id === post.lineId)?.name ?? '—';
  return { code: post.code, lineKey: lineName };
}

function resolveHierarchyPost(code: string) {
  return getHierarchyState().posts.find((p) => p.code.toUpperCase() === code.toUpperCase());
}

/* ------------------------------------------------------------------ */
/* Actions                                                             */
/* ------------------------------------------------------------------ */

/**
 * Why `openSession()` refused to open — surfaced so the scan screen can
 * always tell the operator *something* concrete, instead of the scan
 * silently doing nothing. `shift` is detected client-side (a previous
 * session from another shift was left open); `postBusy`/`userBusy` are the
 * server's own 409s (`post_already_has_active_session` /
 * `user_already_has_active_session`) — both are force-retryable the same
 * way as `shift` (`ForceRelay` on the server); `error` is anything else
 * (network failure, unexpected status) and is not force-retryable.
 */
export type OpenConflict =
  | { reason: 'shift'; previousShift: Shift; previousPost: string; currentShift: Shift }
  | { reason: 'postBusy' | 'userBusy' | 'error' };

let openConflict: OpenConflict | null = null;
const openConflictListeners = new Set<() => void>();

function setOpenConflict(c: OpenConflict | null) {
  openConflict = c;
  openConflictListeners.forEach((l) => l());
}

function subscribeOpenConflict(l: () => void) {
  openConflictListeners.add(l);
  return () => openConflictListeners.delete(l);
}

export function useOpenConflict(): OpenConflict | null {
  return useSyncExternalStore(subscribeOpenConflict, () => openConflict, () => null);
}

export function dismissOpenConflict() {
  setOpenConflict(null);
}

/**
 * Open a session for the scanned post. If a session from a *different* shift
 * was left open (previous operator forgot to end shift), the call is refused
 * once and a warning is raised — pass `force` to open anyway (relayed
 * server-side too, via `ForceRelay`).
 */
export async function openSession(
  code: string,
  operator = getAuth()?.displayName ?? getAuth()?.email ?? '—',
  force = false,
): Promise<OperatorSession | null> {
  const hierarchyPost = resolveHierarchyPost(code);
  if (!hierarchyPost) {
    // Shouldn't happen — `scanCode()` already validated this code against
    // the server — but a stale/not-yet-loaded local hierarchy could still
    // race here, so this stays explicative rather than a silent no-op.
    setOpenConflict({ reason: 'error' });
    return null;
  }
  const lineName = getHierarchyState().lines.find((l) => l.id === hierarchyPost.lineId)?.name ?? '—';
  const currentShift = getShift();
  if (!force && state.session && state.session.shift !== currentShift) {
    setOpenConflict({
      reason: 'shift',
      previousShift: state.session.shift,
      previousPost: state.session.postCode,
      currentShift,
    });
    return null;
  }
  setOpenConflict(null);

  const cadenceRow = getRefState().cadences.find((c) => c.postId === hierarchyPost.id);

  // Best-effort match of the post's line (preferring the cadence's own
  // product) to an open production order, so the session carries a real
  // order/target instead of a placeholder — spec doesn't add a picker screen
  // for this, so it's resolved the same way cadence already is (BL-004/005).
  let matchedOrder: OasProductionOrderDto | undefined;
  try {
    const orders = await productionOrdersApi.list();
    const lineOrders = orders.filter(
      (o) => o.lineId === hierarchyPost.lineId && o.status !== 'done' && o.status !== 'cancelled',
    );
    matchedOrder =
      lineOrders.find((o) => o.status === 'in_progress' && o.productId === cadenceRow?.productId) ??
      lineOrders.find((o) => o.status === 'in_progress') ??
      lineOrders
        .filter((o) => o.productId === cadenceRow?.productId)
        .sort((a, b) => (a.dueDate ?? '').localeCompare(b.dueDate ?? ''))[0] ??
      lineOrders.sort((a, b) => (a.dueDate ?? '').localeCompare(b.dueDate ?? ''))[0];
  } catch {
    matchedOrder = undefined;
  }

  let dto;
  try {
    dto = await postSessionsApi.open({
      clientEventId: uid(), postId: hierarchyPost.id, startedVia: 'qr', forceRelay: force,
      productionOrderId: matchedOrder?.id,
    });
  } catch (e) {
    // The server refuses a second concurrent session on the same post, or a
    // second session for the same user, unless force-relayed — surface which
    // one it was so the scan screen shows a real message instead of nothing.
    const code = e instanceof ApiError ? e.message : null;
    setOpenConflict({
      reason: code === 'post_already_has_active_session' ? 'postBusy' : code === 'user_already_has_active_session' ? 'userBusy' : 'error',
    });
    return null;
  }

  const session: OperatorSession = {
    id: dto.id,
    postId: hierarchyPost.id,
    postCode: hierarchyPost.code,
    lineKey: lineName,
    order: matchedOrder?.orderNumber ?? '—',
    ref: cadenceRow?.ref ?? '—',
    cadence: cadenceRow?.rate ?? 60,
    targetQty: matchedOrder?.quantityPlanned ?? 500,
    operator,
    startedAt: dto.startedAt,
    lastDeclaredAt: dto.startedAt,
    presenceConfirmedAt: null,
    shift: currentShift,
  };
  commit({ session, declarations: [], stops: [] });
  return session;
}

/**
 * BL-017 — shared tablet: hand the running session over to the next operator
 * without closing the post. The queue and the open stops stay untouched; only
 * the holder changes, and presence must be confirmed again.
 */
export async function switchOperator(operator: string) {
  if (!state.session || !operator.trim()) return;
  const newUser = getRefState().users.find((u) => u.name === operator.trim());
  if (newUser) {
    try {
      await postSessionsApi.relay(state.session.id, uid(), newUser.id);
    } catch {
      /* best-effort — the local handover still proceeds so the tablet keeps working offline */
    }
  }
  const now = new Date().toISOString();
  commit({
    ...state,
    session: { ...state.session, operator: operator.trim(), startedAt: now, presenceConfirmedAt: null },
  });
}

/** BL-019 — presence confirmation at shift start. */
export async function confirmPresence() {
  if (!state.session) return;
  const userId = getAuth()?.id;
  const shiftId = currentShiftId();
  if (userId && shiftId) {
    try {
      await presenceApi.confirm(userId, { workDate: currentWorkDate(), shiftTemplateId: shiftId });
    } catch {
      /* best-effort — presence still shows confirmed locally */
    }
  }
  commit({ ...state, session: { ...state.session, presenceConfirmedAt: new Date().toISOString() } });
}

/** BL-023 — how a shift ends: relay to the next operator, or post left stopped. */
export type ClosureType = 'operator' | 'system' | 'relay' | 'post-stop';

/**
 * Close the session. `reason` records how it ended (M3 / BL-023): the operator
 * himself, a relay handover, a post left stopped, or the system when the shift
 * ended without an end-of-shift declaration.
 */
export async function closeSession(reason: ClosureType = 'operator') {
  // A relay keeps the post session alive for the incoming operator: only the
  // holder is cleared, the queue and the open stops stay in place.
  if (reason === 'relay' && state.session) {
    commit({ ...state, session: { ...state.session, presenceConfirmedAt: null } });
    return;
  }
  if (state.session) {
    try {
      await postSessionsApi.close(state.session.id, uid());
    } catch {
      /* best-effort — the local close still proceeds so the tablet keeps working offline */
    }
  }
  commit(EMPTY);
}

/* ------------------------------------------------------------------ */
/* System closure + declaration reminder (M3 / M4)                      */
/* ------------------------------------------------------------------ */

/** Minutes without a production entry before the app nags the operator. */
export const DECLARATION_REMINDER_MIN = 30;

/** Grace period after the shift boundary before the system closes a session. */
const SYSTEM_CLOSE_GRACE_MIN = 15;

/** Was the session opened in a shift that is now over? */
function shiftEndedFor(s: SessionState = state, now = new Date()): boolean {
  if (!s.session) return false;
  if (s.session.shift === getShift(now)) return false;
  const openMin = (now.getTime() - Date.parse(s.session.startedAt)) / 60_000;
  return openMin > SYSTEM_CLOSE_GRACE_MIN;
}

/**
 * Close a session the operator left open after his shift ended — the stop, if
 * any, is closed too so the post does not stay red forever.
 */
function autoCloseEndedShift(): boolean {
  if (!shiftEndedFor()) return false;
  state.stops.filter((x) => !x.closedAt).forEach((x) => void closeStop(x.clientEventId));
  void closeSession('system');
  return true;
}

/** Minutes overdue on the periodic production reading (0 = nothing due). */
function reminderOverdueMin(s: SessionState = state, now = Date.now()): number {
  if (!s.session) return 0;
  const since = Math.floor((now - Date.parse(s.session.lastDeclaredAt)) / 60_000);
  return since >= DECLARATION_REMINDER_MIN ? since : 0;
}

/** Live reminder + system-closure watcher, ticked once a minute. */
export function useDeclarationReminder(): number {
  const s = useSessionState();
  const [, force] = useState(0);
  useEffect(() => {
    const id = window.setInterval(() => {
      autoCloseEndedShift();
      force((n) => n + 1);
    }, 60_000);
    autoCloseEndedShift();
    return () => window.clearInterval(id);
  }, []);
  return reminderOverdueMin(s);
}

/* ------------------------------------------------------------------ */
/* Real persistence for locally-committed rows (immediate attempt +     */
/* retry via flushPending — replaces the old setTimeout/Math.random sim) */
/* ------------------------------------------------------------------ */

async function persistDeclaration(d: DeclarationEntry): Promise<string | null> {
  const hierarchyPost = resolveHierarchyPost(d.postCode);
  if (!hierarchyPost) return null;
  try {
    const dto = d.kind === 'scrap'
      ? await declarationsApi.createScrap({
          clientEventId: d.clientEventId, postId: hierarchyPost.id,
          quantityNok: d.quantityNok, note: d.scrapCause ?? undefined, occurredAt: d.occurredAt,
        })
      : await declarationsApi.createProduction({
          clientEventId: d.clientEventId, postId: hierarchyPost.id,
          quantityOk: d.quantityOk, quantityNok: d.quantityNok, occurredAt: d.occurredAt,
        });
    return dto.id;
  } catch {
    return null;
  }
}

function markDeclarationSynced(clientEventId: string, serverId: string | null) {
  commit({
    ...state,
    declarations: state.declarations.map((d) =>
      d.clientEventId === clientEventId ? { ...d, synced: serverId !== null, serverId: serverId ?? d.serverId } : d,
    ),
  });
}

async function persistStop(s: StopEntry): Promise<boolean> {
  const hierarchyPost = resolveHierarchyPost(s.postCode);
  if (!hierarchyPost) return false;
  try {
    if (!s.closedAt) {
      const kind = (s.eventType in kindToState ? s.eventType : 'technical') as EventKind;
      await declarationsApi.declareStop({
        clientEventId: s.clientEventId,
        eventType: KIND_TO_EVENT_TYPE[kind],
        postId: hierarchyPost.id,
        note: s.note,
        declaredAt: s.declaredAt,
      });
    } else {
      await declarationsApi.closeStop(s.clientEventId, 'resolved');
    }
    return true;
  } catch (e) {
    // A 409 on the create leg means the event already exists (e.g. a retried
    // idempotent call after a prior success went unacknowledged) — treat as synced.
    return e instanceof ApiError && e.status === 409;
  }
}

function markStopSynced(clientEventId: string, synced: boolean) {
  commit({ ...state, stops: state.stops.map((s) => (s.clientEventId === clientEventId ? { ...s, synced } : s)) });
}

export function declareProduction(quantityOk: number, quantityNok: number, scrapCause: string | null = null) {
  if (!state.session) return;
  const now = new Date().toISOString();
  // BL-025 — a reading entered while a stop is running is flagged as deferred.
  const openStop = state.stops.find((x) => !x.closedAt) ?? null;
  const deferredStopId = openStop?.clientEventId;
  const rows: DeclarationEntry[] = [];
  if (quantityOk > 0) {
    rows.push({
      clientEventId: uid(), kind: 'production', postCode: state.session.postCode,
      quantityOk, quantityNok: 0, scrapCause: null, occurredAt: now, synced: false, deferredStopId,
    });
  }
  if (quantityNok > 0) {
    rows.push({
      clientEventId: uid(), kind: 'scrap', postCode: state.session.postCode,
      quantityOk: 0, quantityNok, scrapCause, occurredAt: now, synced: false, deferredStopId,
    });
  }
  if (!rows.length) return;
  commit({
    ...state,
    session: { ...state.session, lastDeclaredAt: now },
    declarations: [...rows, ...state.declarations],
  });
  rows.forEach((row) => { void persistDeclaration(row).then((ok) => markDeclarationSynced(row.clientEventId, ok)); });
}

const CORRECTION_WINDOW_MIN = 10;

/** BL-045 — a declaration may be corrected (with a reason) for 10 minutes. */
export function isCorrectable(d: DeclarationEntry, now = Date.now()): boolean {
  return (now - Date.parse(d.occurredAt)) / 60_000 <= CORRECTION_WINDOW_MIN;
}

export function correctDeclaration(
  clientEventId: string, quantityOk: number, quantityNok: number, reason: string, by?: string,
) {
  const target = state.declarations.find((d) => d.clientEventId === clientEventId);
  commit({
    ...state,
    declarations: state.declarations.map((d) =>
      d.clientEventId === clientEventId
        ? {
            ...d,
            quantityOk: d.kind === 'scrap' ? 0 : quantityOk,
            quantityNok: d.kind === 'scrap' ? quantityNok : 0,
            correctedAt: new Date().toISOString(),
            correctionReason: reason,
            correctedBy: by ?? state.session?.operator ?? 'operator',
            originalQty: d.originalQty ?? (d.kind === 'scrap' ? d.quantityNok : d.quantityOk),
            synced: false,
          }
        : d,
    ),
  });
  // The server looks the original up by its real id, never by clientEventId
  // (see `declarationsApi.correct`) — if it hasn't synced yet (no serverId),
  // there is nothing to correct server-side yet; the row stays unsynced and
  // a future flush of the ORIGINAL will still land, just without this edit.
  if (!target?.serverId) return;
  void declarationsApi
    .correct(target.serverId, { clientEventId: uid(), quantityOk, quantityNok, reason })
    .then(() => markDeclarationSynced(clientEventId, target.serverId!))
    .catch(() => markDeclarationSynced(clientEventId, null));
}

export function declareStop(causeCode: string, causeLabelKey: string, eventType: EventKind, note?: string) {
  if (!state.session) return;
  const stop: StopEntry = {
    clientEventId: uid(),
    postCode: state.session.postCode,
    eventType,
    causeCode,
    causeLabelKey,
    declaredAt: new Date().toISOString(),
    closedAt: null,
    synced: false,
    note,
  };
  commit({ ...state, stops: [stop, ...state.stops] });
  void persistStop(stop).then((ok) => markStopSynced(stop.clientEventId, ok));
}

export function closeStop(clientEventId: string) {
  const stop = state.stops.find((s) => s.clientEventId === clientEventId);
  if (!stop) return;
  const closed: StopEntry = { ...stop, closedAt: new Date().toISOString(), synced: false };
  commit({
    ...state,
    stops: state.stops.map((s) => (s.clientEventId === clientEventId ? closed : s)),
  });
  void persistStop(closed).then((ok) => markStopSynced(clientEventId, ok));
}

/**
 * Flush the offline queue: retries every unsynced declaration/stop against
 * the real API. Safe to call repeatedly — idempotent via `clientEventId`.
 */
async function syncPending(): Promise<number> {
  const declarations = state.declarations.filter((d) => !d.synced);
  const stops = state.stops.filter((s) => !s.synced);
  const results = await Promise.all([
    ...declarations.map(async (d) => { const ok = await persistDeclaration(d); markDeclarationSynced(d.clientEventId, ok); return ok; }),
    ...stops.map(async (s) => { const ok = await persistStop(s); markStopSynced(s.clientEventId, ok); return ok; }),
  ]);
  return results.filter(Boolean).length;
}

/* ------------------------------------------------------------------ */
/* Connectivity + real background sync                                 */
/* ------------------------------------------------------------------ */

export type SyncStatus = 'idle' | 'syncing' | 'error';

let online = typeof navigator === 'undefined' ? true : navigator.onLine;
let syncStatus: SyncStatus = 'idle';
const connListeners = new Set<() => void>();
const statusListeners = new Set<() => void>();

function setOnline(v: boolean) {
  if (online === v) return;
  online = v;
  connListeners.forEach((l) => l());
}

function setSyncStatus(v: SyncStatus) {
  syncStatus = v;
  statusListeners.forEach((l) => l());
}

/** Live connectivity flag — backed by `navigator.onLine` + the browser events. */
export function useOnline(): boolean {
  return useSyncExternalStore(
    (l) => {
      connListeners.add(l);
      return () => connListeners.delete(l);
    },
    () => online,
    () => true,
  );
}

/** Current state of the background sync (`idle` / `syncing` / `error`). */
export function useSyncStatus(): SyncStatus {
  return useSyncExternalStore(
    (l) => {
      statusListeners.add(l);
      return () => statusListeners.delete(l);
    },
    () => syncStatus,
    () => 'idle',
  );
}

let flushInFlight: Promise<void> | null = null;

function pendingCount(): number {
  return state.declarations.filter((d) => !d.synced).length + state.stops.filter((s) => !s.synced).length;
}

/** Real flush: retries every unsynced row against the API, with a short backoff between attempts. */
export function flushPending(): Promise<void> {
  if (!pendingCount()) {
    setSyncStatus('idle');
    return Promise.resolve();
  }
  if (flushInFlight) return flushInFlight;
  if (!online) {
    setSyncStatus('error');
    return Promise.resolve();
  }
  setSyncStatus('syncing');
  flushInFlight = syncPending()
    .then(() => {
      setSyncStatus(pendingCount() > 0 ? 'error' : 'idle');
    })
    .catch(() => setSyncStatus('error'))
    .finally(() => {
      flushInFlight = null;
    });
  return flushInFlight;
}

if (typeof window !== 'undefined') {
  window.addEventListener('online', () => {
    setOnline(true);
    void flushPending();
  });
  window.addEventListener('offline', () => {
    setOnline(false);
    setSyncStatus('error');
  });
}

/* ------------------------------------------------------------------ */
/* Derived metrics — everything the system can compute by itself       */
/* ------------------------------------------------------------------ */

export interface SessionMetrics {
  totalOk: number;
  totalNok: number;
  qualityRate: number | null;
  /** minutes since session start */
  elapsedMin: number;
  /** minutes lost to stops (closed + running) */
  stopMin: number;
  /** minutes since last production declaration */
  sinceLastMin: number;
  /** parts the cadence says should have been made since the last declaration */
  suggestedQty: number;
  progressPct: number;
  openStop: StopEntry | null;
  pendingSync: number;
}

const minutesBetween = (from: string, to: number) =>
  Math.max(0, Math.round((to - Date.parse(from)) / 60_000));

export function computeMetrics(s: SessionState, now = Date.now()): SessionMetrics {
  const totalOk = s.declarations.reduce((a, d) => a + d.quantityOk, 0);
  const totalNok = s.declarations.reduce((a, d) => a + d.quantityNok, 0);
  const openStop = s.stops.find((x) => !x.closedAt) ?? null;
  const stopMin = s.stops.reduce(
    (a, x) => a + minutesBetween(x.declaredAt, x.closedAt ? Date.parse(x.closedAt) : now),
    0,
  );
  const elapsedMin = s.session ? minutesBetween(s.session.startedAt, now) : 0;
  const sinceLastMin = s.session ? minutesBetween(s.session.lastDeclaredAt, now) : 0;
  const runMin = Math.max(0, sinceLastMin - (openStop ? minutesBetween(openStop.declaredAt, now) : 0));
  const cadence = s.session?.cadence ?? 0;
  return {
    totalOk,
    totalNok,
    qualityRate: totalOk + totalNok === 0 ? null : Math.round((totalOk / (totalOk + totalNok)) * 100),
    elapsedMin,
    stopMin,
    sinceLastMin,
    suggestedQty: Math.max(0, Math.round((runMin / 60) * cadence)),
    progressPct: s.session ? Math.min(100, Math.round((totalOk / s.session.targetQty) * 100)) : 0,
    openStop,
    pendingSync: s.declarations.filter((d) => !d.synced).length + s.stops.filter((x) => !x.synced).length,
  };
}
