import type { PluginManifest } from '@/modules/shared/plugins/types';

export const assignmentsPlugin: PluginManifest = {
  code: 'OA0007ASSIGNMENTS',
  moduleKey: 'assignments',
  category: 'planning',
  nameI18nKey: 'plugins.assignments.name',
  descriptionI18nKey: 'plugins.assignments.description',
  icon: 'Users',
  version: '1.0.0',
  isCore: false,
  dependencies: ['OA0003SHOPFLOOR'],
  workspaces: ['web'],
  routes: ['/web/assignments'],
  navKeys: [],
};

export default assignmentsPlugin;
