import type { PluginManifest } from '@/modules/shared/plugins/types';

export const interventionsPlugin: PluginManifest = {
  code: 'OA0005INTERVENTIONS',
  moduleKey: 'interventions',
  category: 'production',
  nameI18nKey: 'plugins.interventions.name',
  descriptionI18nKey: 'plugins.interventions.description',
  icon: 'Wrench',
  version: '1.0.0',
  isCore: false,
  dependencies: ['OA0004DECLARATIONS'],
  workspaces: ['mobile'],
  routes: ['/mobile/inbox'],
  navKeys: [],
};

export default interventionsPlugin;
