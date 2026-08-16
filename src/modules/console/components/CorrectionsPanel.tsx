import { Fragment, useEffect, useState } from 'react';
import { PencilLine, ShieldAlert } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useT } from '@/i18n/I18nProvider';
import { declarationsApi, type OasDeclarationDto } from '@/oas/api/operations';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { useRefState } from '@/oas/refStore';
import { pushToast } from '@/shared/lib/toast';

const CORRECTION_WINDOW_MIN = 10;

function isCorrectable(d: OasDeclarationDto, now = Date.now()): boolean {
  return (now - Date.parse(d.occurredAt)) / 60_000 <= CORRECTION_WINDOW_MIN;
}

/**
 * BL-045 / BL-046 — corrections require a reason, are never deletions, and
 * stay visible. Rows come from `GET /declarations` (tenant-wide) rather than
 * the mobile operator's own local session cache: this is a console screen,
 * running in a different browser/device from whichever one wrote the entry.
 * A correction is always a NEW server row (`correctsId` set, the original
 * flagged `isCorrected`) — saving re-fetches the list instead of mutating
 * in place, to stay byte-for-byte with server truth.
 */
export default function CorrectionsPanel() {
  const t = useT();
  const { posts } = useHierarchyState();
  const { users } = useRefState();
  const [rows, setRows] = useState<OasDeclarationDto[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [editing, setEditing] = useState<string | null>(null);
  const [qty, setQty] = useState('');
  const [reason, setReason] = useState('');

  const load = () => {
    void declarationsApi.list()
      .then((data) => { setRows(data); setLoaded(true); })
      .catch(() => { setLoaded(true); pushToast('common.actionFailed'); });
  };
  useEffect(load, []);

  const postCodeOf = (id: string) => posts.find((p) => p.id === id)?.code ?? id.slice(0, 8);
  const userNameOf = (id: string) => users.find((u) => u.id === id)?.name ?? '—';
  const qtyOf = (d: OasDeclarationDto) => (d.kind === 'scrap' ? d.quantityNok : d.quantityOk);

  // Only originals get a "correct" action; the correction itself (if any) is
  // looked up per-row and rendered as the audit line underneath.
  const originals = rows.filter((d) => !d.correctsId);
  const correctionOf = (d: OasDeclarationDto) => rows.find((c) => c.correctsId === d.id);

  const start = (d: OasDeclarationDto) => {
    setEditing(d.id);
    setQty(String(qtyOf(d)));
    setReason('');
  };

  const apply = (d: OasDeclarationDto) => {
    const n = Number(qty);
    if (!Number.isFinite(n) || n < 0 || !reason.trim()) return;
    void declarationsApi
      .correct(d.id, {
        clientEventId: crypto.randomUUID(),
        quantityOk: d.kind === 'scrap' ? 0 : n,
        quantityNok: d.kind === 'scrap' ? n : 0,
        reason: reason.trim(),
      })
      .then(load)
      .catch(() => pushToast('common.actionFailed'));
    setEditing(null);
  };

  return (
    <Card data-demo="corrections">
      <CardHeader>
        <CardTitle>{t('web.admin.corr.title')}</CardTitle>
        <CardDescription>{t('web.admin.corr.desc')}</CardDescription>
      </CardHeader>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('web.shift.at')}</TableHead>
              <TableHead>{t('web.alerts.post')}</TableHead>
              <TableHead>{t('web.admin.audit.who')}</TableHead>
              <TableHead>{t('web.admin.corr.type')}</TableHead>
              <TableHead>{t('web.admin.corr.qty')}</TableHead>
              <TableHead className="text-end">{t('web.ref.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loaded && !originals.length && (
              <TableRow>
                <TableCell colSpan={6} className="p-6 text-center text-body-sm text-muted-foreground">
                  {t('web.admin.corr.empty')}
                </TableCell>
              </TableRow>
            )}
            {originals.map((d) => {
              const correction = correctionOf(d);
              return (
                <Fragment key={d.id}>
                  <TableRow>
                    <TableCell dir="ltr" className="font-mono">
                      {new Date(d.occurredAt).toLocaleTimeString()}
                    </TableCell>
                    <TableCell className="font-mono">{postCodeOf(d.postId)}</TableCell>
                    <TableCell>{userNameOf(d.userId)}</TableCell>
                    <TableCell>
                      {t(d.kind === 'scrap' ? 'web.admin.corr.kind.scrap' : 'web.admin.corr.kind.production')}
                    </TableCell>
                    <TableCell className="font-mono">
                      {correction ? (
                        <span dir="ltr">
                          <s className="text-muted-foreground">{qtyOf(d)}</s> → {qtyOf(correction)}
                        </span>
                      ) : (
                        qtyOf(d)
                      )}
                    </TableCell>
                    <TableCell className="text-end">
                      {!d.isCorrected && isCorrectable(d) && (
                        <Button size="sm" variant="outline" onClick={() => start(d)}>
                          <PencilLine className="me-1.5 h-3.5 w-3.5" /> {t('web.admin.corr.correct')}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>

                  {editing === d.id && (
                    <TableRow className="bg-muted/40">
                      <TableCell colSpan={6}>
                        <div className="flex flex-wrap items-end gap-2">
                          <div className="w-28">
                            <label className="text-caption text-muted-foreground">{t('web.admin.corr.newQty')}</label>
                            <Input value={qty} onChange={(e) => setQty(e.target.value)} inputMode="numeric" className="font-mono" />
                          </div>
                          <div className="min-w-[16rem] flex-1">
                            <label className="text-caption text-muted-foreground">{t('web.admin.corr.reason')}</label>
                            <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t('web.admin.corr.reasonPlaceholder')} />
                          </div>
                          <Button size="sm" disabled={!reason.trim()} onClick={() => apply(d)}>
                            {t('common.validate')}
                          </Button>
                          <Button size="sm" variant="ghost" onClick={() => setEditing(null)}>{t('common.cancel')}</Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  )}

                  {correction && (
                    <TableRow className="bg-muted/20">
                      <TableCell colSpan={6} className="text-caption text-muted-foreground">
                        <Badge variant="ghost" className="me-2">
                          <ShieldAlert className="me-1 h-3 w-3" /> {t('web.admin.corr.logged')}
                        </Badge>
                        {t('web.admin.corr.logLine', {
                          by: userNameOf(correction.createdBy),
                          at: new Date(correction.occurredAt).toLocaleTimeString(),
                          reason: correction.correctionReason ?? '',
                        })}
                      </TableCell>
                    </TableRow>
                  )}
                </Fragment>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
