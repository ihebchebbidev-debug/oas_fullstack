/**
 * React bindings over the plugin registry + activation store.
 */
import { useCallback, useMemo, useSyncExternalStore } from 'react';

import { getAllPlugins, getDependentsOf, getPluginsByWorkspace } from './registry';
import * as store from './activationStore';
import type { PluginManifest, PluginRuntimeState, PluginWorkspace } from './types';

function useActivations() {
  return useSyncExternalStore(store.subscribe, store.getSnapshot, store.getSnapshot);
}

/** True when the given plugin code is currently active. */
export function useIsPluginEnabled(code: string): boolean {
  useActivations();
  return store.isEnabled(code);
}

/** Full runtime view of every registered plugin — used by the console. */
export function usePlugins() {
  const snapshot = useActivations();

  const plugins = useMemo<PluginRuntimeState[]>(
    () =>
      getAllPlugins().map((manifest) => {
        const enabled = store.isEnabled(manifest.code);
        return {
          manifest,
          isEnabled: enabled,
          hasBrokenDependency:
            enabled && manifest.dependencies.some((dep) => !store.isEnabled(dep)),
          enabledDependents: getDependentsOf(manifest.code)
            .filter((d) => store.isEnabled(d.code))
            .map((d) => d.code),
        };
      }),
    // snapshot participates so the memo recomputes on every toggle
    [snapshot],
  );

  const toggle = useCallback((code: string, enabled: boolean) => {
    store.setEnabled(code, enabled);
  }, []);

  const reset = useCallback(() => store.resetActivations(), []);

  const stats = useMemo(
    () => ({ active: plugins.filter((p) => p.isEnabled).length, total: plugins.length }),
    [plugins],
  );

  return { plugins, toggle, reset, stats };
}

/** Manifests contributing to one shell, filtered to the enabled ones. */
export function useWorkspacePlugins(workspace: PluginWorkspace): PluginManifest[] {
  const snapshot = useActivations();
  return useMemo(
    () => getPluginsByWorkspace(workspace).filter((p) => store.isEnabled(p.code)),
    [workspace, snapshot],
  );
}
