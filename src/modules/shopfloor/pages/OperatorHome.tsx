import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Wrench, PackageSearch, Repeat, CheckCircle2, LogOut, UserCheck, type LucideIcon } from 'lucide-react';

import { StateBadge } from '@/oas/components/StateBadge';
import { useT } from '@/i18n/I18nProvider';
import { findPost } from '@/modules/auth/store/session';
import { publishedOperator, useAssignments } from '@/oas/assignmentStore';
import {
  DECLARATION_REMINDER_MIN, closeStop, computeMetrics, confirmPresence,
  useDeclarationReminder, useSessionState,
} from '@/modules/auth/store/session';

function GloveAction({
  label, icon: Icon, tone, onClick,
}: { label: string; icon: LucideIcon; tone: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex min-h-[88px] flex-col items-center justify-center gap-2 rounded-xl border p-3 text-sm font-semibold shadow-soft transition-all duration-150 active:scale-[0.97] active:shadow-none ${tone}`}
    >
      <Icon className="h-7 w-7" aria-hidden />
      {label}
    </button>
  );
}


export default function OperatorHome() {
  const navigate = useNavigate();
  const t = useT();
  const state = useSessionState();
  const assignments = useAssignments();
  // BL-024 — periodic reminder when no production has been declared for a while,
  // plus the system closure watcher when the shift is over (BL-016).
  const overdueMin = useDeclarationReminder();
  const [, tick] = useState(0);
  // BL-025 — a reading that came due during a stop is flagged as deferred and
  // consolidated on the next declaration after the restart.
  const deferredRef = useRef(false);


  // 1 s tick so the open-stop chrono runs live (spec O-6).
  useEffect(() => {
    const id = setInterval(() => tick((n) => n + 1), 1_000);
    return () => clearInterval(id);
  }, []);


  useEffect(() => {
    if (!state.session) navigate('/mobile/scan', { replace: true });
  }, [state.session, navigate]);

  if (!state.session) return null;

  const s = state.session;
  const m = computeMetrics(state);
  const post = findPost(s.postCode);
  const assignedOperator = publishedOperator(assignments, s.postCode);
  const machineState = m.openStop
    ? (m.openStop.eventType as 'technical' | 'quality' | 'material' | 'changeover')
    : 'production';
  if (m.openStop && overdueMin > 0) deferredRef.current = true;
  if (overdueMin === 0) deferredRef.current = false;


  return (
    <div className="space-y-5">
      {!s.presenceConfirmedAt && (
        <section data-demo="m-presence" className="rounded-xl border border-state-material/40 bg-state-material/10 p-4">
          <p className="text-sm font-semibold text-foreground">{t('mobile.presence.title')}</p>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {t('mobile.presence.desc', { post: s.postCode })}
          </p>
          <button
            type="button"
            onClick={confirmPresence}
            className="mt-3 flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-sm font-semibold text-background"
          >
            <UserCheck className="h-5 w-5" /> {t('mobile.presence.confirm')}
          </button>
        </section>
      )}

      {/* BL-025 — while a stop is open the reading is deferred, not lost. */}
      {overdueMin > 0 && m.openStop && (
        <section className="rounded-xl border border-border bg-muted/50 p-4">
          <p className="text-sm font-semibold text-foreground">{t('mobile.reminder.deferred')}</p>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {t('mobile.reminder.deferredDesc', { min: overdueMin })}
          </p>
        </section>
      )}

      {overdueMin > 0 && !m.openStop && (
        <section className="rounded-xl border border-state-changeover/40 bg-state-changeover/10 p-4">
          <p className="text-sm font-semibold text-foreground">{t('mobile.reminder.title')}</p>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {t('mobile.reminder.desc', { min: overdueMin, every: DECLARATION_REMINDER_MIN })}
          </p>
          <button
            type="button"
            onClick={() => navigate('/mobile/production', { state: { consolidated: deferredRef.current } })}
            className="mt-3 flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-sm font-semibold text-background"
          >
            <CheckCircle2 className="h-5 w-5" /> {t('mobile.reminder.cta')}
          </button>
        </section>
      )}


      <section data-demo="m-post" className="rounded-xl border border-border bg-card p-4 shadow-soft">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-[0.6875rem] font-medium uppercase tracking-[0.04em] text-muted-foreground">
              {t('mobile.home.myPost')}
            </p>
            <h1 className="mt-1 font-mono text-[2rem] font-bold leading-none">{s.postCode}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{post?.lineKey ?? s.lineKey}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {t('mobile.home.assignedTo')}{' '}
              <span className="font-medium text-foreground">
                {assignedOperator ?? t('mobile.home.assignedPending')}
              </span>
            </p>
          </div>
          <button type="button" onClick={() => navigate('/mobile/shift-end')}
            className="inline-flex min-h-[44px] items-center gap-1.5 rounded-lg border border-border px-3 text-xs text-muted-foreground">
            <LogOut className="h-4 w-4 rtl:rotate-180" /> {t('mobile.shiftEnd.title')}
          </button>
        </div>

        <div className="mt-3 flex items-center gap-2">
          <StateBadge state={machineState} size="lg" />
          <span dir="ltr" className="font-mono text-xs text-muted-foreground">
            {t('common.since')} {m.openStop ? m.stopMin : m.elapsedMin} {t('common.min')}
          </span>
        </div>

        <dl className="mt-4 grid grid-cols-2 gap-3 border-t border-border pt-3 text-sm">
          <div>
            <dt className="text-xs text-muted-foreground">{t('mobile.home.order')}</dt>
            <dd className="font-mono font-medium">{s.order}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">{t('mobile.home.reference')}</dt>
            <dd className="font-mono font-medium">{s.ref}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">{t('mobile.home.progress')}</dt>
            <dd dir="ltr" className="font-medium">{m.totalOk} / {s.targetQty}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">{t('mobile.home.cadence')}</dt>
            <dd dir="ltr" className="font-medium">{t('mobile.home.rate', { n: s.cadence })}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">{t('mobile.prod.scrap')}</dt>
            <dd dir="ltr" className="font-medium">{m.totalNok}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">{t('mobile.prod.declaredQuality')}</dt>
            <dd dir="ltr" className="font-medium">{m.qualityRate === null ? '—' : `${m.qualityRate} %`}</dd>
          </div>
        </dl>

        <div className="mt-3 flex items-center gap-2">
          <div className="h-2 flex-1 overflow-hidden rounded-full bg-muted">
            <div className="h-full rounded-full bg-state-production transition-[width] duration-500"
              style={{ width: `${m.progressPct}%` }} />
          </div>
          <span dir="ltr" className="font-mono text-xs tabular-nums text-muted-foreground">{m.progressPct}%</span>
        </div>

      </section>

      {m.openStop && (
        <section className="animate-in fade-in slide-in-from-top-1 rounded-xl border border-state-technical/40 bg-state-technical/10 p-4 shadow-soft">
          <p className="text-sm font-semibold text-state-technical">
            {t('mobile.home.openStop', { label: t(m.openStop.causeLabelKey) })}
          </p>
          <p dir="ltr" className="mt-1 font-mono text-2xl font-bold tabular-nums text-state-technical">
            {(() => {
              const sec = Math.max(0, Math.floor((Date.now() - Date.parse(m.openStop!.declaredAt)) / 1000));
              const pad = (n: number) => String(n).padStart(2, '0');
              return `${pad(Math.floor(sec / 3600))}:${pad(Math.floor(sec / 60) % 60)}:${pad(sec % 60)}`;
            })()}
          </p>
          <p dir="ltr" className="mt-0.5 font-mono text-xs text-muted-foreground">
            {new Date(m.openStop.declaredAt).toLocaleTimeString()}
          </p>
          {/* BL-023 — reminder for a stop left open too long. */}
          {m.stopMin >= 15 && (
            <p className="mt-2 rounded-lg border border-state-technical/50 bg-background/60 p-2 text-xs font-medium text-state-technical">
              {t('mobile.home.forgottenStop', { min: m.stopMin })}
            </p>
          )}
          <button type="button" onClick={() => closeStop(m.openStop!.clientEventId)}

            className="mt-3 flex min-h-[56px] w-full items-center justify-center rounded-xl bg-foreground text-sm font-semibold text-background">
            {t('mobile.home.resumeProduction')}
          </button>
        </section>
      )}

      <section data-demo="m-actions" className="grid grid-cols-2 gap-3">
        <GloveAction label={t('mobile.home.declareStop')} icon={Wrench}
          tone="border-state-technical/40 bg-state-technical/10 text-state-technical"
          onClick={() => navigate('/mobile/stop')} />
        <GloveAction label={t('mobile.home.production')} icon={CheckCircle2}
          tone="border-state-production/40 bg-state-production/10 text-state-production"
          onClick={() => navigate('/mobile/production')} />
        <GloveAction label={t('mobile.home.material')} icon={PackageSearch}
          tone="border-state-material/40 bg-state-material/10 text-state-material"
          onClick={() => navigate('/mobile/stop')} />
        <GloveAction label={t('mobile.home.changeover')} icon={Repeat}
          tone="border-state-changeover/40 bg-state-changeover/10 text-state-changeover"
          onClick={() => navigate('/mobile/changeover')} />
      </section>

      {state.declarations.length > 0 && (
        <section className="rounded-xl border border-border bg-card p-4 shadow-soft">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">{t('mobile.home.lastEntries')}</h2>
            <button type="button" onClick={() => navigate('/mobile/history')}
              className="text-xs font-medium text-muted-foreground underline-offset-2 hover:underline">
              {t('common.history')}
            </button>
          </div>
          <ul className="mt-2 divide-y divide-border text-sm">
            {state.declarations.slice(0, 5).map((d) => (
              <li key={d.clientEventId} className="flex items-center justify-between py-2">
                <span className={d.kind === 'scrap' ? 'text-state-quality' : 'text-state-production'}>
                  {d.kind === 'scrap' ? t('mobile.prod.scrap') : t('mobile.prod.good')}
                </span>
                <span dir="ltr" className="font-mono">
                  +{d.quantityOk + d.quantityNok} · {new Date(d.occurredAt).toLocaleTimeString()}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
