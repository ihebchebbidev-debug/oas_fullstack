/**
 * BL-046 — audit journal, now read-only (spec §6.1 "Audit" — no POST: the
 * `oas_audit_row` Postgres trigger logs every mutation automatically,
 * attached to the tables directly). `logAudit()` is gone — every store that
 * used to call it just relies on the trigger firing on its own writes.
 */

import { useSyncExternalStore } from 'react';
import { auditApi, type OasAuditLogDto } from './api/audit';
import { getRefState } from './refStore';
import { pushToast } from '@/shared/lib/toast';

export interface AuditEntry {
  id: string;
  at: string;
  actor: string;
  action: string;
  entity: string;
  detail: string;
}

function toEntry(dto: OasAuditLogDto): AuditEntry {
  const actorName = dto.actorId ? getRefState().users.find((u) => u.id === dto.actorId)?.name : undefined;
  return {
    id: dto.id,
    at: dto.occurredAt,
    actor: actorName ?? dto.actorId ?? 'system',
    action: dto.action,
    entity: dto.entityId ? `${dto.entityTable} ${dto.entityId.slice(0, 8)}` : dto.entityTable,
    detail: dto.reason ?? '',
  };
}

let entries: AuditEntry[] = [];
const listeners = new Set<() => void>();

function commit(next: AuditEntry[]) {
  entries = next;
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

let loaded = false;
function ensureAuditLoaded() {
  if (loaded) return;
  loaded = true;
  void auditApi.list().then((rows) => commit(rows.map(toEntry))).catch(() => { commit([]); pushToast('common.actionFailed'); });
}

export function useAuditLog(): AuditEntry[] {
  ensureAuditLoaded();
  return useSyncExternalStore(subscribe, () => entries, () => entries);
}
