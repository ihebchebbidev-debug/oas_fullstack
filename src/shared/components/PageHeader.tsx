/**
 * Page title bar. On phones the action cluster drops onto its own scrollable
 * row so buttons never overlap the title; from `sm` up it sits inline.
 */
export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
}) {
  return (
    <header className="sticky top-0 z-20 flex flex-col gap-2 border-b border-border bg-background/90 px-3 py-2 backdrop-blur sm:h-12 sm:flex-row sm:items-center sm:gap-3 sm:px-4 sm:py-0">
      <div className="min-w-0">
        <h1 className="truncate text-title font-heading">{title}</h1>
        {subtitle && <p className="truncate text-caption text-muted-foreground">{subtitle}</p>}
      </div>
      {actions && (
        <div className="-mx-3 flex items-center gap-2 overflow-x-auto px-3 pb-0.5 sm:mx-0 sm:ms-auto sm:overflow-visible sm:px-0 sm:pb-0">
          {actions}
        </div>
      )}
    </header>
  );
}
