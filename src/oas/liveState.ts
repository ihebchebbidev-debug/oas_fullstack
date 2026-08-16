/**
 * Live shop-floor + KPI state — backed by the real `GET /api/oas/post-states`
 * and `GET /api/oas/kpi/*` endpoints (spec §6.1, §7.3). Per spec §12 rule 10
 * ("ne rien recalculer côté client après bascule"), no formula is
 * recomputed here — every number/state comes straight from the server; this
 * module is only a thin fetch + poll cache so screens still feel live.
 */

import { useSyncExternalStore } from 'react';
import { kindToState, type EventKind, type MachineState, type Post } from './demo';
import { useEvents } from './eventStore';
import { publishedOperator, useAssignments } from './assignmentStore';
import { causeLabelIn, getRefState } from './refStore';
import { getHierarchyState, useHierarchyState } from './hierarchyStore';
import { kpiApi, type OasKpiDailyDto, type OasLineComparisonEntryDto, type OasParetoEntryDto, type OasTrendPointDto } from './api/kpi';
import { postStatesApi, type OasPostStateDto } from './api/postStates';
import { computeMetrics, useSessionState } from '@/modules/auth/store/session';
import { getLang, subscribeLang } from '@/i18n/langStore';
import { DICTS } from '@/i18n/translations';

/* ------------------------------------------------------------------ */
/* Posts — real hierarchy + real live state (oas_post_states trigger)   */
/* ------------------------------------------------------------------ */

const STATE_VALUE_TO_MACHINE_STATE: Record<string, MachineState> = {
  production: 'production', material_wait: 'material', changeover: 'changeover',
  technical_stop: 'technical', quality_stop: 'quality', unassigned: 'idle',
};

let postStates: OasPostStateDto[] = [];
const postStateListeners = new Set<() => void>();

async function refreshPostStates() {
  try {
    postStates = await postStatesApi.live();
    postStateListeners.forEach((l) => l());
  } catch {
    // keep the last known values on a transient failure
  }
}

let postStatesStarted = false;
function ensurePostStatesStarted() {
  if (postStatesStarted) return;
  postStatesStarted = true;
  void refreshPostStates();
  if (typeof window !== 'undefined') window.setInterval(() => void refreshPostStates(), POLL_MS);
}

function usePostStates(): OasPostStateDto[] {
  ensurePostStatesStarted();
  return useSyncExternalStore(
    (l) => { postStateListeners.add(l); return () => postStateListeners.delete(l); },
    () => postStates,
    () => [],
  );
}

/**
 * Live post list: real hierarchy posts, coloured by the server-maintained
 * `oas_post_states` row (never derived from events client-side), attributed
 * to the published assignment holder, and overridden by the operator's own
 * running session for instant local feedback before the state row catches up.
 */
export function useLivePosts(): Post[] {
  const { posts, lines } = useHierarchyState();
  const states = usePostStates();
  const events = useEvents();
  const assignments = useAssignments();
  const session = useSessionState();
  const { cadences, users } = getRefState();
  const openStop = session.stops.find((s) => !s.closedAt) ?? null;

  const statesByPost = new Map(states.map((s) => [s.postId, s]));
  const lineNameById = new Map(lines.map((l) => [l.id, l.name]));
  const userNameById = new Map(users.map((u) => [u.id, u.name]));

  return posts.filter((p) => !p.archived).map((p): Post => {
    const st = statesByPost.get(p.id);
    const cadence = cadences.find((c) => c.postId === p.id);
    const since = st ? Date.parse(st.since) : Date.now();

    const next: Post = {
      id: p.id,
      code: p.code,
      lineKey: lineNameById.get(p.lineId) ?? '—',
      state: st ? (STATE_VALUE_TO_MACHINE_STATE[st.state] ?? 'idle') : 'idle',
      ref: cadence?.ref ?? '—',
      order: '—',
      operator: st?.currentUserId ? userNameById.get(st.currentUserId) : undefined,
      sinceMin: Math.max(0, Math.round((Date.now() - since) / 60_000)),
    };

    const operator = publishedOperator(assignments, p.code);
    if (operator) next.operator = operator;

    const open = events.find(
      (e) => e.post === p.code && e.stage !== 'closed' && e.stage !== 'resolved',
    );
    if (open) next.state = kindToState[open.kind];

    if (session.session?.postCode === p.code) {
      next.operator = session.session.operator;
      next.isMine = true;
      next.state = openStop
        ? kindToState[(openStop.eventType as EventKind) in kindToState ? (openStop.eventType as EventKind) : 'technical']
        : 'production';
    }

    return next;
  });
}

