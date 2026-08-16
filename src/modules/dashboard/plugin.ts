import type { PluginManifest } from '@/modules/shared/plugins/types';

export const dashboardPlugin: PluginManifest = {
  code: 'OA0002DASHBOARD',
  moduleKey: 'dashboard',
  category: 'analytics',
  nameI18nKey: 'plugins.dashboard.name',
  descriptionI18nKey: 'plugins.dashboard.description',
  icon: 'LayoutDashboard',
  version: '1.0.0',
  isCore: true,
  dependencies: [],
  workspaces: ['web','mobile'],
  routes: ['/web/dashboard','/mobile/kpi'],
  navKeys: [],
};

export default dashboardPlugin;
