import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { Printer, RefreshCw } from 'lucide-react';

import { useHierarchyState } from '@/oas/hierarchyStore';
import { hierarchyApi } from '@/oas/api/hierarchy';
import { useRefState } from '@/oas/refStore';
import { useAuditLog } from '@/oas/auditStore';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/shared/components/PageHeader';
import { cn } from '@/lib/utils';
import { useT } from '@/i18n/I18nProvider';
import ImportPanel from '../components/ImportPanel';
import UsersPanel from '../components/UsersPanel';
import CorrectionsPanel from '../components/CorrectionsPanel';
import PluginsPanel from '../components/PluginsPanel';
import { csvExport } from '@/shared/lib/csv';
import { excelExport } from '@/shared/lib/excel';
import { pushToast } from '@/shared/lib/toast';

const TABS = ['qr', 'users', 'shifts', 'plugins', 'import', 'corrections', 'audit'] as const;
type Tab = (typeof TABS)[number];

/** BL-046 — audit entries are kept 2 years (IATF 16949); older rows are purged from the view. */
const RETENTION_MS = 2 * 365 * 24 * 60 * 60 * 1000;

export default function Admin() {
  const t = useT();
  // The URL is the source of truth so deep links (and the guided tour) can open any tab.
  const [params, setParams] = useSearchParams();
  const tab: Tab = (TABS as readonly string[]).includes(params.get('tab') ?? '')
    ? (params.get('tab') as Tab)
    : 'qr';
  const setTab = (next: Tab) => setParams({ tab: next }, { replace: true });
  const [auditQuery, setAuditQuery] = useState('');
  const [auditFrom, setAuditFrom] = useState('');
  const [auditTo, setAuditTo] = useState('');

  const { shifts } = useRefState();
  const { posts, lines } = useHierarchyState();
  const lineNameById = new Map(lines.map((l) => [l.id, l.name]));

  /** BL-046 — server-generated + rotated QR tokens (never derived from the post code client-side). */
  const [tokens, setTokens] = useState<Record<string, string>>({});
  useEffect(() => {
    if (tab !== 'qr') return;
    void hierarchyApi.qrTokens()
      .then((rows) => setTokens(Object.fromEntries(rows.map((r) => [r.postId, r.token]))))
      .catch(() => pushToast('common.actionFailed'));
  }, [tab]);

  const rotateToken = (postId: string) => {
    void hierarchyApi.rotateQrToken(postId)
      .then((r) => setTokens((prev) => ({ ...prev, [postId]: r.token })))
      .catch(() => pushToast('common.actionFailed'));
  };

  /** BL-046 — retention window + free-text filter over the live audit journal. */
  const log = useAuditLog();
  const auditRows = useMemo(() => {
    const cutoff = Date.now() - RETENTION_MS;
    const q = auditQuery.trim().toLowerCase();
    const fromMs = auditFrom ? Date.parse(auditFrom) : null;
    // BL-046 — the "to" bound is inclusive of the whole day.
    const toMs = auditTo ? Date.parse(auditTo) + 24 * 60 * 60 * 1000 : null;
    return log
      .filter((e) => Date.parse(e.at) >= cutoff)
      .filter((e) => (fromMs == null || Date.parse(e.at) >= fromMs) && (toMs == null || Date.parse(e.at) < toMs))
      .filter((e) =>
        !q ||
        e.actor.toLowerCase().includes(q) ||
        e.action.toLowerCase().includes(q) ||
        e.entity.toLowerCase().includes(q) ||
        e.detail.toLowerCase().includes(q),
      );
  }, [log, auditQuery, auditFrom, auditTo]);

  return (
    <>
      <PageHeader title={t('web.admin.title')} subtitle={t('web.admin.subtitle')} />

      <div className="space-y-3 p-4">
        <div className="flex flex-wrap gap-1 rounded-lg border border-border bg-card p-1">
          {TABS.map((k) => (
            <button
              key={k}
              type="button"
              onClick={() => setTab(k)}
              className={cn(
                'rounded-md px-3 py-1.5 text-body-sm font-medium transition-colors',
                tab === k ? 'bg-foreground text-background' : 'text-muted-foreground hover:bg-accent',
              )}
            >
              {t(`web.admin.tab.${k}`)}
            </button>
          ))}
        </div>

        {tab === 'qr' && (
          <Card data-demo="qr-panel">
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <div>
                <CardTitle>{t('web.admin.qr.title')}</CardTitle>
                <CardDescription>{t('web.admin.qr.desc')}</CardDescription>
              </div>
              <Button size="sm" onClick={() => window.print()}>
                <Printer className="me-1.5 h-3.5 w-3.5" /> {t('web.admin.qr.print')}
              </Button>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-3 xl:grid-cols-5">
                {posts.filter((p) => !p.archived).map((p) => {
                  const token = tokens[p.id];
                  return (
                    <div key={p.id} className="flex flex-col items-center gap-2 rounded-md border border-border bg-background p-3">
                      {token
                        ? <QRCodeSVG value={`oas://post/${token}`} size={96} bgColor="transparent" fgColor="currentColor" />
                        : <div className="flex h-24 w-24 items-center justify-center text-caption text-muted-foreground">…</div>}
                      <p className="font-mono text-body font-semibold">{p.code}</p>
                      <p className="text-caption text-muted-foreground">{lineNameById.get(p.lineId) ?? '—'}</p>
                      <Button size="sm" variant="ghost" onClick={() => rotateToken(p.id)}>
                        <RefreshCw className="me-1.5 h-3.5 w-3.5" /> {t('web.admin.qr.rotate')}
                      </Button>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        )}

        {tab === 'users' && <UsersPanel />}

        {tab === 'shifts' && (
          <Card data-demo="shifts-panel">
            <CardHeader>
              <CardTitle>{t('web.admin.shifts.title')}</CardTitle>
              <CardDescription>{t('web.admin.shifts.desc')}</CardDescription>
            </CardHeader>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('web.ref.code')}</TableHead>
                    <TableHead>{t('web.ref.label')}</TableHead>
                    <TableHead>{t('web.admin.shifts.range')}</TableHead>
                    <TableHead>{t('web.admin.shifts.break')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {shifts.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell className="font-mono">{s.code}</TableCell>
                      <TableCell>{s.name}</TableCell>
                      <TableCell dir="ltr" className="font-mono">{s.start} → {s.end}</TableCell>
                      <TableCell className="font-mono">{s.breakMinutes} {t('common.min')}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        )}

        {tab === 'plugins' && <PluginsPanel />}

        {tab === 'import' && <ImportPanel />}

        {tab === 'corrections' && <CorrectionsPanel />}

        {tab === 'audit' && (
          <Card data-demo="audit-panel">
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <div>
                <CardTitle>{t('web.admin.audit.title')}</CardTitle>
                <CardDescription>{t('web.admin.audit.desc')}</CardDescription>
              </div>
              <div className="flex flex-wrap items-center gap-2">
              <input
                type="date"
                value={auditFrom}
                onChange={(e) => setAuditFrom(e.target.value)}
                aria-label={t('web.reports.from')}
                className="h-9 rounded-md border border-border bg-background px-2 text-body-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <span className="text-muted-foreground">→</span>
              <input
                type="date"
                value={auditTo}
                onChange={(e) => setAuditTo(e.target.value)}
                aria-label={t('web.reports.to')}
                className="h-9 rounded-md border border-border bg-background px-2 text-body-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <input
                value={auditQuery}
                onChange={(e) => setAuditQuery(e.target.value)}
                placeholder={t('web.admin.audit.search')}
                className="h-9 w-56 rounded-md border border-border bg-background px-3 text-body-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <Button size="sm" variant="outline" onClick={() => csvExport(
                [[t('web.admin.audit.when'), t('web.admin.audit.who'), t('web.admin.audit.action'), t('web.admin.audit.entity'), t('web.admin.audit.detail')],
                 ...auditRows.map((e) => [new Date(e.at).toISOString(), e.actor, e.action, e.entity, e.detail])],
                'oas-audit-trail.csv')}>
                {t('web.admin.audit.export')}
              </Button>
              <Button size="sm" variant="outline" onClick={() => excelExport(
                [[t('web.admin.audit.when'), t('web.admin.audit.who'), t('web.admin.audit.action'), t('web.admin.audit.entity'), t('web.admin.audit.detail')],
                 ...auditRows.map((e) => [new Date(e.at).toISOString(), e.actor, e.action, e.entity, e.detail])],
                'oas-audit-trail.xls')}>
                {t('web.reports.exportExcel')}
              </Button>
              </div>
            </CardHeader>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('web.admin.audit.when')}</TableHead>
                    <TableHead>{t('web.admin.audit.who')}</TableHead>
                    <TableHead>{t('web.admin.audit.action')}</TableHead>
                    <TableHead>{t('web.admin.audit.entity')}</TableHead>
                    <TableHead>{t('web.admin.audit.detail')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {auditRows.map((e) => (
                    <TableRow key={e.id}>
                      <TableCell dir="ltr" className="font-mono">
                        {new Date(e.at).toLocaleString()}
                      </TableCell>
                      <TableCell>{e.actor}</TableCell>
                      <TableCell className="font-mono text-caption">{e.action}</TableCell>
                      <TableCell className="font-mono">{e.entity}</TableCell>
                      <TableCell className="text-caption text-muted-foreground">{e.detail || '—'}</TableCell>
                    </TableRow>
                  ))}
                  {!auditRows.length && (
                    <TableRow>
                      <TableCell colSpan={5} className="p-6 text-center text-body-sm text-muted-foreground">
                        {t('web.admin.audit.empty')}
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
              <p className="p-3 text-caption text-muted-foreground">
                {t('web.admin.audit.retention', { count: auditRows.length })}
              </p>
            </CardContent>
          </Card>
        )}
      </div>
    </>
  );
}
