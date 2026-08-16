import type { PluginManifest } from '@/modules/shared/plugins/types';

/**
 * Web console shell — supervisor / manager / admin workspace.
 * Registered as a plugin so a deployment can ship mobile-only terminals.
 */
export const webAppPlugin: PluginManifest = {
  code: 'OA1000WEBAPP',
  moduleKey: 'web',
  category: 'app',
  nameI18nKey: 'plugins.web.name',
  descriptionI18nKey: 'plugins.web.description',
  icon: 'Monitor',
  version: '1.0.0',
  isCore: true,
  dependencies: ['OA0001AUTH'],
  workspaces: ['web'],
  routes: ['/web'],
  navKeys: [],
};

export default webAppPlugin;
