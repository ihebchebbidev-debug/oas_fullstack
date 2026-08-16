/**
 * Console → Plugins panel.
 *
 * Single place where an administrator sees the module catalogue: which
 * plugin code owns which surface, what it depends on, and whether it is
 * active in this installation. Toggling cascades through the dependency
 * graph (enabling pulls dependencies in, disabling pushes dependents out).
 */
import { useMemo } from 'react';
import { AlertTriangle, Lock, RotateCcw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { useT } from '@/i18n/I18nProvider';
import { usePlugins } from '@/modules/shared/plugins';
import type { PluginCategory } from '@/modules/shared/plugins';

const CATEGORY_ORDER: PluginCategory[] = [
  'app',
  'production',
  'planning',
  'quality',
  'analytics',
  'system',
];

export default function PluginsPanel() {
  const t = useT();
  const { plugins, toggle, reset, stats } = usePlugins();

  const grouped = useMemo(
    () =>
      CATEGORY_ORDER.map((category) => ({
        category,
        rows: plugins.filter((p) => p.manifest.category === category),
      })).filter((g) => g.rows.length > 0),
    [plugins],
  );

  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-4 space-y-0">
        <div>
          <CardTitle>{t('plugins.title')}</CardTitle>
          <CardDescription>{t('plugins.subtitle')}</CardDescription>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Badge variant="secondary" dir="ltr">
            {stats.active}/{stats.total}
          </Badge>
          <Button variant="outline" size="sm" onClick={reset}>
            <RotateCcw className="me-1.5 h-3.5 w-3.5" />
            {t('plugins.reset')}
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        {grouped.map(({ category, rows }) => (
          <section key={category} className="space-y-2">
            <h3 className="text-caption font-semibold uppercase tracking-[0.04em] text-muted-foreground">
              {t(`plugins.category.${category}`)}
            </h3>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[9.5rem]">{t('plugins.col.code')}</TableHead>
                  <TableHead>{t('plugins.col.module')}</TableHead>
                  <TableHead>{t('plugins.col.workspaces')}</TableHead>
                  <TableHead>{t('plugins.col.dependencies')}</TableHead>
                  <TableHead className="text-end">{t('plugins.col.state')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map(({ manifest, isEnabled, hasBrokenDependency, enabledDependents }) => (
                  <TableRow key={manifest.code} className={cn(!isEnabled && 'opacity-60')}>
                    <TableCell className="font-mono text-caption" dir="ltr">
                      {manifest.code}
                    </TableCell>
                    <TableCell>
                      <span className="font-medium">{manifest.moduleKey}</span>
                      <span className="ms-2 text-caption text-muted-foreground" dir="ltr">
                        v{manifest.version}
                      </span>
                      {hasBrokenDependency && (
                        <span className="ms-2 inline-flex items-center gap-1 text-caption text-state-technical">
                          <AlertTriangle className="h-3 w-3" />
                          {t('plugins.brokenDependency')}
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-caption text-muted-foreground">
                      {manifest.workspaces.join(' · ')}
                    </TableCell>
                    <TableCell className="font-mono text-caption text-muted-foreground" dir="ltr">
                      {manifest.dependencies.length ? manifest.dependencies.join(', ') : '—'}
                    </TableCell>
                    <TableCell className="text-end">
                      {manifest.isCore ? (
                        <Badge variant="outline" className="gap-1">
                          <Lock className="h-3 w-3" />
                          {t('plugins.core')}
                        </Badge>
                      ) : (
                        <Button
                          variant={isEnabled ? 'secondary' : 'outline'}
                          size="sm"
                          onClick={() => toggle(manifest.code, !isEnabled)}
                          title={
                            isEnabled && enabledDependents.length
                              ? t('plugins.cascadeHint')
                              : undefined
                          }
                        >
                          {isEnabled ? t('plugins.enabled') : t('plugins.disabled')}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </section>
        ))}
      </CardContent>
    </Card>
  );
}
