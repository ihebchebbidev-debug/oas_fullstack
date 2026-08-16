import type { Config } from "tailwindcss";
import tailwindcssAnimate from "tailwindcss-animate";
import typography from "@tailwindcss/typography";

/**
 * Semantic font-size tokens — each maps to CSS custom properties injected at
 * runtime by `src/config/typography.runtime.ts`, which reads
 * `src/config/typography.config.ts`. Change fonts/sizes in ONE place.
 *
 * Existing text-xs/sm/base/lg/... Tailwind defaults are preserved so 800+
 * leaf files keep compiling.
 */
const semanticFontSizes = {
  display:       ["var(--text-display-size)",       { lineHeight: "var(--text-display-lh)",       fontWeight: "var(--text-display-weight)",       letterSpacing: "var(--text-display-tracking)" }],
  h1:            ["var(--text-h1-size)",            { lineHeight: "var(--text-h1-lh)",            fontWeight: "var(--text-h1-weight)",            letterSpacing: "var(--text-h1-tracking)" }],
  h2:            ["var(--text-h2-size)",            { lineHeight: "var(--text-h2-lh)",            fontWeight: "var(--text-h2-weight)",            letterSpacing: "var(--text-h2-tracking)" }],
  h3:            ["var(--text-h3-size)",            { lineHeight: "var(--text-h3-lh)",            fontWeight: "var(--text-h3-weight)",            letterSpacing: "var(--text-h3-tracking)" }],
  title:         ["var(--text-title-size)",         { lineHeight: "var(--text-title-lh)",         fontWeight: "var(--text-title-weight)" }],
  subtitle:      ["var(--text-subtitle-size)",      { lineHeight: "var(--text-subtitle-lh)",      fontWeight: "var(--text-subtitle-weight)" }],
  body:          ["var(--text-body-size)",          { lineHeight: "var(--text-body-lh)",          fontWeight: "var(--text-body-weight)" }],
  "body-sm":     ["var(--text-body-sm-size)",       { lineHeight: "var(--text-body-sm-lh)",       fontWeight: "var(--text-body-sm-weight)" }],
  caption:       ["var(--text-caption-size)",       { lineHeight: "var(--text-caption-lh)",       fontWeight: "var(--text-caption-weight)" }],
  label:         ["var(--text-label-size)",         { lineHeight: "var(--text-label-lh)",         fontWeight: "var(--text-label-weight)" }],
  overline:      ["var(--text-overline-size)",      { lineHeight: "var(--text-overline-lh)",      fontWeight: "var(--text-overline-weight)",      letterSpacing: "var(--text-overline-tracking)" }],
  metric:        ["var(--text-metric-size)",        { lineHeight: "var(--text-metric-lh)",        fontWeight: "var(--text-metric-weight)" }],
  "metric-sm":   ["var(--text-metric-sm-size)",     { lineHeight: "var(--text-metric-sm-lh)",     fontWeight: "var(--text-metric-sm-weight)" }],
  "metric-lg":   ["var(--text-metric-lg-size)",     { lineHeight: "var(--text-metric-lg-lh)",     fontWeight: "var(--text-metric-lg-weight)" }],
  button:        ["var(--text-button-size)",        { lineHeight: "var(--text-button-lh)",        fontWeight: "var(--text-button-weight)" }],
  "button-sm":   ["var(--text-button-sm-size)",     { lineHeight: "var(--text-button-sm-lh)",     fontWeight: "var(--text-button-sm-weight)" }],
  "input-field": ["var(--text-input-size)",         { lineHeight: "var(--text-input-lh)",         fontWeight: "var(--text-input-weight)" }],
  code:          ["var(--text-code-size)",          { lineHeight: "var(--text-code-lh)",          fontWeight: "var(--text-code-weight)" }],
  "table-header":["var(--text-table-header-size)",  { lineHeight: "var(--text-table-header-lh)",  fontWeight: "var(--text-table-header-weight)",  letterSpacing: "var(--text-table-header-tracking)" }],
  "table-cell":  ["var(--text-table-cell-size)",    { lineHeight: "var(--text-table-cell-lh)",    fontWeight: "var(--text-table-cell-weight)" }],
  nav:           ["var(--text-nav-size)",           { lineHeight: "var(--text-nav-lh)",           fontWeight: "var(--text-nav-weight)" }],
  badge:         ["var(--text-badge-size)",         { lineHeight: "var(--text-badge-lh)",         fontWeight: "var(--text-badge-weight)" }],
  tooltip:       ["var(--text-tooltip-size)",       { lineHeight: "var(--text-tooltip-lh)",       fontWeight: "var(--text-tooltip-weight)" }],
} as const;

