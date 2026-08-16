/**
 * Plugin activation store — backed by the real `GET/PUT /api/oas/plugin-
 * activations` endpoints (spec §6.1, §8.0). The server is now the kill
 * switch: every OAS controller checks `oas_plugin_activations` itself and
 * refuses with 404 when disabled, so a client-only toggle (the old
 * localStorage version) could no longer re-open a module the server has
 * actually shut off — this store just reflects server truth. Cascade
 * (enabling pulls in dependencies, disabling pushes out dependents) is
 * computed server-side now too; a toggle just refetches the resulting list.
 */
import { getPluginByCode } from './registry';
import { pluginActivationsApi } from '@/oas/api/pluginActivations';
import type { PluginActivation } from './types';
import { pushToast } from '@/shared/lib/toast';

type Snapshot = Record<string, boolean>;

let snapshot: Snapshot = {};
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((l) => l());
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getSnapshot(): Snapshot {
  return snapshot;
}

let loaded = false;
export function ensureActivationsLoaded() {
  if (loaded) return;
  loaded = true;
  void reload();
}

async function reload() {
  try {
    const rows = await pluginActivationsApi.list();
    const next: Snapshot = {};
    rows.forEach((r) => { next[r.pluginCode] = r.isCore || r.enabled; });
    snapshot = next;
    emit();
  } catch {
    // stay on the current snapshot (empty on first failure, meaning every
    // non-core plugin defaults to disabled rather than a stale "everything on")
  }
}

/** Unknown codes default to enabled; core plugins can never be off. */
export function isEnabled(code: string): boolean {
  ensureActivationsLoaded();
  if (getPluginByCode(code)?.isCore) return true;
  return snapshot[code] ?? true;
}

export function listActivations(): PluginActivation[] {
  return Object.keys(snapshot).map((code) => ({ code, isEnabled: isEnabled(code) }));
}

/** Toggle a plugin; the server computes the cascade (dependencies/dependents) and this refetches the result. */
export function setEnabled(code: string, enabled: boolean): void {
  const manifest = getPluginByCode(code);
  if (!manifest || manifest.isCore) return;
  void pluginActivationsApi.setEnabled(code, enabled).then(reload).catch(() => pushToast('common.actionFailed'));
}

export function resetActivations(): void {
  void pluginActivationsApi.bulkReset().then(reload).catch(() => pushToast('common.actionFailed'));
}
