import { AlertTriangle, X } from 'lucide-react';
import { dismissToast, useToasts } from '@/shared/lib/toast';
import { useT } from '@/i18n/I18nProvider';

/** Mounted once per shell (web + mobile) — see `src/shared/lib/toast.ts`. */
export function ToastStack() {
  const t = useT();
  const toasts = useToasts();
  if (!toasts.length) return null;

  return (
    <div className="pointer-events-none fixed inset-x-0 bottom-4 z-[100] flex flex-col items-center gap-2 px-4">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          role="alert"
          className="pointer-events-auto flex w-full max-w-sm items-start gap-2 rounded-xl border border-state-technical/40 bg-card p-3 text-sm shadow-medium"
        >
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-state-technical" />
          <p className="flex-1 text-foreground">{t(toast.key)}</p>
          <button
            type="button"
            aria-label={t('common.close')}
            onClick={() => dismissToast(toast.id)}
            className="shrink-0 rounded p-0.5 text-muted-foreground hover:bg-accent"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      ))}
    </div>
  );
}