/* ------------------------------------------------------------------ */
/* KPIs — small polling cache shared by every hook below                */
/* ------------------------------------------------------------------ */

export interface LiveKpi {
  trs: number;
  availability: number;
  performance: number;
  quality: number;
  cadenceKnown: boolean;
  producedOk: number;
  producedNok: number;
  stopsCount: number;
  stopMinutes: number;
  mttrMin: number;
  openingMin: number;
  hasLiveData: boolean;
}

export interface LivePareto {
  causeKey: string;
  minutes: number;
  kind: EventKind;
}

export interface LiveLine {
  lineId: string;
  lineName: string;
  trs: number;
  stops: number;
  scrap: number;
  mtbfMin: number;
}

export interface LiveTrendPoint {
  label: string;
  value: number;
}

const POLL_MS = 30_000;

function toLiveKpi(dto: OasKpiDailyDto): LiveKpi {
  return {
    trs: Math.round(dto.oee ?? 0),
    availability: Math.round(dto.availability),
    performance: Math.round(dto.performance ?? 0),
    quality: Math.round(dto.quality),
    cadenceKnown: dto.cadenceKnown,
    producedOk: dto.producedOk,
    producedNok: dto.producedNok,
    stopsCount: dto.stopsCount,
    stopMinutes: dto.stopMinutes,
    mttrMin: dto.mttr,
    openingMin: dto.openingMin,
    hasLiveData: dto.stopsCount > 0 || dto.producedOk > 0 || dto.producedNok > 0,
  };
}

function toLivePareto(rows: OasParetoEntryDto[]): LivePareto[] {
  const causes = getRefState().causeTree.flatMap((c) => [c, ...c.children.map((ch) => ({ ...ch, kind: c.kind }))]);
  const lang = getLang();
  const unknownCause = DICTS[lang]?.['web.dash.causeUnknown'] ?? DICTS.fr['web.dash.causeUnknown'];
  return rows
    .map((r) => {
      const cause = r.causeId ? causes.find((c) => c.id === r.causeId) : undefined;
      return { causeKey: cause ? causeLabelIn(cause, lang) : unknownCause, minutes: r.lostMinutes, kind: (cause as { kind?: EventKind })?.kind ?? 'technical' };
    })
    .sort((a, b) => b.minutes - a.minutes);
}

function toLiveLines(rows: OasLineComparisonEntryDto[]): LiveLine[] {
  const lines = getHierarchyState().lines;
  return rows.map((r) => ({
    lineId: r.lineId,
    lineName: lines.find((l) => l.id === r.lineId)?.name ?? r.lineId.slice(0, 8),
    trs: r.trs,
    stops: r.stopsCount,
    scrap: r.scrap,
    mtbfMin: r.mtbfMin,
  }));
}

function toLiveTrend(rows: OasTrendPointDto[]): LiveTrendPoint[] {
  const dayLabels = ['D', 'L', 'M', 'M', 'J', 'V', 'S'];
  return rows.map((r) => ({ label: dayLabels[new Date(r.date).getDay()], value: Math.round(r.oee ?? 0) }));
}

const EMPTY_KPI: LiveKpi = {
  trs: 0, availability: 0, performance: 0, quality: 0, cadenceKnown: true,
  producedOk: 0, producedNok: 0, stopsCount: 0, stopMinutes: 0, mttrMin: 0, openingMin: 0, hasLiveData: false,
};

