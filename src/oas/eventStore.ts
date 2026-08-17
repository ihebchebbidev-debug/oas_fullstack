/**
 * Shared shop-floor event store — backed by the real `/api/oas/events` state
 * machine (spec §6.1) and `GET /api/oas/stream` (SSE, spec §6.3). The 30s
 * client-side escalation timer is gone: `OasEscalationSweepHostedService`
 * runs server-side regardless of any tab being open, and pushes
 * `event.updated` over SSE the moment it acts.
 *
 * The old prototype's `stage` had 6 steps (declared → notified → enroute →
 * onsite → resolved → closed); the real backend's `OasEventStatus` has 7
 * (…acknowledged, on_site…, cancelled) and folds "take" and "ack" into the
 * same `acknowledged` status — a manager's BL-034 "seen" click and a
 * technician's "I'm on it" both land there, since the server doesn't
 * distinguish the two the way the old client-only prototype did.
 */

import { useSyncExternalStore } from 'react';
import { causeLabelIn, getRefState } from './refStore';
import { ensureHierarchyLoaded, getHierarchyState } from './hierarchyStore';
import { connectOasStream, eventsApi, slaApi, type OasEventDto } from './api/events';
import { type EventKind, type EventStage, type ShopEvent } from './demo';
import { pushToast } from '@/shared/lib/toast';
import { getLang, subscribeLang } from '@/i18n/langStore';
import { onSessionReminder } from '@/modules/auth/store/session';

export const KIND_TO_EVENT_TYPE: Record<EventKind, string> = {
  technical: 'technical_stop', quality: 'quality_stop', material: 'material_wait', changeover: 'changeover',
};
const EVENT_TYPE_TO_KIND: Record<string, EventKind> = {
  technical_stop: 'technical', quality_stop: 'quality', material_wait: 'material', changeover: 'changeover', other_stop: 'technical',
};
const STATUS_TO_STAGE: Record<string, EventStage> = {
  declared: 'declared', notified: 'notified', acknowledged: 'enroute',
  on_site: 'onsite', resolved: 'resolved', closed: 'closed', cancelled: 'closed',
};
const ESCALATION_TO_LEVEL: Record<string, number> = { none: 0, level_1: 1, level_2: 2 };

/** An event raised live by the operator app. */
export interface LiveEvent extends ShopEvent {
  postId: string;
  causeId?: string;
  escalation: number;
  acknowledgedAt?: string;
}

export interface EventOverride {
  etaMin?: number;
}

/** Cause labels are bilingual data (`labelFr`/`labelAr`), not static i18n keys — resolved in the CURRENT UI language, re-run whenever it changes (see `resyncCauseLabels` below). */
function resolveCauseKey(causeId: string | undefined, eventType: string): string {
  const cause = causeId
    ? getRefState().causeTree.flatMap((c) => [c, ...c.children]).find((c) => c.id === causeId)
    : undefined;
  return cause ? causeLabelIn(cause, getLang()) : eventType;
}

/** `assigneeId` is a user id — resolve it against the roster loaded for admin/user screens, same as `resolveCauseKey` does for causes. */
function resolveAssigneeName(assigneeId: string | undefined): string | undefined {
  if (!assigneeId) return undefined;
  return getRefState().users.find((u) => u.id === assigneeId)?.name ?? assigneeId;
}

function toShopEvent(e: OasEventDto): LiveEvent {
  const postCode = getHierarchyState().posts.find((p) => p.id === e.postId)?.code ?? e.postId.slice(0, 8);
  const lineName = e.lineId ? getHierarchyState().lines.find((l) => l.id === e.lineId)?.name : undefined;
  const slaLeftMin = e.slaDueAt
    ? Math.round((new Date(e.slaDueAt).getTime() - Date.now()) / 60_000)
    : e.slaMinutes - Math.round((Date.now() - new Date(e.declaredAt).getTime()) / 60_000);
  return {
    id: e.id,
    postId: e.postId,
    causeId: e.causeId ?? undefined,
    ref: `EVT-${e.id.slice(0, 4).toUpperCase()}`,
    post: postCode,
    lineKey: lineName ?? '—',
    kind: EVENT_TYPE_TO_KIND[e.eventType] ?? 'technical',
    causeKey: resolveCauseKey(e.causeId ?? undefined, e.eventType),
    stage: STATUS_TO_STAGE[e.status] ?? 'declared',
    declaredAt: new Date(e.declaredAt).toTimeString().slice(0, 5),
    slaLeftMin,
    assignee: resolveAssigneeName(e.assigneeId ?? undefined),
    escalation: ESCALATION_TO_LEVEL[e.escalationLevel] ?? 0,
    acknowledgedAt: e.status === 'declared' || e.status === 'notified' ? undefined : e.declaredAt,
  };
}

