import { useEffect, useRef } from 'react';

import { cn } from '@/lib/utils';

interface Props {
  className?: string;
  /** particle count at 1280x800; scales with area */
  density?: number;
}

/**
 * Subtle animated constellation background: slow drifting dots connected by
 * faint lines. Uses the theme's foreground color so it works in light + dark.
 */
export function ParticleField({ className, density = 46 }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    let width = 0;
    let height = 0;
    let raf = 0;

    type P = { x: number; y: number; vx: number; vy: number; r: number };
    let parts: P[] = [];

    const readColor = () => {
      const styles = getComputedStyle(document.documentElement);
      const fg = styles.getPropertyValue('--primary').trim() || '220 60% 40%';
      return fg;
    };
    let hsl = readColor();

    const seed = () => {
      const dpr = Math.min(window.devicePixelRatio || 1, 2);
      const rect = canvas.getBoundingClientRect();
      width = rect.width;
      height = rect.height;
      canvas.width = Math.floor(width * dpr);
      canvas.height = Math.floor(height * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

      const count = Math.round((density * width * height) / (1280 * 800));
      parts = Array.from({ length: Math.max(18, Math.min(90, count)) }, () => ({
        x: Math.random() * width,
        y: Math.random() * height,
        vx: (Math.random() - 0.5) * 0.18,
        vy: (Math.random() - 0.5) * 0.18,
        r: Math.random() * 1.4 + 0.8,
      }));
    };

    const draw = () => {
      ctx.clearRect(0, 0, width, height);
      const linkDist = Math.min(160, Math.max(90, width * 0.11));

      for (const p of parts) {
        p.x += p.vx;
        p.y += p.vy;
        if (p.x < 0 || p.x > width) p.vx *= -1;
        if (p.y < 0 || p.y > height) p.vy *= -1;
      }

      for (let i = 0; i < parts.length; i++) {
        for (let j = i + 1; j < parts.length; j++) {
          const a = parts[i];
          const b = parts[j];
          const dx = a.x - b.x;
          const dy = a.y - b.y;
          const d = Math.hypot(dx, dy);
          if (d > linkDist) continue;
          ctx.strokeStyle = `hsl(${hsl} / ${(1 - d / linkDist) * 0.16})`;
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.moveTo(a.x, a.y);
          ctx.lineTo(b.x, b.y);
          ctx.stroke();
        }
      }

      for (const p of parts) {
        ctx.fillStyle = `hsl(${hsl} / 0.35)`;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
        ctx.fill();
      }

      if (!reduce) raf = requestAnimationFrame(draw);
    };

    seed();
    draw();

    const onResize = () => {
      hsl = readColor();
      seed();
    };
    window.addEventListener('resize', onResize);

    const observer = new MutationObserver(() => {
      hsl = readColor();
    });
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });

    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener('resize', onResize);
      observer.disconnect();
    };
  }, [density]);

  return (
    <canvas
      ref={canvasRef}
      aria-hidden="true"
      className={cn('pointer-events-none absolute inset-0 h-full w-full', className)}
    />
  );
}
