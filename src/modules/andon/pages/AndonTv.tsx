import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Check, Edit2, Tv, WifiOff, X } from 'lucide-react';

import { useT } from '@/i18n/I18nProvider';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { useEvents } from '@/oas/eventStore';
import { andonMessageApi } from '@/oas/api/kpi';
import { AndonKpiRail } from '../components/AndonKpiRail';
import { AndonLinePanel } from '../components/AndonLinePanel';
import { pushToast } from '@/shared/lib/toast';

/** Rotates each line into focus every N seconds (D2-14 rotation multi-lignes). */
const ROTATE_MS = 12_000;
/** Rotates alerts every 6 seconds. */
const ALERT_ROTATE_MS = 6_000;

/**
 * Andon TV wallboard (D2-14) — full-screen shop-floor view designed to
 * be projected on a wall display. Rotates through every line, surfaces
 * day KPIs and the currently-open events.
 *
 * Enhanced with MOTD (backed by `GET/PUT /api/oas/andon/message`, shared
 * across every wallboard rather than per-browser localStorage), critical
 * alert banner, and connection heartbeat simulation.
 */
export default function AndonTv() {
  const t = useT();
  const [idx, setIdx] = useState(0);
  const [now, setNow] = useState(() => new Date());

  // MOTD State (D2-14 persisted message, server-side)
  const [motd, setMotd] = useState('');
  const [isEditingMotd, setIsEditingMotd] = useState(false);
  const [tempMotd, setTempMotd] = useState('');

  useEffect(() => {
    void andonMessageApi.get().then((dto) => {
      setMotd(dto.message);
      setTempMotd(dto.message);
    }).catch(() => {});
  }, []);

  // Connection State Simulation (heartbeat/stale detection)
  const [connStatus, setConnStatus] = useState<'connected' | 'stale' | 'reconnecting'>('connected');
  const [retrySec, setRetrySec] = useState(0);

  // Alert Rotation State
  const [alertIdx, setAlertIdx] = useState(0);

  const { lines } = useHierarchyState();
  const events = useEvents();
  const openEvents = useMemo(
    () => events.filter((e) => e.stage !== 'closed' && e.stage !== 'resolved'),
    [events],
  );

  const criticalEvents = useMemo(() => {
    return openEvents.filter(e => e.slaLeftMin < 0 || e.escalation > 0);
  }, [openEvents]);

  // Main rotation and clock
  useEffect(() => {
    const lineCount = Math.max(1, lines.length);
    const rot = window.setInterval(() => setIdx((i) => (i + 1) % lineCount), ROTATE_MS);
    const clk = window.setInterval(() => setNow(new Date()), 30_000);

    // Simulate periodic "stale" data for the demo
    const heartbeat = window.setInterval(() => {
      const chance = Math.random();
      if (chance > 0.98 && connStatus === 'connected') {
        setConnStatus('stale');
        setRetrySec(5);
      }
    }, 15_000);

    return () => {
      window.clearInterval(rot);
      window.clearInterval(clk);
      window.clearInterval(heartbeat);
    };
  }, [connStatus, lines.length]);

  // Reconnection logic simulation
  useEffect(() => {
    if (connStatus === 'stale') {
      const timer = window.setInterval(() => {
        setRetrySec((s) => {
          if (s <= 1) {
            setConnStatus('reconnecting');
            return 0;
          }
          return s - 1;
        });
      }, 1000);
      return () => window.clearInterval(timer);
    }
    if (connStatus === 'reconnecting') {
      const timer = window.setTimeout(() => setConnStatus('connected'), 2000);
      return () => window.clearTimeout(timer);
    }
  }, [connStatus]);

  // Alert rotation logic
  useEffect(() => {
    if (criticalEvents.length > 1) {
      const rot = window.setInterval(() => {
        setAlertIdx(i => (i + 1) % criticalEvents.length);
      }, ALERT_ROTATE_MS);
      return () => window.clearInterval(rot);
    } else {
      setAlertIdx(0);
    }
  }, [criticalEvents.length]);

  const clock = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  const currentLine = lines[idx % Math.max(1, lines.length)];

  const saveMotd = () => {
    void andonMessageApi.set(undefined, tempMotd)
      .then(() => {
        setMotd(tempMotd);
        setIsEditingMotd(false);
      })
      .catch(() => pushToast('common.actionFailed'));
  };

  const activeAlert = criticalEvents[alertIdx];

  return (
    <main className="flex min-h-screen flex-col gap-4 bg-background p-4 text-foreground transition-colors duration-500 xl:gap-5 xl:p-6">
      {/* Connection Stale/Reconnecting Banner (Top Overlay) */}
      {connStatus !== 'connected' && (
        <div className="fixed inset-x-0 top-0 z-[60] flex items-center justify-center bg-destructive px-4 py-3 text-destructive-foreground shadow-2xl">
          <div className="flex items-center gap-3 text-center font-bold uppercase tracking-[0.18em]">
            <WifiOff className="h-5 w-5 shrink-0 animate-pulse" />
            <span className="text-xs leading-tight sm:text-sm">
              {connStatus === 'stale'
                ? t('andon.connection.stale')
                : t('andon.connection.reconnecting', { n: retrySec })}
            </span>
          </div>
        </div>
      )}

      <header className="flex flex-wrap items-center gap-x-3 gap-y-3 xl:gap-x-4">
        <div className="flex min-w-0 items-center gap-3">
          <Tv className="h-6 w-6 shrink-0 text-primary xl:h-8 xl:w-8" aria-hidden />
          <h1 className="text-pretty text-xl font-black leading-tight tracking-tight xl:text-2xl 2xl:text-3xl">
            {t('andon.title')}
          </h1>
        </div>

        <div className="flex items-center gap-2 whitespace-nowrap rounded-full border border-border bg-card px-3 py-1.5 text-[0.6rem] font-bold uppercase tracking-[0.12em] text-muted-foreground shadow-sm xl:text-[0.7rem]">
          <div className={`h-2 w-2 shrink-0 rounded-full ${connStatus === 'connected' ? 'bg-success' : 'bg-destructive animate-pulse'}`} />
          {t('andon.connection.connected')}
        </div>

        <span className="flex min-w-0 max-w-full items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-[0.6rem] font-bold uppercase tracking-[0.12em] text-muted-foreground shadow-sm xl:text-[0.7rem]">
          <span className="hidden lg:inline">{t('andon.rotating')} ·</span>
          <span className="truncate text-foreground">{currentLine?.name ?? '—'}</span>
        </span>

        <div className="ms-auto flex min-w-0 items-center gap-4 xl:gap-6">
          <div className="flex min-w-0 flex-col items-end gap-0.5">
            <span className="text-[0.55rem] font-bold uppercase tracking-[0.18em] text-muted-foreground/60 xl:text-[0.65rem]">
              {t('andon.motd.label')}
            </span>
            <div className="group relative flex min-w-0 items-center gap-2">
              {isEditingMotd ? (
                <div className="flex items-center gap-2 rounded-xl border border-primary bg-background p-1.5 shadow-lg">
                  <input
                    autoFocus
                    className="w-52 bg-transparent px-2 py-1 text-sm font-medium outline-none xl:w-72"
                    value={tempMotd}
                    onChange={(e) => setTempMotd(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && saveMotd()}
                    onBlur={() => !tempMotd && setIsEditingMotd(false)}
                    placeholder={t('andon.motd.placeholder')}
                  />
                  <button onClick={saveMotd} className="rounded-lg p-1.5 text-success transition-colors hover:bg-success/20" aria-label={t('andon.motd.edit')}>
                    <Check className="h-4 w-4" />
                  </button>
                  <button onClick={() => setIsEditingMotd(false)} className="rounded-lg p-1.5 text-destructive transition-colors hover:bg-destructive/20" aria-label={t('common.cancel')}>
                    <X className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <>
                  <p className="max-w-[14rem] truncate text-sm font-bold text-primary xl:max-w-md xl:text-lg">
                    {motd || t('andon.motd.placeholder')}
                  </p>
                  <button
                    onClick={() => { setTempMotd(motd); setIsEditingMotd(true); }}
                    className="shrink-0 rounded-full p-1.5 text-muted-foreground opacity-60 transition-opacity hover:bg-muted hover:text-foreground focus-visible:opacity-100 group-hover:opacity-100"
                    aria-label={t('andon.motd.edit')}
                  >
                    <Edit2 className="h-4 w-4" />
                  </button>
                </>
              )}
            </div>
          </div>

          <span dir="ltr" className="shrink-0 font-mono text-2xl font-black leading-none tabular-nums tracking-tight text-foreground xl:text-4xl">
            {clock}
          </span>
        </div>
      </header>

      {/* Rotating Critical Alert Banner (D2-14 Prominent Alerts) */}
      {criticalEvents.length > 0 && activeAlert && (
        <section className="relative overflow-hidden rounded-2xl bg-destructive shadow-lg shadow-destructive/20 animate-in fade-in slide-in-from-top-4 duration-500 xl:rounded-3xl">
          <div className="flex flex-wrap items-center gap-x-5 gap-y-3 px-5 py-4 text-destructive-foreground xl:gap-x-6 xl:px-8 xl:py-5">
            <div className="flex min-w-0 items-center gap-3 xl:gap-4">
              <AlertTriangle className="h-8 w-8 shrink-0 xl:h-10 xl:w-10" strokeWidth={2.5} aria-hidden />
              <div className="flex min-w-0 flex-col gap-0.5">
                <span className="text-pretty text-[0.55rem] font-bold uppercase leading-tight tracking-[0.18em] opacity-80 xl:text-[0.65rem]">
                  {activeAlert.escalation > 0
                    ? t('andon.alert.escalated', { n: activeAlert.escalation })
                    : t('andon.alert.critical')}
                </span>
                <span className="font-mono text-2xl font-black leading-none tracking-tight xl:text-3xl">
                  {activeAlert.post}
                </span>
              </div>
            </div>

            <div className="hidden h-12 w-px bg-current opacity-25 sm:block" />

            <div className="flex min-w-[10rem] flex-1 flex-col gap-0.5 overflow-hidden">
              <span className="truncate text-[0.55rem] font-bold uppercase tracking-[0.18em] opacity-80 xl:text-[0.65rem]">
                {activeAlert.lineKey}
              </span>
              <span className="text-pretty text-lg font-black leading-tight tracking-tight xl:text-2xl">
                {activeAlert.causeKey}
              </span>
            </div>

            <div className="flex flex-col items-end gap-0.5">
              <span className="text-[0.55rem] font-bold uppercase tracking-[0.18em] opacity-80 xl:text-[0.65rem]">
                {t('web.alerts.sla')}
              </span>
              <span dir="ltr" className="flex items-baseline gap-1 whitespace-nowrap font-mono text-2xl font-black leading-none tabular-nums xl:text-3xl">
                {activeAlert.slaLeftMin < 0 ? '−' : ''}{Math.abs(activeAlert.slaLeftMin)}
                <span className="text-sm font-bold opacity-80 xl:text-base">{t('common.min')}</span>
              </span>
            </div>

            {criticalEvents.length > 1 && (
              <div className="flex w-full justify-end gap-2">
                {criticalEvents.map((_, i) => (
                  <div key={i} className={`h-1.5 rounded-full transition-all duration-300 ${i === alertIdx ? 'w-8 bg-current' : 'w-2 bg-current opacity-30'}`} />
                ))}
              </div>
            )}
          </div>
        </section>
      )}


      <AndonKpiRail />

      <div data-demo="andon-board" className="grid flex-1 gap-4 xl:grid-cols-[minmax(0,1fr)_22rem] xl:gap-5 2xl:grid-cols-[minmax(0,1fr)_26rem]">
        <div className="flex min-w-0 flex-col gap-4">
          {currentLine && <AndonLinePanel key={currentLine.id} lineKey={currentLine.name} />}
        </div>

        <aside data-demo="andon-events" className="flex min-w-0 flex-col gap-3 rounded-2xl border border-border bg-card/40 p-4 shadow-sm backdrop-blur-sm xl:gap-4 xl:rounded-3xl xl:p-6">
          <header className="flex items-center justify-between gap-3">
            <h2 className="text-pretty text-base font-black uppercase leading-tight tracking-tight xl:text-xl">
              {t('andon.openEvents')}
            </h2>
            <span dir="ltr" className="shrink-0 rounded-full bg-primary/10 px-3 py-1 font-mono text-base font-black leading-none text-primary xl:text-xl">
              {openEvents.length}
            </span>
          </header>
          <ul className="flex flex-1 flex-col gap-2.5 xl:gap-3">
            {openEvents.slice(0, 5).map((ev) => {
              const late = ev.slaLeftMin < 0;
              const escalated = ev.escalation > 0;
              return (
                <li
                  key={ev.id}
                  className={`flex items-center gap-3 rounded-2xl border p-3 shadow-sm transition-all duration-300 xl:p-4 ${
                    late || escalated
                      ? 'border-destructive/40 bg-destructive/5'
                      : 'border-border bg-background/60'
                  }`}
                >
                  <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl font-mono text-sm font-black xl:h-12 xl:w-12 xl:text-base ${
                    late || escalated ? 'bg-destructive text-destructive-foreground' : 'bg-muted text-muted-foreground'
                  }`}>
                    {ev.post.split('-')[1] || ev.post}
                  </div>

                  <div className="flex min-w-0 flex-1 flex-col">
                    <span className="text-pretty text-sm font-bold leading-tight xl:text-base">
                      {ev.causeKey}
                    </span>
                    <span className="truncate text-[0.6rem] font-bold uppercase tracking-[0.1em] text-muted-foreground xl:text-xs">
                      {ev.lineKey}
                    </span>
                  </div>

                  <span
                    dir="ltr"
                    className={
                      'shrink-0 rounded-xl border px-2.5 py-1.5 font-mono text-sm font-black tabular-nums xl:text-base ' +
                      (late
                        ? 'border-destructive/40 bg-destructive/10 text-destructive'
                        : 'border-border bg-card text-foreground')
                    }
                  >
                    {late ? '−' : '+'}{Math.abs(ev.slaLeftMin)}
                  </span>
                </li>
              );
            })}
            {!openEvents.length && (
              <li className="flex flex-1 items-center justify-center rounded-2xl border border-dashed border-border p-8 text-center text-xs font-bold uppercase tracking-widest text-muted-foreground/50">
                {t('andon.noEvents')}
              </li>
            )}
          </ul>
          <footer className="mt-auto border-t border-border/50 pt-4">
            <p className="text-pretty text-[0.55rem] font-bold uppercase leading-[1.35] tracking-[0.18em] text-muted-foreground/50 xl:text-[0.65rem]">
              {t('common.privacyFooter')}
            </p>
          </footer>
        </aside>

      </div>

    </main>
  );
}
