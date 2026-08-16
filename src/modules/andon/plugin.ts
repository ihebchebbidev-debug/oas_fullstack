import type { PluginManifest } from '@/modules/shared/plugins/types';

export const andonPlugin: PluginManifest = {
  code: 'OA0010ANDON',
  moduleKey: 'andon',
  category: 'production',
  nameI18nKey: 'plugins.andon.name',
  descriptionI18nKey: 'plugins.andon.description',
  icon: 'Tv',
  version: '1.0.0',
  isCore: false,
  dependencies: ['OA0003SHOPFLOOR'],
  workspaces: ['web'],
  routes: ['/web/andon'],
  navKeys: [],
};

export default andonPlugin;
