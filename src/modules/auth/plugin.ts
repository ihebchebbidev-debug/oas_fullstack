import type { PluginManifest } from '@/modules/shared/plugins/types';

export const authPlugin: PluginManifest = {
  code: 'OA0001AUTH',
  moduleKey: 'auth',
  category: 'system',
  nameI18nKey: 'plugins.auth.name',
  descriptionI18nKey: 'plugins.auth.description',
  icon: 'LogIn',
  version: '1.0.0',
  isCore: true,
  dependencies: [],
  workspaces: ['web','mobile'],
  routes: ['/web/login','/mobile/login'],
  navKeys: [],
};

export default authPlugin;
