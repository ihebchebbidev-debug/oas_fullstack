import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, ArrowUpCircle } from 'lucide-react';
import { ShopFloorMap } from '@/modules/shopfloor/components/ShopFloorMap';
import { StateBadge } from '@/oas/components/StateBadge';
import { type Post } from '@/oas/demo';
import { useLivePosts } from '@/oas/liveState';
import { useScope } from '@/oas/scope';
import { acknowledgeEvent, escalateEvent, useEvents } from '@/oas/eventStore';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/shared/components/PageHeader';
import { useT } from '@/i18n/I18nProvider';

export default function ShopFloorBoard() {
  const scope = useScope();
  // BL-012 — the board never shows lines outside the account perimeter.
  const posts = useLivePosts().filter((p) => scope.inScope(p.lineKey));
  const [selectedCode, setSelectedCode] = useState<string | null>(posts[2]?.code ?? null);
  const selected = posts.find((p) => p.code === selectedCode) ?? null;
  const setSelected = (p: Post | null) => setSelectedCode(p?.code ?? null);
  const t = useT();
  const navigate = useNavigate();
  const events = useEvents();
  const openEvent = selected
    ? events.find((e) => e.post === selected.code && e.stage !== 'closed')
    : undefined;


  return (
    <>
      <PageHeader
        title={t('web.floor.title')}
        subtitle={scope.wholeSite
          ? t('web.floor.subtitle')
          : `${t('web.floor.subtitle')} · ${t('web.scope.limited', { lines: scope.lines.join(', ') })}`}
        actions={
          <Button size="sm" variant="outline" onClick={() => navigate('/web/andon')}>
            {t('web.floor.andon')}
          </Button>
        }
      />
      <div className="grid gap-3 p-4 lg:grid-cols-[1fr_280px]">
        <Card data-demo="floor-map">
          <CardContent className="p-4">
            <ShopFloorMap posts={posts} onSelect={setSelected} />
          </CardContent>
        </Card>

        <Card data-demo="post-drawer" className="h-fit">
          <CardHeader>
            <CardTitle>{selected ? selected.code : t('web.floor.noPost')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {selected ? (
              <>
                <StateBadge state={selected.state} />
                <dl className="space-y-2 text-body-sm">
                  <div className="flex justify-between">
                    <dt className="text-muted-foreground">{t('web.floor.line')}</dt>
                    <dd>{selected.lineKey}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted-foreground">{t('web.floor.operator')}</dt>
                    <dd>{selected.operator ?? t('common.notAssigned')}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted-foreground">{t('web.floor.order')}</dt>
                    <dd className="font-mono">{selected.order}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted-foreground">{t('web.floor.reference')}</dt>
                    <dd className="font-mono">{selected.ref}</dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-muted-foreground">{t('web.floor.since')}</dt>
                    <dd className="font-mono">{selected.sinceMin} {t('common.min')}</dd>
                  </div>
                </dl>
                <div className="flex gap-2 pt-1">
                  <Button size="sm" className="flex-1"
                    onClick={() => navigate(`/web/assignments?post=${selected.code}`)}>
                    {t('common.assign')}
                  </Button>
                  <Button size="sm" variant="outline" className="flex-1"
                    onClick={() => navigate(`/web/alerts?post=${selected.code}`)}>
                    {t('common.history')}
                  </Button>
                </div>

                {openEvent && (
                  <div className="space-y-2 rounded-md border border-border p-2.5">
                    <p className="text-caption text-muted-foreground">
                      {openEvent.ref} · {openEvent.causeKey}
                    </p>
                    <div className="flex gap-2">
                      <Button size="sm" variant="outline" className="flex-1"
                        disabled={!!openEvent.acknowledgedAt}
                        onClick={() => void acknowledgeEvent(openEvent.id)}>
                        <Eye className="me-1.5 h-3.5 w-3.5" />
                        {openEvent.acknowledgedAt ? t('web.alerts.acked') : t('web.alerts.ack')}
                      </Button>
                      <Button size="sm" variant="outline" className="flex-1"
                        disabled={openEvent.escalation === 2}
                        onClick={() => void escalateEvent(openEvent.id)}>
                        <ArrowUpCircle className="me-1.5 h-3.5 w-3.5" /> {t('web.alerts.escalate')}
                      </Button>
                    </div>
                  </div>
                )}
              </>

            ) : (
              <p className="text-body-sm text-muted-foreground">{t('web.floor.pickHint')}</p>
            )}
          </CardContent>
        </Card>
      </div>
    </>
  );
}
