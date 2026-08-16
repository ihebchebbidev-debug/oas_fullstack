import { apiFetch } from './client';

export interface OasPluginActivationDto {
  pluginCode: string; enabled: boolean; isCore: boolean; updatedAt: string; updatedBy: string | null;
}
export interface OasBulkPluginActivationResponseDto { success: boolean; reset: string[] }

export const pluginActivationsApi = {
  list: () => apiFetch<OasPluginActivationDto[]>('/plugin-activations'),
  setEnabled: (code: string, enabled: boolean) =>
    apiFetch<{ success: boolean; cascaded: string[] }>(`/plugin-activations/${code}`, { method: 'PUT', body: { enabled } }),
  bulkReset: () => apiFetch<OasBulkPluginActivationResponseDto>('/plugin-activations/bulk', { method: 'POST' }),
};
