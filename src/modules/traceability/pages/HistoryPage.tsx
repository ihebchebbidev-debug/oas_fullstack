import { useState } from 'react';
import { History, Wrench, CheckCircle2, AlertTriangle, Pencil } from 'lucide-react';

import { useT } from '@/i18n/I18nProvider';
import { correctDeclaration, isCorrectable, useSessionState } from '@/modules/auth/store/session';
import { cn } from '@/lib/utils';

type Filter = 'all' | 'production' | 'stops';

interface Row {
  id: string;
  at: string;
  kind: 'production' | 'scrap' | 'rework' | 'stop';
  label: string;
  qty: number | null;
  synced: boolean;
  correctable: boolean;
  corrected: boolean;
  deferred: boolean;
}

export default function HistoryPage() {
  const t = useT();
  const state = useSessionState();
  const [filter, setFilter] = useState<Filter>('all');
  const [editing, setEditing] = useState<string | null>(null);
  const [qty, setQty] = useState(0);
  const [reason, setReason] = useState('');

  const rows: Row[] = [
    ...(filter === 'stops' ? [] : state.declarations.map((d) => ({
      id: d.clientEventId,
      at: d.occurredAt,
      kind: d.kind,
      label: d.kind === 'scrap' ? t('mobile.prod.scrap') : t('mobile.prod.good'),
      qty: d.quantityOk + d.quantityNok,
      synced: d.synced,
      correctable: isCorrectable(d),
      corrected: Boolean(d.correctedAt),
      deferred: Boolean(d.deferredStopId),
    }))),
    ...(filter === 'production' ? [] : state.stops.map((s) => ({
      id: s.clientEventId,
      at: s.declaredAt,
      kind: 'stop' as const,
      label: s.note ? `${t(s.causeLabelKey)} — ${s.note}` : t(s.causeLabelKey),
      qty: null,
      synced: s.synced,
      correctable: false,
      corrected: false,
      deferred: false,
    }))),
  ].sort((a, b) => Date.parse(b.at) - Date.parse(a.at));

  function startEdit(r: Row) {
    setEditing(r.id);
    setQty(r.qty ?? 0);
    setReason('');
  }

  function saveEdit(r: Row) {
    const isScrap = r.kind === 'scrap';
    correctDeclaration(r.id, isScrap ? 0 : qty, isScrap ? qty : 0, reason.trim());
    setEditing(null);
  }

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.history.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('mobile.history.subtitle')}</p>
      </header>

      <div data-demo="m-history" className="flex gap-2">
        {(['all', 'production', 'stops'] as Filter[]).map((f) => (
          <button
            key={f}
            type="button"
            onClick={() => setFilter(f)}
            className={cn(
              'min-h-[44px] flex-1 rounded-lg border text-sm font-medium transition-colors',
              filter === f ? 'border-foreground bg-foreground text-background' : 'border-border text-muted-foreground',
            )}
          >
            {t(`mobile.history.filter.${f}`)}
          </button>
        ))}
      </div>

      {rows.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border p-10 text-center text-muted-foreground">
          <History className="h-7 w-7" />
          <p className="text-sm">{t('mobile.history.empty')}</p>
        </div>
      ) : (
        <ul className="space-y-2">
          {rows.map((r) => (
            <li key={r.id} className="rounded-xl border border-border bg-card p-3">
              <div className="flex items-center gap-3">
                <span
                  className={cn(
                    'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg',
                    r.kind === 'stop' ? 'bg-state-technical/15 text-state-technical'
                      : r.kind === 'scrap' ? 'bg-state-quality/15 text-state-quality'
                        : 'bg-state-production/15 text-state-production',
                  )}
                >
                  {r.kind === 'stop' ? <Wrench className="h-5 w-5" />
                    : r.kind === 'scrap' ? <AlertTriangle className="h-5 w-5" />
                      : <CheckCircle2 className="h-5 w-5" />}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium">{r.label}</span>
                  <span dir="ltr" className="block font-mono text-xs text-muted-foreground">
                    {new Date(r.at).toLocaleTimeString()}
                    {r.corrected && ` · ${t('mobile.history.corrected')}`}
                    {r.deferred && ` · ${t('mobile.history.deferred')}`}
                  </span>
                </span>
                <span className="flex flex-col items-end gap-1">
                  {r.qty !== null && <span dir="ltr" className="font-mono text-sm font-semibold">+{r.qty}</span>}
                  <span className="text-[0.625rem] uppercase tracking-wide text-muted-foreground">
                    {r.synced ? t('mobile.history.synced') : t('mobile.history.queued')}
                  </span>
                </span>
                {r.correctable && editing !== r.id && (
                  <button
                    type="button"
                    onClick={() => startEdit(r)}
                    aria-label={t('mobile.history.correct')}
                    className="flex h-10 w-10 items-center justify-center rounded-lg border border-border text-muted-foreground"
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                )}
              </div>

              {editing === r.id && (
                <div className="mt-3 space-y-2 border-t border-border pt-3">
                  <p className="text-xs text-muted-foreground">{t('mobile.history.correctWindow')}</p>
                  <input
                    type="number"
                    min={0}
                    value={qty}
                    onChange={(e) => setQty(Math.max(0, Number(e.target.value)))}
                    className="h-12 w-full rounded-lg border border-border bg-background px-3 font-mono text-lg"
                    aria-label={t('mobile.history.newQty')}
                  />
                  <input
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder={t('mobile.history.reason')}
                    className="h-12 w-full rounded-lg border border-border bg-background px-3 text-sm"
                  />
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => setEditing(null)}
                      className="min-h-[48px] flex-1 rounded-lg border border-border text-sm font-medium text-muted-foreground"
                    >
                      {t('common.cancel')}
                    </button>
                    <button
                      type="button"
                      disabled={reason.trim().length < 3}
                      onClick={() => saveEdit(r)}
                      className="min-h-[48px] flex-1 rounded-lg bg-foreground text-sm font-semibold text-background disabled:opacity-40"
                    >
                      {t('common.validate')}
                    </button>
                  </div>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
