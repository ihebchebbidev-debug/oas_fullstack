/**
 * Assignment store — shared between the supervision console (M3 Affectations)
 * and the operator mobile app. Backed by the real `/api/oas/assignments` +
 * `/api/oas/presence` endpoints (spec §6.1), scoped server-side by
 * `(shiftTemplateId, workDate)` — a dimension the old prototype never had
 * (it kept exactly one global board). This store picks the shift whose time
 * window contains "now" as the implicit "current board", same as
 * `ShiftReport`'s window check, so every screen keeps working without a
 * shift/date picker.
 *
 * The public shape is unchanged on purpose (`assignments` keyed by post
 * CODE → operator NAME, `presence` keyed by operator NAME) so every existing
 * consumer (`Assignments.tsx`, `RosterPanel.tsx`, `OperatorHome.tsx`,
 * `ScanPage.tsx`) keeps working untouched — this store resolves
 * name ⇄ id against `refStore`/`hierarchyStore` internally.
 */

import { useSyncExternalStore } from 'react';
import { getRefState } from './refStore';
import { ensureHierarchyLoaded, getHierarchyState } from './hierarchyStore';
import { assignmentsApi, presenceApi } from './api/assignments';
import { pushToast } from '@/shared/lib/toast';

export type Presence = 'confirmed' | 'pending' | 'absent';
export const PRESENCES: Presence[] = ['confirmed', 'pending', 'absent'];

export interface AssignmentState {
  /** postCode -> operator full name (null = post left open) */
  assignments: Record<string, string | null>;
  /** operator full name -> presence state for the current shift */
  presence: Record<string, Presence>;
  /** ISO date of the last publication, null while the board is a draft */
  publishedAt: string | null;
}

const EMPTY: AssignmentState = { assignments: {}, presence: {}, publishedAt: null };

let state: AssignmentState = EMPTY;
interface RosterEntry { userId: string; name: string }
/** Mutated in place (never reassigned) so the exported `ROSTER` reference stays stable for consumers that read it directly at render time. */
const rosterEntries: RosterEntry[] = [];
/** Operator names for pickers — same array reference across reloads; consumers re-render via `useAssignments()`. */
export const ROSTER: string[] = [];
/** `{code, lineKey}` per real hierarchy post — same "stable reference, mutated in place" trick as ROSTER. `lineKey` is the real line name. */
export const BOARD_POSTS: { code: string; lineKey: string }[] = [];
const listeners = new Set<() => void>();

