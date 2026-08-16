/**
 * Referential store — M1 (référentiels) + M2 (comptes), backed by the real
 * `/api/oas/{operators,equipments,cadences,products,causes,cause-proposals,
 * shifts,shift-signoffs,imports}` endpoints (spec §6.1). Audit is no longer
 * written from here — the server's `oas_audit_row` trigger logs every
 * mutation automatically (spec §1.4, §6.1 "Audit").
 */

import { useSyncExternalStore } from 'react';
import type { EventKind } from './demo';
import {
  cadencesApi, causesApi, equipmentsApi, importsApi, operatorsApi, productsApi, shiftsApi,
  type OasCadenceDto, type OasCauseDto, type OasCauseProposalDto, type OasEquipmentDto,
  type OasOperatorDto, type OasProductDto, type OasShiftSignoffDto, type OasShiftTemplateDto,
} from './api/referentials';
import { hierarchyApi } from './api/hierarchy';
import type { OasRole } from './authStore';
import { pushToast } from '@/shared/lib/toast';

/* ------------------------------------------------------------------ */
/* Event kind ↔ backend event-type mapping                             */
/* ------------------------------------------------------------------ */

const KIND_TO_EVENT_TYPE: Record<EventKind, string> = {
  technical: 'technical_stop', quality: 'quality_stop', material: 'material_wait', changeover: 'changeover',
};
const EVENT_TYPE_TO_KIND: Record<string, EventKind> = {
  technical_stop: 'technical', quality_stop: 'quality', material_wait: 'material', changeover: 'changeover', other_stop: 'technical',
};

/* ------------------------------------------------------------------ */
/* Types                                                               */
/* ------------------------------------------------------------------ */

export const ROLE_KEYS: OasRole[] = ['operator', 'supervisor', 'admin'];
export type RoleKey = OasRole;

export interface CadenceVersion { rate: number; version: number; since: string }
export interface CadenceRow extends CadenceVersion {
  id: string;
  productId: string;
  ref: string;
  postId: string;
  equipment: string;
  history: CadenceVersion[];
}

export interface CauseProposal {
  id: string;
  label: string;
  by: string;
  at: string;
  status: 'pending' | 'approved' | 'rejected';
}

export interface UserRow {
  id: string;
  name: string;
  code: string;
  role: RoleKey;
  active: boolean;
  interim: boolean;
  /** BL-012 — perimeter: a single line (empty = whole site), matching the server's `scope_line_id`. */
  scopeLineId: string | null;
}

export interface ImportRecord {
  dataset: string;
  rows: number;
  rowsOk: number;
  rowsError: number;
  at: string;
}

/** BL-003 — a catalogue reference. `internalCode` no longer exists server-side. */
export interface ProductRow { id: string; ref: string; label: string; customer: string; unit: string }

/** BL-002 — equipment attached to a post; `kind`/`partsPerCycle` have no server-side field (dropped at the bascule). */
export interface EquipmentRow {
  id: string;
  code: string;
  postId: string | null;
  post: string;
  name: string;
  manufacturer: string;
  criticality: 'low' | 'medium' | 'high' | 'critical';
}

/** BL-043 — a signed shift report (hand-over note + signatory). */
export interface ShiftSignOff {
  id: string;
  shiftTemplateId: string;
  shift: string;
  note: string;
  by: string;
  at: string;
}

/** BL-009 — shift calendar (2×8 / 3×8 / 4×8 / journée) with deducted breaks. */
export interface ShiftRow {
  id: string;
  siteId: string;
  code: string;
  name: string;
  /** HH:MM */
  start: string;
  /** HH:MM */
  end: string;
  breakMinutes: number;
  active: boolean;
}

export interface CauseChild { id: string; code: string; label: string; labelAr: string }

/** BL-007 — editable 2-level cause tree: family → detail, mapped to a service. Deeper server nesting is flattened. */
export interface CauseNode {
  id: string;
  code: string;
  label: string;
  labelAr: string;
  kind: EventKind;
  criticality: 'low' | 'medium' | 'high';
  children: CauseChild[];
  active: boolean;
}

