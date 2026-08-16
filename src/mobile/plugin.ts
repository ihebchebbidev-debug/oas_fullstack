import type { PluginManifest } from '@/modules/shared/plugins/types';

/**
 * Mobile terminal shell — operator / team-leader workspace on the shop floor.
 * Registered as a plugin so a deployment can ship console-only installs.
 */
export const mobileAppPlugin: PluginManifest = {
  code: 'OA1001MOBILEAPP',
  moduleKey: 'mobile',
  category: 'app',
  nameI18nKey: 'plugins.mobile.name',
  descriptionI18nKey: 'plugins.mobile.description',
  icon: 'Smartphone',
  version: '1.0.0',
  isCore: true,
  dependencies: ['OA0001AUTH'],
  workspaces: ['mobile'],
  routes: ['/mobile'],
  navKeys: [],
};

export default mobileAppPlugin;
