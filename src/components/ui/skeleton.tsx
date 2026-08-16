import { cn } from '@/lib/utils';

/** Base shimmer block used by every loading state in the app. */
export function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      aria-hidden
      className={cn(
        'relative overflow-hidden rounded-md bg-muted',
        'after:absolute after:inset-0 after:-translate-x-full after:animate-shimmer',
        'after:bg-gradient-to-r after:from-transparent after:via-foreground/10 after:to-transparent',
        className,
      )}
      {...props}
    />
  );
}
