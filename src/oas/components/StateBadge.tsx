import { cn } from '@/lib/utils';
import { STATES, type MachineState } from '@/oas/demo';
import { useT } from '@/i18n/I18nProvider';

const TEXT: Record<MachineState, string> = {
  production: 'text-state-production',
  material: 'text-state-material',
  changeover: 'text-state-changeover',
  technical: 'text-state-technical',
  quality: 'text-state-quality',
  idle: 'text-state-idle',
};

const BG: Record<MachineState, string> = {
  production: 'bg-state-production/10 border-state-production/30',
  material: 'bg-state-material/10 border-state-material/30',
  changeover: 'bg-state-changeover/10 border-state-changeover/30',
  technical: 'bg-state-technical/10 border-state-technical/30',
  quality: 'bg-state-quality/10 border-state-quality/30',
  idle: 'bg-state-idle/10 border-state-idle/30',
};

const SOLID: Record<MachineState, string> = {
  production: 'bg-state-production',
  material: 'bg-state-material',
  changeover: 'bg-state-changeover',
  technical: 'bg-state-technical',
  quality: 'bg-state-quality',
  idle: 'bg-state-idle',
};

export const stateText = TEXT;
export const stateSoft = BG;
export const stateSolid = SOLID;

/** Color + icon + label — never color alone (colorblind requirement). */
export function StateBadge({
  state,
  size = 'md',
  className,
}: {
  state: MachineState;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}) {
  const t = useT();
  const meta = STATES[state];
  const Icon = meta.icon;
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border font-medium',
        BG[state],
        TEXT[state],
        size === 'sm' && 'px-2 py-0.5 text-[0.6875rem]',
        size === 'md' && 'px-2.5 py-1 text-xs',
        size === 'lg' && 'px-3.5 py-2 text-sm',
        className,
      )}
    >
      <Icon className={cn(size === 'lg' ? 'h-4 w-4' : 'h-3.5 w-3.5')} aria-hidden />
      {t(meta.labelKey)}
    </span>
  );
}

export function StateDot({ state, className }: { state: MachineState; className?: string }) {
  return <span className={cn('inline-block h-2 w-2 rounded-full', SOLID[state], className)} aria-hidden />;
}

export function StateLegend({ className }: { className?: string }) {
  const t = useT();
  return (
    <div className={cn('flex flex-wrap gap-x-4 gap-y-2', className)}>
      {Object.values(STATES).map((s) => (
        <span key={s.key} className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
          <StateDot state={s.key} />
          {t(s.labelKey)}
        </span>
      ))}
    </div>
  );
}