/** Override Tailwind's default text-xs..text-7xl so every raw usage in
 *  existing pages becomes config-driven via CSS vars. */
const rawScaleOverrides = {
  xs:    ["var(--text-raw-xs-size)",    { lineHeight: "var(--text-raw-xs-lh)"    }],
  sm:    ["var(--text-raw-sm-size)",    { lineHeight: "var(--text-raw-sm-lh)"    }],
  base:  ["var(--text-raw-base-size)",  { lineHeight: "var(--text-raw-base-lh)"  }],
  lg:    ["var(--text-raw-lg-size)",    { lineHeight: "var(--text-raw-lg-lh)"    }],
  xl:    ["var(--text-raw-xl-size)",    { lineHeight: "var(--text-raw-xl-lh)"    }],
  "2xl": ["var(--text-raw-2xl-size)",   { lineHeight: "var(--text-raw-2xl-lh)"   }],
  "3xl": ["var(--text-raw-3xl-size)",   { lineHeight: "var(--text-raw-3xl-lh)"   }],
  "4xl": ["var(--text-raw-4xl-size)",   { lineHeight: "var(--text-raw-4xl-lh)"   }],
  "5xl": ["var(--text-raw-5xl-size)",   { lineHeight: "var(--text-raw-5xl-lh)"   }],
  "6xl": ["var(--text-raw-6xl-size)",   { lineHeight: "var(--text-raw-6xl-lh)"   }],
  "7xl": ["var(--text-raw-7xl-size)",   { lineHeight: "var(--text-raw-7xl-lh)"   }],
} as const;

/** Pixel-precise tokens (text-px-7 .. text-px-24) that replace prior
 *  `text-[Npx]` arbitrary values so those become config-driven. */
const pixelScaleTokens = {
  "px-7":  "var(--text-px-7-size)",
  "px-8":  "var(--text-px-8-size)",
  "px-9":  "var(--text-px-9-size)",
  "px-10": "var(--text-px-10-size)",
  "px-11": "var(--text-px-11-size)",
  "px-12": "var(--text-px-12-size)",
  "px-13": "var(--text-px-13-size)",
  "px-14": "var(--text-px-14-size)",
  "px-15": "var(--text-px-15-size)",
  "px-16": "var(--text-px-16-size)",
  "px-18": "var(--text-px-18-size)",
  "px-20": "var(--text-px-20-size)",
  "px-22": "var(--text-px-22-size)",
  "px-24": "var(--text-px-24-size)",
  "rem-60": "var(--text-px-rem-60-size)",
  "rem-65": "var(--text-px-rem-65-size)",
  "rem-70": "var(--text-px-rem-70-size)",
  "rem-75": "var(--text-px-rem-75-size)",
  "rem-80": "var(--text-px-rem-80-size)",
  "rem-85": "var(--text-px-rem-85-size)",
  "rem-95": "var(--text-px-rem-95-size)",
} as const;