export interface RefState {
  cadences: CadenceRow[];
  proposals: CauseProposal[];
  users: UserRow[];
  products: ProductRow[];
  imports: ImportRecord[];
  /** BL-026 — cause usage counter (keyed by cause id), server-derived from real declarations/events. */
  causeUsage: Record<string, number>;
  signOffs: ShiftSignOff[];
  shifts: ShiftRow[];
  equipment: EquipmentRow[];
  causeTree: CauseNode[];
  loading: boolean;
}

/* ------------------------------------------------------------------ */
/* Mapping — API DTO → UI row                                          */
/* ------------------------------------------------------------------ */

function toUser(o: OasOperatorDto): UserRow {
  return {
    id: o.id, name: o.displayName ?? o.email, code: o.employeeCode ?? o.id.slice(0, 8),
    role: (o.role as RoleKey) ?? 'operator', active: o.isActive, interim: o.interim, scopeLineId: o.scopeLineId,
  };
}

function toProduct(p: OasProductDto): ProductRow {
  return { id: p.id, ref: p.reference, label: p.name, customer: p.customer ?? '', unit: p.unit };
}

function toEquipment(e: OasEquipmentDto, postCodeById: Map<string, string>): EquipmentRow {
  return {
    id: e.id, code: e.code, postId: e.postId, post: e.postId ? (postCodeById.get(e.postId) ?? '') : '',
    name: e.name, manufacturer: e.manufacturer ?? '',
    criticality: (e.criticality as EquipmentRow['criticality']) ?? 'medium',
  };
}

function toShift(s: OasShiftTemplateDto): ShiftRow {
  return { id: s.id, siteId: s.siteId, code: s.code, name: s.name, start: s.startTime.slice(0, 5), end: s.endTime.slice(0, 5), breakMinutes: s.breakMinutes, active: s.isActive };
}

function toSignOff(s: OasShiftSignoffDto, shiftCodeById: Map<string, string>): ShiftSignOff {
  return {
    id: s.id, shiftTemplateId: s.shiftTemplateId, shift: shiftCodeById.get(s.shiftTemplateId) ?? s.shiftTemplateId,
    note: s.note ?? '', by: s.signedBy, at: s.signedAt,
  };
}

function toProposal(p: OasCauseProposalDto): CauseProposal {
  return {
    id: p.id, label: p.labelFr, by: p.proposedBy, at: p.createdAt,
    status: p.status === 'accepted' ? 'approved' : (p.status as CauseProposal['status']),
  };
}

/** Flattens the server's arbitrary-depth cause tree to family (level 1) + detail (level 2), matching the existing 2-level UI. */
function toCauseTree(rows: OasCauseDto[]): CauseNode[] {
  return rows.map((c) => ({
    id: c.id, code: c.code, label: c.labelFr, labelAr: c.labelAr,
    kind: EVENT_TYPE_TO_KIND[c.eventType ?? ''] ?? 'technical',
    criticality: (c.defaultCriticality as CauseNode['criticality']) ?? 'medium',
    active: c.isActive,
    children: c.children.map((ch) => ({ id: ch.id, code: ch.code, label: ch.labelFr, labelAr: ch.labelAr })),
  }));
}

/* ------------------------------------------------------------------ */
/* Store                                                               */
/* ------------------------------------------------------------------ */

let state: RefState = {
  cadences: [], proposals: [], users: [], products: [], imports: [],
  causeUsage: {}, signOffs: [], shifts: [], equipment: [], causeTree: [], loading: true,
};
const listeners = new Set<() => void>();

