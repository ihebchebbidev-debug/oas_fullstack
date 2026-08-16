import { useMemo, useState } from 'react';
import { FileText, Download, Save, FileSpreadsheet } from 'lucide-react';

import { STATES, kindToState } from '@/oas/demo';
import { useLiveKpi, useLiveLines, useLivePareto } from '@/oas/liveState';
import { useEvents } from '@/oas/eventStore';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/shared/components/PageHeader';
import { useT } from '@/i18n/I18nProvider';
import { csvExport } from '@/shared/lib/csv';
import { excelExport } from '@/shared/lib/excel';
import { exportPdf } from '@/shared/lib/pdf';
import { signShiftReport, useRefState } from '@/oas/refStore';

/**
 * BL-043 — the shift report writes itself: KPI summary, event chronology and
 * line results, plus the hand-over note the team leader adds before signing off.
 */
export default function ShiftReport() {
  const t = useT();
  const events = useEvents();
  const KPI = useLiveKpi();
  const PARETO = useLivePareto();
  const LINE_COMPARISON = useLiveLines();
  const { shifts, signOffs } = useRefState();
  const [shiftId, setShiftId] = useState<string>('');
  const [note, setNote] = useState('');
  const template = shifts.find((s) => s.id === shiftId) ?? shifts[0];
  const shift = template?.code ?? '';
  const lastSignOff = signOffs.find((s) => s.shift === shift) ?? null;

  /** BL-043 — the chronology only shows events whose time falls inside the
      selected shift window (morning 06–14, afternoon 14–22, night 22–06). */
  const inShiftWindow = (time: string) => {
    if (!template) return true;
    const [h, m] = time.split(':').map(Number);
    if (!Number.isFinite(h) || !Number.isFinite(m)) return true;
    const minutes = h * 60 + m;
    const [sh, sm] = template.start.split(':').map(Number);
    const [eh, em] = template.end.split(':').map(Number);
    const start = sh * 60 + sm;
    const end = eh * 60 + em;
    return start < end ? minutes >= start && minutes < end : minutes >= start || minutes < end;
  };

  const chronology = useMemo(
    () => [...events].filter((e) => inShiftWindow(e.declaredAt)).sort((a, b) => a.declaredAt.localeCompare(b.declaredAt)),
    [events, template],
  );

  const summary = [
    { label: t('web.dash.trs'), value: `${KPI.trs}%` },
    { label: t('web.shift.produced'), value: `${KPI.producedOk}` },
    { label: t('web.shift.scrap'), value: `${KPI.producedNok}` },
    { label: t('web.shift.downtime'), value: `${KPI.stopMinutes} ${t('common.min')}` },
  ];

  if (!template) {
    return <PageHeader title={t('web.shift.title')} subtitle={t('web.shift.subtitle')} />;
  }

  return (
    <>
      <PageHeader
        title={t('web.shift.title')}
        subtitle={t('web.shift.subtitle')}
        actions={
          <>
            <div className="w-40">
              <Select value={shiftId} onChange={(e) => setShiftId(e.target.value)}>
                {shifts.map((s) => (
                  <option key={s.id} value={s.id}>{s.name} · {s.start}–{s.end}</option>
                ))}
              </Select>
            </div>
            <Button size="sm" variant="outline" onClick={() =>
              csvExport(
                [
                  [t('web.alerts.ref'), t('web.alerts.post'), t('web.alerts.type'), t('web.alerts.cause'), t('web.shift.at'), t('web.alerts.assignee')],
                  ...chronology.map((e) => [e.ref, e.post, t(STATES[kindToState[e.kind]].labelKey), e.causeKey, e.declaredAt, e.assignee ?? '']),
                ],
                `oas-shift-report-${shift}.csv`,
              )
            }>
              <Download className="me-1.5 h-3.5 w-3.5" /> {t('web.reports.export')}
            </Button>
            <Button size="sm" variant="outline" onClick={() =>
              excelExport(
                [
                  [t('web.alerts.ref'), t('web.alerts.post'), t('web.alerts.type'), t('web.alerts.cause'), t('web.shift.at'), t('web.alerts.assignee')],
                  ...chronology.map((e) => [e.ref, e.post, t(STATES[kindToState[e.kind]].labelKey), e.causeKey, e.declaredAt, e.assignee ?? '']),
                ],
                `oas-shift-report-${shift}.xls`,
              )
            }>
              <FileSpreadsheet className="me-1.5 h-3.5 w-3.5" /> {t('web.reports.exportExcel')}
            </Button>
            {/* BL-043 — real PDF file, downloadable and shareable. */}
            <Button size="sm" onClick={() => exportPdf({
              title: t('web.shift.title'),
              subtitle: `${template.name} · ${template.start}–${template.end}`,
              summary,
              tables: [
                {
                  title: t('web.shift.chronology'),
                  head: [t('web.alerts.post'), t('web.alerts.type'), t('web.alerts.cause'), t('web.shift.at')],
                  rows: chronology.map((e) => [e.post, t(STATES[kindToState[e.kind]].labelKey), e.causeKey, new Date(e.declaredAt).toLocaleTimeString()]),
                },
                {
                  title: t('web.shift.lines'),
                  head: [t('web.dash.line'), t('web.dash.trs'), t('web.dash.stops'), t('web.shift.scrap')],
                  rows: LINE_COMPARISON.map((l) => [l.lineName, `${l.trs}%`, `${l.stops}`, `${l.scrap}`]),

                },
              ],
              footer: `${note || t('web.shift.noNote')} — ${lastSignOff ? `${t('web.shift.signedBy')} ${lastSignOff.by}` : t('web.shift.notSigned')}`,
              fileName: `oas-shift-report-${shift}.pdf`,
            })}>
              <FileText className="me-1.5 h-3.5 w-3.5" /> {t('web.shift.pdf')}
            </Button>
          </>
        }
      />

      <div className="space-y-4 p-4" data-demo="shift-report">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {summary.map((s) => (
            <Card key={s.label}>
              <CardContent className="p-3">
                <p className="text-overline uppercase text-muted-foreground">{s.label}</p>
                <p dir="ltr" className="mt-1 font-mono text-metric font-bold tabular-nums">{s.value}</p>
              </CardContent>
            </Card>
          ))}
        </div>

        <Card>
          <CardHeader>
            <CardTitle>{t('web.shift.chronology')}</CardTitle>
            <CardDescription>
              {template.name} · {template.start}–{template.end} · {t('web.shift.breaks', { n: template.breakMinutes })}
            </CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('web.shift.at')}</TableHead>
                  <TableHead>{t('web.alerts.post')}</TableHead>
                  <TableHead>{t('web.alerts.type')}</TableHead>
                  <TableHead>{t('web.alerts.cause')}</TableHead>
                  <TableHead>{t('web.alerts.assignee')}</TableHead>
                  <TableHead>{t('web.shift.stage')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {chronology.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell dir="ltr" className="font-mono">{e.declaredAt}</TableCell>
                    <TableCell className="font-mono">{e.post}</TableCell>
                    <TableCell>{t(STATES[kindToState[e.kind]].labelKey)}</TableCell>
                    <TableCell>{e.causeKey}</TableCell>
                    <TableCell>{e.assignee ?? '—'}</TableCell>
                    <TableCell>{t(`stage.${e.stage}`)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <div className="grid gap-3 lg:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle>{t('web.reports.byLine')}</CardTitle>
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
                    <TableRow key={l.lineId}>
                      <TableCell>{l.lineName}</TableCell>
                      <TableCell className="font-mono">{l.trs}%</TableCell>
                      <TableCell className="font-mono">{l.stops}</TableCell>
                      <TableCell className="font-mono">{l.scrap}%</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t('web.shift.topCauses')}</CardTitle>
              <CardDescription>{t('web.dash.paretoDesc')}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2">
              {PARETO.slice(0, 4).map((p) => (
                <div key={p.causeKey} className="flex justify-between text-body-sm">
                  <span>{p.causeKey}</span>
                  <span dir="ltr" className="font-mono text-muted-foreground">{p.minutes} {t('common.min')}</span>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>{t('web.shift.handover')}</CardTitle>
            <CardDescription>{t('web.shift.handoverDesc')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <Textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={4}
              placeholder={t('web.shift.handoverPlaceholder')}
            />
            <div className="flex items-center gap-3">
              <Button
                size="sm"
                disabled={!note.trim()}
                onClick={() => { void signShiftReport(template.id, note); setNote(''); }}
              >
                <Save className="me-1.5 h-3.5 w-3.5" /> {t('web.shift.sign')}
              </Button>
              {lastSignOff && (
                <span className="text-body-sm text-state-production">
                  {t('web.shift.signed', { at: new Date(lastSignOff.at).toLocaleTimeString() })} · {lastSignOff.by}
                </span>
              )}
            </div>
            {signOffs.length > 0 && (
              <ul className="space-y-1 border-t border-border pt-2">
                {signOffs.slice(0, 5).map((so) => (
                  <li key={so.id} className="flex flex-wrap gap-2 text-caption text-muted-foreground">
                    <span className="font-mono">{so.shift}</span>
                    <span>{new Date(so.at).toLocaleString()}</span>
                    <span className="font-medium text-foreground">{so.by}</span>
                    <span className="min-w-0 flex-1 truncate">{so.note}</span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </>
  );
}
