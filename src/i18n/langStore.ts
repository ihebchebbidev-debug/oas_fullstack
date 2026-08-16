/**
 * Current UI language — a module-level singleton, same pattern as every
 * other store in this app (authStore, refStore, hierarchyStore, ...).
 * `I18nProvider` used to keep this in local `useState`, which made it
 * unreachable from non-component code (`eventStore.ts`, `liveState.ts`) —
 * those map server DTOs to display objects (`causeKey` etc.) outside any
 * component, so they had no way to pick the right language and silently
 * always resolved bilingual fields (cause labels) to French. `I18nProvider`
 * now just mirrors this store via `useLang()`; nothing about its public
 * API (`useI18n()`, `useT()`) changed.
 */

import { useSyncExternalStore } from 'react';
import { DICTS, type Lang } from './translations';

const STORAGE_KEY = 'oas.lang';

function detectInitial(): Lang {
  if (typeof window === 'undefined') return 'fr';
  const stored = window.localStorage.getItem(STORAGE_KEY) as Lang | null;
  if (stored && DICTS[stored]) return stored;
  const nav = window.navigator?.language?.slice(0, 2);
  if (nav === 'ar' || nav === 'en' || nav === 'fr') return nav;
  return 'fr';
}

let lang: Lang = detectInitial();
const listeners = new Set<() => void>();

/** Plain (non-hook) snapshot for code outside a component — mapping functions, stores. */
export function getLang(): Lang {
  return lang;
}

export function setLang(next: Lang) {
  if (next === lang) return;
  lang = next;
  try { window.localStorage.setItem(STORAGE_KEY, next); } catch { /* private mode — keep the in-memory copy */ }
  listeners.forEach((l) => l());
}

/** Non-component subscription — lets a store re-derive language-dependent display fields (e.g. cause labels) when the UI language changes, not just when new data arrives. */
export function subscribeLang(l: () => void): () => void {
  listeners.add(l);
  return () => listeners.delete(l);
}

export function useLang(): Lang {
  return useSyncExternalStore(subscribeLang, () => lang, () => lang);
}
