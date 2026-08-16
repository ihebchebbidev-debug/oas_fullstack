import { AlertTriangle, Timer } from 'lucide-react';
import { cn } from '@/lib/utils';
import { EVENT_STAGES, kindToState, type EventStage, type ShopEvent } from '@/oas/demo';
import { StateBadge, stateSolid, stateText } from './StateBadge';
import { useT } from '@/i18n/I18nProvider';

/** SLA countdown — turns red once the threshold is breached (spec §SLA). */
export function SlaCountdown({ minutes, className }: { minutes: number; className?: string }) {
  const t = useT();
  const breached = minutes < 0;
  const label = breached
    ? `+${Math.abs(minutes)} ${t('common.min')}`
    : `${minutes} ${t('common.min')}`;
  return (
    <span
      dir="ltr"
      className={cn(
        'inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 font-mono text-xs font-medium',
        breached
          ? 'bg-state-technical/15 text-state-technical'
          : minutes <= 5
            ? 'bg-state-material/15 text-state-material'
            : 'bg-muted text-muted-foreground',
        className,
      )}
    >
      {breached ? <AlertTriangle className="h-3 w-3" /> : <Timer className="h-3 w-3" />}
      {label}
    </span>
  );
}

/** Event circuit: declared -> notified -> en route -> on site -> resolved -> closed. */
export function EventTimeline({
  stage, className, compact,
}: { stage: EventStage; className?: string; compact?: boolean }) {
  const t = useT();
  const activeIndex = EVENT_STAGES.findIndex((s) => s.key === stage);
  return (
    <ol className={cn('flex items-center gap-1', className)}>
      {EVENT_STAGES.map((s, i) => (
        <li key={s.key} className="flex flex-1 flex-col items-center gap-1">
          <span className={cn('h-1.5 w-full rounded-full', i <= activeIndex ? 'bg-foreground' : 'bg-muted')} />
          {(!compact || i === activeIndex) && (
            <span
              className={cn(
                'whitespace-nowrap text-[0.625rem] leading-tight',
                i === activeIndex ? 'font-semibold text-foreground' : 'text-muted-foreground',
              )}
            >
              {t(s.labelKey)}
            </span>
          )}
        </li>
      ))}
    </ol>
  );
}

export function AlertFeedItem({
  event,
  onOpen,
  actionKey = 'common.open',
}: {
  event: ShopEvent;
  onOpen?: (e: ShopEvent) => void;
  actionKey?: string;
}) {
  const t = useT();
  const state = kindToState[event.kind];
  return (
    <button
      type="button"
      onClick={() => onOpen?.(event)}
      className="flex w-full min-h-[56px] items-center gap-3 rounded-lg border border-border bg-card p-3 text-start transition-colors hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className={cn('h-9 w-1 shrink-0 rounded-full', stateSolid[state])} aria-hidden />
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-2">
          <span className="font-mono text-xs text-muted-foreground">{event.post}</span>
          <span className="truncate text-sm font-medium text-foreground">{event.causeKey}</span>
        </span>
        <span className="mt-0.5 flex items-center gap-2 text-xs text-muted-foreground">
          <span className={stateText[state]}>{event.declaredAt}</span>
          <span>·</span>
          <span className="truncate">{event.assignee ?? t('common.notTaken')}</span>
        </span>
      </span>
      <span className="flex shrink-0 flex-col items-end gap-1">
        <SlaCountdown minutes={event.slaLeftMin} />
        <span className="text-[0.625rem] uppercase tracking-wide text-muted-foreground">{t(actionKey)}</span>
      </span>
    </button>
  );
}
