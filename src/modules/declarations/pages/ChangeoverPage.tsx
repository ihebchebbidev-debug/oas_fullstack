import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Repeat, Timer, Check, Loader2 } from 'lucide-react';
import { useT } from '@/i18n/I18nProvider';
import { useSessionState } from '@/modules/auth/store/session';
import { useRefState } from '@/oas/refStore';
import { changeoversApi, type OasChangeoverDto, type OasChangeoverStepDto } from '@/oas/api/operations';
import { ApiError } from '@/oas/api/client';
import { pushToast } from '@/shared/lib/toast';

/** Same 5 steps as the server's own DefaultSteps (OasChangeoverService.cs) — ids kept in sync so a fresh, backend-generated checklist and one built here read the same; labels are ours so they stay localized instead of the server's hardcoded English. */
const STEP_DEFS = [
  { id: 'clear', key: 'mobile.co.step1' },
  { id: 'setup', key: 'mobile.co.step2' },
  { id: 'adjust', key: 'mobile.co.step3' },
  { id: 'first_part', key: 'mobile.co.step4' },
  { id: 'cleanup', key: 'mobile.co.step5' },
] as const;

const uid = () =>
  (globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`);

/** One in-flight changeover's client-generated id per post, so a page reload can resume updating the same server record instead of hitting "post_already_has_open_changeover" with no way back to it (OasChangeoverDto never echoes clientEventId, only its own server-side id). */
const pendingKey = (postId: string) => `oas.changeover.pending.${postId}`;

/**
 * BL-028 — changeover: real backend record (`/api/oas/changeovers`), not a
 * generic stop. The checklist persists to the server on every tick (resumable
 * after navigate-away, per the DTO's own doc comment) and finishing is
 * refused server-side (409 checklist_incomplete) unless every step is done.
 */
export default function ChangeoverPage() {
  const navigate = useNavigate();
  const t = useT();
  const state = useSessionState();
  const { products } = useRefState();
  const postId = state.session?.postId ?? null;

  const [changeover, setChangeover] = useState<OasChangeoverDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [toProductId, setToProductId] = useState('');
  const [starting, setStarting] = useState(false);
  const [finishing, setFinishing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // No open post session → nothing can be recorded: send the operator back to the scan.
  useEffect(() => {
    if (!state.session) navigate('/mobile/scan', { replace: true });
  }, [state.session, navigate]);

  // Resume: if this device already started (and hasn't finished) a
  // changeover at this post, reload its live state from the server instead
  // of silently losing progress and restarting the timer on every visit.
  useEffect(() => {
    if (!postId) { setLoading(false); return; }
    let cancelled = false;
    (async () => {
      const clientEventId = localStorage.getItem(pendingKey(postId));
      if (!clientEventId) { setLoading(false); return; }
      try {
        const rows = await changeoversApi.list(postId, 'open');
        if (!cancelled) setChangeover(rows[0] ?? null); // one open changeover per post, enforced server-side
      } catch {
        /* stay on the picker — operator can retry once reconnected */
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [postId]);

  async function start() {
    if (!postId || !toProductId || starting) return;
    setStarting(true);
    setError(null);
    const clientEventId = uid();
    const steps: OasChangeoverStepDto[] = STEP_DEFS.map((s) => ({ id: s.id, label: t(s.key), done: false }));
    try {
      const dto = await changeoversApi.createOrUpdate({ clientEventId, postId, toProductId, steps });
      localStorage.setItem(pendingKey(postId), clientEventId);
      setChangeover(dto);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.actionFailed'));
    } finally {
      setStarting(false);
    }
  }

  async function toggleStep(stepId: string) {
    if (!changeover || !postId) return;
    const clientEventId = localStorage.getItem(pendingKey(postId));
    if (!clientEventId) return;
    const nextSteps = changeover.steps.map((s) => (s.id === stepId ? { ...s, done: !s.done } : s));
    const previous = changeover;
    setChangeover({ ...changeover, steps: nextSteps }); // optimistic — this is a fast, low-stakes toggle
    try {
      await changeoversApi.createOrUpdate({
        clientEventId, postId,
        toProductId: changeover.toProductId, fromProductId: changeover.fromProductId ?? undefined,
        steps: nextSteps,
      });
    } catch {
      setChangeover(previous); // roll back — the tick didn't actually persist
      pushToast('common.actionFailed');
    }
  }

  const allDone = changeover ? changeover.steps.length > 0 && changeover.steps.every((s) => s.done) : false;

  async function finish() {
    if (!changeover || !allDone || !postId || finishing) return;
    setFinishing(true);
    setError(null);
    try {
      await changeoversApi.finish(changeover.id);
      localStorage.removeItem(pendingKey(postId));
      navigate('/mobile/home');
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('common.actionFailed'));
    } finally {
      setFinishing(false);
    }
  }

  const startedAt = changeover ? Date.parse(changeover.startedAt) : Date.now();
  const [elapsed, setElapsed] = useState(0);
  useEffect(() => {
    if (!changeover) return;
    const tick = () => setElapsed(Math.max(0, Math.floor((Date.now() - startedAt) / 1000)));
    tick();
    const id = setInterval(tick, 1_000);
    return () => clearInterval(id);
  }, [startedAt, changeover]);

  const pad = (n: number) => String(n).padStart(2, '0');
  const chrono = `${pad(Math.floor(elapsed / 60))}:${pad(elapsed % 60)}`;
  const toProductLabel = (id: string) => {
    const p = products.find((x) => x.id === id);
    return p ? `${p.ref} · ${p.label}` : id.slice(0, 8);
  };

  return (
    <div className="space-y-4">
      <button type="button" onClick={() => navigate(-1)}
        className="inline-flex min-h-[44px] items-center gap-1.5 text-sm text-muted-foreground">
        <ArrowLeft className="h-4 w-4 rtl:rotate-180" /> {t('common.back')}
      </button>

      {loading ? (
        <div className="flex min-h-[200px] items-center justify-center text-muted-foreground">
          <Loader2 className="h-6 w-6 animate-spin" />
        </div>
      ) : !changeover ? (
        <div className="space-y-3 rounded-xl border border-border bg-card p-4">
          <p className="text-sm font-medium">{t('mobile.co.selectProduct')}</p>
          <select
            value={toProductId}
            onChange={(e) => setToProductId(e.target.value)}
            className="h-12 w-full rounded-lg border border-border bg-card px-3 text-sm"
          >
            <option value="">{t('mobile.co.selectProductPlaceholder')}</option>
            {products.map((p) => (
              <option key={p.id} value={p.id}>{p.ref} · {p.label}</option>
            ))}
          </select>
          {error && <p className="text-sm font-medium text-state-technical">{error}</p>}
          <button type="button" onClick={() => void start()} disabled={!toProductId || starting}
            className="flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-sm font-semibold text-background disabled:opacity-40">
            {starting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Repeat className="h-4 w-4" />}
            {t('mobile.co.start')}
          </button>
        </div>
      ) : (
        <>
          <header data-demo="m-changeover" className="rounded-xl border border-state-changeover/40 bg-state-changeover/10 p-4">
            <p className="inline-flex items-center gap-2 text-sm font-semibold text-state-changeover">
              <Repeat className="h-4 w-4" /> {t('mobile.co.header')}
            </p>
            <p dir="ltr" className="mt-2 font-mono text-[2rem] font-bold leading-none tabular-nums text-state-changeover">{chrono}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t('mobile.co.subtitle')}</p>
            <p className="mt-0.5 text-xs text-muted-foreground">{t('mobile.co.to', { product: toProductLabel(changeover.toProductId) })}</p>
          </header>

          <ol className="space-y-2">
            {changeover.steps.map((s, i) => {
              const nextIdx = changeover.steps.findIndex((x) => !x.done);
              return (
                <li key={s.id}>
                  <button
                    type="button"
                    role="checkbox"
                    aria-checked={s.done}
                    onClick={() => void toggleStep(s.id)}
                    className={`flex min-h-[56px] w-full items-center gap-3 rounded-lg border p-3 text-start ${
                      s.done ? 'border-state-changeover/40 bg-state-changeover/10' : 'border-border bg-card'
                    }`}
                  >
                    <span className="flex h-7 w-7 items-center justify-center rounded-full border border-border font-mono text-xs">
                      {s.done ? <Check className="h-4 w-4" /> : i + 1}
                    </span>
                    <span className="flex-1 text-sm font-medium">{s.label}</span>
                    {i === nextIdx && <Timer className="h-4 w-4 text-state-changeover" />}
                  </button>
                </li>
              );
            })}
          </ol>

          {error && <p className="text-sm font-medium text-state-technical">{error}</p>}

          <button type="button" onClick={() => void finish()} disabled={!allDone || finishing}
            className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background disabled:opacity-40">
            {finishing ? <Loader2 className="h-5 w-5 animate-spin" /> : <Check className="h-5 w-5" />}
            {t('mobile.co.done')}
          </button>
          {!allDone && (
            <p className="text-center text-xs text-muted-foreground">
              {t('mobile.co.gate', { n: changeover.steps.filter((s) => s.done).length, total: changeover.steps.length })}
            </p>
          )}
        </>
      )}
    </div>
  );
}
