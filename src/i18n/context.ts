import { createContext, useContext } from 'react';
import type { Lang } from './translations';

export interface I18nCtx {
  lang: Lang;
  dir: 'ltr' | 'rtl';
  setLang: (l: Lang) => void;
  t: (key: string, vars?: Record<string, string | number>) => string;
}

export const I18nContext = createContext<I18nCtx | null>(null);

export function useI18n(): I18nCtx {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useI18n must be used inside <I18nProvider>');
  return ctx;
}

/** Convenience: read `t` only. */
export function useT() {
  return useI18n().t;
}
