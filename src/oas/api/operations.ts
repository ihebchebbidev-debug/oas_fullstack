import { apiFetch } from './client';

/* ------------------------------------------------------------------ */
/* Post sessions (spec §6.1 "Session de poste")                         */
/* ------------------------------------------------------------------ */

export interface OasPostSessionDto {
  id: string; postId: string; userId: string; assignmentId: string | null;
  productionOrderId: string | null; shiftTemplateId: string | null;
  startedAt: string; endedAt: string | null;
}
export interface OasScanResultDto { postId: string | null; postCode: string | null; resolved: boolean }

export const postSessionsApi = {
  open: (body: { clientEventId: string; postId: string; assignmentId?: string; productionOrderId?: string; shiftTemplateId?: string; startedVia?: string; forceRelay?: boolean }) =>
    apiFetch<OasPostSessionDto>('/post-sessions/open', { method: 'POST', body }),
  relay: (id: string, clientEventId: string, newUserId: string) =>
    apiFetch<OasPostSessionDto>(`/post-sessions/${id}/relay`, { method: 'POST', body: { clientEventId, newUserId } }),
  close: (id: string, clientEventId: string) =>
    apiFetch<OasPostSessionDto>(`/post-sessions/${id}/close`, { method: 'POST', body: { clientEventId } }),
  active: () => apiFetch<OasPostSessionDto>('/post-sessions/active'),
  scan: (code: string) => apiFetch<OasScanResultDto>('/post-sessions/scan', { method: 'POST', body: { code } }),
};

/* ------------------------------------------------------------------ */
/* Declarations (spec §6.1 "Déclarations")                              */
/* ------------------------------------------------------------------ */

export interface OasDeclarationDto {
  id: string; kind: string; postSessionId: string | null; postId: string; userId: string;
  productionOrderId: string | null; productId: string | null; quantityOk: number; quantityNok: number;
  scrapCauseId: string | null; note: string | null; occurredAt: string; correctsId: string | null; isCorrected: boolean;
  correctionReason: string | null; createdBy: string;
}

export const declarationsApi = {
  createProduction: (body: { clientEventId: string; postSessionId?: string; postId: string; productionOrderId?: string; productId?: string; quantityOk: number; quantityNok: number; note?: string; occurredAt: string }) =>
    apiFetch<OasDeclarationDto>('/declarations/production', { method: 'POST', body }),
  createScrap: (body: { clientEventId: string; postSessionId?: string; postId: string; productionOrderId?: string; productId?: string; quantityNok: number; scrapCauseId?: string; note?: string; occurredAt: string }) =>
    apiFetch<OasDeclarationDto>('/declarations/scrap', { method: 'POST', body }),
  /** `id` must be the declaration's real server id (`OasDeclarationDto.id`), never its `clientEventId` — the server looks it up by primary key. */
  correct: (id: string, body: { clientEventId: string; quantityOk?: number; quantityNok?: number; reason: string }) =>
    apiFetch<OasDeclarationDto>(`/declarations/${id}/correct`, { method: 'PUT', body }),
  list: (params?: { postId?: string; sessionId?: string; from?: string; to?: string }) =>
    apiFetch<OasDeclarationDto[]>('/declarations', { query: params }),
  declareStop: (body: { clientEventId: string; eventType: string; postId: string; causeId?: string; postSessionId?: string; note?: string; declaredAt: string }) =>
    apiFetch<{ id: string; status: string }>('/declarations/stop', { method: 'POST', body }),
  closeStop: (clientEventId: string, closureType: string, note?: string) =>
    apiFetch<{ id: string; status: string }>(`/declarations/stop/${clientEventId}/close`, { method: 'POST', body: { closureType, note } }),
};

/* ------------------------------------------------------------------ */
/* Changeovers (spec §6.1 "Changement de série")                        */
/* ------------------------------------------------------------------ */

export interface OasChangeoverStepDto { id: string; label: string; done: boolean }
export interface OasChangeoverDto {
  id: string; postId: string; fromProductId: string | null; toProductId: string;
  startedAt: string; endedAt: string | null; steps: OasChangeoverStepDto[];
}

export const changeoversApi = {
  createOrUpdate: (body: { clientEventId: string; postId: string; fromProductId?: string; toProductId: string; productionOrderId?: string; targetMin?: number; steps?: OasChangeoverStepDto[] }) =>
    apiFetch<OasChangeoverDto>('/changeovers', { method: 'POST', body }),
  finish: (id: string) => apiFetch<OasChangeoverDto>(`/changeovers/${id}/finish`, { method: 'PUT' }),
  list: (postId?: string, status?: string) => apiFetch<OasChangeoverDto[]>('/changeovers', { query: { postId, status } }),
};

/* ------------------------------------------------------------------ */
/* Quality checks (spec §6.1 "Contrôle qualité")                        */
/* ------------------------------------------------------------------ */

export interface OasQualityCheckDto {
  id: string; postId: string; checkType: string; result: string;
  quantityChecked: number; quantityRejected: number; occurredAt: string;
}
export interface OasQualityCheckTemplateDto { id: string; code: string; name: string; checkType: string; isActive: boolean }
export interface OasQualityCheckTemplateItemDto {
  id: string; label: string; valueType: string; minValue: number | null; maxValue: number | null; isRequired: boolean; sortOrder: number;
}

export const qualityChecksApi = {
  create: (body: {
    clientEventId: string; postId: string; productionOrderId?: string; productId?: string; changeoverId?: string;
    templateId?: string; checkType: string; result: string; quantityChecked?: number; quantityRejected?: number;
    causeId?: string; note?: string; occurredAt: string;
  }) => apiFetch<OasQualityCheckDto>('/quality-checks', { method: 'POST', body }),
  list: (postId?: string) => apiFetch<OasQualityCheckDto[]>('/quality-checks', { query: { postId } }),
};

export const qualityTemplatesApi = {
  list: () => apiFetch<OasQualityCheckTemplateDto[]>('/quality-check-templates'),
  create: (body: { code: string; name: string; checkType: string }) =>
    apiFetch<OasQualityCheckTemplateDto>('/quality-check-templates', { method: 'POST', body }),
  update: (id: string, body: { code: string; name: string; checkType: string }) =>
    apiFetch<{ success: boolean }>(`/quality-check-templates/${id}`, { method: 'PUT', body }),
  delete: (id: string) => apiFetch<{ success: boolean }>(`/quality-check-templates/${id}`, { method: 'DELETE' }),
  items: (id: string) => apiFetch<OasQualityCheckTemplateItemDto[]>(`/quality-check-templates/${id}/items`),
  putItems: (id: string, items: { label: string; valueType?: string; minValue?: number; maxValue?: number; isRequired?: boolean; sortOrder?: number }[]) =>
    apiFetch<{ success: boolean }>(`/quality-check-templates/${id}/items`, { method: 'PUT', body: items }),
};
