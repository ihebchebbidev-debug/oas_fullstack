import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Check, HelpCircle } from 'lucide-react';

import { cn } from '@/lib/utils';
import { STATES, kindToState, type EventKind } from '@/oas/demo';
import { stateSoft, stateText } from '@/oas/components/StateBadge';
import { causeLabelIn, proposeCause, useRefState, type CauseNode } from '@/oas/refStore';
import { useI18n } from '@/i18n/I18nProvider';
import { declareStop, useSessionState } from '@/modules/auth/store/session';

const FAMILIES: EventKind[] = ['technical', 'material', 'quality', 'changeover'];

type Reason = Pick<CauseNode, 'code' | 'label' | 'labelAr' | 'kind'>;

/** BL-026 — stop in 2 taps: family, then cause (most frequent first). */
export default function DeclareStop() {
  const navigate = useNavigate();
  const { t, lang } = useI18n();
  const state = useSessionState();
  const { causeTree, causeUsage } = useRefState();
  const [family, setFamily] = useState<EventKind | null>(null);
  const [missing, setMissing] = useState(false);
  const [note, setNote] = useState('');

  const reasons = useMemo<(Reason & { id: string })[]>(
    () => causeTree.filter((c) => c.active),
    [causeTree],
  );

  const familyReasons = useMemo(
    () =>
      reasons
        .filter((r) => r.kind === family)
        .sort((a, b) => (causeUsage[b.id] ?? 0) - (causeUsage[a.id] ?? 0)),
    [reasons, family, causeUsage],
  );

  useEffect(() => {
    if (!state.session) navigate('/mobile/scan', { replace: true });
  }, [state.session, navigate]);

  if (!state.session) return null;

  function pickReason(reason: Reason) {
    // `causeLabelKey` is opaque display text captured at declare-time, not
    // an i18n dictionary key — causes are admin-editable data, not static
    // UI strings, so there's no fixed key to translate through downstream.
    declareStop(reason.code, causeLabelIn(reason, lang), reason.kind);
    navigate('/mobile/home');
  }

  function confirmMissing() {
    // BL-008 — cause absent from the tree: log it and queue it for review.
    declareStop('AUTRE', 'mobile.stop.missing.label', 'technical', note.trim());
    void proposeCause(note.trim());
    navigate('/mobile/home');
  }

  return (
    <div className="space-y-4">
      <button
        type="button"
        onClick={() => (family ? setFamily(null) : navigate(-1))}
        className="inline-flex min-h-[44px] items-center gap-1.5 text-sm text-muted-foreground"
      >
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" /> {t('common.back')}
      </button>

      <header>
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.stop.title')}</h1>
        <p className="text-sm text-muted-foreground">
          {family
            ? t('mobile.stop.pickCause')
            : t('mobile.stop.context', { post: state.session.postCode, order: state.session.order })}
        </p>
      </header>

      {!family && !missing && (
        <div data-demo="m-stop-causes" className="grid grid-cols-2 gap-3">
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
                <span className="text-[0.625rem] text-muted-foreground">
                  {t('mobile.stop.causeCount', { n: reasons.filter((r) => r.kind === k).length })}
                </span>
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
                onClick={() => pickReason(reason)}
                className={cn(
                  'flex min-h-[72px] w-full items-center gap-3 rounded-xl border p-3 text-start transition-transform active:scale-[0.99]',
                  stateSoft[st],
                )}
              >
                <Icon className={cn('h-6 w-6 shrink-0', stateText[st])} aria-hidden />
                <span className="flex-1 text-base font-semibold text-foreground">
                  {causeLabelIn(reason, lang)}
                </span>
                <span className="font-mono text-[0.625rem] text-muted-foreground">{reason.code}</span>
              </button>
            );
          })}
          {!familyReasons.length && (
            <p className="text-sm text-muted-foreground">{t('mobile.stop.noCause')}</p>
          )}
        </div>
      )}

      {!family && (
        <>
          <button
            type="button"
            onClick={() => setMissing((v) => !v)}
            className={cn(
              'flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl border text-sm font-medium',
              missing ? 'border-foreground bg-muted text-foreground' : 'border-dashed border-border text-muted-foreground',
            )}
          >
            <HelpCircle className="h-5 w-5" /> {t('mobile.stop.missing.cta')}
          </button>

          {missing && (
            <>
              <textarea
                value={note}
                onChange={(e) => setNote(e.target.value)}
                rows={3}
                placeholder={t('mobile.stop.missing.placeholder')}
                className="w-full rounded-xl border border-border bg-card p-3 text-sm"
              />
              <button
                type="button"
                disabled={note.trim().length < 3}
                onClick={confirmMissing}
                className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background disabled:opacity-40"
              >
                <Check className="h-5 w-5" />
                {t('mobile.stop.confirm')}
              </button>
            </>
          )}
        </>
      )}
    </div>
  );
}
