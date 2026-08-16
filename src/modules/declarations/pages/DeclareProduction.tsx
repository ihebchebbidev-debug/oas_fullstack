import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { AlertTriangle, ArrowLeft, Check, Clock, Minus, Plus } from 'lucide-react';
import { useT } from '@/i18n/I18nProvider';
import { closeStop, computeMetrics, declareProduction, useSessionState } from '@/modules/auth/store/session';

function Stepper({
  label, value, onChange, tone, decreaseLabel, increaseLabel, step = 1,
}: {
  label: string; value: number; onChange: (v: number) => void; tone: string;
  decreaseLabel: string; increaseLabel: string; step?: number;
}) {
  return (
    <div className="rounded-xl border border-border bg-card p-3">
      <p className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">{label}</p>
      <div className="mt-2 flex items-center gap-3">
        <button type="button" aria-label={decreaseLabel} onClick={() => onChange(Math.max(0, value - step))}
          className="flex h-[72px] w-[72px] items-center justify-center rounded-xl border border-border bg-muted">
          <Minus className="h-6 w-6" />
        </button>
        <span className={`flex-1 text-center font-mono text-[2rem] font-bold ${tone}`}>{value}</span>
        <button type="button" aria-label={increaseLabel} onClick={() => onChange(value + step)}
          className="flex h-[72px] w-[72px] items-center justify-center rounded-xl border border-border bg-muted">
          <Plus className="h-6 w-6" />
        </button>
      </div>
    </div>
  );
}

const SCRAP_CAUSES = ['aspect', 'dimension', 'material', 'setting'] as const;

export default function DeclareProduction() {
  const navigate = useNavigate();
  const t = useT();
  const state = useSessionState();
  const metrics = computeMetrics(state);
  const [ok, setOk] = useState(metrics.suggestedQty);
  const [nok, setNok] = useState(0);
  const [scrapCause, setScrapCause] = useState<string | null>(null);
  const [confirming, setConfirming] = useState(false);
  // BL-025 — reading deferred during a stop, consolidated here after the restart.
  const consolidated = Boolean((useLocation().state as { consolidated?: boolean } | null)?.consolidated);

  // BL-030 — flag an entry that is far off the cadence-based suggestion.
  const expected = metrics.suggestedQty;
  const incoherent = expected > 0 && (ok > expected * 1.5 || ok < expected * 0.5);

  function submit() {
    if (metrics.openStop) return;
    if (incoherent && !confirming) { setConfirming(true); return; }
    declareProduction(ok, nok, scrapCause);
    navigate('/mobile/home');
  }

  useEffect(() => {
    if (!state.session) navigate('/mobile/scan', { replace: true });
  }, [state.session, navigate]);

  if (!state.session) return null;

  const good = t('mobile.prod.good');
  const scrap = t('mobile.prod.scrap');

  return (
    <div className="space-y-4">
      <button type="button" onClick={() => navigate(-1)}
        className="inline-flex min-h-[44px] items-center gap-1.5 text-sm text-muted-foreground">
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" /> {t('common.back')}
      </button>

      <header>
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.prod.title')}</h1>
        <p className="text-sm text-muted-foreground">
          {t('mobile.prod.prefilled', {
            post: state.session.postCode,
            qty: metrics.suggestedQty,
            min: metrics.sinceLastMin,
          })}
        </p>
      </header>

      {/* BL-023 — production cannot be declared while a stop is still open:
          the operator closes it here, in one tap, then keeps going. */}
      {metrics.openStop && (
        <div className="space-y-2 rounded-xl border border-state-technical/40 bg-state-technical/10 p-3 text-sm text-state-technical">
          <p className="flex items-start gap-2">
            <Clock className="mt-0.5 h-4 w-4 shrink-0" /> {t('mobile.prod.stopBlocking')}
          </p>
          <button
            type="button"
            onClick={() => closeStop(metrics.openStop!.clientEventId)}
            className="min-h-[56px] w-full rounded-xl bg-state-technical text-sm font-semibold text-background"
          >
            {t('mobile.prod.closeStopAndContinue')}
          </button>
        </div>
      )}

      {consolidated && !metrics.openStop && (
        <p className="flex items-start gap-2 rounded-xl border border-border bg-muted/50 p-3 text-sm text-muted-foreground">
          <Clock className="mt-0.5 h-4 w-4 shrink-0" /> {t('mobile.prod.consolidated')}
        </p>
      )}

      <div data-demo="m-prod-anchor" className="sr-only" />
      <Stepper label={good} value={ok} onChange={setOk} tone="text-state-production" step={1}
        decreaseLabel={t('mobile.prod.decrease', { label: good })}
        increaseLabel={t('mobile.prod.increase', { label: good })} />
      <Stepper label={scrap} value={nok} onChange={setNok} tone="text-state-quality"
        decreaseLabel={t('mobile.prod.decrease', { label: scrap })}
        increaseLabel={t('mobile.prod.increase', { label: scrap })} />

      <div className="space-y-1.5 rounded-xl border border-border bg-muted/50 p-3 text-sm">
        <div className="flex justify-between">
          <span className="text-muted-foreground">{t('mobile.prod.declaredQuality')}</span>
          <span dir="ltr" className="font-mono font-semibold">
            {ok + nok === 0 ? '—' : `${Math.round((ok / (ok + nok)) * 100)} %`}
          </span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">{t('mobile.prod.runningTotal')}</span>
          <span dir="ltr" className="font-mono font-semibold">
            {metrics.totalOk + ok} / {state.session.targetQty}
          </span>
        </div>
      </div>

      {/* BL-024 — a scrapped quantity always carries its cause. */}
      {nok > 0 && (
        <div className="space-y-2">
          <p className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">
            {t('mobile.prod.scrapCause')}
          </p>
          <div className="grid grid-cols-2 gap-2">
            {SCRAP_CAUSES.map((c) => (
              <button
                key={c}
                type="button"
                onClick={() => setScrapCause(c)}
                className={`min-h-[56px] rounded-xl border p-2 text-sm font-semibold ${
                  scrapCause === c
                    ? 'border-foreground bg-muted text-foreground'
                    : 'border-border bg-card text-muted-foreground'
                }`}
              >
                {t(`mobile.prod.scrapCause.${c}`)}
              </button>
            ))}
          </div>
        </div>
      )}

      {incoherent && (
        <p className="flex items-start gap-2 rounded-xl border border-state-quality/40 bg-state-quality/10 p-3 text-sm text-state-quality">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          {confirming
            ? t('mobile.prod.incoherent.confirm', { qty: expected })
            : t('mobile.prod.incoherent.warn', { qty: expected })}
        </p>
      )}

      <button type="button" disabled={Boolean(metrics.openStop) || ok + nok === 0 || (nok > 0 && !scrapCause)}
        onClick={submit}
        className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background disabled:opacity-40">
        <Check className="h-5 w-5" /> {confirming ? t('mobile.prod.incoherent.cta') : t('common.validate')}
      </button>
    </div>
  );
}