export default {
  darkMode: ["class"],
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    container: {
      center: true,
      padding: "2rem",
      screens: { "2xl": "1400px" },
    },
    extend: {
      colors: {
        border: "hsl(var(--border))",
        input: "hsl(var(--input))",
        ring: "hsl(var(--ring))",
        background: "hsl(var(--background))",
        foreground: "hsl(var(--foreground))",
        primary: {
          DEFAULT: "hsl(var(--primary))",
          foreground: "hsl(var(--primary-foreground))",
          hover: "hsl(var(--primary-hover))",
          light: "hsl(var(--primary-light))",
        },
        secondary: {
          DEFAULT: "hsl(var(--secondary))",
          foreground: "hsl(var(--secondary-foreground))",
          hover: "hsl(var(--secondary-hover))",
          light: "hsl(var(--secondary-light))",
        },
        accent: {
          DEFAULT: "hsl(var(--accent))",
          foreground: "hsl(var(--accent-foreground))",
          hover: "hsl(var(--accent-hover))",
          light: "hsl(var(--accent-light))",
        },
        destructive: {
          DEFAULT: "hsl(var(--destructive))",
          foreground: "hsl(var(--destructive-foreground))",
          light: "hsl(var(--destructive-light))",
        },
        success: {
          DEFAULT: "hsl(var(--success))",
          foreground: "hsl(var(--success-foreground))",
          light: "hsl(var(--success-light))",
        },
        warning: {
          DEFAULT: "hsl(var(--warning))",
          foreground: "hsl(var(--warning-foreground))",
          light: "hsl(var(--warning-light))",
        },
        info: {
          DEFAULT: "hsl(var(--info))",
          foreground: "hsl(var(--info-foreground))",
          light: "hsl(var(--info-light))",
        },
        muted: {
          DEFAULT: "hsl(var(--muted))",
          foreground: "hsl(var(--muted-foreground))",
        },
        popover: {
          DEFAULT: "hsl(var(--popover))",
          foreground: "hsl(var(--popover-foreground))",
        },
        card: {
          DEFAULT: "hsl(var(--card))",
          foreground: "hsl(var(--card-foreground))",
        },
        sidebar: {
          DEFAULT: "hsl(var(--sidebar-background))",
          foreground: "hsl(var(--sidebar-foreground))",
          primary: "hsl(var(--sidebar-primary))",
          "primary-foreground": "hsl(var(--sidebar-primary-foreground))",
          accent: "hsl(var(--sidebar-accent))",
          "accent-foreground": "hsl(var(--sidebar-accent-foreground))",
          border: "hsl(var(--sidebar-border))",
          ring: "hsl(var(--sidebar-ring))",
        },
        state: {
          production: "hsl(var(--state-production))",
          material: "hsl(var(--state-material))",
          changeover: "hsl(var(--state-changeover))",
          technical: "hsl(var(--state-technical))",
          quality: "hsl(var(--state-quality))",
          idle: "hsl(var(--state-idle))",
        },
        chart: {
          "1": "hsl(var(--chart-1))",
          "2": "hsl(var(--chart-2))",
          "3": "hsl(var(--chart-3))",
          "4": "hsl(var(--chart-4))",
          "5": "hsl(var(--chart-5))",
          "6": "hsl(var(--chart-6))",
        },
      },
      boxShadow: {
        soft: "var(--shadow-soft)",
        medium: "var(--shadow-medium)",
        strong: "var(--shadow-strong)",
        card: "var(--shadow-card)",
      },
      borderRadius: {
        lg: "var(--radius)",
        md: "calc(var(--radius) - 2px)",
        sm: "calc(var(--radius) - 4px)",
      },

      fontFamily: {
        sans:    ["var(--font-body)", "Inter", "system-ui", "sans-serif"],
        display: ["var(--font-display)", "Geist", "Inter", "system-ui", "sans-serif"],
        heading: ["var(--font-heading)", "Inter", "system-ui", "sans-serif"],
        body:    ["var(--font-body)", "Inter", "system-ui", "sans-serif"],
        mono:    ["var(--font-mono)", "ui-monospace", "monospace"],
      },
      fontSize: { ...rawScaleOverrides, ...pixelScaleTokens, ...semanticFontSizes } as unknown as Record<string, unknown>,
      keyframes: {
        "accordion-down": {
          from: { height: "0" },
          to: { height: "var(--radix-accordion-content-height)" },
        },
        "accordion-up": {
          from: { height: "var(--radix-accordion-content-height)" },
          to: { height: "0" },
        },
        shimmer: {
          "100%": { transform: "translateX(100%)" },
        },
        "fade-in": {
          from: { opacity: "0", transform: "translateY(4px)" },
          to: { opacity: "1", transform: "translateY(0)" },
        },
      },
      animation: {
        "accordion-down": "accordion-down 0.2s ease-out",
        "accordion-up": "accordion-up 0.2s ease-out",
        shimmer: "shimmer 1.6s infinite",
        "fade-in": "fade-in 0.25s ease-out both",
      },
    },
  },
  plugins: [tailwindcssAnimate, typography],
} satisfies Config;
