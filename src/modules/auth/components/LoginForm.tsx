import { useState } from 'react';
import { Eye, EyeOff, Loader2 } from 'lucide-react';
import { useT, useI18n } from '@/i18n/I18nProvider';
import { cn } from '@/lib/utils';
import { signInConsole } from '@/oas/authStore';

interface Props {
  onSuccess: () => void;
}

export function LoginForm({ onSuccess }: Props) {
  const t = useT();
  const { dir } = useI18n();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const canSubmit = email.trim().length >= 3 && password.length >= 8 && !loading;

  const submit = async () => {
    setLoading(true);
    const result = await signInConsole(email.trim(), password);
    setLoading(false);
    if (typeof result === 'string') {
      setError(t(`web.login.error.${result}`));
      return;
    }
    setError(null);
    onSuccess();
  };


  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (canSubmit) void submit();
      }}
      className="w-full max-w-sm space-y-4 rounded-xl border border-border bg-card p-6 shadow-[var(--shadow-medium)]"
    >
      <label className="block">
        <span className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">
          {t('web.login.email')}
        </span>
        <input
          type="text"
          autoComplete="username"
          value={email}
          placeholder="emp-102@oas.io"
          onChange={(e) => { setEmail(e.target.value); setError(null); }}
          className="mt-1 h-11 w-full rounded-md border border-input bg-card px-3 text-sm text-foreground focus:border-ring focus:outline-none"
        />
      </label>

      <label className="block">
        <span className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">
          {t('web.login.password')}
        </span>
        <div className="relative mt-1">
          <input
            type={showPassword ? 'text' : 'password'}
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className={cn(
              'h-11 w-full rounded-md border border-input bg-card px-3 text-sm text-foreground focus:border-ring focus:outline-none',
              dir === 'rtl' ? 'pl-11' : 'pr-11',
            )}
          />
          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            aria-label={t(showPassword ? 'web.login.hidePassword' : 'web.login.showPassword')}
            aria-pressed={showPassword}
            className={cn(
              'absolute top-1/2 grid h-8 w-8 -translate-y-1/2 place-items-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              dir === 'rtl' ? 'left-1.5' : 'right-1.5',
            )}
          >
            {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>
        </div>

      </label>

      {error && (
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={!canSubmit}
        className={cn(
          'h-11 w-full rounded-md text-sm font-semibold transition-colors',
          canSubmit
            ? 'bg-primary text-primary-foreground hover:bg-primary/90'
            : 'cursor-not-allowed bg-muted text-muted-foreground',
        )}
      >
        {loading ? <Loader2 className="mx-auto h-4 w-4 animate-spin" /> : t('web.login.signIn')}
      </button>

      <p className="text-center text-xs text-muted-foreground">{t('web.login.accountHelp')}</p>
    </form>
  );
}
