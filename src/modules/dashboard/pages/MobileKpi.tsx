import { useEffect, useState } from 'react';
import { kindToState } from '@/oas/demo';
import { fetchLiveKpiForPost, type LiveKpi, type LivePareto } from '@/oas/liveState';
import { stateSolid } from '@/oas/components/StateBadge';
import { useSessionState } from '@/modules/auth/store/session';
import { useI18n } from '@/i18n/I18nProvider';

const EMPTY_KPI: LiveKpi = {
  trs: 0, availability: 0, performance: 0, quality: 0, cadenceKnown: true,
  producedOk: 0, producedNok: 0, stopsCount: 0, stopMinutes: 0, mttrMin: 0, openingMin: 0, hasLiveData: false,
};

function Gauge({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl border border-border bg-card p-3">
      <p className="text-[0.6875rem] uppercase tracking-[0.04em] text-muted-foreground">{label}</p>
      <p dir="ltr" className="mt-1 font-mono text-2xl font-bold">{value}%</p>
      <div className="mt-2 h-1.5 w-full rounded-full bg-muted">
        <div className="h-full rounded-full bg-foreground" style={{ width: `${value}%` }} />
      </div>
    </div>
  );
}

/** BL — "TRS du poste": scoped to the operator's own post (spec §1.5), not the plant-wide aggregate. */
export default function MobileKpi() {
  const { t, lang } = useI18n();
  const { session } = useSessionState();
  const [KPI, setKPI] = useState<LiveKpi>(EMPTY_KPI);
  const [PARETO, setPareto] = useState<LivePareto[]>([]);

  useEffect(() => {
    if (!session) return;
    let cancelled = false;
    void fetchLiveKpiForPost(session.postId).then((r) => {
      if (cancelled) return;
      setKPI(r.kpi);
      setPareto(r.pareto);
    });
    return () => { cancelled = true; };
    // `lang` re-triggers so pareto cause labels (bilingual data) refresh immediately on switch.
  }, [session?.postId, lang]);

  const max = Math.max(1, ...PARETO.map((p) => p.minutes));

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.kpi.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('mobile.kpi.updated')}</p>
      </header>

      <div className="rounded-xl border border-border bg-card p-4 text-center">
        <p className="text-[0.6875rem] uppercase tracking-[0.04em] text-muted-foreground">{t('web.dash.trs')}</p>
        <p dir="ltr" className="mt-1 font-mono text-[3rem] font-bold leading-none">{KPI.trs}%</p>
        {!KPI.cadenceKnown && (
          <span className="mt-2 inline-block rounded-full bg-muted px-2 py-0.5 text-[0.6875rem] text-muted-foreground">
            {t('mobile.kpi.trsLite')}
          </span>
        )}
      </div>

      <div data-demo="m-kpi" className="grid grid-cols-3 gap-2">
        <Gauge label={t('mobile.kpi.availability')} value={KPI.availability} />
        <Gauge label={t('mobile.kpi.performance')} value={KPI.performance} />
        <Gauge label={t('mobile.kpi.quality')} value={KPI.quality} />
      </div>

      <section className="rounded-xl border border-border bg-card p-4">
        <h2 className="text-sm font-semibold">{t('mobile.kpi.topStops')}</h2>
        <ul className="mt-3 space-y-2">
          {PARETO.map((p) => (
            <li key={p.causeKey}>
              <div className="flex justify-between text-xs">
                <span>{p.causeKey}</span>
                <span dir="ltr" className="font-mono text-muted-foreground">{p.minutes} {t('common.min')}</span>
              </div>
              <div className="mt-1 h-1.5 w-full rounded-full bg-muted">
                <div className={`h-full rounded-full ${stateSolid[kindToState[p.kind]]}`}
                  style={{ width: `${(p.minutes / max) * 100}%` }} />
              </div>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
