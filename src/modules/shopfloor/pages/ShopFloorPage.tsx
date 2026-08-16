import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShopFloorMap } from '@/modules/shopfloor/components/ShopFloorMap';
import { type Post } from '@/oas/demo';
import { useLivePosts } from '@/oas/liveState';
import { StateBadge } from '@/oas/components/StateBadge';
import { useT } from '@/i18n/I18nProvider';
import { publishedOperator, useAssignments } from '@/oas/assignmentStore';

export default function ShopFloorPage() {
  const [selected, setSelected] = useState<Post | null>(null);
  const t = useT();
  const navigate = useNavigate();
  const assignments = useAssignments();
  const posts = useLivePosts();

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.map.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('mobile.map.subtitle')}</p>
      </header>

      <div data-demo="m-map">
        <ShopFloorMap posts={posts} compact onSelect={setSelected} />
      </div>

      {selected && (
        <div className="fixed inset-x-0 bottom-[56px] z-30 mx-auto w-full max-w-[430px] rounded-t-2xl border border-border bg-card p-4 shadow-2xl">
          <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-border" />
          <div className="flex items-center justify-between">
            <h2 className="font-mono text-lg font-bold">{selected.code}</h2>
            <StateBadge state={selected.state} />
          </div>
          <p className="mt-1 text-sm text-muted-foreground">{selected.lineKey}</p>
          <dl className="mt-3 grid grid-cols-2 gap-2 text-sm">
            <div>
              <dt className="text-xs text-muted-foreground">{t('mobile.map.operator')}</dt>
              <dd className="font-medium">
                {publishedOperator(assignments, selected.code) ?? selected.operator ?? t('common.notAssigned')}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">{t('web.floor.order')}</dt>
              <dd className="font-mono font-medium">{selected.order}</dd>
            </div>
          </dl>
          <div className="mt-4 flex gap-2">
            <button type="button" onClick={() => navigate('/mobile/inbox')}
              className="min-h-[56px] flex-1 rounded-xl bg-foreground text-sm font-semibold text-background">
              {t('mobile.inbox.title')}
            </button>
            <button type="button" onClick={() => setSelected(null)}
              className="min-h-[56px] flex-1 rounded-xl border border-border text-sm font-medium">
              {t('common.close')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
