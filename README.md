# OAS — Design Templates (React + Vite + Tailwind + Ionic + Capacitor)

Standalone, runnable project containing the app's design system (tokens, typography config,
UI primitives) and page templates on a single demo page, plus Ionic mobile controls and a
Capacitor native shell.

No backend, no data layer — demo data only (`src/data/demo.ts`).

## Run

```bash
cd OAS
npm install
npm run dev      # http://localhost:8080
npm run build
npm run preview
```

## Lovable compatibility

This folder is configured exactly like a Lovable project so it can be dropped into Lovable
as its own project and work out of the box:

- Dev server on host `::`, port **8080**
- `lovable-tagger` `componentTagger()` enabled in development mode
- `@` → `./src` path alias (vite + tsconfig)
- `components.json` for shadcn-style generation
- `vercel.json` SPA rewrites + built-in SPA fallback compatibility
- SEO head tags in `index.html`

To use it as a Lovable project, make `OAS/` the repository root (or copy its contents into a
new project) and click Publish.

## Ionic

- Bootstrapped in `src/ionic/setup.ts` (`setupIonicReact({ mode: 'md' })`)
- Only Ionic's core/structural CSS is imported — `typography.css` and `padding.css` are
  intentionally skipped so the app's 13px root scale and Tailwind spacing stay authoritative
- `src/ionic/ionic-theme.css` maps every Ionic CSS variable onto our HSL design tokens, so
  Ionic components follow the light/dark theme automatically
- Demo section: `src/components/templates/IonicShowcase.tsx` (buttons, segments, chips,
  list rows, toggle, range, progress, spinner)

## Capacitor (native iOS / Android)

`capacitor.config.ts` is ready, with hot-reload pointed at the Lovable sandbox URL.

```bash
npm install
npx cap add ios          # and/or: npx cap add android
npx cap update ios       # or android
npm run build
npx cap sync
npx cap run ios          # or: npx cap run android
```

Requires Xcode (iOS) or Android Studio (Android). Run `npx cap sync` after every
dependency change or new build. Remove the `server.url` block in `capacitor.config.ts`
before shipping a store build so the app loads the bundled `dist/` assets.

Read more: https://lovable.dev/blogs/TODO

## What's inside

- `src/index.css` + `tailwind.config.ts` — design tokens (light/dark, shadows, gradients)
- `src/config/typography.*` — config-driven type scale, 13px root font size
- `src/components/ui/*` — button, input, textarea, label, select, checkbox/switch, badge, card, table
- `src/components/templates/*` — `AppSidebar`, `StatCard`, `ChartCard`, `SimplePaginationBar`, `IonicShowcase`
- `src/App.tsx` — showcase page: sidebar, topbar, stat cards, list toolbar + filters +
  table/grid + pagination, reporting cards, form inputs, buttons/badges, typography, Ionic
