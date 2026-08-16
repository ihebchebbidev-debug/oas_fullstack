import { useState } from 'react';

import { kindToState } from '@/oas/demo';
import { useLiveKpi, useLiveLines, useLivePareto, useLiveTrend } from '@/oas/liveState';
import { stateSolid } from '@/oas/components/StateBadge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/shared/components/PageHeader';
import { useT } from '@/i18n/I18nProvider';

/** BL-042 — role-specific dashboard views over the same dataset. */
type Role = 'manager' | 'direction' | 'maintenance' | 'quality';
const ROLES: Role[] = ['manager', 'direction', 'maintenance', 'quality'];

const VIEW: Record<Role, { kpis: string[]; trend: boolean; pareto: boolean; lines: boolean }> = {
  manager:     { kpis: ['trs', 'availability', 'performance', 'quality', 'mttr'], trend: true,  pareto: true,  lines: true },
  direction:   { kpis: ['trs', 'availability', 'quality'],                        trend: true,  pareto: false, lines: true },
  maintenance: { kpis: ['availability', 'mttr'],                                  trend: false, pareto: true,  lines: true },
  quality:     { kpis: ['quality', 'trs'],                                        trend: true,  pareto: false, lines: true },
};

function Kpi({ label, value, hint, tone = 'bg-foreground/25' }: {
  label: string; value: string; hint?: string; tone?: string;
}) {
  return (
    <Card className="relative overflow-hidden transition-shadow hover:shadow-medium">
      <span aria-hidden className={`absolute inset-y-0 start-0 w-1 ${tone}`} />
      <CardContent className="p-3 ps-4">
        <p className="text-overline uppercase text-muted-foreground">{label}</p>
        <p className="mt-1 font-mono text-metric font-bold tabular-nums">{value}</p>
        {hint && <p className="text-caption text-muted-foreground">{hint}</p>}
      </CardContent>
    </Card>
  );
}


export default function ManagerDashboard() {
  const t = useT();
  const [role, setRole] = useState<Role>('manager');
  const view = VIEW[role];
  const KPI = useLiveKpi();
  const PARETO = useLivePareto();
  const LINE_COMPARISON = useLiveLines();
  const TRS_TREND = useLiveTrend();
  const maxTrend = Math.max(1, ...TRS_TREND.map((x) => x.value));
  const maxPareto = Math.max(1, ...PARETO.map((p) => p.minutes));

  return (
    <>
      <PageHeader
        title={t('web.dash.title')}
        subtitle={t('web.dash.subtitle')}
        actions={
          <div data-demo="dash-role" className="flex items-center gap-2">
            <span className="text-caption text-muted-foreground">{t('web.dash.view')}</span>
            <Select value={role} onChange={(e) => setRole(e.target.value as Role)} className="h-8 w-48">
              {ROLES.map((r) => (
                <option key={r} value={r}>{t(`web.dash.role.${r}`)}</option>
              ))}
            </Select>
          </div>
        }
      />
      <div className="space-y-4 p-4">
        <div data-demo="dash-kpis" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          {view.kpis.includes('trs') && (
          <Kpi label={t('web.dash.trs')} value={`${KPI.trs}%`} tone="bg-state-production"
            hint={KPI.cadenceKnown ? t('web.dash.complete') : t('web.dash.trsLite')} />)}
          {view.kpis.includes('availability') && (
          <Kpi label={t('web.dash.availability')} value={`${KPI.availability}%`} tone="bg-state-idle" />)}
          {view.kpis.includes('performance') && (
          <Kpi label={t('web.dash.performance')} value={`${KPI.performance}%`} tone="bg-state-changeover" />)}
          {view.kpis.includes('quality') && (
          <Kpi label={t('web.dash.quality')} value={`${KPI.quality}%`} tone="bg-state-quality"
            hint={t('web.dash.scraps', { n: KPI.producedNok })} />)}
          {view.kpis.includes('mttr') && (
          <Kpi label={t('web.dash.mttr')} value={`${KPI.mttrMin} ${t('common.min')}`} tone="bg-state-technical"
            hint={t('web.dash.stopsHint', { stops: KPI.stopsCount, minutes: KPI.stopMinutes })} />)}

        </div>

        <div data-demo="dash-charts" className="grid gap-3 lg:grid-cols-2">
          {view.trend && (
          <Card>
            <CardHeader>
              <CardTitle>{t('web.dash.trend')}</CardTitle>
              <CardDescription>{t('web.dash.target')}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex h-40 items-end gap-2">
                {TRS_TREND.map((x, i) => (
                  <div key={i} className="group flex flex-1 flex-col items-center gap-1">
                    <span className="font-mono text-caption tabular-nums text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100">
                      {x.value}%
                    </span>
                    <div className="relative flex h-32 w-full items-end">
                      <div
                        className={`w-full rounded-t transition-[height,opacity] duration-500 group-hover:opacity-90 ${
                          x.value >= 75 ? 'bg-state-production' : 'bg-state-idle'
                        }`}
                        style={{ height: `${(x.value / maxTrend) * 100}%` }}
                      />
                    </div>
                    <span className="font-mono text-caption text-muted-foreground">{x.label}</span>
                  </div>
                ))}
              </div>
            </CardContent>

          </Card>)}

          {view.pareto && (
          <Card>
            <CardHeader>
              <CardTitle>{t('web.dash.pareto')}</CardTitle>
              <CardDescription>{t('web.dash.paretoDesc')}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2.5">
              {PARETO.map((p) => (
                <div key={p.causeKey}>
                  <div className="flex justify-between text-body-sm">
                    <span>{p.causeKey}</span>
                    <span className="font-mono text-muted-foreground">{p.minutes} {t('common.min')}</span>
                  </div>
                  <div className="mt-1 h-2 w-full overflow-hidden rounded-full bg-muted">
                    <div className={`h-full rounded-full transition-[width] duration-500 ${stateSolid[kindToState[p.kind]]}`}
                      style={{ width: `${(p.minutes / maxPareto) * 100}%` }} />
                  </div>

                </div>
              ))}
            </CardContent>
          </Card>)}
        </div>

        {view.lines && (
        <Card>
          <CardHeader>
            <CardTitle>{t('web.dash.lineCompare')}</CardTitle>
            <CardDescription>{t('web.dash.lineCompareDesc')}</CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('web.dash.line')}</TableHead>
                  <TableHead>{t('web.dash.trs')}</TableHead>
                  <TableHead>{t('web.dash.stops')}</TableHead>
                  <TableHead>{t('web.dash.scrap')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {LINE_COMPARISON.map((l) => (
                  <TableRow key={l.lineId} className="transition-colors hover:bg-muted/60">
                    <TableCell className="font-medium">{l.lineName}</TableCell>
                    <TableCell className={`font-mono tabular-nums ${l.trs >= 75 ? 'text-state-production' : 'text-state-technical'}`}>{l.trs}%</TableCell>
                    <TableCell className="font-mono tabular-nums">{l.stops}</TableCell>
                    <TableCell className="font-mono tabular-nums">{l.scrap}%</TableCell>

                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>)}
      </div>
    </>
  );
}
