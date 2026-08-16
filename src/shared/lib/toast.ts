/**
 * Minimal error/status toast — a reactive singleton in the same style as
 * `session.ts`'s `shiftWarning`/`openConflict`. Exists because most web
 * console CRUD actions (`refStore.ts`, `hierarchyStore.ts`, etc.) are fired
 * as `void someAction()` from a button's `onClick`: without this, a failed
 * write (a duplicate code, a 409, a dropped connection) left the UI showing
 * nothing at all — the form just quietly reset as if it had worked. Stores
 * push an i18n *key* (not translated text, so this file stays decoupled
 * from the `t()` hook) — `ToastStack` resolves it at render time.
 */

import { useSyncExternalStore } from 'react';

export interface ToastEntry {
  id: string;
  key: string;
  tone: 'error' | 'success';
}

let toasts: ToastEntry[] = [];
const listeners = new Set<() => void>();

function commit() {
  listeners.forEach((l) => l());
}

export function pushToast(key: string, tone: ToastEntry['tone'] = 'error') {
  const id = crypto.randomUUID();
  toasts = [...toasts, { id, key, tone }];
  commit();
  if (typeof window !== 'undefined') {
    window.setTimeout(() => dismissToast(id), 6000);
  }
}

export function dismissToast(id: string) {
  toasts = toasts.filter((t) => t.id !== id);
  commit();
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

export function useToasts(): ToastEntry[] {
  return useSyncExternalStore(subscribe, () => toasts, () => []);
}
