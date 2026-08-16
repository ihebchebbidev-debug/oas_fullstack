import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

function CardBlock({ className }: { className?: string }) {
  return (
    <div className={cn('rounded-lg border border-border bg-card p-4', className)}>
      <Skeleton className="h-3 w-24" />
      <Skeleton className="mt-3 h-7 w-20" />
      <Skeleton className="mt-2 h-2.5 w-16" />
    </div>
  );
}

/** Generic supervisor/manager page skeleton: header + KPI rail + charts + table. */
export function WebPageSkeleton() {
  return (
    <div role="status" aria-busy="true" className="animate-fade-in">
      <div className="flex h-12 items-center gap-3 border-b border-border px-4">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-3 w-24" />
      </div>
      <div className="space-y-4 p-4">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          {Array.from({ length: 5 }).map((_, i) => <CardBlock key={i} />)}
        </div>
        <div className="grid gap-3 lg:grid-cols-2">
          {Array.from({ length: 2 }).map((_, i) => (
            <div key={i} className="rounded-lg border border-border bg-card p-4">
              <Skeleton className="h-4 w-36" />
              <Skeleton className="mt-2 h-3 w-52" />
              <div className="mt-4 flex h-40 items-end gap-2">
                {[70, 45, 88, 60, 95, 52, 78].map((h, j) => (
                  <Skeleton key={j} className="flex-1 rounded-t" style={{ height: `${h}%` }} />
                ))}
              </div>
            </div>
          ))}
        </div>
        <div className="rounded-lg border border-border bg-card p-4">
          <Skeleton className="h-4 w-40" />
          <div className="mt-4 space-y-2.5">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="flex gap-3">
                <Skeleton className="h-3.5 flex-[2]" />
                <Skeleton className="h-3.5 flex-1" />
                <Skeleton className="h-3.5 flex-1" />
                <Skeleton className="h-3.5 flex-1" />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

/** Generic operator page skeleton: status card, action tiles and a list. */
export function MobilePageSkeleton() {
  return (
    <div role="status" aria-busy="true" className="animate-fade-in space-y-4">
      <div className="rounded-xl border border-border bg-card p-4">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="mt-3 h-8 w-32" />
        <Skeleton className="mt-3 h-2.5 w-full rounded-full" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-24 rounded-xl" />
        ))}
      </div>
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex items-center gap-3 rounded-lg border border-border bg-card p-3">
            <Skeleton className="h-9 w-9 rounded-lg" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-3 w-1/2" />
              <Skeleton className="h-2.5 w-1/3" />
            </div>
            <Skeleton className="h-5 w-12 rounded-full" />
          </div>
        ))}
      </div>
    </div>
  );
}
