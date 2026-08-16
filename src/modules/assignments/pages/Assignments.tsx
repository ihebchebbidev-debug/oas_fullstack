import { useSearchParams } from 'react-router-dom';
import { CheckCircle2, Eraser, Send, UserMinus, Users, Wand2 } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/shared/components/PageHeader';
import { useT } from '@/i18n/I18nProvider';
import { cn } from '@/lib/utils';
import {
  assignPost, assignmentCounts, autoFill, BOARD_POSTS, clearAll, presenceOf, publishAssignments,
  releasePost, ROSTER, useAssignments,
} from '@/oas/assignmentStore';
import { RosterPanel } from '../components/RosterPanel';
import { useScope } from '@/oas/scope';

/** M3 Affectations — assignment board driven by the shared assignment store. */
export default function Assignments() {
  const t = useT();
  const [params] = useSearchParams();
  // Deep link from the shop-floor board: /web/assignments?post=PO-103
  const focusPost = params.get('post');

  const scope = useScope();
  // BL-012 — a line supervisor only assigns the posts of his own lines.
  const boardPosts = BOARD_POSTS.filter((p) => scope.inScope(p.lineKey));
  const state = useAssignments();
  const { assigned, total, confirmed, pending, absent } = assignmentCounts(state);
  const published = Boolean(state.publishedAt);
  const taken = new Set(Object.values(state.assignments).filter(Boolean) as string[]);

  return (
    <>
      <PageHeader
        title={t('web.assign.title')}
        subtitle={t('web.assign.subtitle')}
        actions={
          <div data-demo="assign-actions" className="flex flex-wrap gap-2">
            <Button size="sm" variant="outline" onClick={autoFill}>
              <Wand2 className="me-1.5 h-3.5 w-3.5" /> {t('web.assign.autoFill')}
            </Button>
            <Button size="sm" variant="outline" onClick={clearAll}>
              <Eraser className="me-1.5 h-3.5 w-3.5" /> {t('web.assign.clearAll')}
            </Button>
            <Button size="sm" onClick={publishAssignments} disabled={published}>
              <Send className="me-1.5 h-3.5 w-3.5" />
              {published ? t('web.assign.published') : t('web.assign.publish')}
            </Button>
          </div>
        }
      />

      <div className="grid gap-3 p-4 lg:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{t('web.assign.kpi.assigned')}</CardDescription>
            <CardTitle className="font-mono text-2xl">{assigned}/{total}</CardTitle>
          </CardHeader>
        </Card>
        {/* BL-020 — presence split across the three lists. */}
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{t('web.assign.kpi.presence')}</CardDescription>
            <CardTitle className="flex items-baseline gap-2 font-mono text-2xl">
              <span className="text-state-production">{confirmed}</span>
              <span className="text-muted-foreground">/</span>
              <span className="text-state-material">{pending}</span>
              <span className="text-muted-foreground">/</span>
              <span className="text-destructive">{absent}</span>
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{t('web.assign.kpi.status')}</CardDescription>
            <CardTitle className="text-2xl">
              {published ? t('web.assign.published') : t('web.assign.draft')}
            </CardTitle>
          </CardHeader>
        </Card>
      </div>

      <div className="grid gap-4 px-4 pb-6 xl:grid-cols-[2fr_1fr]">
        <Card data-demo="assign-board">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Users className="h-4 w-4" /> {t('web.assign.board')}
            </CardTitle>
            <CardDescription>{t('web.assign.boardDesc')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {boardPosts.map((p) => {
              const operator = state.assignments[p.code] ?? '';
              return (
                <div
                  key={p.code}
                  className={cn(
                    'flex flex-wrap items-center gap-3 rounded-md border p-2.5 transition-colors hover:bg-muted/50',
                    focusPost === p.code ? 'border-foreground bg-muted/60' : 'border-border',
                  )}
                >
                  <span className="font-mono text-body font-semibold">{p.code}</span>
                  <Badge variant="ghost">{p.lineKey}</Badge>

                  <select
                    value={operator}
                    onChange={(e) => assignPost(p.code, e.target.value || null)}
                    className="h-8 min-w-[11rem] rounded-md border border-border bg-background px-2 text-body"
                    aria-label={t('web.assign.operator')}
                  >
                    <option value="">— {t('web.assign.unassigned')} —</option>
                    {/* BL-020 — absent operators drop out of the picker. */}
                    {ROSTER.filter((o) => presenceOf(state, o) !== 'absent' || o === operator).map((o) => (
                      <option key={o} value={o} disabled={taken.has(o) && o !== operator}>
                        {o}{presenceOf(state, o) === 'pending' ? ` · ${t('web.assign.presence.pending')}` : ''}
                      </option>
                    ))}
                  </select>

                  <Button
                    size="sm"
                    variant="ghost"
                    className="ms-auto"
                    disabled={!operator}
                    onClick={() => releasePost(p.code)}
                  >
                    <UserMinus className="me-1.5 h-3.5 w-3.5" /> {t('web.assign.release')}
                  </Button>
                </div>
              );
            })}
          </CardContent>
        </Card>

        <RosterPanel state={state} />
      </div>

      {published && (
        <p className="mx-4 mb-6 flex items-center gap-2 rounded-md border border-state-production/40 bg-state-production/10 p-3 text-body text-state-production">
          <CheckCircle2 className="h-4 w-4" />
          {t('web.assign.publishedHint')}{' '}
          <span dir="ltr" className="font-mono">
            {new Date(state.publishedAt!).toLocaleTimeString()}
          </span>
        </p>
      )}
    </>
  );
}
