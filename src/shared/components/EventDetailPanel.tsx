import { X, UserCheck, CheckCircle2, ChevronRight, Eye, ArrowUpCircle } from 'lucide-react';

import { EVENT_STAGES, STATES, kindToState, type EventKind } from '@/oas/demo';
import {
  acknowledgeEvent, advanceEvent, closeEvent, escalateEvent, etaOf, requalifyEvent, takeEvent, type LiveEvent,
} from '@/oas/eventStore';
import { EventTimeline, SlaCountdown } from '@/oas/components/EventPieces';
import { StateBadge } from '@/oas/components/StateBadge';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { useT } from '@/i18n/I18nProvider';

const KINDS: EventKind[] = ['technical', 'quality', 'material', 'changeover'];

/** Slide-over with the full event circuit — declared → closed (spec §Event detail). */
export function EventDetailPanel({ event, onClose }: { event: LiveEvent; onClose: () => void }) {
  const t = useT();
  const activeIndex = EVENT_STAGES.findIndex((s) => s.key === event.stage);
  const eta = etaOf(event.id);
  const acknowledged = !!event.acknowledgedAt;
  const closed = event.stage === 'closed';


  return (
    <div className="fixed inset-0 z-40 flex justify-end">
      <button
        type="button"
        aria-label={t('common.close')}
        onClick={onClose}
        className="flex-1 backdrop-blur-[1px]"
        style={{ background: 'hsl(var(--overlay) / 0.4)' }}
      />
      <aside data-demo="event-panel" className="flex h-full w-full max-w-md flex-col overflow-y-auto border-s border-border bg-card shadow-2xl">
        <header className="sticky top-0 flex items-start gap-3 border-b border-border bg-card px-4 py-3">
          <div className="min-w-0">
            <p className="font-mono text-caption text-muted-foreground">{event.ref}</p>
            <h2 className="truncate text-title font-heading">{event.causeKey}</h2>
          </div>
          <button type="button" data-demo="drawer-close" onClick={onClose} className="ms-auto rounded p-1 text-muted-foreground hover:bg-accent">
            <X className="h-4 w-4" />
          </button>
        </header>

        <div className="space-y-4 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <StateBadge state={kindToState[event.kind]} size="sm" />
            <SlaCountdown minutes={event.slaLeftMin} />
            {eta != null && (
              <span dir="ltr" className="rounded-md bg-muted px-1.5 py-0.5 font-mono text-caption text-muted-foreground">
                {t('common.eta')} {eta} {t('common.min')}
              </span>
            )}
            {acknowledged && (
              <span className="rounded-md bg-state-production/15 px-1.5 py-0.5 text-caption text-state-production">
                {t('web.alerts.acked')}
              </span>
            )}
            {!!event.escalation && (
              <span className="rounded-md bg-state-technical/15 px-1.5 py-0.5 text-caption text-state-technical">
                {t('web.alerts.escalationLevel', { n: event.escalation })}
              </span>
            )}
          </div>


          <dl className="grid grid-cols-2 gap-3 rounded-md border border-border p-3 text-body-sm">
            <div>
              <dt className="text-caption text-muted-foreground">{t('web.alerts.post')}</dt>
              <dd className="font-mono font-medium">{event.post}</dd>
            </div>
            <div>
              <dt className="text-caption text-muted-foreground">{t('web.dash.line')}</dt>
              <dd className="font-medium">{event.lineKey}</dd>
            </div>
            <div>
              <dt className="text-caption text-muted-foreground">{t('web.alerts.type')}</dt>
              <dd className="font-medium">{t(STATES[kindToState[event.kind]].labelKey)}</dd>
            </div>
            <div>
              <dt className="text-caption text-muted-foreground">{t('web.alerts.assignee')}</dt>
              <dd className="font-medium">{event.assignee ?? t('common.notAssigned')}</dd>
            </div>
          </dl>

          <section>
            <h3 className="text-body-sm font-semibold">{t('web.alerts.circuit')}</h3>
            <ol className="mt-2 space-y-0">
              {EVENT_STAGES.map((s, i) => (
                <li key={s.key} className="flex gap-3">
                  <div className="flex flex-col items-center">
                    <span className={`mt-1 h-2.5 w-2.5 rounded-full ${i <= activeIndex ? 'bg-foreground' : 'bg-muted'}`} />
                    {i < EVENT_STAGES.length - 1 && (
                      <span className={`w-px flex-1 ${i < activeIndex ? 'bg-foreground' : 'bg-border'}`} />
                    )}
                  </div>
                  <div className="pb-4">
                    <p className={`text-body-sm ${i === activeIndex ? 'font-semibold' : 'text-muted-foreground'}`}>
                      {t(s.labelKey)}
                    </p>
                    {i <= activeIndex && (
                      <p dir="ltr" className="font-mono text-caption text-muted-foreground">{event.declaredAt}</p>
                    )}
                  </div>
                </li>
              ))}
            </ol>
          </section>

          {closed ? (
            <p className="flex items-center gap-2 rounded-md border border-state-production/40 bg-state-production/10 p-3 text-body-sm text-state-production">
              <CheckCircle2 className="h-4 w-4" /> {t('common.eventClosed')}
            </p>
          ) : (
            <div data-demo="event-actions" className="flex flex-wrap gap-2">
              <Button size="sm" className="flex-1" disabled={!!event.assignee} onClick={() => void takeEvent(event.id)}>
                <UserCheck className="me-1.5 h-3.5 w-3.5" />
                {event.assignee ? t('common.taken') : t('common.take')}
              </Button>
              <Button size="sm" variant="outline" className="flex-1" onClick={() => void advanceEvent(event.id)}>
                <ChevronRight className="me-1.5 h-3.5 w-3.5 rtl:rotate-180" /> {t('common.advance')}
              </Button>
              <Button size="sm" variant="outline" className="flex-1" onClick={() => void closeEvent(event.id)}>
                <CheckCircle2 className="me-1.5 h-3.5 w-3.5" /> {t('web.alerts.close')}
              </Button>
              <Button size="sm" variant="outline" className="flex-1" disabled={acknowledged}
                onClick={() => void acknowledgeEvent(event.id)}>
                <Eye className="me-1.5 h-3.5 w-3.5" /> {acknowledged ? t('web.alerts.acked') : t('web.alerts.ack')}
              </Button>
              <Button size="sm" variant="outline" className="flex-1" disabled={event.escalation === 2}
                onClick={() => void escalateEvent(event.id)}>
                <ArrowUpCircle className="me-1.5 h-3.5 w-3.5" /> {t('web.alerts.escalate')}
              </Button>
              <div className="w-full">
                <label className="text-caption text-muted-foreground">{t('web.alerts.requalify')}</label>
                <Select
                  value={event.kind}
                  onChange={(e) => void requalifyEvent(event.id, e.target.value as EventKind)}
                  className="mt-1 h-8"
                >
                  {KINDS.map((k) => (
                    <option key={k} value={k}>{t(STATES[kindToState[k]].labelKey)}</option>
                  ))}
                </Select>
              </div>
            </div>
          )}

        </div>
      </aside>
    </div>
  );
}