function commit(next: AssignmentState) {
  state = next;
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

export function useAssignments(): AssignmentState {
  ensureAssignmentsLoaded();
  return useSyncExternalStore(subscribe, () => state, () => state);
}

function span(s: { start: string; end: string }): number {
  const min = (v: string) => { const [h, m] = v.split(':').map(Number); return (h || 0) * 60 + (m || 0); };
  const diff = min(s.end) - min(s.start);
  return diff > 0 ? diff : diff + 1440;
}

/** The shift template whose time window contains "now" — the implicit "current shift" every screen assumes absent a picker. Shared with `session.ts` (BL-019 presence confirmation needs the same resolution). */
export function currentShiftId(): string | null {
  const { shifts } = getRefState();
  if (!shifts.length) return null;
  const now = new Date();
  const minutes = now.getHours() * 60 + now.getMinutes();
  const inWindow = (s: (typeof shifts)[number]) => {
    const [sh, sm] = s.start.split(':').map(Number);
    const start = (sh || 0) * 60 + (sm || 0);
    const end = (start + span(s)) % 1440;
    return start < end ? minutes >= start && minutes < end : minutes >= start || minutes < end;
  };
  return (shifts.find(inWindow) ?? shifts[0]).id;
}

/**
 * `toISOString().slice(0,10)` reads the UTC calendar date, not the local
 * one — `currentShiftId()` right above already correctly uses local
 * `getHours()`, so the two "current shift"/"current work date" notions
 * would silently diverge for any tenant not at UTC+0 (a night shift local
 * time can already be "tomorrow" in UTC), filing assignment/presence rows
 * under the wrong date.
 */
export function currentWorkDate(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

let loaded = false;
export function ensureAssignmentsLoaded() {
  if (loaded) return;
  loaded = true;
  ensureHierarchyLoaded();
  void reload();
}

async function reload() {
  const shiftId = currentShiftId();
  if (!shiftId) return;
  const date = currentWorkDate();
  const [rows, presenceRows, rosterRows] = await Promise.all([
    assignmentsApi.list(shiftId, date),
    presenceApi.list(shiftId, date),
    assignmentsApi.roster(),
  ]);

  const freshRoster = rosterRows.filter((r) => r.isActive).map((r) => ({ userId: r.userId, name: r.displayName ?? r.userId.slice(0, 8) }));
  rosterEntries.length = 0;
  rosterEntries.push(...freshRoster);
  ROSTER.length = 0;
  ROSTER.push(...freshRoster.map((r) => r.name));

  const nameById = new Map(rosterEntries.map((r) => [r.userId, r.name]));
  const posts = getHierarchyState().posts;
  const codeById = new Map(posts.map((p) => [p.id, p.code]));
  const lineNameById = new Map(getHierarchyState().lines.map((l) => [l.id, l.name]));

  BOARD_POSTS.length = 0;
  BOARD_POSTS.push(...posts.map((p) => ({ code: p.code, lineKey: lineNameById.get(p.lineId) ?? '—' })));

  const assignments: Record<string, string | null> = {};
  posts.forEach((p) => { assignments[p.code] = null; });
  rows.forEach((row) => {
    const code = codeById.get(row.postId);
    if (code) assignments[code] = row.userDisplayName ?? nameById.get(row.userId) ?? null;
  });

  const presence: Record<string, Presence> = {};
  rosterEntries.forEach((r) => { presence[r.name] = 'pending'; });
  presenceRows.forEach((row) => {
    const name = nameById.get(row.userId);
    if (name) presence[name] = row.status as Presence;
  });

  const anyPublished = rows.some((r) => r.published);
  commit({ assignments, presence, publishedAt: anyPublished ? new Date().toISOString() : null });
}

function resolvePostId(postCode: string): string | undefined {
  return getHierarchyState().posts.find((p) => p.code === postCode)?.id;
}

function resolveUserId(name: string): string | undefined {
  return rosterEntries.find((r) => r.name === name)?.userId;
}

/* ------------------------------------------------------------------ */
/* Actions                                                             */
/* ------------------------------------------------------------------ */

/** Assign (or clear with null) one post — an operator holds a single post. */
export async function assignPost(postCode: string, operator: string | null) {
  const postId = resolvePostId(postCode);
  if (!postId) return;
  const shiftId = currentShiftId();
  if (!shiftId) return;
  const date = currentWorkDate();
  try {
    if (!operator) {
      await assignmentsApi.remove(postId, shiftId, date);
    } else {
      const userId = resolveUserId(operator);
      if (!userId) return;
      await assignmentsApi.upsert(postId, { userId, shiftTemplateId: shiftId, workDate: date });
    }
    await reload();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function releasePost(postCode: string) {
  await assignPost(postCode, null);
}

/** Fill every open post with the next confirmed operator (BL-020) — server-side auto-fill. */
export async function autoFill() {
  const shiftId = currentShiftId();
  if (!shiftId) return;
  try {
    await assignmentsApi.autoFill(shiftId, currentWorkDate());
    await reload();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function clearAll() {
  const shiftId = currentShiftId();
  if (!shiftId) return;
  try {
    await assignmentsApi.clearAll(shiftId, currentWorkDate());
    await reload();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function publishAssignments() {
  const shiftId = currentShiftId();
  if (!shiftId) return;
  try {
    await assignmentsApi.publish(shiftId, currentWorkDate());
    await reload();
  } catch {
    pushToast('common.actionFailed');
  }
}

/**
 * BL-020 — move an operator between the three presence lists.
 * Declaring an absence frees the post immediately and un-publishes the
 * board server-side, so the team leader has to re-arbitrate.
 */
export async function setPresence(operator: string, presenceState: Presence) {
  const userId = resolveUserId(operator);
  const shiftId = currentShiftId();
  if (!userId || !shiftId) return;
  try {
    await presenceApi.set(userId, { workDate: currentWorkDate(), shiftTemplateId: shiftId, status: presenceState });
    await reload();
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-019 — the operator confirms their own presence from the tablet. */
export async function confirmPresence(operator: string) {
  const userId = resolveUserId(operator);
  const shiftId = currentShiftId();
  if (!userId || !shiftId || state.presence[operator] === 'confirmed') return;
  try {
    await presenceApi.confirm(userId, { workDate: currentWorkDate(), shiftTemplateId: shiftId });
    await reload();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Selectors                                                           */
/* ------------------------------------------------------------------ */

/** Operator the team leader assigned to a post — only once published. */
export function publishedOperator(s: AssignmentState, postCode: string): string | null {
  if (!s.publishedAt) return null;
  return s.assignments[postCode.toUpperCase()] ?? null;
}

export function presenceOf(s: AssignmentState, operator: string): Presence {
  return s.presence[operator] ?? 'pending';
}

export function assignmentCounts(s: AssignmentState) {
  const codes = Object.keys(s.assignments);
  const assigned = codes.filter((c) => s.assignments[c]).length;
  const names = rosterEntries.map((r) => r.name);
  const count = (p: Presence) => names.filter((n) => presenceOf(s, n) === p).length;
  return {
    assigned,
    total: codes.length,
    confirmed: count('confirmed'),
    pending: count('pending'),
    absent: count('absent'),
    /** kept for the header badge: operators unavailable for assignment */
    inactive: count('absent'),
  };
}
