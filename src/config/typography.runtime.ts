/**
 * Typography runtime — injects the config as CSS custom properties on :root
 * so Tailwind tokens (var(--text-*-size), var(--font-*)) and raw CSS in
 * module stylesheets all resolve to the same values.
 *
 * Call once from `src/main.tsx` before render. Overrides persisted in
 * localStorage are re-applied on next boot.
 */
import {
  typographyConfig as baseConfig,
  mergeTypography,
  type TypographyConfig,
  type TypographyOverride,
  type TypographyToken,
  type RawScaleKey,
  type PixelScaleKey,
} from './typography.config';

const STORAGE_KEY = 'typography-overrides';

let current: TypographyConfig = baseConfig;

function loadOverride(): TypographyOverride | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as TypographyOverride) : null;
  } catch {
    return null;
  }
}

function injectIntoRoot(config: TypographyConfig): void {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;

  // Font families
  root.style.setProperty('--font-display', config.families.display);
  root.style.setProperty('--font-heading', config.families.heading);
  root.style.setProperty('--font-body', config.families.body);
  root.style.setProperty('--font-mono', config.families.mono);

  // Weights
  root.style.setProperty('--fw-regular', String(config.weights.regular));
  root.style.setProperty('--fw-medium', String(config.weights.medium));
  root.style.setProperty('--fw-semibold', String(config.weights.semibold));
  root.style.setProperty('--fw-bold', String(config.weights.bold));

  // Scale tokens
  for (const key of Object.keys(config.scale) as TypographyToken[]) {
    const entry = config.scale[key];
    root.style.setProperty(`--text-${key}-size`, entry.size);
    root.style.setProperty(`--text-${key}-lh`, entry.lh);
    root.style.setProperty(`--text-${key}-weight`, String(entry.w));
    root.style.setProperty(`--text-${key}-tracking`, entry.tr ?? '0');
    root.style.setProperty(
      `--text-${key}-family`,
      `var(--font-${entry.fam})`,
    );
  }

  // Raw scale (overrides Tailwind defaults: text-xs .. text-7xl)
  for (const key of Object.keys(config.rawScale) as RawScaleKey[]) {
    const e = config.rawScale[key];
    root.style.setProperty(`--text-raw-${key}-size`, e.size);
    root.style.setProperty(`--text-raw-${key}-lh`, e.lh);
  }

  // Pixel scale (drives text-px-* tokens that replace text-[Npx] literals)
  for (const key of Object.keys(config.pixelScale) as PixelScaleKey[]) {
    root.style.setProperty(`--text-px-${key}-size`, config.pixelScale[key]);
  }

  // Root html font-size — reference for all rem values app-wide
  root.style.setProperty('--typo-root-size', config.rootFontSize);

  // Mobile
  root.style.setProperty('--typo-mobile-scale', String(config.mobile.scale));
  root.style.setProperty('--typo-mobile-bp', config.mobile.breakpoint);
}

/** Apply a fully-resolved config (base + optional override). */
export function applyTypography(patch: TypographyOverride | null = loadOverride()): TypographyConfig {
  current = mergeTypography(baseConfig, patch);
  injectIntoRoot(current);
  return current;
}