let kpi: LiveKpi = EMPTY_KPI;
let pareto: LivePareto[] = [];
let lines: LiveLine[] = [];
let trend: LiveTrendPoint[] = [];
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

async function refresh() {
  try {
    const [dailyDto, paretoRows, lineRows] = await Promise.all([
      kpiApi.daily(), kpiApi.pareto(), kpiApi.lineComparison(),
    ]);
    kpi = toLiveKpi(dailyDto);
    pareto = toLivePareto(paretoRows);
    lines = toLiveLines(lineRows);
    emit();
  } catch {
    // keep the last known values on a transient failure
  }
}

async function refreshTrend() {
  try {
    const to = new Date();
    const from = new Date(to);
    from.setDate(from.getDate() - 6);
    const iso = (d: Date) => d.toISOString().slice(0, 10);
    trend = toLiveTrend(await kpiApi.trend({ from: iso(from), to: iso(to) }));
    emit();
  } catch {
    // keep the last known values
  }
}

let started = false;
function ensureStarted() {
  if (started) return;
  started = true;
  void refresh();
  void refreshTrend();
  if (typeof window !== 'undefined') {
    window.setInterval(() => void refresh(), POLL_MS);
  }
  // Pareto cause labels are bilingual data resolved at fetch time — a
  // language switch needs a fresh `refresh()` to show the new language
  // immediately, not wait up to POLL_MS for the next scheduled poll.
  subscribeLang(() => void refresh());
}

/** Live TRS decomposition — identical on dashboard, reports, Andon and mobile. */
export function useLiveKpi(): LiveKpi {
  ensureStarted();
  return useSyncExternalStore(subscribe, () => kpi, () => EMPTY_KPI);
}

/** Pareto of lost minutes for the day, server-computed. */
export function useLivePareto(): LivePareto[] {
  ensureStarted();
  return useSyncExternalStore(subscribe, () => pareto, () => []);
}

/** Per-line results for the day, server-computed — never blended with a static seed. */
export function useLiveLines(): LiveLine[] {
  ensureStarted();
  return useSyncExternalStore(subscribe, () => lines, () => []);
}

/** OEE trend over the last 7 days. */
export function useLiveTrend(): LiveTrendPoint[] {
  ensureStarted();
  return useSyncExternalStore(subscribe, () => trend, () => []);
}

/**
 * One-shot fetch over an explicit `[from, to]` range (ISO dates) — for
 * screens with their own period selector (`Reports.tsx`) rather than the
 * shared "today" polling cache the hooks above use.
 */
export async function fetchLiveRange(from: string, to: string): Promise<{ kpi: LiveKpi; pareto: LivePareto[]; lines: LiveLine[]; trend: LiveTrendPoint[] }> {
  const [dailyDto, paretoRows, lineRows, trendRows] = await Promise.all([
    kpiApi.daily({ from, to }), kpiApi.pareto({ from, to }), kpiApi.lineComparison({ from, to }), kpiApi.trend({ from, to }),
  ]);
  return { kpi: toLiveKpi(dailyDto), pareto: toLivePareto(paretoRows), lines: toLiveLines(lineRows), trend: toLiveTrend(trendRows) };
}

/**
 * Today's KPI/Pareto scoped to a single post (`MobileKpi` — spec §1.5 "TRS du
 * poste": `GET /kpi/daily?postId=` · `GET /kpi/pareto?postId=`), a different
 * question from the tenant-wide `useLiveKpi()`/`useLivePareto()` above.
 */
export async function fetchLiveKpiForPost(postId: string): Promise<{ kpi: LiveKpi; pareto: LivePareto[] }> {
  const [dailyDto, paretoRows] = await Promise.all([
    kpiApi.daily({ postId }), kpiApi.pareto({ postId }),
  ]);
  return { kpi: toLiveKpi(dailyDto), pareto: toLivePareto(paretoRows) };
}
