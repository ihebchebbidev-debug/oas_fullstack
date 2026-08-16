import type { PluginManifest } from '@/modules/shared/plugins/types';

export const alertsPlugin: PluginManifest = {
  code: 'OA0006ALERTS',
  moduleKey: 'alerts',
  category: 'quality',
  nameI18nKey: 'plugins.alerts.name',
  descriptionI18nKey: 'plugins.alerts.description',
  icon: 'Bell',
  version: '1.0.0',
  isCore: false,
  dependencies: ['OA0004DECLARATIONS'],
  workspaces: ['web'],
  routes: ['/web/alerts'],
  navKeys: [],
};

export default alertsPlugin;
