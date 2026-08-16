import type { PluginManifest } from '@/modules/shared/plugins/types';

export const demoPlugin: PluginManifest = {
  code: 'OA0013DEMO',
  moduleKey: 'demo',
  category: 'system',
  nameI18nKey: 'plugins.demo.name',
  descriptionI18nKey: 'plugins.demo.description',
  icon: 'PlayCircle',
  version: '1.0.0',
  isCore: false,
  dependencies: [],
  workspaces: ['web','mobile'],
  routes: [],
  navKeys: [],
};

export default demoPlugin;
