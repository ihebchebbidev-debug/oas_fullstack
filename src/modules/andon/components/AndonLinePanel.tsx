import { cn } from '@/lib/utils';
import { STATES, type MachineState } from '@/oas/demo';
import { useLivePosts } from '@/oas/liveState';
import { useT } from '@/i18n/I18nProvider';
import { StateDot, stateSoft, stateText } from '@/oas/components/StateBadge';

/**
 * Focused view of a single production line for the wallboard rotation.
 * Shows every post as an over-sized tile (readable at distance) plus
 * a mini state distribution.
 */
export function AndonLinePanel({ lineKey }: { lineKey: string }) {
  const t = useT();
  const posts = useLivePosts().filter((p) => p.lineKey === lineKey);
  const counts = posts.reduce<Record<MachineState, number>>(
    (acc, p) => ({ ...acc, [p.state]: (acc[p.state] ?? 0) + 1 }),
    { production: 0, material: 0, changeover: 0, technical: 0, quality: 0, idle: 0 },
  );

  return (
    <section className="flex h-full flex-col gap-4 rounded-2xl border border-border bg-card/40 p-4 shadow-xl backdrop-blur-sm xl:gap-6 xl:rounded-3xl xl:p-6 2xl:p-8">
      <header className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <h2 className="text-pretty text-xl font-black leading-tight tracking-tight text-foreground xl:text-2xl 2xl:text-3xl">
          {lineKey}
        </h2>
        <div className="flex items-center gap-1.5" aria-hidden>
          {posts.map((p) => (
            <StateDot key={p.id} state={p.state} className="h-3 w-3 xl:h-4 xl:w-4" />
          ))}
        </div>
        <span
          dir="ltr"
          className="ms-auto whitespace-nowrap rounded-full bg-muted px-3 py-1.5 text-[0.6rem] font-bold uppercase tracking-[0.12em] text-muted-foreground xl:text-[0.7rem]"
        >
          {posts.length} {t('andon.posts')}
        </span>
      </header>

      <div className="grid flex-1 grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-5 xl:gap-4 2xl:gap-6">
        {posts.map((p) => {
          const Icon = STATES[p.state].icon;
          const isStop = p.state !== 'production' && p.state !== 'idle';

          return (
            <div
              key={p.id}
              className={cn(
                'flex min-h-[8rem] flex-col justify-between gap-3 rounded-2xl border p-4 shadow-sm transition-all duration-500 xl:min-h-[11rem] xl:rounded-3xl 2xl:p-6',
                stateSoft[p.state],
                p.state === 'idle' && 'oas-hatch opacity-60',
                isStop && 'ring-2 ring-destructive/20',
              )}
            >
              <div className="flex items-start justify-between gap-2">
                <span className="font-mono text-xl font-black leading-none tracking-tight text-foreground xl:text-2xl 2xl:text-3xl">
                  {p.code}
                </span>
                <Icon
                  className={cn('h-5 w-5 shrink-0 xl:h-6 xl:w-6 2xl:h-7 2xl:w-7', stateText[p.state])}
                  aria-hidden
                  strokeWidth={2.5}
                />
              </div>

              <div className="flex flex-col gap-1">
                <span
                  className={cn(
                    'text-pretty text-[0.6rem] font-bold uppercase leading-[1.3] tracking-[0.1em] xl:text-[0.7rem]',
                    stateText[p.state],
                  )}
                >
                  {t(STATES[p.state].labelKey)}
                </span>
                <span dir="ltr" className="font-mono text-xs font-bold text-muted-foreground xl:text-sm">
                  {p.sinceMin} {t('common.min')}
                </span>
              </div>
            </div>
          );
        })}
      </div>

      <footer className="grid grid-cols-3 gap-3 border-t border-border pt-4 text-center sm:grid-cols-6 xl:gap-4 xl:pt-6">
        {Object.entries(counts).map(([state, n]) => (
          <div key={state} className="flex flex-col gap-1">
            <p
              className={cn(
                'font-mono text-2xl font-black leading-none tabular-nums xl:text-3xl',
                stateText[state as MachineState],
              )}
            >
              {n}
            </p>
            <p className="text-pretty text-[0.55rem] font-bold uppercase leading-[1.3] tracking-[0.1em] text-muted-foreground/70 xl:text-[0.65rem]">
              {t(STATES[state as MachineState].labelKey)}
            </p>
          </div>
        ))}
      </footer>
    </section>
  );
}
