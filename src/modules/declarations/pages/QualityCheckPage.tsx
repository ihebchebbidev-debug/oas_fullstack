import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, ClipboardCheck, Check, X, Wrench, Loader2 } from 'lucide-react';
import { useT, useI18n } from '@/i18n/I18nProvider';
import { cn } from '@/lib/utils';
import { useSessionState } from '@/modules/auth/store/session';
import { causeLabelIn, useRefState } from '@/oas/refStore';
import { qualityChecksApi, qualityTemplatesApi, type OasQualityCheckTemplateDto } from '@/oas/api/operations';
import { ApiError } from '@/oas/api/client';
import { pushToast } from '@/shared/lib/toast';

const CHECK_TYPES = ['first_part', 'in_process', 'final'] as const;
const RESULTS = ['ok', 'rework', 'scrap'] as const;

const uid = () =>
  (globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`);

/** BL — quality control: a real oas_quality_checks row (spec §6.1), not folded into a generic declaration. Template selection is optional (TemplateId is nullable server-side) — this records the check itself; per-item template checklists are a further admin-defined refinement not built here. */
export default function QualityCheckPage() {
  const navigate = useNavigate();
  const t = useT();
  const { lang } = useI18n();
  const state = useSessionState();
  const { causeTree } = useRefState();

  const [checkType, setCheckType] = useState<(typeof CHECK_TYPES)[number]>('in_process');
  const [templates, setTemplates] = useState<OasQualityCheckTemplateDto[]>([]);
  const [templateId, setTemplateId] = useState('');
  const [result, setResult] = useState<(typeof RESULTS)[number] | null>(null);
  const [quantityChecked, setQuantityChecked] = useState('1');
  const [quantityRejected, setQuantityRejected] = useState('0');
  const [causeId, setCauseId] = useState('');
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  useEffect(() => {
    if (!state.session) navigate('/mobile/scan', { replace: true });
  }, [state.session, navigate]);

  useEffect(() => {
    void qualityTemplatesApi.list().then(setTemplates).catch(() => setTemplates([]));
  }, []);

  const templatesForType = useMemo(
    () => templates.filter((tpl) => tpl.isActive && tpl.checkType === checkType),
    [templates, checkType],
  );
  useEffect(() => { setTemplateId(''); }, [checkType]);

  const qualityCauses = useMemo(
    () => causeTree.filter((c) => c.active && c.kind === 'quality'),
    [causeTree],
  );

  const needsCause = result === 'rework' || result === 'scrap';
  const canSubmit = !!state.session && !!result && (!needsCause || !!causeId) && !submitting;

  async function submit() {
    if (!state.session || !result || !canSubmit) return;
    setSubmitting(true);
    setError(null);
    try {
      await qualityChecksApi.create({
        clientEventId: uid(),
        postId: state.session.postId,
        templateId: templateId || undefined,
        checkType,
        result,
        quantityChecked: Number(quantityChecked) || 1,
        quantityRejected: needsCause ? Number(quantityRejected) || 0 : 0,
        causeId: needsCause ? causeId : undefined,
        note: note.trim() || undefined,
        occurredAt: new Date().toISOString(),
      });
      setDone(true);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.actionFailed'));
      pushToast('common.actionFailed');
    } finally {
      setSubmitting(false);
    }
  }

  if (!state.session) return null;

  if (done) {
    return (
      <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 text-center">
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-state-production/10 text-state-production">
          <Check className="h-8 w-8" />
        </div>
        <p className="text-lg font-semibold">{t('mobile.quality.recorded')}</p>
        <button type="button" onClick={() => navigate('/mobile/home')}
          className="flex min-h-[56px] w-full max-w-xs items-center justify-center rounded-xl bg-foreground text-sm font-semibold text-background">
          {t('common.back')}
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <button type="button" onClick={() => navigate(-1)}
        className="inline-flex min-h-[44px] items-center gap-1.5 text-sm text-muted-foreground">
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" /> {t('common.back')}
      </button>

      <header className="rounded-xl border border-border bg-card p-4">
        <p className="inline-flex items-center gap-2 text-sm font-semibold">
          <ClipboardCheck className="h-4 w-4" /> {t('mobile.quality.header')}
        </p>
        <p className="mt-1 text-xs text-muted-foreground">{t('mobile.quality.subtitle')}</p>
      </header>

      <section className="space-y-2 rounded-xl border border-border bg-card p-4">
        <p className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">{t('mobile.quality.checkType')}</p>
        <div className="grid grid-cols-3 gap-2">
          {CHECK_TYPES.map((ct) => (
            <button key={ct} type="button" onClick={() => setCheckType(ct)}
              className={cn(
                'min-h-[52px] rounded-lg border px-2 text-xs font-semibold',
                checkType === ct ? 'border-foreground bg-foreground text-background' : 'border-border bg-card',
              )}>
              {t(`mobile.quality.type.${ct}`)}
            </button>
          ))}
        </div>

        {templatesForType.length > 0 && (
          <>
            <p className="pt-2 text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">{t('mobile.quality.template')}</p>
            <select value={templateId} onChange={(e) => setTemplateId(e.target.value)}
              className="h-11 w-full rounded-lg border border-border bg-card px-3 text-sm">
              <option value="">{t('mobile.quality.templateNone')}</option>
              {templatesForType.map((tpl) => <option key={tpl.id} value={tpl.id}>{tpl.code} · {tpl.name}</option>)}
            </select>
          </>
        )}
      </section>

      <section className="space-y-2 rounded-xl border border-border bg-card p-4">
        <p className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">{t('mobile.quality.result')}</p>
        <div className="grid grid-cols-3 gap-2">
          <button type="button" onClick={() => setResult('ok')}
            className={cn('flex min-h-[64px] flex-col items-center justify-center gap-1 rounded-lg border text-xs font-semibold',
              result === 'ok' ? 'border-state-production bg-state-production/10 text-state-production' : 'border-border bg-card')}>
            <Check className="h-5 w-5" /> {t('mobile.quality.result.ok')}
          </button>
          <button type="button" onClick={() => setResult('rework')}
            className={cn('flex min-h-[64px] flex-col items-center justify-center gap-1 rounded-lg border text-xs font-semibold',
              result === 'rework' ? 'border-state-material bg-state-material/10 text-state-material' : 'border-border bg-card')}>
            <Wrench className="h-5 w-5" /> {t('mobile.quality.result.rework')}
          </button>
          <button type="button" onClick={() => setResult('scrap')}
            className={cn('flex min-h-[64px] flex-col items-center justify-center gap-1 rounded-lg border text-xs font-semibold',
              result === 'scrap' ? 'border-state-technical bg-state-technical/10 text-state-technical' : 'border-border bg-card')}>
            <X className="h-5 w-5" /> {t('mobile.quality.result.scrap')}
          </button>
        </div>

        <div className="grid grid-cols-2 gap-3 pt-2">
          <label className="block">
            <span className="text-xs text-muted-foreground">{t('mobile.quality.qtyChecked')}</span>
            <input type="number" min={1} inputMode="numeric" value={quantityChecked}
              onChange={(e) => setQuantityChecked(e.target.value)}
              className="mt-1 h-11 w-full rounded-lg border border-border bg-card px-3 text-sm" />
          </label>
          {needsCause && (
            <label className="block">
              <span className="text-xs text-muted-foreground">{t('mobile.quality.qtyRejected')}</span>
              <input type="number" min={0} inputMode="numeric" value={quantityRejected}
                onChange={(e) => setQuantityRejected(e.target.value)}
                className="mt-1 h-11 w-full rounded-lg border border-border bg-card px-3 text-sm" />
            </label>
          )}
        </div>

        {needsCause && (
          <>
            <p className="pt-2 text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">{t('mobile.quality.cause')}</p>
            <div className="grid grid-cols-2 gap-2">
              {qualityCauses.map((c) => (
                <button key={c.id} type="button" onClick={() => setCauseId(c.id)}
                  className={cn('min-h-[48px] rounded-lg border px-2 text-xs font-medium text-start',
                    causeId === c.id ? 'border-foreground bg-foreground text-background' : 'border-border bg-card')}>
                  {causeLabelIn(c, lang)}
                </button>
              ))}
              {qualityCauses.length === 0 && (
                <p className="col-span-2 text-xs text-muted-foreground">{t('mobile.quality.noCauses')}</p>
              )}
            </div>
          </>
        )}

        <label className="block pt-2">
          <span className="text-xs text-muted-foreground">{t('mobile.quality.note')}</span>
          <textarea value={note} onChange={(e) => setNote(e.target.value)} rows={2}
            className="mt-1 w-full rounded-lg border border-border bg-card p-2 text-sm" />
        </label>
      </section>

      {error && <p className="text-sm font-medium text-state-technical">{error}</p>}

      <button type="button" onClick={() => void submit()} disabled={!canSubmit}
        className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background disabled:opacity-40">
        {submitting ? <Loader2 className="h-5 w-5 animate-spin" /> : <Check className="h-5 w-5" />}
        {t('mobile.quality.submit')}
      </button>
    </div>
  );
}
