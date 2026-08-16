/**
 * Plugin Registry — auto-discovery of every manifest in the codebase.
 *
 * Each module exports a PluginManifest from `<module>/plugin.ts`; the two
 * application shells do the same from `src/web/plugin.ts` and
 * `src/mobile/plugin.ts`. Vite eagerly globs them at build time, so discovery
 * costs nothing at runtime and a new module is registered simply by existing.
 */
import type { PluginCategory, PluginManifest, PluginWorkspace } from './types';

const modules = import.meta.glob<{ default?: PluginManifest } & Record<string, unknown>>(
  '/src/**/plugin.ts',
  { eager: true },
);

const _registry = new Map<string, PluginManifest>();

for (const [path, mod] of Object.entries(modules)) {
  const manifest =
    mod.default ??
    (Object.values(mod).find(
      (v): v is PluginManifest =>
        !!v && typeof v === 'object' && typeof (v as PluginManifest).code === 'string',
    ) as PluginManifest | undefined);

  if (!manifest) {
    if (import.meta.env.DEV) console.warn(`[plugins] No manifest exported from ${path}`);
    continue;
  }
  if (_registry.has(manifest.code)) {
    if (import.meta.env.DEV) {
      console.warn(`[plugins] Duplicate plugin code "${manifest.code}" at ${path} — ignored.`);
    }
    continue;
  }
  _registry.set(manifest.code, manifest);
}

// ───────────── Lookups ─────────────

export function getAllPlugins(): PluginManifest[] {
  return Array.from(_registry.values()).sort((a, b) => a.code.localeCompare(b.code));
}

export function getPluginByCode(code: string): PluginManifest | undefined {
  return _registry.get(code);
}

export function getPluginsByCategory(category: PluginCategory): PluginManifest[] {
  return getAllPlugins().filter((p) => p.category === category);
}

export function getPluginsByWorkspace(workspace: PluginWorkspace): PluginManifest[] {
  return getAllPlugins().filter((p) => p.workspaces.includes(workspace));
}

export function getPluginByModuleKey(moduleKey: string): PluginManifest | undefined {
  return getAllPlugins().find((p) => p.moduleKey === moduleKey);
}

export const ALL_PLUGIN_CODES = (): string[] => getAllPlugins().map((p) => p.code);

// ───────────── Dependency graph ─────────────

export function getDependentsOf(code: string): PluginManifest[] {
  return getAllPlugins().filter((p) => p.dependencies.includes(code));
}

/** Everything this plugin needs, directly or transitively. */
export function getTransitiveDependencies(code: string): string[] {
  const out = new Set<string>();
  const walk = (c: string) => {
    for (const dep of _registry.get(c)?.dependencies ?? []) {
      if (out.has(dep)) continue;
      out.add(dep);
      walk(dep);
    }
  };
  walk(code);
  return Array.from(out);
}

/** Everything that breaks when this plugin is turned off. */
export function getTransitiveDependents(code: string): string[] {
  const out = new Set<string>();
  const walk = (c: string) => {
    for (const dep of getDependentsOf(c)) {
      if (out.has(dep.code)) continue;
      out.add(dep.code);
      walk(dep.code);
    }
  };
  walk(code);
  return Array.from(out);
}
