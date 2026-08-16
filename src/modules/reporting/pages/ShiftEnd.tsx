import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CheckCircle2, FileDown, LogOut } from 'lucide-react';

import { useT } from '@/i18n/I18nProvider';
import { closeSession, computeMetrics, useSessionState, type ClosureType } from '@/modules/auth/store/session';
import { signOut } from '@/oas/authStore';
import { exportPdf } from '@/shared/lib/pdf';

/** BL-023 — the three closure types an operator can declare. */
const CLOSURES: ClosureType[] = ['operator', 'relay', 'post-stop'];

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between border-b border-border py-2.5 last:border-0">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span dir="ltr" className="font-mono text-sm font-semibold">{value}</span>
    </div>
  );
}

/** End-of-shift recap + handover — closes the operator session (spec §Fin de poste). */
export default function ShiftEnd() {
  const t = useT();
  const navigate = useNavigate();
  const state = useSessionState();
  const m = computeMetrics(state);
  const [closure, setClosure] = useState<ClosureType>('operator');


  useEffect(() => {
    if (!state.session) navigate('/mobile/scan', { replace: true });
  }, [state.session, navigate]);

  if (!state.session) return null;
  const s = state.session;

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.shiftEnd.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('mobile.shiftEnd.subtitle')}</p>
      </header>

      <section data-demo="m-shiftend" className="rounded-xl border border-border bg-card p-4">
        <p className="text-[0.6875rem] uppercase tracking-[0.04em] text-muted-foreground">{t('mobile.home.myPost')}</p>
        <p className="font-mono text-2xl font-bold">{s.postCode}</p>
        <div className="mt-3">
          <Row label={t('mobile.prod.good')} value={`${m.totalOk}`} />
          <Row label={t('mobile.prod.scrap')} value={`${m.totalNok}`} />
          <Row label={t('mobile.prod.declaredQuality')} value={m.qualityRate === null ? '—' : `${m.qualityRate} %`} />
          <Row label={t('mobile.shiftEnd.stops')} value={`${state.stops.length}`} />
          <Row label={t('mobile.shiftEnd.stopTime')} value={`${m.stopMin} ${t('common.min')}`} />
          <Row label={t('mobile.shiftEnd.worked')} value={`${m.elapsedMin} ${t('common.min')}`} />
          <Row label={t('mobile.shiftEnd.progress')} value={`${m.progressPct} %`} />
          <Row label={t('mobile.shiftEnd.pending')} value={`${m.pendingSync}`} />
        </div>
      </section>

      {m.openStop && (
        <p className="rounded-xl border border-state-technical/40 bg-state-technical/10 p-3 text-sm text-state-technical">
          {t('mobile.shiftEnd.openStopWarning')}
        </p>
      )}

      <button
        type="button"
        onClick={() => exportPdf({
          title: t('mobile.shiftEnd.title'),
          subtitle: `${s.postCode} · ${new Date().toLocaleDateString()}`,
          summary: [
            { label: t('mobile.prod.good'), value: String(m.totalOk) },
            { label: t('mobile.prod.scrap'), value: String(m.totalNok) },
            { label: t('mobile.prod.declaredQuality'), value: m.qualityRate === null ? '—' : `${m.qualityRate} %` },
            { label: t('mobile.shiftEnd.stops'), value: String(state.stops.length) },
            { label: t('mobile.shiftEnd.stopTime'), value: `${m.stopMin} ${t('common.min')}` },
            { label: t('mobile.shiftEnd.worked'), value: `${m.elapsedMin} ${t('common.min')}` },
            { label: t('mobile.shiftEnd.progress'), value: `${m.progressPct} %` },
          ],
          tables: [{
            title: t('mobile.shiftEnd.stops'),
            head: [t('web.alerts.cause'), t('web.shift.at'), t('common.min')],
            rows: state.stops.map((st) => [
              t(st.causeLabelKey),
              new Date(st.declaredAt).toLocaleTimeString(),
              st.closedAt ? Math.round((Date.parse(st.closedAt) - Date.parse(st.declaredAt)) / 60000) : '—',
            ]),
          }],
          fileName: `oas-fin-de-poste-${s.postCode}.pdf`,
        })}
        className="flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl border border-border text-sm font-medium"
      >
        <FileDown className="h-4 w-4" /> {t('mobile.shiftEnd.pdf')}
      </button>

      {/* BL-023 — closure type: normal end, relay handover, or post left stopped. */}
      <fieldset className="space-y-2 rounded-xl border border-border bg-card p-3">
        <legend className="px-1 text-[0.6875rem] uppercase tracking-[0.04em] text-muted-foreground">
          {t('mobile.shiftEnd.closureType')}
        </legend>
        {CLOSURES.map((type) => (
          <label
            key={type}
            className={`flex min-h-[52px] items-center gap-3 rounded-lg border px-3 text-sm ${
              closure === type ? 'border-foreground bg-muted/60 font-medium' : 'border-border'
            }`}
          >
            <input
              type="radio"
              name="closure"
              value={type}
              checked={closure === type}
              onChange={() => setClosure(type)}
              className="h-4 w-4 accent-current"
            />
            <span>
              {t(`mobile.shiftEnd.closure.${type}`)}
              <span className="block text-xs font-normal text-muted-foreground">
                {t(`mobile.shiftEnd.closure.${type}.hint`)}
              </span>
            </span>
          </label>
        ))}
      </fieldset>

      <button
        type="button"
        onClick={() => {
          void (async () => {
            await closeSession(closure);
            await signOut();
            navigate('/mobile/login', { replace: true });
          })();
        }}
        className="flex min-h-[64px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background"
      >
        <LogOut className="h-5 w-5 rtl:rotate-180" /> {t('mobile.shiftEnd.confirm')}
      </button>

      <button
        type="button"
        onClick={() => navigate('/mobile/home')}
        className="flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl border border-border text-sm font-medium text-muted-foreground"
      >
        <CheckCircle2 className="h-4 w-4" /> {t('mobile.shiftEnd.keepWorking')}
      </button>
    </div>
  );
}
