/**
 * Plugin Registry — type definitions (OAS Production).
 *
 * Mirrors the mother_app plugin contract: every feature module owns a
 * `plugin.ts` manifest, and the shared registry auto-discovers them.
 * The two application shells (web console, mobile terminal) are plugins too,
 * so an integrator can ship a build with only one workspace enabled.
 */

export type PluginCategory =
  | 'app'
  | 'production'
  | 'planning'
  | 'quality'
  | 'analytics'
  | 'system';

/** Which application shell(s) a plugin contributes surfaces to. */
export type PluginWorkspace = 'web' | 'mobile';

export interface PluginManifest {
  /** Immutable identifier, e.g. "OA0003SHOPFLOOR". Never change once assigned. */
  code: string;
  /** Stable folder key under src/modules (or "web" / "mobile" for shells). */
  moduleKey: string;
  category: PluginCategory;
  /** i18n key resolving to the human-readable plugin name. */
  nameI18nKey: string;
  /** i18n key resolving to a one-line description. */
  descriptionI18nKey: string;
  /** lucide-react icon name (e.g. "Map"). */
  icon: string;
  version: string;
  /** Core plugins cannot be disabled (auth, shells, console). */
  isCore: boolean;
  /** Plugin codes that must remain enabled for this one to function. */
  dependencies: string[];
  /** Shells this plugin renders into. */
  workspaces: PluginWorkspace[];
  /** Routes owned by this plugin (used for nav + route gating). */
  routes: string[];
  /** Nav item keys owned by this plugin. */
  navKeys: string[];
}

export interface PluginActivation {
  code: string;
  isEnabled: boolean;
  updatedAt?: string;
}

export interface PluginRuntimeState {
  manifest: PluginManifest;
  isEnabled: boolean;
  /** True when this plugin is enabled but a dependency is not. */
  hasBrokenDependency: boolean;
  /** Codes of enabled plugins that depend on this one. */
  enabledDependents: string[];
}
