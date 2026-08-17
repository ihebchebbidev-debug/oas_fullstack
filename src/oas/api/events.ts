import { apiFetch, getSession, OAS_API_BASE, OAS_TENANT_SLUG } from './client';

/* ------------------------------------------------------------------ */
/* Events (spec §6.1 Événements) — the state machine                   */
/* ------------------------------------------------------------------ */

export interface OasEventDto {
  id: string; eventType: string; status: string; postId: string; lineId: string | null;
  causeId: string | null; criticality: string; declaredBy: string; declaredAt: string;
  assigneeId: string | null; etaMinutes: number | null; slaMinutes: number; slaDueAt: string | null;
  slaBreached: boolean; escalationLevel: string; closedAt: string | null;
}
export interface OasEventTransitionDto { fromStatus: string | null; toStatus: string; actorId: string | null; occurredAt: string }

export const eventsApi = {
  create: (body: {
    clientEventId: string; eventType: string; postId: string; causeId?: string; criticality?: string;
    postSessionId?: string; productionOrderId?: string; productId?: string; equipmentId?: string; note?: string; declaredAt: string;
  }) => apiFetch<OasEventDto>('/events', { method: 'POST', body }),
  list: (params?: { kind?: string; stage?: string; q?: string; lineId?: string; from?: string; to?: string }) =>
    apiFetch<OasEventDto[]>('/events', { query: params }),
  getOne: (id: string) => apiFetch<OasEventDto>(`/events/${id}`),
  transitions: (id: string) => apiFetch<OasEventTransitionDto[]>(`/events/${id}/transitions`),
  take: (id: string) => apiFetch<OasEventDto>(`/events/${id}/take`, { method: 'POST' }),
  setEta: (id: string, etaMinutes: number) => apiFetch<OasEventDto>(`/events/${id}/eta`, { method: 'PUT', body: { etaMinutes } }),
  arrive: (id: string) => apiFetch<OasEventDto>(`/events/${id}/arrive`, { method: 'POST' }),
  advance: (id: string) => apiFetch<OasEventDto>(`/events/${id}/advance`, { method: 'POST' }),
  ack: (id: string) => apiFetch<OasEventDto>(`/events/${id}/ack`, { method: 'POST' }),
  escalate: (id: string) => apiFetch<OasEventDto>(`/events/${id}/escalate`, { method: 'POST' }),
  requalify: (id: string, eventType: string) => apiFetch<OasEventDto>(`/events/${id}/requalify`, { method: 'POST', body: { eventType } }),
  decline: (id: string) => apiFetch<OasEventDto>(`/events/${id}/decline`, { method: 'POST' }),
  close: (id: string, closureType: string, note?: string) => apiFetch<OasEventDto>(`/events/${id}/close`, { method: 'POST', body: { closureType, note } }),
};

/* ------------------------------------------------------------------ */
/* SLA rules + escalations + responder availability                    */
/* ------------------------------------------------------------------ */

export interface OasEscalationDto { id: string; eventId: string; level: string; triggeredAt: string; reason: string | null; acknowledgedAt: string | null }
export interface OasResponderAvailabilityDto { profileId: string; busy: boolean; reason: string | null }
export interface OasSlaRuleDto {
  id: string; eventType: string; criticality: string | null; lineId: string | null; targetMin: number; priority: number; isActive: boolean;
}

export const slaApi = {
  rules: () => apiFetch<OasSlaRuleDto[]>('/sla/rules'),
  escalations: (eventId?: string) => apiFetch<OasEscalationDto[]>('/sla/escalations', { query: { eventId } }),
  ackEscalation: (id: string) => apiFetch<{ success: boolean }>(`/sla/escalations/${id}/ack`, { method: 'POST' }),
  availability: () => apiFetch<OasResponderAvailabilityDto[]>('/responder-availability'),
  setAvailability: (profileId: string, busy: boolean, reason?: string) =>
    apiFetch<{ success: boolean }>(`/responder-availability/${profileId}`, { method: 'PUT', body: { busy, reason } }),
};

/* ------------------------------------------------------------------ */
/* Interventions — thin projection over events (spec §13)              */
/* ------------------------------------------------------------------ */

export interface OasInterventionDto { id: string; eventId: string; assigneeId: string | null; status: string }

export const interventionsApi = {
  list: () => apiFetch<OasInterventionDto[]>('/interventions'),
  inbox: () => apiFetch<OasInterventionDto[]>('/interventions/inbox'),
  create: (eventId: string) => apiFetch<OasInterventionDto>('/interventions', { method: 'POST', body: { eventId } }),
  assign: (id: string) => apiFetch<{ success: boolean }>(`/interventions/${id}/assign`, { method: 'POST' }),
  start: (id: string) => apiFetch<{ success: boolean }>(`/interventions/${id}/start`, { method: 'POST' }),
  close: (id: string) => apiFetch<{ success: boolean }>(`/interventions/${id}/close`, { method: 'POST' }),
};

/* ------------------------------------------------------------------ */
/* SSE — the sole realtime channel (spec §6.3)                         */
/* ------------------------------------------------------------------ */

export type OasStreamHandler = (type: string, data: unknown) => void;

/**
 * `EventSource` cannot set ANY custom header — not Authorization, not
 * X-Tenant — so both travel as query params: `?access_token=` (spec §6.3
 * "JWT en query ou header", read back out by the OAS JWT bearer scheme in
 * `OasModuleRegistration.cs`) and `&tenant=` (read back out by
 * `OasTenantMiddleware` for this one path — every other route still
 * requires the X-Tenant header). Without the second one this 400s before
 * auth ever runs, confirmed live: every real-time-consuming screen (shop
 * floor map, alerts, andon) was silently failing to connect.
 * Heartbeats (`: ping`) arrive as comment lines and are ignored by
 * `EventSource` automatically — nothing to filter here.
 */
export function connectOasStream(onMessage: OasStreamHandler): () => void {
  let source: EventSource | null = null;
  let retryTimer: ReturnType<typeof setTimeout> | null = null;
  let closed = false;

  function open() {
    const token = getSession()?.accessToken;
    if (!token || closed) return;
    source = new EventSource(`${OAS_API_BASE}/stream?access_token=${encodeURIComponent(token)}&tenant=${encodeURIComponent(OAS_TENANT_SLUG)}`);
    source.onmessage = (ev) => {
      try {
        const envelope = JSON.parse(ev.data) as { type: string; data: unknown };
        onMessage(envelope.type, envelope.data);
      } catch {
        /* malformed frame — ignore */
      }
    };
    // A shift is up to 8h; the access token embedded in the URL above
    // expires long before that, and a dropped/rejected connection's native
    // EventSource auto-retry keeps replaying that SAME now-stale token
    // forever — silently killing real-time updates for the rest of the
    // shift. Rebuild from scratch with whatever token is current instead.
    source.onerror = () => {
      if (closed) return;
      source?.close();
      retryTimer = setTimeout(open, 5000);
    };
  }
  open();

  return () => {
    closed = true;
    if (retryTimer) clearTimeout(retryTimer);
    source?.close();
  };
}
