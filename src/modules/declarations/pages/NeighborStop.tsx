import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Check, MapPin } from 'lucide-react';

import { cn } from '@/lib/utils';
import { STATES, kindToState, type EventKind } from '@/oas/demo';
import { stateSoft, stateText } from '@/oas/components/StateBadge';
import { causeLabelIn, useRefState, type CauseNode } from '@/oas/refStore';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { useI18n } from '@/i18n/I18nProvider';
import { findPost } from '@/modules/auth/store/session';
import { addEvent } from '@/oas/eventStore';
import { pushToast } from '@/shared/lib/toast';

const FAMILIES: EventKind[] = ['technical', 'material', 'quality', 'changeover'];

type Reason = Pick<CauseNode, 'id' | 'code' | 'label' | 'labelAr' | 'kind'>;

/**
 * BL-029 — declare a stop on a *neighbouring* post, scanned without opening a
 * session there. Stops only: no production entry, no session, no takeover of
 * the colleague's post. The alert reaches the support services immediately.
 */
export default function NeighborStop() {
  const navigate = useNavigate();
  const { t, lang } = useI18n();
  const [params] = useSearchParams();
  const code = (params.get('post') ?? '').toUpperCase();
  const post = findPost(code);
  const { posts: hierarchyPosts } = useHierarchyState();
  const { causeTree, causeUsage } = useRefState();
  const [family, setFamily] = useState<EventKind | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const reasons = useMemo<Reason[]>(() => causeTree.filter((c) => c.active), [causeTree]);
  const familyReasons = useMemo(
    () =>
      reasons
        .filter((r) => r.kind === family)
        .sort((a, b) => (causeUsage[b.id] ?? 0) - (causeUsage[a.id] ?? 0)),
    [reasons, family, causeUsage],
  );

  if (!post) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={() => navigate('/mobile/scan')}
          className="inline-flex min-h-[44px] items-center gap-1.5 text-sm text-muted-foreground"
        >
          <ArrowLeft className="h-4 w-4 rtl:rotate-180" /> {t('common.back')}
        </button>
        <p className="text-sm text-state-technical">{t('mobile.scan.unknown')}</p>
      </div>
    );
  }

  async function pick(reason: Reason) {
    const hierarchyPost = hierarchyPosts.find((p) => p.code === post!.code);
    if (!hierarchyPost) return;
    try {
      await addEvent({ postId: hierarchyPost.id, kind: reason.kind, causeId: reason.id });
      setDone(causeLabelIn(reason, lang));
    } catch {
      pushToast('common.actionFailed');
    }
  }

  if (done) {
    return (
      <div className="space-y-4">
        <div className="flex flex-col items-center gap-3 rounded-xl border border-state-production/50 bg-state-production/10 p-6 text-center">
          <Check className="h-10 w-10 text-state-production" />
          <p className="text-base font-semibold">{t('mobile.neighbor.sent', { post: post.code })}</p>
          <p className="text-sm text-muted-foreground">{t('mobile.neighbor.sentHint')}</p>
        </div>
        <button
          type="button"
          onClick={() => navigate('/mobile/home')}
          className="min-h-[48px] w-full rounded-xl bg-foreground text-sm font-semibold text-background"
        >
          {t('common.done')}
        </button>
      </div>
    );
  }

  return (
    <div data-demo="m-neighbor" className="space-y-4">
      <button
        type="button"
        onClick={() => (family ? setFamily(null) : navigate('/mobile/scan'))}
        className="inline-flex min-h-[44px] items-center gap-1.5 text-sm text-muted-foreground"
      >
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" /> {t('common.back')}
      </button>

      <header className="space-y-1">
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.neighbor.title')}</h1>
        <p className="inline-flex items-center gap-1.5 text-sm text-muted-foreground">
          <MapPin className="h-4 w-4" /> {post.code} · {post.lineKey}
        </p>
        <p className="text-xs text-muted-foreground">{t('mobile.neighbor.hint')}</p>
      </header>

      {!family && (
        <div className="grid grid-cols-2 gap-3">
          {FAMILIES.map((k) => {
            const st = kindToState[k];
            const Icon = STATES[st].icon;
            return (
              <button
                key={k}
                type="button"
                onClick={() => setFamily(k)}
                className={cn(
                  'flex min-h-[112px] flex-col items-center justify-center gap-2 rounded-xl border p-3 text-center transition-transform active:scale-[0.98]',
                  stateSoft[st],
                )}
              >
                <Icon className={cn('h-8 w-8', stateText[st])} aria-hidden />
                <span className="text-base font-semibold text-foreground">{t(STATES[st].labelKey)}</span>
              </button>
            );
          })}
        </div>
      )}

      {family && (
        <div className="space-y-2">
          {familyReasons.map((reason) => {
            const st = kindToState[reason.kind];
            const Icon = STATES[st].icon;
            return (
              <button
                key={reason.code}
                type="button"
                onClick={() => void pick(reason)}
                className={cn(
                  'flex min-h-[72px] w-full items-center gap-3 rounded-xl border p-3 text-start transition-transform active:scale-[0.99]',
                  stateSoft[st],
                )}
              >
                <Icon className={cn('h-6 w-6 shrink-0', stateText[st])} aria-hidden />
                <span className="flex-1 text-base font-semibold text-foreground">
                  {causeLabelIn(reason, lang)}
                </span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
