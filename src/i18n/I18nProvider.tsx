import { useCallback, useEffect, useMemo, type ReactNode } from 'react';
import { DICTS, LANGS } from './translations';
import { I18nContext, type I18nCtx } from './context';
import { setLang as setGlobalLang, useLang } from './langStore';

export { useI18n, useT } from './context';

export function I18nProvider({ children }: { children: ReactNode }) {
  const lang = useLang();

  const dir = useMemo(() => LANGS.find((l) => l.code === lang)?.dir ?? 'ltr', [lang]);

  useEffect(() => {
    if (typeof document === 'undefined') return;
    document.documentElement.lang = lang;
    document.documentElement.dir = dir;
  }, [lang, dir]);

  const t = useCallback(
    (key: string, vars?: Record<string, string | number>) => {
      const raw = DICTS[lang]?.[key] ?? DICTS.fr[key] ?? key;
      if (!vars) return raw;
      return raw.replace(/\{(\w+)\}/g, (_, k) => (vars[k] !== undefined ? String(vars[k]) : `{${k}}`));
    },
    [lang],
  );

  const value = useMemo<I18nCtx>(() => ({ lang, dir, setLang: setGlobalLang, t }), [lang, dir, t]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}
