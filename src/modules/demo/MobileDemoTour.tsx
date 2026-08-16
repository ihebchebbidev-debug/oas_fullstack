import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Pause, Play, SkipBack, SkipForward, Volume2, VolumeX, X, Sparkles } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useI18n } from '@/i18n/I18nProvider';
import { useNarrator } from './useNarrator';
import { MOBILE_TOUR_STEPS } from './mobileTourSteps';

interface Rect { top: number; left: number; width: number; height: number }

const SCROLLER = '[data-demo="mobile-scroll"]';

/** Fallback height reserved by the narration sheet + tab bar. */
const PANEL_H = 300;

/**
 * Auto-playing narrated tour of the operator app, sized for the phone frame:
 * the spotlight stays inside the 430 px shell and the narration panel sits
 * above the bottom tab bar so nothing is ever hidden.
 */
export function MobileDemoTour({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const { t, lang, dir } = useI18n();
  const narrator = useNarrator(lang as 'fr' | 'en' | 'ar');
  const [index, setIndex] = useState(0);
  const [playing, setPlaying] = useState(true);
  const [spot, setSpot] = useState<Rect | null>(null);
  const [place, setPlace] = useState<'bottom' | 'top'>('bottom');
  const panelRef = useRef<HTMLDivElement>(null);
  const tokenRef = useRef(0);

  const step = MOBILE_TOUR_STEPS[index];
  const last = index === MOBILE_TOUR_STEPS.length - 1;

  /** Panel footprint (sheet + tab bar) used for centring and flip decisions. */
  const panelH = useCallback(
    () => (panelRef.current ? panelRef.current.offsetHeight + 80 : PANEL_H),
    [],
  );

  /** Spotlight an element and flip the sheet away from it when they overlap. */
  const applySpot = useCallback(
    (el: HTMLElement) => {
      const r = el.getBoundingClientRect();
      setSpot({ top: r.top - 6, left: r.left - 6, width: r.width + 12, height: r.height + 12 });
      const h = panelH();
      const bottomTop = window.innerHeight - h;
      setPlace(r.bottom + 12 > bottomTop && r.top - 12 > h ? 'top' : 'bottom');
    },
    [panelH],
  );


  const stop = useCallback(() => {
    tokenRef.current += 1;
    narrator.cancel();
  }, [narrator]);

  const close = useCallback(() => {
    stop();
    setPlaying(false);
    onClose();
  }, [stop, onClose]);

  useEffect(() => {
    if (open) {
      setIndex(0);
      setPlaying(true);
    } else {
      stop();
      setSpot(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Play one step: navigate → run its action → spotlight → narrate → next.
  useEffect(() => {
    if (!open) return;
    const token = ++tokenRef.current;
    let cancelled = false;
    const alive = () => !cancelled && token === tokenRef.current;

    const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));
    const waitFor = async (selector: string, ms = 2000) => {
      const until = Date.now() + ms;
      while (Date.now() < until && alive()) {
        const el = document.querySelector<HTMLElement>(selector);
        if (el) return el;
        await sleep(80);
      }
      return null;
    };

    const play = async () => {
      narrator.cancel();
      step.run?.();
      navigate(step.route);
      await sleep(420);
      if (!alive()) return;
      step.run?.();

      const scroller = document.querySelector<HTMLElement>(SCROLLER);
      let target = step.selector ? await waitFor(step.selector) : null;
      if (step.selector && !target && step.run) {
        step.run();
        target = await waitFor(step.selector, 1500);
      }
      if (!alive()) return;

      if (target && scroller) {
        // Centre the target in the visible slice of the phone screen, i.e.
        // the scroller minus the narration panel that covers one half.
        const box = scroller.getBoundingClientRect();
        const r = target.getBoundingClientRect();
        const visible = Math.max(120, box.height - panelH());
        const delta = r.top - box.top - (visible - r.height) / 2;
        scroller.scrollTo({ top: Math.max(0, scroller.scrollTop + delta), behavior: 'smooth' });
        await sleep(520);
        if (!alive()) return;
        applySpot(target);
      } else {
        await sleep(180);
        scroller?.scrollTo({ top: 0, behavior: 'smooth' });
        setSpot(null);
        setPlace('bottom');
      }


      if (!alive() || !playing) return;
      await narrator.speak(t(step.textKey), lang as 'fr' | 'en' | 'ar');
      if (!alive() || !playing) return;
      await sleep(narrator.muted ? 4200 : 900);
      if (!alive() || !playing) return;
      if (last) setPlaying(false);
      else setIndex((i) => i + 1);
    };

    void play();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, index, playing, lang, narrator.muted]);

  // Keep the spotlight glued to its element while the phone screen scrolls.
  useEffect(() => {
    if (!open || !step.selector) return;
    const sync = () => {
      const el = document.querySelector<HTMLElement>(step.selector!);
      if (el) applySpot(el);
    };
    window.addEventListener('scroll', sync, true);
    window.addEventListener('resize', sync);
    return () => {
      window.removeEventListener('scroll', sync, true);
      window.removeEventListener('resize', sync);
    };
  }, [open, index, step.selector, applySpot]);


  if (!open) return null;

  const go = (i: number) => { stop(); setIndex(i); setPlaying(true); };
  const progress = ((index + 1) / MOBILE_TOUR_STEPS.length) * 100;

  return (
    <div dir={dir} className="pointer-events-none fixed inset-0 z-[70]">
      {spot ? (
        <div
          className="absolute rounded-lg ring-2 ring-primary transition-all duration-300"
          style={{
            top: spot.top, left: spot.left, width: spot.width, height: spot.height,
            boxShadow: '0 0 0 9999px hsl(var(--overlay) / 0.65)',
          }}
        />
      ) : (
        <div className="absolute inset-0" style={{ background: 'hsl(var(--overlay) / 0.55)' }} />
      )}

      {/* Narration sheet — flips above the tab bar or under the header so it
          never covers the element being demonstrated. */}
      <div
        className={cn(
          'absolute inset-x-0 flex justify-center transition-all duration-300',
          place === 'top' ? 'top-0' : 'bottom-0',
        )}
      >
        <div
          className={cn(
            'pointer-events-auto w-full max-w-[430px] px-3',
            place === 'top' ? 'pt-3' : 'pb-[calc(64px+env(safe-area-inset-bottom))]',
          )}
        >
          <div ref={panelRef} className="rounded-2xl border border-border bg-card p-3 shadow-2xl">

            <div className="flex items-start gap-2">
              <span className="mt-0.5 grid h-7 w-7 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
                <Sparkles className="h-3.5 w-3.5" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="text-[0.625rem] uppercase tracking-[0.08em] text-muted-foreground">
                  <span data-demo="tour-label">{t('demo.stepLabel')} {index + 1}/{MOBILE_TOUR_STEPS.length}</span>
                </p>
                <h2 className="mt-0.5 text-sm font-semibold leading-tight">{t(step.titleKey)}</h2>
              </div>
              <button
                type="button" onClick={close} aria-label={t('common.close')}
                className="grid h-8 w-8 place-items-center rounded-md text-muted-foreground active:bg-muted"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <p className="mt-1.5 max-h-[7.5rem] overflow-y-auto text-xs leading-relaxed text-muted-foreground">
              {t(step.textKey)}
            </p>

            <div className="mt-2 h-1 overflow-hidden rounded-full bg-muted">
              <div className="h-full rounded-full bg-primary transition-all duration-500"
                style={{ width: `${progress}%` }} />
            </div>

            <div className="mt-2.5 flex items-center gap-2">
              <button
                type="button" onClick={() => go(Math.max(0, index - 1))} disabled={index === 0}
                aria-label={t('demo.previous')}
                className="grid h-11 w-11 place-items-center rounded-xl border border-border text-foreground disabled:opacity-40"
              >
                <SkipBack className="h-4 w-4 rtl:rotate-180" />
              </button>
              <button
                type="button"
                onClick={() => { if (playing) { stop(); setPlaying(false); } else { setPlaying(true); go(index); } }}
                className="flex h-11 flex-1 items-center justify-center gap-2 rounded-xl bg-primary text-sm font-semibold text-primary-foreground"
              >
                {playing ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                {t(playing ? 'demo.pause' : 'demo.play')}
              </button>
              <button
                type="button" onClick={() => go(Math.min(MOBILE_TOUR_STEPS.length - 1, index + 1))} disabled={last}
                aria-label={t('demo.next')}
                className="grid h-11 w-11 place-items-center rounded-xl border border-border text-foreground disabled:opacity-40"
              >
                <SkipForward className="h-4 w-4 rtl:rotate-180" />
              </button>
              <button
                type="button"
                onClick={() => { narrator.cancel(); narrator.setMuted(!narrator.muted); }}
                aria-label={t(narrator.muted ? 'demo.unmute' : 'demo.mute')}
                aria-pressed={narrator.muted}
                className={cn(
                  'grid h-11 w-11 place-items-center rounded-xl border border-border text-foreground',
                  narrator.muted && 'bg-muted',
                )}
              >
                {narrator.muted ? <VolumeX className="h-4 w-4" /> : <Volume2 className="h-4 w-4" />}
              </button>
            </div>

            {!narrator.supported && (
              <p className="mt-1.5 text-[0.625rem] text-muted-foreground">{t('demo.noVoice')}</p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
