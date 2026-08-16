import { useEffect } from 'react';
import { Download, X, ShieldAlert } from 'lucide-react';

import { APK_DOWNLOAD } from '@/config/download.config';
import { useI18n } from '@/i18n/I18nProvider';
import { Button, buttonVariants } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import playProtect from '@/assets/play-protect-install.png';

/** Explains the Play Protect warning before sending the user to the APK link. */
export default function DownloadApkDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t, dir } = useI18n();

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  const steps = [1, 2, 3] as const;

  return (
    <div dir={dir} className="fixed inset-0 z-[100] flex items-end justify-center sm:items-center">
      <button
        type="button"
        aria-label={t('common.close')}
        onClick={onClose}
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={t('apk.modal.title')}
        className="relative z-10 flex max-h-[92dvh] w-full max-w-2xl flex-col overflow-hidden rounded-t-2xl border border-border bg-card shadow-lg sm:max-h-[88dvh] sm:rounded-2xl"
      >
        <div className="flex shrink-0 items-start gap-3 border-b border-border p-4 sm:p-5">
          <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-muted text-foreground">
            <ShieldAlert className="h-5 w-5" aria-hidden />
          </span>
          <div className="min-w-0">
            <h2 className="text-base font-semibold sm:text-lg">{t('apk.modal.title')}</h2>
            <p className="mt-1 text-sm text-muted-foreground">{t('apk.modal.subtitle')}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            className="ms-auto grid h-8 w-8 shrink-0 place-items-center rounded-md border border-border text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
          >
            <X className="h-4 w-4" aria-hidden />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-5">
          <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] sm:items-start">
            <figure className="mx-auto w-full max-w-[160px] sm:max-w-[240px] overflow-hidden rounded-xl border border-border bg-muted">
              <img
                src={playProtect}
                alt={t('apk.modal.imageAlt')}
                loading="lazy"
                className="h-auto w-full"
              />
              <figcaption className="p-2 text-center text-xs text-muted-foreground">
                {t('apk.modal.imageAlt')}
              </figcaption>
            </figure>

            <ol className="space-y-3">
              {steps.map((n) => (
                <li key={n} className="flex gap-3">
                  <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-foreground text-xs font-bold text-background">
                    {n}
                  </span>
                  <p className="text-sm leading-relaxed">{t(`apk.modal.step${n}`)}</p>
                </li>
              ))}
            </ol>
          </div>

          <p className="mt-4 rounded-lg border border-border bg-muted/50 p-3 text-xs leading-relaxed text-muted-foreground">
            {t('apk.modal.note')}
          </p>
        </div>

        <div className="flex shrink-0 flex-col gap-2 border-t border-border p-4 sm:flex-row sm:justify-end sm:p-5">
          <Button variant="outline" onClick={onClose} className="w-full sm:w-auto">
            {t('common.cancel')}
          </Button>
          <a
            href={APK_DOWNLOAD.url}
            target="_blank"
            rel="noopener noreferrer"
            className={cn(buttonVariants({ size: 'lg' }), 'w-full sm:w-auto')}
          >
            <Download className="me-1.5 h-4 w-4" aria-hidden />
            {t('apk.modal.confirm')}
          </a>
        </div>
      </div>
    </div>
  );
}