function commit(patch: Partial<RefState>) {
  state = { ...state, ...patch };
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

export function getRefState(): RefState {
  return state;
}

export function useRefState(): RefState {
  ensureRefLoaded();
  return useSyncExternalStore(subscribe, () => state, () => state);
}

async function reloadCadences() {
  const [cadences, products] = await Promise.all([cadencesApi.list(), productsApi.list()]);
  const productRefById = new Map(products.map((p) => [p.id, p.reference]));
  const histories = await Promise.all(cadences.map((c) => cadencesApi.history(c.id).catch(() => [])));
  const rows: CadenceRow[] = cadences.map((c, i) => {
    const history = [...histories[i]].sort((a, b) => b.version - a.version);
    const current = history[0];
    return {
      id: c.id, productId: c.productId, ref: productRefById.get(c.productId) ?? c.productId.slice(0, 8),
      postId: c.postId, equipment: '',
      rate: current?.rate ?? Number(c.rate), version: current?.version ?? 1, since: current?.since?.slice(0, 10) ?? '',
      history: history.slice(1).map((h) => ({ rate: h.rate, version: h.version, since: h.since.slice(0, 10) })),
    };
  });
  commit({ cadences: rows });
}

async function reloadAll() {
  const [operators, equipment, products, causeTree, causeUsage, proposals, shifts, signoffs, imports, posts] = await Promise.all([
    operatorsApi.search(), equipmentsApi.list(), productsApi.list(), causesApi.tree(), causesApi.usage(),
    causesApi.listProposals(), shiftsApi.list(), shiftsApi.signoffs(), importsApi.list(), hierarchyApi.listPosts(),
  ]);
  const postCodeById = new Map(posts.map((p) => [p.id, p.code]));
  const shiftCodeById = new Map(shifts.map((s) => [s.id, s.code]));
  const usageMap: Record<string, number> = {};
  causeUsage.forEach((u) => { usageMap[u.causeId] = u.count; });

  commit({
    users: operators.map(toUser),
    equipment: equipment.map((e) => toEquipment(e, postCodeById)),
    products: products.map(toProduct),
    causeTree: toCauseTree(causeTree),
    causeUsage: usageMap,
    proposals: proposals.map(toProposal),
    shifts: shifts.map(toShift),
    signOffs: signoffs.map((s) => toSignOff(s, shiftCodeById)),
    imports: imports.map((i) => ({ dataset: i.kind, rows: i.rowsTotal, rowsOk: i.rowsOk, rowsError: i.rowsError, at: i.createdAt })),
    loading: false,
  });
  await reloadCadences();
}

let loaded = false;
export function ensureRefLoaded() {
  if (loaded) return;
  loaded = true;
  void reloadAll().catch(() => commit({ loading: false }));
}

/* ------------------------------------------------------------------ */
/* Actions — cadences (BL-004 / BL-005)                                */
/* ------------------------------------------------------------------ */

/** Creating a version for an existing (product, post) pair or a brand-new pair both go through the same upsert endpoint. */
export async function saveCadence(id: string, rate: number) {
  if (!Number.isFinite(rate) || rate <= 0) return;
  const row = state.cadences.find((c) => c.id === id);
  if (!row) return;
  try {
    await cadencesApi.createVersion({ productId: row.productId, postId: row.postId, rate });
    await reloadCadences();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function addCadence(productId: string, postId: string, rate: number) {
  if (!productId || !postId || !Number.isFinite(rate) || rate <= 0) return;
  try {
    await cadencesApi.createVersion({ productId, postId, rate });
    await reloadCadences();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Actions — cause review (BL-008)                                     */
/* ------------------------------------------------------------------ */

export async function reviewProposal(id: string, decision: 'approved' | 'rejected', code?: string) {
  try {
    await causesApi.review(id, { decision: decision === 'approved' ? 'accept' : 'reject', code });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/** An operator flagged a missing cause from the mobile app. */
export async function proposeCause(label: string, domain = 'stop') {
  if (!label.trim()) return;
  try {
    await causesApi.propose({ domain, labelFr: label.trim() });
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Actions — users (BL-012 / BL-013 / BL-015)                          */
/* ------------------------------------------------------------------ */

export async function addUser(name: string, role: RoleKey, interim: boolean) {
  if (!name.trim()) return;
  try {
    await operatorsApi.create({ email: `${name.trim().toLowerCase().replace(/\s+/g, '.')}@oas.local`, displayName: name.trim(), role, interim, workspace: role === 'operator' ? 'mobile' : 'web' });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function toggleUserActive(id: string) {
  const user = state.users.find((u) => u.id === id);
  if (!user) return;
  try {
    await operatorsApi.setActive(id, !user.active);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-012 — restrict a console account to a single line (null = whole site). */
export async function setUserScope(id: string, scopeLineId: string | null) {
  try {
    await operatorsApi.setScope(id, { scopeLineId });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function setUserRole(id: string, role: RoleKey) {
  try {
    await operatorsApi.setRole(id, role);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/** Returns the plaintext PIN exactly once (spec decision v12) — never persisted in RefState. */
export async function regeneratePin(id: string): Promise<string | null> {
  try {
    const result = await operatorsApi.regeneratePin(id);
    if (!result.success) pushToast('common.actionFailed');
    return result.success ? (result.pin ?? null) : null;
  } catch {
    pushToast('common.actionFailed');
    return null;
  }
}

/* ------------------------------------------------------------------ */
/* Actions — equipment (BL-002)                                        */
/* ------------------------------------------------------------------ */

export async function upsertEquipment(row: EquipmentRow) {
  if (!row.code.trim()) return;
  const body = { postId: row.postId ?? undefined, code: row.code.trim().toUpperCase(), name: row.name.trim() || row.code, manufacturer: row.manufacturer || undefined, criticality: row.criticality };
  try {
    if (row.id) await equipmentsApi.update(row.id, body);
    else await equipmentsApi.create(body);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function removeEquipment(id: string) {
  try {
    await equipmentsApi.remove(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Actions — shift sign-off (BL-043)                                   */
/* ------------------------------------------------------------------ */

export async function signShiftReport(shiftTemplateId: string, note: string): Promise<ShiftSignOff | null> {
  if (!note.trim()) return null;
  try {
    const dto = await shiftsApi.createSignoff({ shiftTemplateId, workDate: new Date().toISOString().slice(0, 10), note: note.trim() });
    await reloadAll();
    const shiftCodeById = new Map(state.shifts.map((s) => [s.id, s.code]));
    return toSignOff(dto, shiftCodeById);
  } catch {
    pushToast('common.actionFailed');
    return null;
  }
}

/* ------------------------------------------------------------------ */
/* Actions — import (BL-010)                                           */
/* ------------------------------------------------------------------ */

export async function applyImport(dataset: string, rows: Record<string, string>[]): Promise<number> {
  try {
    const created = await importsApi.create(dataset, rows);
    const committed = await importsApi.commit(created.id);
    await reloadAll();
    return committed.rowsOk;
  } catch {
    pushToast('common.actionFailed');
    return 0;
  }
}

/* ------------------------------------------------------------------ */
/* BL-011 — setup completeness, computed server-side                   */
/* ------------------------------------------------------------------ */

export interface CompletenessRow { key: string; pct: number; detail: string }

let completenessCache: CompletenessRow[] = [];
let completenessRequested = false;
/** Fetched once and cached; every mutation elsewhere in this store calls `reloadAll()`, not this — call `refreshCompleteness()` after an edit if a fresher score is needed. */
export function completeness(): CompletenessRow[] {
  if (!completenessRequested) {
    completenessRequested = true;
    // `completeness()` is called directly from a component's render body
    // (Referentials.tsx), so a failure must not reset the flag — that would
    // retry on every re-render and hammer the endpoint. One try per page
    // load, same as before; a stale/zero score just isn't worth a toast.
    void refreshCompleteness().catch(() => {});
  }
  return completenessCache;
}

/** Background metric, not a discrete user action — a failure retries silently on the next render rather than interrupting with a toast. */
export async function refreshCompleteness(): Promise<void> {
  const c = await hierarchyApi.completeness();
  completenessCache = [
    { key: 'web.ref.hierarchy', pct: Math.round(c.postsRatio * 100), detail: `${Math.round(c.postsRatio * 100)}%` },
    { key: 'web.ref.completeness.products', pct: Math.round(c.namedProductsRatio * 100), detail: `${Math.round(c.namedProductsRatio * 100)}%` },
    { key: 'web.ref.completeness.cadences', pct: Math.round(c.referencesWithCadenceRatio * 100), detail: `${Math.round(c.referencesWithCadenceRatio * 100)}%` },
    { key: 'web.ref.stopReasons', pct: Math.round(c.causesRatio * 100), detail: `${Math.round(c.causesRatio * 100)}%` },
    { key: 'web.ref.completeness.calendars', pct: Math.round(c.shiftCoverageRatio * 100), detail: `${Math.round(c.shiftCoverageRatio * 100)}%` },
  ];
  listeners.forEach((l) => l());
}

/* ------------------------------------------------------------------ */
/* Actions — cause tree (BL-007)                                       */
/* ------------------------------------------------------------------ */

/**
 * Cause/reason labels are real bilingual data (`labelFr`/`labelAr`, both
 * always present server-side — spec §6.1), never a static i18n key: an
 * admin can add an arbitrary cause at any time, so there's no fixed
 * dictionary entry to translate through. Every screen that shows a cause
 * or reason must resolve it through this, in the CURRENT UI language, or
 * it silently shows French even when the operator picked Arabic/English.
 */
export function causeLabelIn(n: { label: string; labelAr: string }, lang: 'fr' | 'en' | 'ar'): string {
  return lang === 'ar' ? (n.labelAr || n.label) : n.label;
}

/** Create or update a level-1 family (service mapping + criticality). */
export async function upsertCause(input: { id?: string; code: string; label: string; labelAr: string; kind: EventKind; criticality: CauseNode['criticality'] }) {
  const code = input.code.trim().toUpperCase();
  if (!code || !input.label.trim()) return;
  // Falls back to the French text only if no Arabic translation was given —
  // never silently duplicates French into the Arabic column as "translated".
  const body = { domain: 'stop', code, labelFr: input.label.trim(), labelAr: input.labelAr.trim() || input.label.trim(), eventType: KIND_TO_EVENT_TYPE[input.kind], defaultCriticality: input.criticality };
  try {
    if (input.id) await causesApi.update(input.id, body);
    else await causesApi.create(body);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function setCauseKind(id: string, kind: EventKind) {
  try {
    await causesApi.setKind(id, KIND_TO_EVENT_TYPE[kind]);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function setCauseCriticality(id: string, criticality: CauseNode['criticality']) {
  try {
    await causesApi.setCriticality(id, criticality);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function toggleCauseActive(id: string) {
  const cause = state.causeTree.find((c) => c.id === id);
  if (!cause) return;
  try {
    await causesApi.setActive(id, !cause.active);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function addCauseChild(parentId: string, label: string, labelAr: string) {
  if (!label.trim()) return;
  const parent = state.causeTree.find((c) => c.id === parentId);
  if (!parent) return;
  const code = `${parent.code}.${parent.children.length + 1}`;
  try {
    await causesApi.addChild(parentId, { domain: 'stop', code, labelFr: label.trim(), labelAr: labelAr.trim() || label.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function removeCauseChild(parentId: string, childId: string) {
  try {
    await causesApi.removeChild(parentId, childId);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function removeCause(id: string) {
  try {
    await causesApi.remove(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Actions — references / products (BL-003)                            */
/* ------------------------------------------------------------------ */

export async function upsertProduct(row: ProductRow) {
  const ref = row.ref.trim().toUpperCase();
  if (!ref) return;
  const body = { reference: ref, name: row.label.trim() || ref, customer: row.customer.trim() || undefined, unit: row.unit || 'pcs' };
  try {
    if (row.id) await productsApi.update(row.id, body);
    else await productsApi.create(body);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function removeProduct(id: string) {
  try {
    await productsApi.remove(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Actions — shift calendars (BL-009)                                  */
/* ------------------------------------------------------------------ */

/** Gross span of a shift in minutes (handles shifts crossing midnight). */
export function shiftSpanMin(s: Pick<ShiftRow, 'start' | 'end'>): number {
  const min = (v: string) => {
    const [h, m] = v.split(':').map(Number);
    return (Number.isFinite(h) ? h : 0) * 60 + (Number.isFinite(m) ? m : 0);
  };
  const diff = min(s.end) - min(s.start);
  return diff > 0 ? diff : diff + 1440;
}

/** Theoretical opening time = span − deducted breaks (BL-009, feeds the OEE). */
export function shiftOpenMin(s: ShiftRow): number {
  return Math.max(0, shiftSpanMin(s) - Math.max(0, s.breakMinutes || 0));
}

export async function upsertShift(row: ShiftRow) {
  const code = row.code.trim().toLowerCase();
  if (!code || !row.name.trim()) return;
  try {
    const siteId = row.siteId || (await hierarchyApi.listSites())[0]?.id;
    if (!siteId) return;
    const body = { siteId, code, name: row.name.trim(), startTime: row.start, endTime: row.end, breakMinutes: Math.max(0, Math.round(row.breakMinutes || 0)) };
    if (row.id) await shiftsApi.update(row.id, body);
    else await shiftsApi.create(body);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function removeShift(id: string) {
  try {
    await shiftsApi.remove(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/** Total theoretical opening time of the active calendars, in minutes / day. */
export function dailyOpenMin(s: RefState = state): number {
  return s.shifts.filter((x) => x.active).reduce((sum, x) => sum + shiftOpenMin(x), 0);
}
