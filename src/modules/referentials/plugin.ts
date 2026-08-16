import type { PluginManifest } from '@/modules/shared/plugins/types';

export const referentialsPlugin: PluginManifest = {
  code: 'OA0008REFERENTIALS',
  moduleKey: 'referentials',
  category: 'system',
  nameI18nKey: 'plugins.referentials.name',
  descriptionI18nKey: 'plugins.referentials.description',
  icon: 'Database',
  version: '1.0.0',
  isCore: true,
  dependencies: [],
  workspaces: ['web'],
  routes: ['/web/referentials'],
  navKeys: [],
};

export default referentialsPlugin;
