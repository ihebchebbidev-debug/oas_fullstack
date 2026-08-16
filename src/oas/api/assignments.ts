import { apiFetch } from './client';

export interface OasAssignmentDto {
  postId: string; userId: string; userDisplayName: string | null; shiftTemplateId: string;
  workDate: string; productionOrderId: string | null; note: string | null; published: boolean;
}
export interface OasAssignmentCountsDto { totalPosts: number; assigned: number; published: number }
export interface OasRosterEntryDto { userId: string; displayName: string | null; isActive: boolean }
export interface OasPresenceDto {
  userId: string; workDate: string; shiftTemplateId: string; status: string; confirmedAt: string | null; reason: string | null;
}

export const assignmentsApi = {
  list: (shift: string, date: string) => apiFetch<OasAssignmentDto[]>('/assignments', { query: { shift, date } }),
  published: (shift: string, date: string) => apiFetch<OasAssignmentDto[]>('/assignments/published', { query: { shift, date } }),
  upsert: (postId: string, body: { userId: string; shiftTemplateId: string; workDate: string; productionOrderId?: string; note?: string }) =>
    apiFetch<OasAssignmentDto>(`/assignments/${postId}`, { method: 'PUT', body }),
  remove: (postId: string, shift: string, date: string) =>
    apiFetch<{ success: boolean }>(`/assignments/${postId}`, { method: 'DELETE', query: { shift, date } }),
  autoFill: (shift: string, date: string) => apiFetch<{ filled: number }>('/assignments/auto-fill', { method: 'POST', query: { shift, date } }),
  clearAll: (shift: string, date: string) => apiFetch<{ removed: number }>('/assignments', { method: 'DELETE', query: { shift, date } }),
  publish: (shift: string, date: string, postId?: string) =>
    apiFetch<{ published: number }>('/assignments/publish', { method: 'POST', query: { shift, date, postId } }),
  counts: (shift: string, date: string) => apiFetch<OasAssignmentCountsDto>('/assignments/counts', { query: { shift, date } }),
  roster: () => apiFetch<OasRosterEntryDto[]>('/assignments/roster'),
};

export const presenceApi = {
  set: (operatorId: string, body: { workDate: string; shiftTemplateId: string; status: string; reason?: string }) =>
    apiFetch<{ success: boolean }>(`/presence/${operatorId}`, { method: 'PUT', body }),
  confirm: (operatorId: string, body: { workDate: string; shiftTemplateId: string }) =>
    apiFetch<{ success: boolean }>(`/presence/${operatorId}/confirm`, { method: 'POST', body }),
  list: (shift: string, date: string) => apiFetch<OasPresenceDto[]>('/presence', { query: { shift, date } }),
};
