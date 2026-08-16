/**
 * Hierarchy store — BL-001 Site / Zone / Line / Post CRUD, backed by the
 * real `GET/POST/PUT /api/oas/{sites,zones,lines,posts}` endpoints (spec
 * §6.1 Hiérarchie). The critical-post flag now lives on the post row itself
 * (`isCritical`, `PUT /posts/{id}/critical`) rather than a separate
 * `refState.criticalPosts` list — the server is the single source of truth.
 */

import { useSyncExternalStore } from 'react';
import { hierarchyApi, type OasLineDto, type OasPostDto, type OasSiteDto, type OasZoneDto } from './api/hierarchy';
import { pushToast } from '@/shared/lib/toast';

export interface SiteItem { id: string; code: string; name: string; archived: boolean }
export interface ZoneItem { id: string; siteId: string; code: string; name: string; archived: boolean }
export interface LineItem { id: string; zoneId: string; code: string; name: string; archived: boolean }
/** BL-001 — a post carries its nature; capacity is now a server-derived stat (`GET /posts/{id}/capacity`), not an editable attribute. */
export type PostType = 'production' | 'assembly' | 'control' | 'packaging';

export interface PostItem {
  id: string;
  lineId: string;
  code: string;
  name: string;
  archived: boolean;
  type: PostType;
  isCritical: boolean;
}

export interface HierarchyState {
  sites: SiteItem[];
  zones: ZoneItem[];
  lines: LineItem[];
  posts: PostItem[];
  loading: boolean;
}

function toSite(s: OasSiteDto): SiteItem { return { id: s.id, code: s.code, name: s.name, archived: s.archived }; }
function toZone(z: OasZoneDto): ZoneItem { return { id: z.id, siteId: z.siteId, code: z.code, name: z.name, archived: z.archived }; }
function toLine(l: OasLineDto): LineItem { return { id: l.id, zoneId: l.zoneId, code: l.code, name: l.name, archived: l.archived }; }
const POST_TYPES: readonly string[] = ['production', 'assembly', 'control', 'packaging'];
function toPost(p: OasPostDto): PostItem {
  return {
    id: p.id, lineId: p.lineId, code: p.code, name: p.name, archived: p.archived,
    type: (POST_TYPES.includes(p.postType ?? '') ? p.postType : 'production') as PostType,
    isCritical: p.isCritical,
  };
}

let state: HierarchyState = { sites: [], zones: [], lines: [], posts: [], loading: true };
const listeners = new Set<() => void>();

function commit(patch: Partial<HierarchyState>) {
  state = { ...state, ...patch };
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

export function useHierarchyState(): HierarchyState {
  ensureHierarchyLoaded();
  return useSyncExternalStore(subscribe, () => state, () => state);
}

/** Plain (non-hook) snapshot accessor for cross-store lookups (e.g. eventStore resolving a post id to its code). */
export function getHierarchyState(): HierarchyState {
  return state;
}

async function reloadAll() {
  const [sites, zones, lines, posts] = await Promise.all([
    hierarchyApi.listSites(), hierarchyApi.listZones(), hierarchyApi.listLines(), hierarchyApi.listPosts(),
  ]);
  commit({
    sites: sites.map(toSite), zones: zones.map(toZone), lines: lines.map(toLine), posts: posts.map(toPost),
    loading: false,
  });
}

let loaded = false;
export function ensureHierarchyLoaded() {
  if (loaded) return;
  loaded = true;
  void reloadAll().catch(() => commit({ loading: false }));
}

function nextCode(prefix: string, existing: string[]) {
  let n = 1;
  while (existing.includes(`${prefix}-${String(n).padStart(2, '0')}`)) n += 1;
  return `${prefix}-${String(n).padStart(2, '0')}`;
}

/* ------------------------------------------------------------------ */
/* Sites                                                               */
/* ------------------------------------------------------------------ */

export async function addSite(name: string) {
  if (!name.trim()) return;
  const code = nextCode('SITE', state.sites.map((s) => s.code));
  try {
    await hierarchyApi.createSite({ code, name: name.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function renameSite(id: string, name: string) {
  const site = state.sites.find((s) => s.id === id);
  if (!site || !name.trim()) return;
  try {
    await hierarchyApi.updateSite(id, { code: site.code, name: name.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function archiveSite(id: string) {
  try {
    await hierarchyApi.archiveSite(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Zones                                                               */
/* ------------------------------------------------------------------ */

export async function addZone(siteId: string, name: string) {
  if (!name.trim()) return;
  const code = nextCode('ZN', state.zones.map((z) => z.code));
  try {
    await hierarchyApi.createZone({ siteId, code, name: name.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function renameZone(id: string, name: string) {
  const zone = state.zones.find((z) => z.id === id);
  if (!zone || !name.trim()) return;
  try {
    await hierarchyApi.updateZone(id, { siteId: zone.siteId, code: zone.code, name: name.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function archiveZone(id: string) {
  try {
    await hierarchyApi.archiveZone(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Lines                                                               */
/* ------------------------------------------------------------------ */

export async function addLine(zoneId: string, name: string) {
  if (!name.trim()) return;
  const code = nextCode('LN', state.lines.map((l) => l.code));
  try {
    await hierarchyApi.createLine({ zoneId, code, name: name.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function renameLine(id: string, name: string) {
  const line = state.lines.find((l) => l.id === id);
  if (!line || !name.trim()) return;
  try {
    await hierarchyApi.updateLine(id, { zoneId: line.zoneId, code: line.code, name: name.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function archiveLine(id: string) {
  try {
    await hierarchyApi.archiveLine(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/* ------------------------------------------------------------------ */
/* Posts                                                               */
/* ------------------------------------------------------------------ */

export async function addPost(lineId: string, name: string) {
  if (!name.trim()) return;
  const code = nextCode('PO', state.posts.map((p) => p.code));
  try {
    await hierarchyApi.createPost({ lineId, code, name: name.trim() });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/** BL-001 — edit the nature of a post (capacity is now server-derived, read-only). */
export async function setPostType(id: string, type: PostType) {
  try {
    await hierarchyApi.updatePostAttributes(id, { postType: type });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function togglePostCritical(id: string) {
  const post = state.posts.find((p) => p.id === id);
  if (!post) return;
  try {
    await hierarchyApi.setPostCritical(id, !post.isCritical);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

/** Live operator-capacity lookup — server-derived, fetched on demand (not cached in the list state). */
export function postCapacity(id: string): Promise<number> {
  return hierarchyApi.postCapacity(id).then((r) => r.operatorsRequired);
}

export async function renamePost(id: string, name: string) {
  const post = state.posts.find((p) => p.id === id);
  if (!post || !name.trim()) return;
  try {
    await hierarchyApi.updatePost(id, { lineId: post.lineId, code: post.code, name: name.trim(), postType: post.type });
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}

export async function archivePost(id: string) {
  try {
    await hierarchyApi.archivePost(id);
    await reloadAll();
  } catch {
    pushToast('common.actionFailed');
  }
}
