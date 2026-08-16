import { apiFetch } from './client';

export interface OasAuditLogDto {
  id: string; entityTable: string; entityId: string | null; action: string; actorId: string | null; reason: string | null; occurredAt: string;
}

export const auditApi = {
  list: (params?: { entity?: string; from?: string; to?: string; actor?: string; action?: string; q?: string }) =>
    apiFetch<OasAuditLogDto[]>('/audit', { query: params }),
};
