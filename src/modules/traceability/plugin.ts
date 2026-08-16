import type { PluginManifest } from '@/modules/shared/plugins/types';

export const traceabilityPlugin: PluginManifest = {
  code: 'OA0012TRACEABILITY',
  moduleKey: 'traceability',
  category: 'quality',
  nameI18nKey: 'plugins.traceability.name',
  descriptionI18nKey: 'plugins.traceability.description',
  icon: 'ScanLine',
  version: '1.0.0',
  isCore: false,
  dependencies: ['OA0004DECLARATIONS'],
  workspaces: ['mobile'],
  routes: ['/mobile/scan','/mobile/history'],
  navKeys: [],
};

export default traceabilityPlugin;
