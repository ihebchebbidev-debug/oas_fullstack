import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowRight, Download, MonitorSmartphone, Smartphone } from 'lucide-react';

import { Logo } from '@/brand/Logo';
import { useI18n } from '@/i18n/I18nProvider';
import { LangSwitcher } from '@/i18n/LangSwitcher';
import { ThemeToggle } from '@/theme/ThemeProvider';
import DownloadApkDialog from '@/shared/components/DownloadApkDialog';

/** Entry screen — pick the operator mobile app or the supervision console. */
export default function WorkspaceChooser() {
  const navigate = useNavigate();
  const { t, dir } = useI18n();
  const [apkOpen, setApkOpen] = useState(false);

  const cards = [
    {
      to: '/mobile/login',
      icon: Smartphone,
      title: t('entry.mobile.title'),
      desc: t('entry.mobile.desc'),
      cta: t('entry.mobile.cta'),
    },
    {
      to: '/web/login',
      icon: MonitorSmartphone,
      title: t('entry.desktop.title'),
      desc: t('entry.desktop.desc'),
      cta: t('entry.desktop.cta'),
    },
  ];

  return (
    <div dir={dir} className="flex min-h-screen flex-col bg-background text-foreground">
      <header className="flex items-center justify-between px-6 py-4">
        <Logo size="sm" />
        <div className="flex items-center gap-2">
          <ThemeToggle />
          <LangSwitcher variant="compact" />
        </div>
      </header>

      <main className="mx-auto flex w-full max-w-4xl flex-1 flex-col justify-center gap-8 px-6 pb-16">
        <div className="text-center">
          <h1 className="text-3xl font-bold tracking-tight">{t('entry.title')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('entry.subtitle')}</p>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          {cards.map(({ to, icon: Icon, title, desc, cta }) => (
            <button
              key={to}
              type="button"
              onClick={() => navigate(to)}
              className="group flex flex-col items-start gap-3 rounded-2xl border border-border bg-card p-6 text-start shadow-soft transition-all hover:border-foreground/40 hover:shadow-lg active:scale-[0.99]"
            >
              <span className="flex h-12 w-12 items-center justify-center rounded-xl border border-border bg-muted">
                <Icon className="h-6 w-6" aria-hidden />
              </span>
              <span className="text-lg font-semibold">{title}</span>
              <span className="text-sm text-muted-foreground">{desc}</span>
              <span className="mt-2 inline-flex items-center gap-1.5 text-sm font-medium">
                {cta}
                <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5 rtl:rotate-180" />
              </span>
            </button>
          ))}
        </div>

        <div className="flex flex-col items-center gap-2">
          <button
            type="button"
            onClick={() => setApkOpen(true)}
            className="inline-flex w-full items-center justify-center gap-2 rounded-xl border border-border bg-card px-5 py-3 text-sm font-semibold shadow-soft transition-colors hover:bg-accent sm:w-auto"
          >
            <Download className="h-4 w-4" aria-hidden />
            {t('apk.download.cta')}
          </button>
          <p className="text-xs text-muted-foreground">{t('apk.download.hint')}</p>
        </div>
      </main>

      <DownloadApkDialog open={apkOpen} onClose={() => setApkOpen(false)} />
    </div>
  );
}
