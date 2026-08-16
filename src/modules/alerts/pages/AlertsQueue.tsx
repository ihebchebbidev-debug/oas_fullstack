import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Search } from 'lucide-react';

import { EVENT_STAGES, STATES, kindToState, type EventKind } from '@/oas/demo';
import { useEvents } from '@/oas/eventStore';
import { useScope } from '@/oas/scope';
import { EventDetailPanel } from '@/shared/components/EventDetailPanel';
import { EventTimeline, SlaCountdown } from '@/oas/components/EventPieces';
import { StateBadge } from '@/oas/components/StateBadge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/shared/components/PageHeader';
import { useT } from '@/i18n/I18nProvider';

const KIND_KEYS: EventKind[] = ['technical', 'quality', 'material', 'changeover'];

export default function AlertsQueue() {
  const t = useT();
  const [params] = useSearchParams();
  const scope = useScope();
  // BL-012 — only the events of the lines the account covers.
  const events = useEvents().filter((e) => scope.inScope(e.lineKey));
  // Deep link from the shop-floor board: /web/alerts?post=PO-103
  const [search, setSearch] = useState(() => params.get('post') ?? '');
  const [kind, setKind] = useState<'all' | EventKind>('all');
  const [stage, setStage] = useState('all');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = events.find((e) => e.id === selectedId) ?? null;

  const rows = useMemo(
    () =>
      events.filter((e) => {
        if (kind !== 'all' && e.kind !== kind) return false;
        if (stage !== 'all' && e.stage !== stage) return false;
        const q = search.toLowerCase();
        if (!q) return true;
        return (
          e.causeKey.toLowerCase().includes(q) ||
          e.post.toLowerCase().includes(q) ||
          e.ref.toLowerCase().includes(q)
        );
      }),
    [events, search, kind, stage, t],
  );

  return (
    <>
      <PageHeader
        title={t('web.alerts.title')}
        subtitle={scope.wholeSite
          ? t('web.alerts.subtitle', { n: rows.length })
          : `${t('web.alerts.subtitle', { n: rows.length })} · ${t('web.scope.limited', { lines: scope.lines.join(', ') })}`}
      />
      <div className="space-y-3 p-3 sm:p-4">
        <Card>
          <CardContent className="flex flex-wrap items-center gap-2 p-3">
            <div className="relative w-full sm:w-56">
              <Search className="pointer-events-none absolute start-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input value={search} onChange={(e) => setSearch(e.target.value)}
                placeholder={t('web.alerts.search')} className="ps-8" />
            </div>
            <div className="w-[calc(50%-0.25rem)] sm:w-44">
              <Select value={kind} onChange={(e) => setKind(e.target.value as 'all' | EventKind)}>
                <option value="all">{t('web.alerts.allKinds')}</option>
                {KIND_KEYS.map((k) => (
                  <option key={k} value={k}>{t(STATES[kindToState[k]].labelKey)}</option>
                ))}
              </Select>
            </div>
            <div className="w-[calc(50%-0.25rem)] sm:w-44">

              <Select value={stage} onChange={(e) => setStage(e.target.value)}>
                <option value="all">{t('web.alerts.allStages')}</option>
                {EVENT_STAGES.map((s) => (
                  <option key={s.key} value={s.key}>{t(s.labelKey)}</option>
                ))}
              </Select>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('web.alerts.ref')}</TableHead>
                  <TableHead>{t('web.alerts.post')}</TableHead>
                  <TableHead>{t('web.alerts.type')}</TableHead>
                  <TableHead>{t('web.alerts.cause')}</TableHead>
                  <TableHead className="w-64">{t('web.alerts.circuit')}</TableHead>
                  <TableHead>{t('web.alerts.assignee')}</TableHead>
                  <TableHead>{t('web.alerts.sla')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((e) => (
                  <TableRow key={e.id} data-demo="event-row" onClick={() => setSelectedId(e.id)} className="cursor-pointer">
                    <TableCell className="font-mono">{e.ref}</TableCell>
                    <TableCell className="font-mono">{e.post}</TableCell>
                    <TableCell><StateBadge state={kindToState[e.kind]} size="sm" /></TableCell>
                    <TableCell>{e.causeKey}</TableCell>
                    <TableCell><EventTimeline stage={e.stage} compact /></TableCell>
                    <TableCell>{e.assignee ?? '—'}</TableCell>
                    <TableCell><SlaCountdown minutes={e.slaLeftMin} /></TableCell>
                  </TableRow>
                ))}
                {!rows.length && (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      {t('web.alerts.empty')}
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
      {selected && <EventDetailPanel event={selected} onClose={() => setSelectedId(null)} />}
    </>
  );
}
