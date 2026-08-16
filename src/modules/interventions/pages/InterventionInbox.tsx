import { useMemo, useState } from 'react';
import { MapPin, UserX } from 'lucide-react';
import { AlertFeedItem, EventTimeline } from '@/oas/components/EventPieces';
import { type EventKind } from '@/oas/demo';
import { causeLabelIn, useRefState } from '@/oas/refStore';
import { arriveOnSite, closeEvent, declineEvent, etaOf, setEventEta, setResponderBusy, takeEvent, useEvents, useResponderBusy } from '@/oas/eventStore';
import { getAuth } from '@/oas/authStore';
import { cn } from '@/lib/utils';
import { useI18n, useT } from '@/i18n/I18nProvider';

const FILTERS: { key: 'all' | EventKind; labelKey: string }[] = [
  { key: 'all',        labelKey: 'common.all' },
  { key: 'technical',  labelKey: 'kind.technical' },
  { key: 'quality',    labelKey: 'kind.quality' },
  { key: 'material',   labelKey: 'kind.material' },
];

export default function InterventionInbox() {
  const t = useT();
  const [filter, setFilter] = useState<'all' | EventKind>('all');
  const [openId, setOpenId] = useState<string | null>(null);
  const events = useEvents();
  const profileId = getAuth()?.id;
  const unavailable = useResponderBusy(profileId);
  // Closed events leave the inbox — it only shows what still needs a responder.
  const list = events.filter((e) => e.stage !== 'closed' && (filter === 'all' || e.kind === filter));
  const open = events.find((e) => e.id === openId) ?? null;

  return (
    <div className="space-y-4">
      <header className="flex items-start justify-between gap-2">
        <div>
          <h1 className="text-[1.375rem] font-semibold">{t('mobile.inbox.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('common.eventsOpen', { n: list.length })}</p>
        </div>
        {profileId && (
          <button
            type="button"
            onClick={() => void setResponderBusy(profileId, !unavailable)}
            className={cn(
              'min-h-[40px] shrink-0 rounded-lg border px-3 text-xs font-medium transition-colors',
              unavailable
                ? 'border-state-technical bg-state-technical/10 text-state-technical'
                : 'border-border bg-card text-muted-foreground',
            )}
          >
            {unavailable ? t('mobile.inbox.unavailable') : t('mobile.inbox.available')}
          </button>
        )}
      </header>

      <div data-demo="m-inbox" className="flex gap-1 rounded-xl bg-muted p-1">
        {FILTERS.map((f) => (
          <button key={f.key} type="button" onClick={() => setFilter(f.key)}
            className={cn(
              'min-h-[40px] flex-1 rounded-lg text-xs font-medium transition-colors',
              filter === f.key ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground',
            )}
          >
            {t(f.labelKey)}
          </button>
        ))}
      </div>

      <div className="space-y-2">
        {list.map((e) => (
          <AlertFeedItem
            key={e.id}
            event={e}
            onOpen={(ev) => { setOpenId(ev.id); void takeEvent(ev.id); }}
            actionKey={e.assignee ? 'common.taken' : 'common.take'}
          />
        ))}
      </div>

      {open && (
        <InterventionPanel key={open.id} event={open} onDone={() => setOpenId(null)} />
      )}
    </div>
  );
}

function InterventionPanel({
  event,
  onDone,
}: {
  event: ReturnType<typeof useEvents>[number];
  onDone: () => void;
}) {
  const { t, lang } = useI18n();
  const { causeTree } = useRefState();
  const [closing, setClosing] = useState(false);
  const [causeCode, setCauseCode] = useState<string | null>(null);
  const [freeCause, setFreeCause] = useState('');

  const causeOptions = useMemo(
    () => causeTree.filter((r) => r.active && r.kind === event.kind),
    [causeTree, event.kind],
  );

  const arrived = event.stage === 'onsite' || event.stage === 'resolved' || event.stage === 'closed';

  const observedCauseOption = causeCode ? causeOptions.find((r) => r.code === causeCode) : undefined;
  const observedCause = causeCode
    ? (observedCauseOption ? causeLabelIn(observedCauseOption, lang) : causeCode)
    : freeCause.trim();

  function confirmClose() {
    if (!observedCause) return;
    void closeEvent(event.id, 'resolved', observedCause);
    onDone();
  }

  return (
    <section className="rounded-xl border border-border bg-card p-4">
      <div className="flex items-center justify-between">
        <h2 className="font-mono text-sm font-semibold">{event.ref}</h2>
        <button type="button" onClick={onDone} className="text-xs text-muted-foreground">
          {t('common.close')}
        </button>
      </div>
      <p className="mt-1 text-sm">{event.causeKey} — {event.post}</p>
      <EventTimeline stage={event.stage} className="mt-4" />

      {!arrived && (
        <div className="mt-4 grid grid-cols-2 gap-2">
          <button
            type="button"
            onClick={() => void arriveOnSite(event.id)}
            className="flex min-h-[56px] items-center justify-center gap-2 rounded-xl border border-foreground text-sm font-semibold text-foreground active:bg-muted"
          >
            <MapPin className="h-4 w-4" /> {t('mobile.inbox.arrived')}
          </button>
          {/* BL-032 — "occupé": the event is handed back and escalated. */}
          <button
            type="button"
            onClick={() => { void declineEvent(event.id); onDone(); }}
            className="flex min-h-[56px] items-center justify-center gap-2 rounded-xl border border-state-technical text-sm font-semibold text-state-technical active:bg-muted"
          >
            <UserX className="h-4 w-4" /> {t('mobile.inbox.busy')}
          </button>
        </div>
      )}


      <p className="mt-4 text-xs text-muted-foreground">{t('common.eta')}</p>
      <div className="mt-1 grid grid-cols-3 gap-2">
        {[5, 10, 15].map((eta) => (
          <button
            key={eta}
            type="button"
            onClick={() => void setEventEta(event.id, eta)}
            className={cn(
              'min-h-[56px] rounded-xl border text-sm font-semibold transition-colors',
              etaOf(event.id) === eta
                ? 'border-foreground bg-foreground text-background'
                : 'border-border bg-muted',
            )}
          >
            <span dir="ltr">{eta} {t('common.min')}</span>
          </button>
        ))}
      </div>

      {!closing && (
        <button
          type="button"
          onClick={() => setClosing(true)}
          className="mt-2 flex min-h-[56px] w-full items-center justify-center rounded-xl bg-foreground text-sm font-semibold text-background"
        >
          {t('web.alerts.close')}
        </button>
      )}

      {closing && (
        <div className="mt-3 space-y-2 rounded-xl border border-border p-3">
          <p className="text-xs font-medium text-muted-foreground">{t('mobile.inbox.observedCause')}</p>
          <div className="flex flex-wrap gap-2">
            {causeOptions.map((r) => (
              <button
                key={r.code}
                type="button"
                onClick={() => { setCauseCode(r.code); setFreeCause(''); }}
                className={cn(
                  'min-h-[40px] rounded-lg border px-3 text-xs font-medium',
                  causeCode === r.code ? 'border-foreground bg-foreground text-background' : 'border-border bg-muted',
                )}
              >
                {causeLabelIn(r, lang)}
              </button>
            ))}
          </div>
          <input
            value={freeCause}
            onChange={(e) => { setFreeCause(e.target.value); setCauseCode(null); }}
            placeholder={t('mobile.inbox.observedCause.placeholder')}
            className="h-11 w-full rounded-lg border border-border bg-background px-3 text-sm"
          />
          <button
            type="button"
            disabled={!observedCause}
            onClick={confirmClose}
            className="flex min-h-[56px] w-full items-center justify-center rounded-xl bg-foreground text-sm font-semibold text-background disabled:opacity-40"
          >
            {t('mobile.inbox.resolveConfirm')}
          </button>
        </div>
      )}
    </section>
  );
}
