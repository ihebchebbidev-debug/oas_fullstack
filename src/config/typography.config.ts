/**
 * Shared Typography Config — single source of truth for every font family,
 * size, weight, line-height, tracking, and mobile scaling in the app.
 *
 * A future Settings > Appearance panel can mutate this at runtime via
 * `updateTypography(...)` in `./typography.runtime.ts` — no code changes
 * needed elsewhere.
 */

export type TypographyFamilyKey = 'display' | 'heading' | 'body' | 'mono';

export type TypographyToken =
  | 'display'
  | 'h1'
  | 'h2'
  | 'h3'
  | 'title'
  | 'subtitle'
  | 'body'
  | 'body-sm'
  | 'caption'
  | 'label'
  | 'overline'
  | 'metric'
  | 'metric-sm'
  | 'metric-lg'
  | 'button'
  | 'button-sm'
  | 'input'
  | 'code'
  | 'table-header'
  | 'table-cell'
  | 'nav'
  | 'badge'
  | 'tooltip';

export interface TypographyScaleEntry {
  /** CSS font-size (rem, px, or clamp()) */
  size: string;
  /** unitless line-height */
  lh: string;
  /** numeric font-weight 100..900 */
  w: number;
  /** letter-spacing (optional) */
  tr?: string;
  /** which family key from `families` */
  fam: TypographyFamilyKey;
}

export type RawScaleKey =
  | 'xs' | 'sm' | 'base' | 'lg' | 'xl'
  | '2xl' | '3xl' | '4xl' | '5xl' | '6xl' | '7xl';

export type PixelScaleKey =
  | '7' | '8' | '9' | '10' | '11' | '12' | '13' | '14' | '15' | '16' | '18' | '20' | '22' | '24'
  | 'rem-60' | 'rem-65' | 'rem-70' | 'rem-75' | 'rem-80' | 'rem-85' | 'rem-95';

export interface RawScaleEntry {
  size: string;
  lh: string;
}

export interface TypographyConfig {
  families: Record<TypographyFamilyKey, string>;
  weights: { regular: number; medium: number; semibold: number; bold: number };
  scale: Record<TypographyToken, TypographyScaleEntry>;
  /** Raw Tailwind scale (text-xs, text-sm, ...) — overrides Tailwind defaults
   *  so ~thousands of leaf usages become config-driven. */
  rawScale: Record<RawScaleKey, RawScaleEntry>;
  /** Pixel-precise scale (text-px-10, text-px-11, ...) — replaces the
   *  previous `text-[Npx]` arbitrary literals so they too flow through
   *  the config. */
  pixelScale: Record<PixelScaleKey, string>;
  /** Root html font-size — the reference for every `rem` value in the app. */
  rootFontSize: string;
  mobile: { breakpoint: string; scale: number; denseTables: boolean };
  print: { bodyPt: number; headingPt: number };
}

