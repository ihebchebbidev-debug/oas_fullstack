import { useEffect, useMemo, useState } from 'react';
import { Download, FileSpreadsheet } from 'lucide-react';

import { STATES, kindToState } from '@/oas/demo';
import { useRefState } from '@/oas/refStore';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { fetchLiveRange, useLivePosts, type LiveKpi, type LiveLine, type LivePareto, type LiveTrendPoint } from '@/oas/liveState';
import { kpiApi, type OasCadenceGapEntryDto } from '@/oas/api/kpi';
import { slaApi, type OasSlaRuleDto } from '@/oas/api/events';
import { KIND_TO_EVENT_TYPE, useEvents } from '@/oas/eventStore';
import { stateSolid } from '@/oas/components/StateBadge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/shared/components/PageHeader';
import { useI18n } from '@/i18n/I18nProvider';
import { csvExport } from '@/shared/lib/csv';
import { excelExport } from '@/shared/lib/excel';
import { pushToast } from '@/shared/lib/toast';

/** BL-039 — shift day (06:00 → 06:00) presets plus a free from/to range. */
const PERIODS = ['shift', 'day', 'week', 'month', 'custom'] as const;

/** Number of shift-days covered by each preset — drives the aggregation. */
/** BL-046 — go-live date of the pilot; the first 30 days are a ramp-up. */
const ADOPTION_START = '2026-05-20';

const EMPTY_KPI: LiveKpi = {
  trs: 0, availability: 0, performance: 0, quality: 0, cadenceKnown: true,
  producedOk: 0, producedNok: 0, stopsCount: 0, stopMinutes: 0, mttrMin: 0, openingMin: 0, hasLiveData: false,
};

