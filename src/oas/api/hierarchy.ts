import { apiFetch } from './client';

export interface OasSiteDto {
  id: string; code: string; name: string; timezone: string; address: string | null; archived: boolean;
}
export interface OasZoneDto {
  id: string; siteId: string; code: string; name: string; sortOrder: number; archived: boolean;
}
export interface OasLineDto {
  id: string; zoneId: string; code: string; name: string; sortOrder: number; targetOee: number | null; archived: boolean;
}
export interface OasPostDto {
  id: string; lineId: string; code: string; name: string; sortOrder: number;
  postType: string | null; isCritical: boolean; isActive: boolean; archived: boolean;
}
export interface OasCompletenessDto {
  postsRatio: number; namedProductsRatio: number; referencesWithCadenceRatio: number;
  causesRatio: number; shiftCoverageRatio: number; overall: number;
}
export interface OasQrTokenDto { postId: string; token: string }

export const hierarchyApi = {
  listSites: () => apiFetch<OasSiteDto[]>('/sites'),
  createSite: (body: { code: string; name: string; timezone?: string; address?: string }) =>
    apiFetch<OasSiteDto>('/sites', { method: 'POST', body }),
  updateSite: (id: string, body: { code: string; name: string; timezone?: string; address?: string }) =>
    apiFetch<{ success: boolean }>(`/sites/${id}`, { method: 'PUT', body }),
  archiveSite: (id: string) => apiFetch<{ success: boolean }>(`/sites/${id}/archive`, { method: 'POST' }),

  listZones: (siteId?: string) => apiFetch<OasZoneDto[]>('/zones', { query: { siteId } }),
  createZone: (body: { siteId: string; code: string; name: string; sortOrder?: number }) =>
    apiFetch<OasZoneDto>('/zones', { method: 'POST', body }),
  updateZone: (id: string, body: { siteId: string; code: string; name: string; sortOrder?: number }) =>
    apiFetch<{ success: boolean }>(`/zones/${id}`, { method: 'PUT', body }),
  archiveZone: (id: string) => apiFetch<{ success: boolean }>(`/zones/${id}/archive`, { method: 'POST' }),

  listLines: (zoneId?: string) => apiFetch<OasLineDto[]>('/lines', { query: { zoneId } }),
  createLine: (body: { zoneId: string; code: string; name: string; sortOrder?: number; targetOee?: number }) =>
    apiFetch<OasLineDto>('/lines', { method: 'POST', body }),
  updateLine: (id: string, body: { zoneId: string; code: string; name: string; sortOrder?: number; targetOee?: number }) =>
    apiFetch<{ success: boolean }>(`/lines/${id}`, { method: 'PUT', body }),
  archiveLine: (id: string) => apiFetch<{ success: boolean }>(`/lines/${id}/archive`, { method: 'POST' }),

  listPosts: (lineId?: string) => apiFetch<OasPostDto[]>('/posts', { query: { lineId } }),
  createPost: (body: { lineId: string; code: string; name: string; sortOrder?: number; postType?: string }) =>
    apiFetch<OasPostDto>('/posts', { method: 'POST', body }),
  updatePost: (id: string, body: { lineId: string; code: string; name: string; sortOrder?: number; postType?: string }) =>
    apiFetch<{ success: boolean }>(`/posts/${id}`, { method: 'PUT', body }),
  updatePostAttributes: (id: string, body: { postType?: string; sortOrder?: number }) =>
    apiFetch<{ success: boolean }>(`/posts/${id}/attributes`, { method: 'PUT', body }),
  setPostCritical: (id: string, isCritical: boolean) =>
    apiFetch<{ success: boolean }>(`/posts/${id}/critical`, { method: 'PUT', body: { isCritical } }),
  archivePost: (id: string) => apiFetch<{ success: boolean }>(`/posts/${id}/archive`, { method: 'POST' }),
  postCapacity: (id: string) => apiFetch<{ operatorsRequired: number }>(`/posts/${id}/capacity`),
  postByCode: (code: string) => apiFetch<OasPostDto>(`/posts/by-code/${encodeURIComponent(code)}`),
  qrToken: (id: string) => apiFetch<OasQrTokenDto>(`/posts/${id}/qr-token`),
  rotateQrToken: (id: string) => apiFetch<OasQrTokenDto>(`/posts/${id}/qr-token/rotate`, { method: 'POST' }),
  qrTokens: (lineId?: string) => apiFetch<OasQrTokenDto[]>('/posts/qr-tokens', { query: { lineId } }),

  completeness: () => apiFetch<OasCompletenessDto>('/referentials/completeness'),
};