export const typographyConfig: TypographyConfig = {
  families: {
    display: '"Geist", Inter, system-ui, -apple-system, sans-serif',
    heading: 'Inter, system-ui, -apple-system, sans-serif',
    body: 'Inter, system-ui, -apple-system, sans-serif',
    mono: 'ui-monospace, "SF Mono", Menlo, Monaco, Consolas, monospace',
  },
  weights: { regular: 400, medium: 500, semibold: 600, bold: 700 },
  scale: {
    display:       { size: 'clamp(1.75rem, 2vw + 1rem, 2.5rem)',   lh: '1.1',  w: 700, tr: '-0.02em',  fam: 'display' },
    h1:            { size: 'clamp(1.5rem, 1.5vw + 0.8rem, 2rem)',  lh: '1.15', w: 600, tr: '-0.015em', fam: 'heading' },
    h2:            { size: 'clamp(1.25rem, 1vw + 0.7rem, 1.5rem)', lh: '1.2',  w: 600, tr: '-0.01em',  fam: 'heading' },
    h3:            { size: '1.125rem',  lh: '1.3',  w: 600, tr: '-0.005em', fam: 'heading' },
    title:         { size: '1rem',      lh: '1.4',  w: 600, fam: 'heading' },
    subtitle:      { size: '0.875rem',  lh: '1.4',  w: 500, fam: 'heading' },
    body:          { size: '0.875rem',  lh: '1.5',  w: 400, fam: 'body' },
    'body-sm':     { size: '0.8125rem', lh: '1.5',  w: 400, fam: 'body' },
    caption:       { size: '0.75rem',   lh: '1.4',  w: 400, fam: 'body' },
    label:         { size: '0.8125rem', lh: '1.3',  w: 500, fam: 'body' },
    overline:      { size: '0.6875rem', lh: '1.2',  w: 600, tr: '0.08em', fam: 'body' },
    metric:        { size: 'clamp(1.5rem, 1.5vw + 0.5rem, 2rem)',  lh: '1',    w: 700, fam: 'display' },
    'metric-sm':   { size: '1.125rem',  lh: '1.2',  w: 600, fam: 'display' },
    'metric-lg':   { size: 'clamp(2rem, 3vw + 0.5rem, 3rem)',      lh: '1',    w: 700, fam: 'display' },
    button:        { size: '0.875rem',  lh: '1',    w: 500, fam: 'body' },
    'button-sm':   { size: '0.8125rem', lh: '1',    w: 500, fam: 'body' },
    input:         { size: '0.875rem',  lh: '1.4',  w: 400, fam: 'body' },
    code:          { size: '0.8125rem', lh: '1.5',  w: 400, fam: 'mono' },
    'table-header':{ size: '0.75rem',   lh: '1.2',  w: 600, tr: '0.02em', fam: 'body' },
    'table-cell':  { size: '0.8125rem', lh: '1.4',  w: 400, fam: 'body' },
    nav:           { size: '0.875rem',  lh: '1.2',  w: 500, fam: 'body' },
    badge:         { size: '0.6875rem', lh: '1',    w: 600, fam: 'body' },
    tooltip:       { size: '0.75rem',   lh: '1.3',  w: 400, fam: 'body' },
  },
  rawScale: {
    xs:    { size: '0.875rem', lh: '1rem'     },
    sm:    { size: '0.9375rem', lh: '1.25rem'  },
    base:  { size: '1rem',      lh: '1.375rem' },
    lg:    { size: '1.125rem',  lh: '1.5rem'   },
    xl:    { size: '1.25rem',   lh: '1.6rem'   },
    '2xl': { size: '1.5rem',    lh: '1.8rem'   },
    '3xl': { size: '1.875rem',  lh: '2.1rem'   },
    '4xl': { size: '2.3125rem', lh: '2.5rem'   },
    '5xl': { size: '2.9375rem', lh: '1.1'      },
    '6xl': { size: '3.625rem',  lh: '1'        },
    '7xl': { size: '4.375rem',  lh: '1'        },
  },
  pixelScale: {
    '7':  '9px',
    '8':  '10px',
    '9':  '11px',
    '10': '12px',
    '11': '13px',
    '12': '14px',
    '13': '15px',
    '14': '16px',
    '15': '17px',
    '16': '18px',
    '18': '20px',
    '20': '22px',
    '22': '24px',
    '24': '26px',
    // rem literals kept as pixel-scale keys with rem values (config-driven)
    'rem-60':  '0.7rem',
    'rem-65':  '0.75rem',
    'rem-70':  '0.8rem',
    'rem-75':  '0.85rem',
    'rem-80':  '0.9rem',
    'rem-85':  '0.95rem',
    'rem-95':  '1.05rem',
  },
  rootFontSize: '15px',
  mobile: { breakpoint: '640px', scale: 0.95, denseTables: true },
  print: { bodyPt: 10, headingPt: 14 },
};

/** Deep-partial helper for `updateTypography(partial)` */
export type TypographyOverride = {
  families?: Partial<TypographyConfig['families']>;
  weights?: Partial<TypographyConfig['weights']>;
  scale?: Partial<Record<TypographyToken, Partial<TypographyScaleEntry>>>;
  rawScale?: Partial<Record<RawScaleKey, Partial<RawScaleEntry>>>;
  pixelScale?: Partial<Record<PixelScaleKey, string>>;
  rootFontSize?: string;
  mobile?: Partial<TypographyConfig['mobile']>;
  print?: Partial<TypographyConfig['print']>;
};

export function mergeTypography(
  base: TypographyConfig,
  patch: TypographyOverride | null | undefined,
): TypographyConfig {
  if (!patch) return base;
  const next: TypographyConfig = {
    ...base,
    families: { ...base.families, ...(patch.families || {}) },
    weights: { ...base.weights, ...(patch.weights || {}) },
    scale: { ...base.scale },
    rawScale: { ...base.rawScale },
    pixelScale: { ...base.pixelScale, ...(patch.pixelScale || {}) },
    rootFontSize: patch.rootFontSize ?? base.rootFontSize,
    mobile: { ...base.mobile, ...(patch.mobile || {}) },
    print: { ...base.print, ...(patch.print || {}) },
  };
  if (patch.scale) {
    for (const key of Object.keys(patch.scale) as TypographyToken[]) {
      const p = patch.scale[key];
      if (!p) continue;
      next.scale[key] = { ...base.scale[key], ...p };
    }
  }
  if (patch.rawScale) {
    for (const key of Object.keys(patch.rawScale) as RawScaleKey[]) {
      const p = patch.rawScale[key];
      if (!p) continue;
      next.rawScale[key] = { ...base.rawScale[key], ...p };
    }
  }
  return next;
}