interface StoreState {
  events: LiveEvent[];
  overrides: Record<string, EventOverride>;
  busy: string[];
}

let state: StoreState = { events: [], overrides: {}, busy: [] };
const listeners = new Set<() => void>();

function commit(patch: Partial<StoreState>) {
  state = { ...state, ...patch };
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

function snapshot(): LiveEvent[] {
  return state.events;
}

/** Bumped on every SSE-driven `event.created`/`event.updated` commit, so `reload()` can tell whether live data landed while its GET was still in flight. */
let sseRevision = 0;

async function reload() {
  const revisionBefore = sseRevision;
  const rows = await eventsApi.list();
  if (sseRevision !== revisionBefore) {
    // A live SSE update arrived while this snapshot GET was in flight —
    // this response is now stale; a blind replace would silently drop
    // whatever SSE just delivered. Merge instead, preferring the
    // already-live rows over this snapshot for anything both contain.
    const byId = new Map(rows.map((r) => [r.id, toShopEvent(r)]));
    for (const e of state.events) byId.set(e.id, e);
    commit({ events: Array.from(byId.values()) });
    return;
  }
  commit({ events: rows.map(toShopEvent) });
}

/**
 * Re-resolve every cached event's cause label when the UI language changes
 * — otherwise an event loaded before the switch stays in the old language
 * until its next unrelated SSE update. Events without a matched cause fall
 * back to their raw event-type string (language-independent), so re-running
 * this is a no-op for them either way.
 */
function resyncCauseLabels() {
  if (!state.events.length) return;
  commit({ events: state.events.map((e) => ({ ...e, causeKey: resolveCauseKey(e.causeId, e.causeKey) })) });
}

/** Hydrate the "responder busy" flag from server truth (`GET /responder-availability`) — otherwise a toggle made by someone else, or in a prior session, would be invisible until this tab toggled it locally itself. */
async function reloadAvailability() {
  try {
    const rows = await slaApi.availability();
    commit({ busy: rows.filter((r) => r.busy).map((r) => r.profileId) });
  } catch {
    // keep last known values on a transient failure
  }
}

let streamDisconnect: (() => void) | null = null;
let loaded = false;

export function ensureEventsLoaded() {
  if (loaded) return;
  loaded = true;
  ensureHierarchyLoaded();
  void reload();
  void reloadAvailability();
  subscribeLang(resyncCauseLabels);
  streamDisconnect = connectOasStream((type, data) => {
    if (type === 'event.created' || type === 'event.updated') {
      sseRevision++;
      const mapped = toShopEvent(data as OasEventDto);
      const idx = state.events.findIndex((e) => e.id === mapped.id);
      const events = idx >= 0 ? state.events.map((e, i) => (i === idx ? mapped : e)) : [mapped, ...state.events];
      commit({ events });
    } else if (type === 'event.escalated') {
      // OasEscalationSweepHostedService pushes this instead of a full
      // event.updated (spec §6.3) — {eventId, level}, patched locally
      // rather than round-tripped, same as the rest of this handler.
      const { eventId, level } = data as { eventId: string; level: string };
      const escalation = ESCALATION_TO_LEVEL[level] ?? 0;
      commit({ events: state.events.map((e) => (e.id === eventId ? { ...e, escalation } : e)) });
    } else if (type === 'session.reminder') {
      // OasSessionWatchdogHostedService force-closed a post session
      // (shift ended past grace period) — tell the mobile session store to
      // re-check right away instead of only at next app load.
      onSessionReminder();
    }
  });
}

export function useEvents(): LiveEvent[] {
  ensureEventsLoaded();
  return useSyncExternalStore(subscribe, snapshot, snapshot);
}

export function etaOf(id: string): number | undefined {
  return state.overrides[id]?.etaMin;
}

function replaceFromDto(dto: OasEventDto) {
  const mapped = toShopEvent(dto);
  commit({ events: state.events.map((e) => (e.id === mapped.id ? mapped : e)) });
}

/**
 * Raise a new event from an operator declaration. `clientEventId` makes the
 * call idempotent (spec §4.3 offline-safe) — a retried queue POST never
 * double-creates. `postId` is the real hierarchy post id (resolve it via
 * `getHierarchyState().posts` from the post code before calling).
 */
export async function addEvent(input: {
  postId: string; kind: EventKind; causeId?: string; clientEventId?: string;
}): Promise<LiveEvent> {
  const dto = await eventsApi.create({
    clientEventId: input.clientEventId ?? crypto.randomUUID(),
    eventType: KIND_TO_EVENT_TYPE[input.kind],
    postId: input.postId,
    causeId: input.causeId,
    declaredAt: new Date().toISOString(),
  });
  const mapped = toShopEvent(dto);
  commit({ events: [mapped, ...state.events] });
  return mapped;
}

/** Assign the event to the current responder and move it to "en route" (backend: `/take`). */
export async function takeEvent(id: string) {
  try {
    replaceFromDto(await eventsApi.take(id));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** Announce an arrival time (backend: `PUT /eta`, does not by itself change status). */
export async function setEventEta(id: string, etaMin: number) {
  try {
    replaceFromDto(await eventsApi.setEta(id, etaMin));
    commit({ overrides: { ...state.overrides, [id]: { ...state.overrides[id], etaMin } } });
  } catch {
    pushToast('common.actionFailed');
  }
}

/** Technician confirms physical arrival at the post (backend: `/arrive`). */
export async function arriveOnSite(id: string) {
  try {
    replaceFromDto(await eventsApi.arrive(id));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** Marks the event resolved (backend: `/advance` always jumps straight to `resolved`). */
export async function advanceEvent(id: string) {
  try {
    replaceFromDto(await eventsApi.advance(id));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** Close the intervention — an observed cause (picked or free text) is kept for the record. */
export async function closeEvent(id: string, closureType: 'resolved' | 'palliative' | 'no_fault' | 'cancelled' = 'resolved', note?: string) {
  try {
    replaceFromDto(await eventsApi.close(id, closureType, note));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-034 — acknowledge (manager "Vu" or technician taking the job — the server folds both into the same status). */
export async function acknowledgeEvent(id: string) {
  try {
    replaceFromDto(await eventsApi.ack(id));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-033 — raise the escalation level (1 = supervisor, 2 = production manager). */
export async function escalateEvent(id: string) {
  try {
    replaceFromDto(await eventsApi.escalate(id));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-031 — requalify the event, which re-routes it to the mapped service. */
export async function requalifyEvent(id: string, kind: EventKind) {
  try {
    replaceFromDto(await eventsApi.requalify(id, KIND_TO_EVENT_TYPE[kind]));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-032 — the responder answers "occupé": loses the assignee, escalates one level (server applies both atomically). */
export async function declineEvent(id: string) {
  try {
    replaceFromDto(await eventsApi.decline(id));
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-032 — responder-busy toggle, server truth via `/responder-availability/{profileId}`. */
export async function setResponderBusy(profileId: string, busy: boolean, reason?: string) {
  try {
    await slaApi.setAvailability(profileId, busy, reason);
    const list = new Set(state.busy);
    if (busy) list.add(profileId); else list.delete(profileId);
    commit({ busy: [...list] });
  } catch {
    pushToast('common.actionFailed');
  }
}

export function isResponderBusy(profileId?: string): boolean {
  return !!profileId && state.busy.includes(profileId);
}

/** Reactive form of `isResponderBusy` — `/mobile/inbox`'s own-availability toggle (spec §1.5). */
export function useResponderBusy(profileId?: string): boolean {
  return useSyncExternalStore(subscribe, () => isResponderBusy(profileId), () => false);
}
