import { apiFetch } from './client';

/* ------------------------------------------------------------------ */
/* Operators (oas_users) — spec §6.1 "Opérateurs"                      */
/* ------------------------------------------------------------------ */

export interface OasOperatorDto {
  id: string; email: string; employeeCode: string | null; displayName: string | null; phone: string | null;
  role: string; workspace: string; isActive: boolean;
  scopeSiteId: string | null; scopeZoneId: string | null; scopeLineId: string | null; interim: boolean;
}
export interface OasPinRegenerateResponseDto { success: boolean; message?: string; pin?: string }

export const operatorsApi = {
  search: (q?: string, scopeLineId?: string) => apiFetch<OasOperatorDto[]>('/operators', { query: { q, scopeLineId } }),
  create: (body: { email: string; employeeCode?: string; displayName: string; phone?: string; role?: string; workspace?: string; interim?: boolean }) =>
    apiFetch<OasOperatorDto>('/operators', { method: 'POST', body }),
  setActive: (id: string, isActive: boolean) => apiFetch<{ success: boolean }>(`/operators/${id}/active`, { method: 'PUT', body: { isActive } }),
  setRole: (id: string, role: string) => apiFetch<{ success: boolean }>(`/operators/${id}/role`, { method: 'PUT', body: { role } }),
  setScope: (id: string, body: { scopeSiteId?: string | null; scopeZoneId?: string | null; scopeLineId?: string | null }) =>
    apiFetch<{ success: boolean }>(`/operators/${id}/scope`, { method: 'PUT', body }),
  regeneratePin: (id: string) => apiFetch<OasPinRegenerateResponseDto>(`/operators/${id}/regenerate-pin`, { method: 'POST' }),
};

/* ------------------------------------------------------------------ */
/* Equipments                                                          */
/* ------------------------------------------------------------------ */

export interface OasEquipmentDto {
  id: string; postId: string | null; code: string; name: string;
  serialNumber: string | null; manufacturer: string | null; commissionedAt: string | null; criticality: string;
}

export const equipmentsApi = {
  list: (postId?: string) => apiFetch<OasEquipmentDto[]>('/equipments', { query: { postId } }),
  create: (body: { postId?: string; code: string; name: string; serialNumber?: string; manufacturer?: string; criticality?: string }) =>
    apiFetch<OasEquipmentDto>('/equipments', { method: 'POST', body }),
  update: (id: string, body: { postId?: string; code: string; name: string; serialNumber?: string; manufacturer?: string; criticality?: string }) =>
    apiFetch<{ success: boolean }>(`/equipments/${id}`, { method: 'PUT', body }),
  remove: (id: string) => apiFetch<{ success: boolean }>(`/equipments/${id}`, { method: 'DELETE' }),
};

/* ------------------------------------------------------------------ */
/* Cadences (keyed by product + post, not by equipment)                 */
/* ------------------------------------------------------------------ */

export interface OasCadenceDto {
  id: string; productId: string; postId: string; rate: number; operatorsRequired: number; changeoverTargetMin: number | null;
}
export interface OasCadenceVersionDto { version: number; rate: number; since: string }

export const cadencesApi = {
  list: () => apiFetch<OasCadenceDto[]>('/cadences'),
  createVersion: (body: { productId: string; postId: string; rate: number; operatorsRequired?: number; changeoverTargetMin?: number }) =>
    apiFetch<OasCadenceDto>('/cadences', { method: 'POST', body }),
  update: (id: string, body: { productId: string; postId: string; rate: number; operatorsRequired?: number; changeoverTargetMin?: number }) =>
    apiFetch<{ success: boolean }>(`/cadences/${id}`, { method: 'PUT', body }),
  remove: (id: string) => apiFetch<{ success: boolean }>(`/cadences/${id}`, { method: 'DELETE' }),
  history: (id: string) => apiFetch<OasCadenceVersionDto[]>(`/cadences/${id}/history`),
};

/* ------------------------------------------------------------------ */
/* Products + production orders                                        */
/* ------------------------------------------------------------------ */

export interface OasProductDto { id: string; reference: string; name: string; customer: string | null; unit: string }

export const productsApi = {
  list: () => apiFetch<OasProductDto[]>('/products'),
  create: (body: { reference: string; name: string; customer?: string; unit?: string }) =>
    apiFetch<OasProductDto>('/products', { method: 'POST', body }),
  update: (id: string, body: { reference: string; name: string; customer?: string; unit?: string }) =>
    apiFetch<{ success: boolean }>(`/products/${id}`, { method: 'PUT', body }),
  remove: (id: string) => apiFetch<{ success: boolean }>(`/products/${id}`, { method: 'DELETE' }),
};

export interface OasProductionOrderDto {
  id: string; orderNumber: string; productId: string; lineId: string | null;
  quantityPlanned: number; quantityProduced: number; quantityScrapped: number;
  dueDate: string | null; status: string; priority: number;
}

export const productionOrdersApi = {
  list: () => apiFetch<OasProductionOrderDto[]>('/production-orders'),
  create: (body: { orderNumber: string; productId: string; lineId?: string; quantityPlanned?: number; dueDate?: string; priority?: number }) =>
    apiFetch<OasProductionOrderDto>('/production-orders', { method: 'POST', body }),
  setStatus: (id: string, status: string) => apiFetch<{ success: boolean }>(`/production-orders/${id}/status`, { method: 'PUT', body: { status } }),
};

/* ------------------------------------------------------------------ */
/* Causes (tree) + proposals                                           */
/* ------------------------------------------------------------------ */

