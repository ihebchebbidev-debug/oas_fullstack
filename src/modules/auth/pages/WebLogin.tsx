import { useNavigate } from 'react-router-dom';

import { Logo } from '@/brand/Logo';
import { ParticleField } from '@/components/effects/ParticleField';
import { useI18n } from '@/i18n/I18nProvider';

import { LangSwitcher } from '@/i18n/LangSwitcher';
import { ThemeToggle } from '@/theme/ThemeProvider';

import { LoginForm } from '../components/LoginForm';

export default function WebLogin() {
  const navigate = useNavigate();
  const { t, dir } = useI18n();

  return (
    <div
      dir={dir}
      className="relative flex min-h-screen w-full flex-col overflow-hidden bg-background text-foreground"
    >
      <ParticleField />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_35%,hsl(var(--primary)/0.07),transparent_60%)]"
      />

      <header className="relative z-10 flex items-center justify-between px-6 py-4 lg:px-10">
        <Logo size="sm" />
        <div className="flex items-center gap-2">
          <ThemeToggle />
          <LangSwitcher variant="compact" />
        </div>
      </header>

      <main className="relative z-10 flex flex-1 flex-col items-center justify-center gap-8 px-6 pb-16">
        <Logo size="lg" className="flex-col gap-3 text-center [&>span:last-child]:items-center" />
        <LoginForm onSuccess={() => navigate('/web/dashboard')} />
        <button type="button" onClick={() => navigate('/')}
          className="text-xs font-medium text-muted-foreground underline-offset-2 hover:underline">
          {t('entry.switch')}
        </button>
      </main>
    </div>
  );
}

