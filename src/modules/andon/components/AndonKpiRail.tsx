import { useT } from '@/i18n/I18nProvider';
import { useLiveKpi } from '@/oas/liveState';

/** Big-format KPI rail for the wallboard — readable from across a shop-floor. */
export function AndonKpiRail() {
  const t = useT();
  const KPI = useLiveKpi();
  const trsLabel = KPI.cadenceKnown ? 'kpi.trs' : 'kpi.trsLite';

  const items: { key: string; value: string; unit?: string; sub: string }[] = [
    { key: trsLabel, value: String(KPI.trs), unit: '%', sub: t('kpi.jour') },
    { key: 'kpi.availability', value: String(KPI.availability), unit: '%', sub: t('kpi.availability.sub') },
    { key: 'kpi.performance', value: String(KPI.performance), unit: '%', sub: t('kpi.performance.sub') },
    { key: 'kpi.quality', value: String(KPI.quality), unit: '%', sub: t('kpi.quality.sub') },
    { key: 'kpi.stopsToday', value: String(KPI.stopsCount), sub: `${KPI.stopMinutes} ${t('common.min')}` },
    { key: 'kpi.mttr', value: String(KPI.mttrMin), unit: t('common.min'), sub: t('kpi.mttr.sub') },
  ];

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6 xl:gap-4">
      {items.map((it) => (
        <div
          key={it.key}
          className="flex min-h-[7.5rem] flex-col justify-between gap-2 rounded-2xl border border-border bg-card/60 p-4 text-start shadow-sm backdrop-blur-md 2xl:min-h-[9.5rem] 2xl:rounded-3xl 2xl:p-5"
        >
          <p className="text-pretty text-[0.6rem] font-bold uppercase leading-[1.3] tracking-[0.12em] text-muted-foreground 2xl:text-[0.7rem]">
            {t(it.key)}
          </p>
          <p
            dir="ltr"
            className="flex items-baseline gap-1 whitespace-nowrap font-mono text-3xl font-black leading-none tracking-tight text-foreground sm:text-4xl 2xl:text-5xl"
          >
            {it.value}
            {it.unit && <span className="text-base font-bold text-muted-foreground 2xl:text-xl">{it.unit}</span>}
          </p>
          <p
            dir="ltr"
            className="text-pretty text-[0.55rem] font-bold uppercase leading-[1.3] tracking-[0.12em] text-muted-foreground/60 2xl:text-[0.65rem]"
          >
            {it.sub}
          </p>
        </div>
      ))}
    </div>
  );
}
