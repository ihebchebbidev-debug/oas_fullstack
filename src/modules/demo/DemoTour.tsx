import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Pause, Play, SkipBack, SkipForward, Volume2, VolumeX, X, Sparkles } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useI18n } from '@/i18n/I18nProvider';
import { TOUR_STEPS } from './tourSteps';
import { useNarrator } from './useNarrator';

interface Rect { top: number; left: number; width: number; height: number }

/**
 * Auto-playing guided demo: walks every web surface, opens the drawers and
 * panels, spotlights what it talks about and narrates it out loud in the
 * language currently selected (FR / EN / AR).
 */
export function DemoTour({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const { t, lang, dir } = useI18n();
  const narrator = useNarrator(lang as 'fr' | 'en' | 'ar');
  const [index, setIndex] = useState(0);
  const [playing, setPlaying] = useState(true);
  const [spot, setSpot] = useState<Rect | null>(null);
  const tokenRef = useRef(0);

  const step = TOUR_STEPS[index];
  const last = index === TOUR_STEPS.length - 1;

  const stop = useCallback(() => {
    tokenRef.current += 1;
    narrator.cancel();
  }, [narrator]);

  const close = useCallback(() => {
    stop();
    setPlaying(false);
    onClose();
  }, [stop, onClose]);

  // Reset to the beginning every time the tour is opened.
  useEffect(() => {
    if (open) {
      setIndex(0);
      setPlaying(true);
    } else {
      stop();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Play one step: navigate → run its UI action → spotlight → narrate → next.
  useEffect(() => {
    if (!open) return;
    const token = ++tokenRef.current;
    let cancelled = false;
    const alive = () => !cancelled && token === tokenRef.current;

    const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));
    /** Poll for a selector — lazy-loaded pages need a moment to mount. */
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
      navigate(step.route);
      await sleep(400);
      if (!alive()) return;
      step.run?.();

      const scroller = document.querySelector<HTMLElement>('[data-demo="scroll-area"]');
      let target = step.selector ? await waitFor(step.selector) : null;
      // Jumping straight to a step whose panel is closed: replay the action once.
      if (step.selector && !target && step.run) {
        step.run();
        target = await waitFor(step.selector, 1500);
      }
      if (!alive()) return;

      if (target) {
        // Centre it inside every scrollable ancestor — scrolls back up as well as down.
        target.scrollIntoView({ block: 'center', behavior: 'smooth' });
        await sleep(500);
        if (!alive()) return;
        const r = target.getBoundingClientRect();
        setSpot({ top: r.top - 6, left: r.left - 6, width: r.width + 12, height: r.height + 12 });
      } else {
        // Full-page step: start from the top of the page.
        await sleep(200);
        scroller?.scrollTo({ top: 0, behavior: 'smooth' });
        setSpot(null);
      }


      if (!alive() || !playing) return;

      await narrator.speak(t(step.textKey), lang as 'fr' | 'en' | 'ar');
      if (!alive() || !playing) return;
      // Breathing room between steps (and the whole delay when muted).
      await new Promise((r) => setTimeout(r, narrator.muted ? 4200 : 900));
      if (!alive() || !playing) return;
      if (last) {
        setPlaying(false);
      } else {
        setIndex((i) => i + 1);
      }
    };

    void play();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, index, playing, lang, narrator.muted]);

  // Keep the spotlight glued to the element while the page scrolls / resizes.
  useEffect(() => {
    if (!open || !step.selector) return;
    const sync = () => {
      const el = document.querySelector<HTMLElement>(step.selector!);
      if (!el) return;
      const r = el.getBoundingClientRect();
      setSpot({ top: r.top - 6, left: r.left - 6, width: r.width + 12, height: r.height + 12 });
    };
    window.addEventListener('scroll', sync, true);
    window.addEventListener('resize', sync);
    return () => {
      window.removeEventListener('scroll', sync, true);
      window.removeEventListener('resize', sync);
    };
  }, [open, index, step.selector]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close();
      if (e.key === 'ArrowRight') { stop(); setIndex((i) => Math.min(TOUR_STEPS.length - 1, i + 1)); }
      if (e.key === 'ArrowLeft') { stop(); setIndex((i) => Math.max(0, i - 1)); }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, close, stop]);

  if (!open) return null;

  const go = (i: number) => { stop(); setIndex(i); setPlaying(true); };

  return (
    <div dir={dir} className="pointer-events-none fixed inset-0 z-[60]">
      {/* Spotlight — a hole punched in the dimmer around the highlighted element. */}
      {spot && (
        <div
          className="absolute rounded-lg ring-2 ring-primary transition-all duration-300"
          style={{
            top: spot.top, left: spot.left, width: spot.width, height: spot.height,
            boxShadow: '0 0 0 9999px hsl(var(--overlay) / 0.62)',
          }}
        />
      )}
      {!spot && (
        <div
          className="absolute inset-0 transition-opacity"
          style={{ background: 'hsl(var(--overlay) / 0.5)' }}
        />
      )}

      {/* Narration panel */}
      <div className="pointer-events-auto absolute inset-x-0 bottom-0 flex justify-center p-4">
        <div className="w-full max-w-3xl rounded-xl border border-border bg-card p-4 shadow-2xl">
          <div className="flex items-start gap-3">
            <span className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
              <Sparkles className="h-4 w-4" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="text-caption uppercase tracking-[0.08em] text-muted-foreground">
                <span data-demo="tour-label">{t('demo.stepLabel')} {index + 1}/{TOUR_STEPS.length}</span>
                {narrator.voiceName && !narrator.muted && ` · ${narrator.voiceName}`}
              </p>
              <h2 className="mt-0.5 text-title font-heading">{t(step.titleKey)}</h2>
              <p className="mt-1 text-sm leading-relaxed text-muted-foreground">{t(step.textKey)}</p>
            </div>
            <button
              type="button" onClick={close} aria-label={t('common.close')}
              className="rounded-md p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="mt-3 flex items-center gap-2">
            <button
              type="button" onClick={() => go(Math.max(0, index - 1))} disabled={index === 0}
              aria-label={t('demo.previous')}
              className="grid h-9 w-9 place-items-center rounded-md border border-border text-foreground disabled:opacity-40"
            >
              <SkipBack className="h-4 w-4" />
            </button>
            <button
              type="button"
              onClick={() => { if (playing) { stop(); setPlaying(false); } else { setPlaying(true); go(index); } }}
              className="flex h-9 items-center gap-2 rounded-md bg-primary px-4 text-sm font-semibold text-primary-foreground"
            >
              {playing ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
              {t(playing ? 'demo.pause' : 'demo.play')}
            </button>
            <button
              type="button" onClick={() => go(Math.min(TOUR_STEPS.length - 1, index + 1))} disabled={last}
              aria-label={t('demo.next')}
              className="grid h-9 w-9 place-items-center rounded-md border border-border text-foreground disabled:opacity-40"
            >
              <SkipForward className="h-4 w-4" />
            </button>
            <button
              type="button"
              onClick={() => { narrator.cancel(); narrator.setMuted(!narrator.muted); }}
              aria-label={t(narrator.muted ? 'demo.unmute' : 'demo.mute')}
              aria-pressed={narrator.muted}
              className="grid h-9 w-9 place-items-center rounded-md border border-border text-foreground"
            >
              {narrator.muted ? <VolumeX className="h-4 w-4" /> : <Volume2 className="h-4 w-4" />}
            </button>

            <div className="ms-auto flex items-center gap-1">
              {TOUR_STEPS.map((s, i) => (
                <button
                  key={s.id} type="button" onClick={() => go(i)} aria-label={t(s.titleKey)}
                  className={cn(
                    'h-1.5 rounded-full transition-all',
                    i === index ? 'w-6 bg-primary' : 'w-1.5 bg-border hover:bg-muted-foreground',
                  )}
                />
              ))}
            </div>
          </div>

          {!narrator.supported && (
            <p className="mt-2 text-caption text-muted-foreground">{t('demo.noVoice')}</p>
          )}
        </div>
      </div>
    </div>
  );
}
