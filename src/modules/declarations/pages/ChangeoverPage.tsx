import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Repeat, Timer, Check } from 'lucide-react';
import { useT } from '@/i18n/I18nProvider';
import { closeStop, declareStop, useSessionState } from '@/modules/auth/store/session';

const STEP_KEYS = [
  'mobile.co.step1',
  'mobile.co.step2',
  'mobile.co.step3',
  'mobile.co.step4',
  'mobile.co.step5',
] as const;

/**
 * BL-028 — changeover: the elapsed time is measured automatically and the
 * changeover is recorded as a real stop, so it lands in the history, the
 * offline queue and the shift recap.
 */
export default function ChangeoverPage() {
  const navigate = useNavigate();
  const t = useT();
  const state = useSessionState();
  // BL-028 — every step must be ticked before the changeover can be closed.
  const [done, setDone] = useState<boolean[]>(() => STEP_KEYS.map(() => false));
  const allDone = done.every(Boolean);
  const step = done.findIndex((d) => !d);

  const openChangeover = useMemo(
    () => state.stops.find((s) => s.eventType === 'changeover' && !s.closedAt) ?? null,
    [state.stops],
  );

  // No open post session → nothing can be recorded: send the operator back to the scan.
  useEffect(() => {
    if (!state.session) navigate('/mobile/scan', { replace: true });
  }, [state.session, navigate]);

  // Opening the guide starts (or resumes) the measured changeover.
  const started = useRef(false);
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    if (!openChangeover && state.session) declareStop('CS-01', 'reason.CS-01', 'changeover');
  }, [openChangeover, state.session]);


  const startedAt = openChangeover ? Date.parse(openChangeover.declaredAt) : Date.now();
  const [elapsed, setElapsed] = useState(0);
  useEffect(() => {
    const tick = () => setElapsed(Math.max(0, Math.floor((Date.now() - startedAt) / 1000)));
    tick();
    const id = setInterval(tick, 1_000);
    return () => clearInterval(id);
  }, [startedAt]);

  const pad = (n: number) => String(n).padStart(2, '0');
  const chrono = `${pad(Math.floor(elapsed / 60))}:${pad(elapsed % 60)}`;

  function finish() {
    if (!allDone) return;
    if (openChangeover) closeStop(openChangeover.clientEventId);
    navigate('/mobile/home');
  }

  return (
    <div className="space-y-4">
      <button type="button" onClick={() => navigate(-1)}
        className="inline-flex min-h-[44px] items-center gap-1.5 text-sm text-muted-foreground">
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" /> {t('common.back')}
      </button>

      <header data-demo="m-changeover" className="rounded-xl border border-state-changeover/40 bg-state-changeover/10 p-4">
        <p className="inline-flex items-center gap-2 text-sm font-semibold text-state-changeover">
          <Repeat className="h-4 w-4" /> {t('mobile.co.header')}
        </p>
        <p dir="ltr" className="mt-2 font-mono text-[2rem] font-bold leading-none tabular-nums text-state-changeover">{chrono}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t('mobile.co.subtitle')}</p>
      </header>

      <ol className="space-y-2">
        {STEP_KEYS.map((key, i) => (
          <li key={key}>
            <button
              type="button"
              role="checkbox"
              aria-checked={done[i]}
              onClick={() => setDone((prev) => prev.map((v, j) => (j === i ? !v : v)))}
              className={`flex min-h-[56px] w-full items-center gap-3 rounded-lg border p-3 text-start ${
                done[i] ? 'border-state-changeover/40 bg-state-changeover/10' : 'border-border bg-card'
              }`}
            >
              <span className="flex h-7 w-7 items-center justify-center rounded-full border border-border font-mono text-xs">
                {done[i] ? <Check className="h-4 w-4" /> : i + 1}
              </span>
              <span className="flex-1 text-sm font-medium">{t(key)}</span>
              {i === step && <Timer className="h-4 w-4 text-state-changeover" />}
            </button>
          </li>
        ))}
      </ol>

      <button type="button" onClick={finish} disabled={!allDone}
        className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background disabled:opacity-40">
        <Check className="h-5 w-5" /> {t('mobile.co.done')}
      </button>
      {!allDone && (
        <p className="text-center text-xs text-muted-foreground">
          {t('mobile.co.gate', { n: done.filter(Boolean).length, total: STEP_KEYS.length })}
        </p>
      )}
    </div>
  );
}
