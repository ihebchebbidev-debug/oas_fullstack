import type { PluginManifest } from '@/modules/shared/plugins/types';

export const shopfloorPlugin: PluginManifest = {
  code: 'OA0003SHOPFLOOR',
  moduleKey: 'shopfloor',
  category: 'production',
  nameI18nKey: 'plugins.shopfloor.name',
  descriptionI18nKey: 'plugins.shopfloor.description',
  icon: 'Map',
  version: '1.0.0',
  isCore: true,
  dependencies: [],
  workspaces: ['web','mobile'],
  routes: ['/web/shopfloor','/mobile/home','/mobile/map'],
  navKeys: [],
};

export default shopfloorPlugin;
