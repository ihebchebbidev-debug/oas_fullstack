import type { PluginManifest } from '@/modules/shared/plugins/types';

export const declarationsPlugin: PluginManifest = {
  code: 'OA0004DECLARATIONS',
  moduleKey: 'declarations',
  category: 'production',
  nameI18nKey: 'plugins.declarations.name',
  descriptionI18nKey: 'plugins.declarations.description',
  icon: 'ClipboardCheck',
  version: '1.0.0',
  isCore: false,
  dependencies: ['OA0003SHOPFLOOR','OA0008REFERENTIALS'],
  workspaces: ['mobile'],
  routes: ['/mobile/stop','/mobile/production','/mobile/changeover','/mobile/neighbor'],
  navKeys: [],
};

export default declarationsPlugin;
