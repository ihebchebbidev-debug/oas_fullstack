import type { PluginManifest } from '@/modules/shared/plugins/types';

export const consolePlugin: PluginManifest = {
  code: 'OA0011CONSOLE',
  moduleKey: 'console',
  category: 'system',
  nameI18nKey: 'plugins.console.name',
  descriptionI18nKey: 'plugins.console.description',
  icon: 'Settings',
  version: '1.0.0',
  isCore: true,
  dependencies: [],
  workspaces: ['web'],
  routes: ['/web/admin'],
  navKeys: [],
};

export default consolePlugin;
