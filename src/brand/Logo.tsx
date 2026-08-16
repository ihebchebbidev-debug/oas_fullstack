import { cn } from '@/lib/utils';

/**
 * OAS brand mark — a hand-drawn geometric glyph on a 32×32 grid.
 * Three ascending bars (production rate) sitting inside an open bracket
 * (the workstation), with a scan corner at the top-right. Monochrome by
 * design: machine-state colors stay the only chromatic system in the product.
 */
function LogoMark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 32 32"
      role="img"
      aria-hidden="true"
      focusable="false"
      className={cn('h-8 w-8', className)}
    >
      {/* open bracket / workstation frame */}
      <path
        d="M11 3H6.5A3.5 3.5 0 0 0 3 6.5V25.5A3.5 3.5 0 0 0 6.5 29H11"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.4"
        strokeLinecap="square"
      />
      <path
        d="M21 3h4.5A3.5 3.5 0 0 1 29 6.5V25.5A3.5 3.5 0 0 1 25.5 29H21"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.4"
        strokeLinecap="square"
        opacity="0.4"
      />
      {/* ascending cadence bars */}
      <rect x="9.6" y="19" width="3.4" height="6" rx="1" fill="currentColor" opacity="0.55" />
      <rect x="14.3" y="14.5" width="3.4" height="10.5" rx="1" fill="currentColor" opacity="0.8" />
      <rect x="19" y="8.5" width="3.4" height="16.5" rx="1" fill="currentColor" />
    </svg>
  );
}

interface LogoProps {
  /** Height of the mark; the wordmark scales with it. */
  size?: 'sm' | 'md' | 'lg';
  /** Hide the wordmark and show the glyph only. */
  markOnly?: boolean;
  className?: string;
}

const SIZES = {
  sm: { mark: 'h-7 w-7', word: 'text-sm', tag: 'text-[0.5rem]' },
  md: { mark: 'h-9 w-9', word: 'text-lg', tag: 'text-[0.5625rem]' },
  lg: { mark: 'h-12 w-12', word: 'text-2xl', tag: 'text-[0.625rem]' },
} as const;

export function Logo({ size = 'md', markOnly = false, className }: LogoProps) {
  const s = SIZES[size];
  return (
    <span className={cn('inline-flex items-center gap-3', className)}>
      <span
        className={cn(
          'flex items-center justify-center rounded-[0.4em] bg-foreground text-background',
          s.mark,
        )}
      >
        <LogoMark className="h-[70%] w-[70%]" />
      </span>
      {!markOnly && (
        <span className="flex flex-col leading-none">
          <span className={cn('font-semibold tracking-[0.14em] text-foreground', s.word)}>OAS</span>
          <span
            className={cn(
              'mt-1 font-medium uppercase tracking-[0.22em] text-muted-foreground',
              s.tag,
            )}
          >
            Production Suite
          </span>
        </span>
      )}
    </span>
  );
}
