import {
  Activity, PackageSearch, Repeat, Wrench, ShieldAlert, CircleDashed,
  type LucideIcon,
} from 'lucide-react';

/**
 * The 6 machine states and the event circuit's 6 stages are hardcoded here
 * on purpose (spec §7.2/§7.4 "EVENT_STAGES et STATES restent en dur") — they
 * mirror the backend's Postgres enums and triggers, not seed data, so they
 * never come from an API. Everything else that used to live in this file
 * (POSTS, LINE_KEYS, STOP_REASONS, KPI/PARETO/TRS_TREND/LINE_COMPARISON,
 * EVENTS) was demo/fixture data and has been fully replaced by real
 * `/api/oas/*` calls (`hierarchyStore`, `refStore`, `eventStore`,
 * `liveState`) — see git history if the old seed shapes are ever needed
 * for reference.
 */

/** The 6 machine states — the only chromatic system in the product. */
export type MachineState =
  | 'production'
  | 'material'
  | 'changeover'
  | 'technical'
  | 'quality'
  | 'idle';

export interface StateMeta {
  key: MachineState;
  labelKey: string;
  icon: LucideIcon;
  /** Tailwind color token suffix -> text-state-production etc. */
  token: string;
}

export const STATES: Record<MachineState, StateMeta> = {
  production: { key: 'production', labelKey: 'state.production', icon: Activity,      token: 'production' },
  material:   { key: 'material',   labelKey: 'state.material',   icon: PackageSearch, token: 'material' },
  changeover: { key: 'changeover', labelKey: 'state.changeover', icon: Repeat,        token: 'changeover' },
  technical:  { key: 'technical',  labelKey: 'state.technical',  icon: Wrench,        token: 'technical' },
  quality:    { key: 'quality',    labelKey: 'state.quality',    icon: ShieldAlert,   token: 'quality' },
  idle:       { key: 'idle',       labelKey: 'state.idle',       icon: CircleDashed,  token: 'idle' },
};

/** A live shop-floor post, as rendered on the map/wallboard — `lineKey` holds the real line name. */
export interface Post {
  id: string;
  code: string;
  lineKey: string;
  state: MachineState;
  ref: string;
  order: string;
  operator?: string;
  isMine?: boolean;
  sinceMin: number;
}

export type EventKind = 'technical' | 'quality' | 'material' | 'changeover';
export type EventStage = 'declared' | 'notified' | 'enroute' | 'onsite' | 'resolved' | 'closed';

/** A live event, as rendered in the alerts queue/Andon/inbox — `causeKey`/`lineKey` hold real text, not i18n keys. */
export interface ShopEvent {
  id: string;
  ref: string;
  post: string;
  lineKey: string;
  kind: EventKind;
  causeKey: string;
  stage: EventStage;
  declaredAt: string;
  /** Minutes remaining before the SLA breach; negative = breached. */
  slaLeftMin: number;
  assignee?: string;
}

export const EVENT_STAGES: { key: EventStage; labelKey: string }[] = [
  { key: 'declared', labelKey: 'stage.declared' },
  { key: 'notified', labelKey: 'stage.notified' },
  { key: 'enroute',  labelKey: 'stage.enroute' },
  { key: 'onsite',   labelKey: 'stage.onsite' },
  { key: 'resolved', labelKey: 'stage.resolved' },
  { key: 'closed',   labelKey: 'stage.closed' },
];

export const kindToState: Record<EventKind, MachineState> = {
  technical:  'technical',
  quality:    'quality',
  material:   'material',
  changeover: 'changeover',
};