export interface OasCauseDto {
  id: string; parentId: string | null; domain: string; code: string;
  labelFr: string; labelAr: string; eventType: string | null; defaultCriticality: string;
  isActive: boolean; children: OasCauseDto[];
}
export interface OasCauseUsageDto { causeId: string; count: number }
export interface OasCauseProposalDto {
  id: string; domain: string; labelFr: string; labelAr: string | null; proposedBy: string; status: string; createdAt: string;
}

export const causesApi = {
  tree: () => apiFetch<OasCauseDto[]>('/causes'),
  usage: () => apiFetch<OasCauseUsageDto[]>('/causes/usage'),
  create: (body: { parentId?: string; domain: string; code: string; labelFr: string; labelAr: string; eventType?: string; defaultCriticality?: string }) =>
    apiFetch<OasCauseDto>('/causes', { method: 'POST', body }),
  update: (id: string, body: { parentId?: string; domain: string; code: string; labelFr: string; labelAr: string; eventType?: string; defaultCriticality?: string }) =>
    apiFetch<{ success: boolean }>(`/causes/${id}`, { method: 'PUT', body }),
  setKind: (id: string, eventType: string) => apiFetch<{ success: boolean }>(`/causes/${id}/kind`, { method: 'PUT', body: { eventType } }),
  setCriticality: (id: string, criticality: string) => apiFetch<{ success: boolean }>(`/causes/${id}/criticality`, { method: 'PUT', body: { criticality } }),
  setActive: (id: string, isActive: boolean) => apiFetch<{ success: boolean }>(`/causes/${id}/active`, { method: 'PUT', body: { isActive } }),
  addChild: (id: string, body: { domain: string; code: string; labelFr: string; labelAr: string }) =>
    apiFetch<OasCauseDto>(`/causes/${id}/children`, { method: 'POST', body }),
  removeChild: (id: string, childId: string) => apiFetch<{ success: boolean }>(`/causes/${id}/children/${childId}`, { method: 'DELETE' }),
  remove: (id: string) => apiFetch<{ success: boolean }>(`/causes/${id}`, { method: 'DELETE' }),

  listProposals: () => apiFetch<OasCauseProposalDto[]>('/cause-proposals'),
  propose: (body: { domain: string; labelFr: string; labelAr?: string }) => apiFetch<OasCauseProposalDto>('/cause-proposals', { method: 'POST', body }),
  review: (id: string, body: { decision: 'accept' | 'reject'; code?: string; parentId?: string }) =>
    apiFetch<{ success: boolean }>(`/cause-proposals/${id}/review`, { method: 'POST', body }),
};

/* ------------------------------------------------------------------ */
/* Shifts + calendar + sign-offs                                       */
/* ------------------------------------------------------------------ */

export interface OasShiftTemplateDto {
  id: string; siteId: string; code: string; name: string; startTime: string; endTime: string;
  crossesMidnight: boolean; breakMinutes: number; isActive: boolean; openingMinutes: number;
}
export interface OasShiftSignoffDto { id: string; shiftTemplateId: string; workDate: string; signedBy: string; note: string | null; signedAt: string }

export const shiftsApi = {
  list: () => apiFetch<OasShiftTemplateDto[]>('/shifts'),
  create: (body: { siteId: string; code: string; name: string; startTime: string; endTime: string; breakMinutes?: number }) =>
    apiFetch<OasShiftTemplateDto>('/shifts', { method: 'POST', body }),
  update: (id: string, body: { siteId: string; code: string; name: string; startTime: string; endTime: string; breakMinutes?: number }) =>
    apiFetch<{ success: boolean }>(`/shifts/${id}`, { method: 'PUT', body }),
  remove: (id: string) => apiFetch<{ success: boolean }>(`/shifts/${id}`, { method: 'DELETE' }),

  signoffs: (shift?: string, date?: string) => apiFetch<OasShiftSignoffDto[]>('/shift-signoffs', { query: { shift, date } }),
  createSignoff: (body: { shiftTemplateId: string; workDate: string; note?: string }) =>
    apiFetch<OasShiftSignoffDto>('/shift-signoffs', { method: 'POST', body }),
};

/* ------------------------------------------------------------------ */
/* Imports                                                              */
/* ------------------------------------------------------------------ */

export interface OasImportDto { id: string; kind: string; status: string; rowsTotal: number; rowsOk: number; rowsError: number; createdAt: string }

export const importsApi = {
  list: () => apiFetch<OasImportDto[]>('/imports'),
  create: (kind: string, rows: Record<string, unknown>[]) => apiFetch<OasImportDto>('/imports', { method: 'POST', body: { kind, rows } }),
  commit: (id: string) => apiFetch<OasImportDto>(`/imports/${id}/commit`, { method: 'POST' }),
};

/* ------------------------------------------------------------------ */
/* Lookups (flat OAS lists)                                             */
/* ------------------------------------------------------------------ */

export interface OasLookupValueDto { id: string; type: string; code: string; label: string; color: string | null; sortOrder: number; isDefault: boolean }

export const lookupsApi = {
  list: (type: string) => apiFetch<OasLookupValueDto[]>(`/lookups/${type}`),
  create: (type: string, body: { code: string; label: string; color?: string; sortOrder?: number; isDefault?: boolean }) =>
    apiFetch<OasLookupValueDto>(`/lookups/${type}`, { method: 'POST', body }),
  update: (type: string, id: string, body: { code: string; label: string; color?: string; sortOrder?: number; isDefault?: boolean }) =>
    apiFetch<{ success: boolean }>(`/lookups/${type}/${id}`, { method: 'PUT', body }),
  remove: (type: string, id: string) => apiFetch<{ success: boolean }>(`/lookups/${type}/${id}`, { method: 'DELETE' }),
};
