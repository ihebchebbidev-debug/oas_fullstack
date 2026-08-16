import { apiFetch } from './client';

export interface OasKpiDailyDto {
  openingMin: number; availability: number; quality: number; performance: number | null;
  cadenceKnown: boolean; oee: number | null; stopMinutes: number; stopsCount: number; mttr: number;
  producedOk: number; producedNok: number;
}
export interface OasParetoEntryDto { causeId: string | null; lostMinutes: number }
export interface OasTrendPointDto { date: string; oee: number | null }
export interface OasLineComparisonEntryDto { lineId: string; trs: number; scrap: number; stopsCount: number; mtbfMin: number }
export interface OasSlaSummaryEntryDto { eventType: string; total: number; onTime: number; onTimeRatio: number }
export interface OasCadenceGapEntryDto { postId: string; actualQty: number; theoreticalQty: number; gapPercent: number }
export interface OasAndonMessageDto { lineId: string | null; message: string }

export const kpiApi = {
  daily: (params?: { postId?: string; lineId?: string; from?: string; to?: string }) =>
    apiFetch<OasKpiDailyDto>('/kpi/daily', { query: params }),
  pareto: (params?: { postId?: string; from?: string; to?: string }) =>
    apiFetch<OasParetoEntryDto[]>('/kpi/pareto', { query: params }),
  trend: (params?: { postId?: string; lineId?: string; from?: string; to?: string }) =>
    apiFetch<OasTrendPointDto[]>('/kpi/trend', { query: params }),
  lineComparison: (params?: { from?: string; to?: string }) =>
    apiFetch<OasLineComparisonEntryDto[]>('/kpi/line-comparison', { query: params }),
  slaSummary: (params?: { from?: string; to?: string }) =>
    apiFetch<OasSlaSummaryEntryDto[]>('/kpi/sla-summary', { query: params }),
  cadenceGap: (params?: { from?: string; to?: string }) =>
    apiFetch<OasCadenceGapEntryDto[]>('/kpi/cadence-gap', { query: params }),
};

export const andonMessageApi = {
  get: (lineId?: string) => apiFetch<OasAndonMessageDto>('/andon/message', { query: { lineId } }),
  set: (lineId: string | undefined, message: string) => apiFetch<OasAndonMessageDto>('/andon/message', { method: 'PUT', body: { lineId, message } }),
};
