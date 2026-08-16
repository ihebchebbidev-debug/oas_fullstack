import { AlertOctagon } from 'lucide-react';

import { cn } from '@/lib/utils';
import { STATES, type Post } from '@/oas/demo';
import { StateDot, StateLegend, stateSoft, stateText } from '@/oas/components/StateBadge';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { useT } from '@/i18n/I18nProvider';

function PostTile({
  post,
  onSelect,
  compact,
}: {
  post: Post;
  onSelect?: (post: Post) => void;
  compact?: boolean;
}) {
  const t = useT();
  const { posts: hierarchyPosts } = useHierarchyState();
  // BL-001 / BL-036 — a critical post is visible at a glance on the map.
  const critical = hierarchyPosts.find((p) => p.code === post.code)?.isCritical ?? false;
  const Icon = STATES[post.state].icon;
  return (
    <button
      type="button"
      onClick={() => onSelect?.(post)}
      className={cn(
        'flex w-full flex-col items-start gap-1 rounded-lg border p-2.5 text-start transition-colors',
        'hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        stateSoft[post.state],
        post.state === 'idle' && 'oas-hatch',
        compact ? 'min-h-[64px]' : 'min-h-[84px]',
      )}
    >
      <span className="flex w-full items-center gap-1.5">
        <Icon className={cn('h-3.5 w-3.5 shrink-0', stateText[post.state])} aria-hidden />
        <span className="font-mono text-xs font-medium text-foreground">{post.code}</span>
        {critical && (
          <AlertOctagon className="h-3.5 w-3.5 shrink-0 text-state-technical" aria-label={t('ref.critical')} />
        )}
        {post.isMine && (
          <span className="ms-auto rounded-full border border-border bg-background px-1.5 py-0.5 text-[0.625rem] font-semibold uppercase tracking-wide text-foreground">
            {t('common.take')}
          </span>
        )}
      </span>
      <span className={cn('text-[0.6875rem] font-medium', stateText[post.state])}>
        {t(STATES[post.state].labelKey)}
      </span>
      {!compact && (
        <span className="mt-auto flex w-full items-center justify-between text-[0.6875rem] text-muted-foreground">
          <span className="font-mono">{post.ref}</span>
          <span dir="ltr">{post.sinceMin} {t('common.min')}</span>
        </span>
      )}
    </button>
  );
}

export function ShopFloorMap({
  posts,
  onSelect,
  compact,
  className,
}: {
  posts: Post[];
  onSelect?: (post: Post) => void;
  compact?: boolean;
  className?: string;
}) {
  const lineNames = [...new Set(posts.map((p) => p.lineKey))];
  return (
    <div className={cn('space-y-4', className)}>
      {lineNames.map((lineName) => {
        const linePosts = posts.filter((p) => p.lineKey === lineName);
        if (!linePosts.length) return null;
        return (
          <section key={lineName}>
            <header className="mb-2 flex items-center gap-2">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{lineName}</h3>
              <span className="flex items-center gap-1">
                {linePosts.map((p) => (
                  <StateDot key={p.id} state={p.state} />
                ))}
              </span>
            </header>
            <div className={cn('grid gap-2', compact ? 'grid-cols-2 sm:grid-cols-3' : 'grid-cols-2 sm:grid-cols-3 lg:grid-cols-5')}>
              {linePosts.map((p) => (
                <PostTile key={p.id} post={p} onSelect={onSelect} compact={compact} />
              ))}
            </div>
          </section>
        );
      })}
      <StateLegend className="border-t border-border pt-3" />
    </div>
  );
}
