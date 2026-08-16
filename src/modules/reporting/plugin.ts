import type { PluginManifest } from '@/modules/shared/plugins/types';

export const reportingPlugin: PluginManifest = {
  code: 'OA0009REPORTING',
  moduleKey: 'reporting',
  category: 'analytics',
  nameI18nKey: 'plugins.reporting.name',
  descriptionI18nKey: 'plugins.reporting.description',
  icon: 'BarChart3',
  version: '1.0.0',
  isCore: false,
  dependencies: ['OA0004DECLARATIONS'],
  workspaces: ['web','mobile'],
  routes: ['/web/reports','/web/shift-report','/mobile/shift-end'],
  navKeys: [],
};

export default reportingPlugin;
