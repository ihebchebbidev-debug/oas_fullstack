import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ScanLine, Fingerprint } from 'lucide-react';

import { Logo } from '@/brand/Logo';
import { cn } from '@/lib/utils';

import { useI18n } from '@/i18n/I18nProvider';
import { LangSwitcher } from '@/i18n/LangSwitcher';
import { ThemeToggle } from '@/theme/ThemeProvider';


import { ScanPanel } from '../components/ScanPanel';
import { PinPanel } from '../components/PinPanel';

export default function MobileLogin() {
  const navigate = useNavigate();
  const { t, dir } = useI18n();
  const [mode, setMode] = useState<'scan' | 'pin'>('scan');

  const enter = () => navigate('/mobile/scan');

  const modes = [
    { k: 'scan' as const, label: t('mobile.login.tabScan'), icon: ScanLine },
    { k: 'pin' as const, label: t('mobile.login.tabPin'), icon: Fingerprint },
  ];

  return (
    <div dir={dir} className="oas-mobile-root flex h-[100dvh] justify-center overflow-hidden bg-muted/40">
      <div className="relative flex h-full w-full max-w-[430px] flex-col overflow-y-auto bg-background text-foreground shadow-2xl">

        <header className="flex items-center justify-between px-5 pt-[max(1rem,env(safe-area-inset-top))] pb-3">
          <Logo size="sm" />

          <div className="flex items-center gap-2">
            <ThemeToggle />
            <LangSwitcher variant="compact" />
          </div>
        </header>


        <section className="px-5 pt-4">
          <h1 className="text-[1.75rem] font-bold leading-tight">{t('mobile.login.welcome')}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t('mobile.login.subtitle')}</p>
        </section>

        <div className="mx-5 mt-5 flex rounded-xl border border-border bg-muted p-1">
          {modes.map(({ k, label, icon: Icon }) => (
            <button key={k} type="button" onClick={() => setMode(k)}
              className={cn(
                'flex min-h-[44px] flex-1 items-center justify-center gap-2 rounded-lg text-sm font-medium transition-colors',
                mode === k ? 'bg-background text-foreground shadow-sm' : 'text-muted-foreground',
              )}
            >
              <Icon className="h-4 w-4" />
              {label}
            </button>
          ))}
        </div>

        <main className="flex-1 px-5 py-5">
          {mode === 'scan' ? (
            <ScanPanel onSuccess={enter} onFallback={() => setMode('pin')} />
          ) : (
            <PinPanel onSuccess={enter} />
          )}
        </main>

        <footer className="px-5 pb-[max(1rem,env(safe-area-inset-bottom))] pt-2 text-center text-xs text-muted-foreground">
          <button type="button" onClick={() => navigate('/')}
            className="mb-2 block w-full min-h-[40px] text-xs font-medium underline-offset-2 hover:underline">
            {t('entry.switch')}
          </button>
          {t('common.privacyFooter')}
        </footer>
      </div>
    </div>
  );
}