export default function Reports() {
  const { t, lang } = useI18n();
  const refState = useRefState();
  const events = useEvents();
  const livePosts = useLivePosts();
  const { posts: hierarchyPosts } = useHierarchyState();
  const [period, setPeriod] = useState<(typeof PERIODS)[number]>('week');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  /** BL-039 — the selected period drives the real server-side aggregation range. */
  const range = useMemo(() => {
    const iso = (d: Date) => d.toISOString().slice(0, 10);
    const today = new Date();
    if (period === 'custom' && from && to) return { from, to };
    const backDays = period === 'week' ? 6 : period === 'month' ? 29 : 0;
    const start = new Date(today);
    start.setDate(start.getDate() - backDays);
    return { from: iso(start), to: iso(today) };
  }, [period, from, to]);

  const days = useMemo(() => Math.max(1, (Date.parse(range.to) - Date.parse(range.from)) / 86_400_000 + 1), [range]);

  const [KPI, setKPI] = useState<LiveKpi>(EMPTY_KPI);
  const [pareto, setPareto] = useState<LivePareto[]>([]);
  const [LINE_COMPARISON, setLineComparison] = useState<LiveLine[]>([]);
  const [trend, setTrend] = useState<LiveTrendPoint[]>([]);
  const [cadenceGapRows, setCadenceGapRows] = useState<OasCadenceGapEntryDto[]>([]);
  const [slaRules, setSlaRules] = useState<OasSlaRuleDto[]>([]);

  useEffect(() => {
    let cancelled = false;
    // Unlike liveState.ts's own polling (which catches and keeps last-known
    // state on a failed tick), these were unguarded — a single 4xx/5xx left
    // an unhandled rejection and the page silently stuck on stale/empty data
    // with no indication anything went wrong.
    void fetchLiveRange(range.from, range.to).then((r) => {
      if (cancelled) return;
      setKPI(r.kpi);
      setPareto(r.pareto);
      setLineComparison(r.lines);
      setTrend(r.trend);
    }).catch(() => { if (!cancelled) pushToast('common.actionFailed'); });
    void kpiApi.cadenceGap({ from: range.from, to: range.to }).then((rows) => {
      if (!cancelled) setCadenceGapRows(rows);
    }).catch(() => { if (!cancelled) pushToast('common.actionFailed'); });
    return () => { cancelled = true; };
    // `lang` triggers a re-fetch so pareto cause labels (resolved server-round-trip
    // side, bilingual data — never a static i18n key) refresh immediately on switch.
  }, [range.from, range.to, lang]);

  // SLA targets are admin-configurable (`/sla/rules`) — fetched once, not
  // tied to the period, and re-used to label the compliance table below
  // instead of a hardcoded per-kind constant that could silently drift.
  useEffect(() => {
    void slaApi.rules().then(setSlaRules).catch(() => pushToast('common.actionFailed'));
  }, []);

  const producedOk = KPI.producedOk;
  const producedNok = KPI.producedNok;
  const stopMinutes = KPI.stopMinutes;
  const stopsCount = Math.max(1, KPI.stopsCount);

  // BL-044 — every ratio below is guarded against a zero denominator so the
  // page never renders NaN/Infinity when a period has no stops or output.
  const maxTrend = Math.max(1, ...trend.map((x) => x.value));
  const maxPareto = Math.max(1, ...pareto.map((p) => p.minutes));
  const totalPareto = Math.max(1, pareto.reduce((a, p) => a + p.minutes, 0));
  const mtbf = stopsCount > 0 ? Math.max(0, Math.round((KPI.openingMin * days - stopMinutes) / stopsCount)) : 0;
  const scrapRate = ((producedNok / Math.max(1, producedOk + producedNok)) * 100).toFixed(1);

  /**
   * BL-035 — SLA compliance derived from the event circuit, no manual entry.
   * The target minutes come straight from the admin-configured rules
   * (`GET /sla/rules`) — the same domain-scoped, line-scoped, priority-
   * ordered table the server itself resolves an event's real SLA against —
   * never a client-side guess that could drift once an admin edits a rule.
   */
  const slaByService = useMemo(() => {
    const kinds = ['technical', 'quality', 'material', 'changeover'] as const;
    return kinds.map((k) => {
      const rows = events.filter((e) => e.kind === k);
      const onTime = rows.filter((e) => e.slaLeftMin >= 0).length;
      const matching = slaRules.filter((r) => r.isActive && r.eventType === KIND_TO_EVENT_TYPE[k]);
      const byPriorityDesc = (a: OasSlaRuleDto, b: OasSlaRuleDto) => b.priority - a.priority;
      const rule = matching.filter((r) => !r.lineId).sort(byPriorityDesc)[0] ?? matching.sort(byPriorityDesc)[0];
      return {
        kind: k,
        target: rule?.targetMin ?? null,
        total: rows.length,
        onTime,
        pct: rows.length ? Math.round((onTime / rows.length) * 100) : 100,
      };
    });
  }, [events, slaRules]);

  /**
   * BL-042 — cadence gap per post: the theoretical output the reference cadence
   * promises over the period, against what the posts actually declared,
   * server-computed (`GET /kpi/cadence-gap`). Sorted descending so the worst
   * offenders read as a Pareto.
   */
  const cadenceGap = useMemo(() => {
    const codeById = new Map(hierarchyPosts.map((p) => [p.id, p.code]));
    const refByPostId = new Map(refState.cadences.map((c) => [c.postId, c.ref]));
    return cadenceGapRows
      .map((r) => ({
        post: codeById.get(r.postId) ?? r.postId.slice(0, 8),
        ref: refByPostId.get(r.postId) ?? '—',
        theoretical: Math.round(r.theoreticalQty),
        actual: Math.round(r.actualQty),
        gap: Math.round(r.theoreticalQty - r.actualQty),
      }))
      .filter((x) => x.gap > 0)
      .sort((a, b) => b.gap - a.gap)
      .slice(0, 8);
  }, [cadenceGapRows, hierarchyPosts, refState.cadences]);

  const maxGap = Math.max(1, ...cadenceGap.map((g) => g.gap));

  /** BL-046 — adoption: share of posts actually declaring, over the 30-day ramp — real live post state, not a fixture. */
  const adoption = useMemo(() => {
    const total = livePosts.length;
    const active = livePosts.filter((p) => p.state !== 'idle').length;
    const dayInWindow = Math.min(
      30,
      Math.max(1, Math.round((Date.now() - Date.parse(ADOPTION_START)) / 86_400_000)),
    );
    return { total, active, pct: total ? Math.round((active / total) * 100) : 0, dayInWindow };
  }, [livePosts]);

  const periodLabel = period === 'custom' && from && to ? `${from}_${to}` : period;

  /** BL-044 — raw data exports over the filtered period. */
  const rawExports = [
    {
      key: 'events',
      rows: () => [
        ['ref', 'post', 'line', 'type', 'cause', 'stage', 'declared_at', 'assignee', 'sla_left_min'],
        ...events.map((e) => [e.ref, e.post, e.lineKey, e.kind, e.causeKey, e.stage, e.declaredAt, e.assignee ?? '', e.slaLeftMin]),
      ],
    },
    {
      key: 'stops',
      rows: () => [
        ['cause', 'type', 'lost_minutes'],
        ...pareto.map((p) => [p.causeKey, p.kind, p.minutes]),
      ],
    },
    {
      key: 'production',
      rows: () => [
        ['line', 'oee_pct', 'stops', 'scrap_pct'],
        ...LINE_COMPARISON.map((l) => [l.lineName, l.trs, l.stops, l.scrap]),
      ],
    },
    {
      key: 'sla',
      rows: () => [
        ['service', 'target_min', 'events', 'on_time', 'compliance_pct'],
        ...slaByService.map((s) => [s.kind, s.target ?? '', s.total, s.onTime, s.pct]),
      ],
    },
  ];


  return (
    <>
      <PageHeader
        title={t('web.reports.title')}
        subtitle={t('web.reports.subtitle')}
        actions={
          <div data-demo="reports-filter" className="flex flex-wrap items-center gap-2">
            <div className="w-40">
              <Select value={period} onChange={(e) => setPeriod(e.target.value as typeof period)}>
                {PERIODS.map((p) => (
                  <option key={p} value={p}>{t(`web.reports.period.${p}`)}</option>
                ))}
              </Select>
            </div>
            {period === 'custom' && (
              <div className="flex items-center gap-1">
                <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="h-9 w-36" aria-label={t('web.reports.from')} />
                <span className="text-muted-foreground">→</span>
                <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="h-9 w-36" aria-label={t('web.reports.to')} />
              </div>
            )}
            <Button
              size="sm"
              onClick={() =>
                csvExport(
                  [
                    [t('web.dash.line'), t('web.dash.trs'), t('web.dash.stops'), t('web.dash.scrap')],
                    ...LINE_COMPARISON.map((l) => [l.lineName, `${l.trs}`, `${l.stops}`, `${l.scrap}`]),
                  ],
                  `oas-report-${periodLabel}.csv`,
                )
              }
            >
              <Download className="me-1.5 h-3.5 w-3.5" />
              {t('web.reports.export')}
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={() =>
                excelExport(
                  [
                    [t('web.dash.line'), t('web.dash.trs'), t('web.dash.stops'), t('web.dash.scrap')],
                    ...LINE_COMPARISON.map((l) => [l.lineName, `${l.trs}`, `${l.stops}`, `${l.scrap}`]),
                  ],
                  `oas-report-${periodLabel}.xls`,
                )
              }
            >
              <FileSpreadsheet className="me-1.5 h-3.5 w-3.5" />
              {t('web.reports.exportExcel')}
            </Button>
          </div>
        }
      />


      <div className="space-y-4 p-4">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {[
            { label: t('web.reports.mtbf'), value: `${mtbf} ${t('common.min')}`, hint: t('web.reports.mtbfHint') },
            { label: t('web.dash.mttr'), value: `${KPI.mttrMin} ${t('common.min')}`, hint: t('kpi.mttr.sub') },
            { label: t('web.reports.scrapRate'), value: `${scrapRate}%`, hint: t('web.dash.scraps', { n: producedNok }) },
            { label: t('web.reports.lostTime'), value: `${stopMinutes} ${t('common.min')}`, hint: t('web.dash.stopsHint', { stops: stopsCount, minutes: stopMinutes }) },
          ].map((k) => (
            <Card key={k.label}>
              <CardContent className="p-3">
                <p className="text-overline uppercase text-muted-foreground">{k.label}</p>
                <p dir="ltr" className="mt-1 font-mono text-metric font-bold">{k.value}</p>
                <p className="text-caption text-muted-foreground">{k.hint}</p>
              </CardContent>
            </Card>
          ))}
        </div>

        <div className="grid gap-3 lg:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle>{t('web.dash.trend')}</CardTitle>
              <CardDescription>{t('web.dash.target')}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex h-40 items-end gap-2">
                {trend.map((x, i) => (
                  <div key={i} className="flex flex-1 flex-col items-center gap-1">
                    <span className="font-mono text-caption text-muted-foreground">{x.value}</span>
                    <div className="flex h-28 w-full items-end">
                      <div
                        className={`w-full rounded-t ${x.value >= 75 ? 'bg-state-production' : 'bg-foreground/70'}`}
                        style={{ height: `${(x.value / maxTrend) * 100}%` }}
                      />
                    </div>
                    <span className="font-mono text-caption text-muted-foreground">{x.label}</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t('web.dash.pareto')}</CardTitle>
              <CardDescription>{t('web.reports.paretoCumul')}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2.5">
              {pareto.map((p, i) => {
                const cumul = pareto.slice(0, i + 1).reduce((a, x) => a + x.minutes, 0);
                return (
                  <div key={p.causeKey}>
                    <div className="flex justify-between text-body-sm">
                      <span>{p.causeKey}</span>
                      <span dir="ltr" className="font-mono text-muted-foreground">
                        {p.minutes} {t('common.min')} · {Math.round((cumul / totalPareto) * 100)}%
                      </span>
                    </div>
                    <div className="mt-1 h-2 w-full rounded-full bg-muted">
                      <div className={`h-full rounded-full ${stateSolid[kindToState[p.kind]]}`}
                        style={{ width: `${(p.minutes / maxPareto) * 100}%` }} />
                    </div>
                  </div>
                );
              })}
            </CardContent>
          </Card>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>{t('web.reports.byLine')}</CardTitle>
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
                  <TableHead>{t('web.reports.mtbf')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {LINE_COMPARISON.map((l) => (
                  <TableRow key={l.lineId}>
                    <TableCell className="font-medium">{l.lineName}</TableCell>
                    <TableCell className="font-mono">{l.trs}%</TableCell>
                    <TableCell className="font-mono">{l.stops}</TableCell>
                    <TableCell className="font-mono">{l.scrap}%</TableCell>
                    <TableCell className="font-mono">{l.mtbfMin} {t('common.min')}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card data-demo="sla-service">
          <CardHeader>
            <CardTitle>{t('web.reports.sla.title')}</CardTitle>
            <CardDescription>{t('web.reports.sla.desc')}</CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('web.reports.sla.service')}</TableHead>
                  <TableHead>{t('web.reports.sla.target')}</TableHead>
                  <TableHead>{t('web.reports.sla.events')}</TableHead>
                  <TableHead>{t('web.reports.sla.onTime')}</TableHead>
                  <TableHead>{t('web.reports.sla.rate')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {slaByService.map((s) => (
                  <TableRow key={s.kind}>
                    <TableCell>{t(STATES[kindToState[s.kind]].labelKey)}</TableCell>
                    <TableCell dir="ltr" className="font-mono">{s.target != null ? `${s.target} ${t('common.min')}` : '—'}</TableCell>
                    <TableCell className="font-mono">{s.total}</TableCell>
                    <TableCell className="font-mono">{s.onTime}</TableCell>
                    <TableCell className={`font-mono ${s.pct >= 90 ? 'text-state-production' : 'text-state-technical'}`}>{s.pct}%</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        {/* BL-042 — cadence-gap Pareto: where the theoretical rate is not met. */}
        <Card data-demo="cadence-gap">
          <CardHeader>
            <CardTitle>{t('web.reports.gap.title')}</CardTitle>
            <CardDescription>{t('web.reports.gap.desc')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2.5">
            {cadenceGap.map((g) => (
              <div key={g.post} className="space-y-1">
                <div className="flex items-baseline justify-between text-body">
                  <span className="font-mono">{g.post} · {g.ref}</span>
                  <span dir="ltr" className="font-mono text-caption text-muted-foreground">
                    {g.actual}/{g.theoretical} · −{g.gap}
                  </span>
                </div>
                <span className="block h-2 overflow-hidden rounded-full bg-muted">
                  <span className="block h-full rounded-full bg-state-technical"
                    style={{ width: `${(g.gap / maxGap) * 100}%` }} />
                </span>
              </div>
            ))}
            {!cadenceGap.length && (
              <p className="text-caption text-muted-foreground">{t('web.reports.gap.none')}</p>
            )}
          </CardContent>
        </Card>

        {/* BL-046 — adoption over the 30-day ramp-up window. */}
        <Card data-demo="adoption">
          <CardHeader>
            <CardTitle>{t('web.reports.adoption.title')}</CardTitle>
            <CardDescription>{t('web.reports.adoption.desc')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex items-baseline justify-between">
              <span className="text-body">{t('web.reports.adoption.postsDeclaring')}</span>
              <span dir="ltr" className="font-mono text-h3">{adoption.pct}%</span>
            </div>
            <span className="block h-2 overflow-hidden rounded-full bg-muted">
              <span className={`block h-full rounded-full ${adoption.pct >= 80 ? 'bg-state-production' : 'bg-state-changeover'}`}
                style={{ width: `${adoption.pct}%` }} />
            </span>
            <p className="text-caption text-muted-foreground">
              {t('web.reports.adoption.detail', { active: adoption.active, total: adoption.total })}
            </p>
            <div className="rounded-lg border border-border bg-muted/40 p-2 text-caption">
              {adoption.dayInWindow < 30
                ? t('web.reports.adoption.ramp', { day: adoption.dayInWindow })
                : t('web.reports.adoption.stable')}
            </div>
          </CardContent>
        </Card>



        <Card data-demo="raw-export">
          <CardHeader>
            <CardTitle>{t('web.reports.raw.title')}</CardTitle>
            <CardDescription>{t('web.reports.raw.desc')}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {rawExports.map((x) => (
              <Button key={x.key} size="sm" variant="outline"
                onClick={() => csvExport(x.rows(), `oas-${x.key}-${periodLabel}.csv`)}>
                <Download className="me-1.5 h-3.5 w-3.5" /> {t(`web.reports.raw.${x.key}`)}
              </Button>
            ))}
            {rawExports.map((x) => (
              <Button key={`${x.key}-xls`} size="sm" variant="outline"
                onClick={() => excelExport(x.rows(), `oas-${x.key}-${periodLabel}.xls`)}>
                <FileSpreadsheet className="me-1.5 h-3.5 w-3.5" /> {t(`web.reports.raw.${x.key}`)} (Excel)
              </Button>
            ))}
          </CardContent>
        </Card>
      </div>

    </>
  );
}
