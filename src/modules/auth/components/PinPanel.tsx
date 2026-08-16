import { useState } from 'react';
import { ArrowRight, Delete, Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useT } from '@/i18n/I18nProvider';
import { signInWithPin } from '@/oas/authStore';

interface Props { onSuccess: () => void }

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', 'del'] as const;

/** Matricules are stored as `EMP-102`; operators type the digits only. */
const toCode = (raw: string) => (/^\d+$/.test(raw) ? `EMP-${raw}` : raw);

export function PinPanel({ onSuccess }: Props) {
  const t = useT();
  const [matricule, setMatricule] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const canSubmit = matricule.length >= 3 && pin.length === 4 && !loading;

  const submit = async () => {
    if (!canSubmit) return;
    setLoading(true);
    const result = await signInWithPin(toCode(matricule), pin);
    setLoading(false);
    if (typeof result === 'string') {
      setError(t(result === 'network' ? 'mobile.login.network' : 'mobile.login.invalid'));
      setPin('');
      return;
    }
    setError(null);
    onSuccess();
  };

  return (
    <div className="space-y-4">
      <label className="block">
        <span className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">
          {t('mobile.login.matricule')}
        </span>
        <input inputMode="numeric" autoComplete="username" value={matricule}
          onChange={(e) => { setMatricule(e.target.value.replace(/\D/g, '').slice(0, 6)); setError(null); }}
          placeholder="102"
          className="mt-1 h-14 w-full rounded-xl border border-border bg-card px-4 font-mono text-lg tracking-widest text-foreground focus:border-foreground focus:outline-none" />
      </label>

      <div>
        <span className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">
          {t('mobile.login.pin')}
        </span>
        <div className="mt-1 flex gap-2">
          {[0, 1, 2, 3].map((i) => (
            <div key={i}
              className={cn(
                'flex h-14 flex-1 items-center justify-center rounded-xl border font-mono text-2xl font-semibold',
                pin.length > i
                  ? 'border-foreground bg-card text-foreground'
                  : 'border-border bg-muted/60 text-transparent',
              )}
            >
              {pin[i] ?? '·'}
            </div>
          ))}
        </div>
      </div>

      {error && (
        <p role="alert" className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      <div className="grid grid-cols-3 gap-2 pt-1">
        {KEYS.map((k, i) => {
          if (k === '') return <div key={i} />;
          if (k === 'del') {
            return (
              <button key={i} type="button" onClick={() => setPin((p) => p.slice(0, -1))}
                className="flex min-h-[56px] items-center justify-center rounded-xl border border-border bg-muted text-muted-foreground"
                aria-label={t('mobile.login.erase')}>
                <Delete className="h-5 w-5 rtl:rotate-180" />
              </button>
            );
          }
          return (
            <button key={i} type="button"
              onClick={() => { setPin((p) => (p.length < 4 ? p + k : p)); setError(null); }}
              className="flex min-h-[56px] items-center justify-center rounded-xl border border-border bg-card font-mono text-xl font-semibold active:scale-[0.98]">
              {k}
            </button>
          );
        })}
      </div>

      <button type="button" disabled={!canSubmit} onClick={() => void submit()}
        className={cn(
          'mt-2 flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl text-base font-semibold transition-colors',
          canSubmit ? 'bg-foreground text-background' : 'bg-muted text-muted-foreground',
        )}>
        {loading ? <Loader2 className="h-5 w-5 animate-spin" /> : (
          <>
            {t('mobile.login.enter')}
            <ArrowRight className="h-5 w-5 rtl:rotate-180" />
          </>
        )}
      </button>

      <p className="text-center text-xs text-muted-foreground">{t('mobile.login.pinHelp')}</p>
    </div>
  );
}
