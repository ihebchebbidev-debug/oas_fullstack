# Build targets

The same React/Ionic codebase ships three ways.

## 1. Web (deployed)

`npm run build` → `dist/`, served by Lovable hosting. Browser history routing,
absolute `base: "/"`.

## 2. Android APK (Capacitor)

One script does everything: dependency check, icon/splash generation, web
build, `cap add android` (first run), `cap sync`, Gradle, and it copies the
finished APK into `dist-apk/`.

```bash
npm run android:apk           # debug APK  → dist-apk/app-debug.apk
npm run android:apk:release   # unsigned release APK
npm run android:apk:win       # same, Windows PowerShell
```

Prerequisites on your machine (only these two — everything else is handled):

- Node 18+
- A JDK — optional: Gradle 8.14 only supports JDK 17–24, so if your only JDK is
  newer (e.g. Android Studio's bundled Java 25, which fails with "Unsupported
  class file major version 69"), the script downloads a private Temurin 21 into
  `~/.oas-jdk/current` and uses it just for the build.


The script self-heals the rest: it auto-detects/locates the Android SDK
(`ANDROID_HOME`, `ANDROID_SDK_ROOT` or the default install path), installs any
missing SDK platform / build-tools / platform-tools through `sdkmanager` and
accepts their licenses, runs `npm ci`/`npm install` when packages are missing,
regenerates launcher icons and splash screens, recreates a missing or broken
`android/` project, writes `android/local.properties`, and fails with a clear
message at the exact step that went wrong.

Install on a device: `adb install -r dist-apk/oas-production-debug.apk`.

The `android/` folder is generated, not committed (`.gitignore`); the script
recreates it when missing, so a fresh clone builds with a single command.


### Icons and splash

Source artwork lives in `resources/`:

| File | Purpose |
| --- | --- |
| `icon-only.png` (1024×1024) | Legacy launcher icon |
| `icon-foreground.png` (1024×1024) | Adaptive-icon foreground |
| `icon-background.png` (1024×1024) | Adaptive-icon background (`#0b1220`) |
| `splash.png` / `splash-dark.png` (2732×2732) | Splash screens |

`npm run android:icons` regenerates every Android density from those files via
`@capacitor/assets`; the APK script runs it automatically. Replace the PNGs in
`resources/` to rebrand — nothing else needs editing.

### Live reload while developing natively

```bash
CAP_LIVE_RELOAD=1 npx cap sync android && npx cap run android
```

The APK then loads the Lovable preview URL instead of bundled assets. Never
ship a store build made this way — run the plain script instead.

### Notes specific to this app

- The service worker is not registered inside the native shell (assets are
  already bundled, and a stale cache would pin an old build).
- `capacitor.config.ts` uses `androidScheme: 'https'`, so the WebView serves
  from `https://localhost` and `BrowserRouter` deep links keep working.
- Camera permission is requested at first use by the QR scan screen.

## 3. Desktop (Electron)

```bash
npm run desktop:dev         # against the vite dev server
npm run desktop:build       # packages into electron-release/
```

`vite.config.ts` uses `base: "./"` in electron mode and the app falls back to
hash routing under `file://`, which is what makes the packaged build work.
`electron-release/` is git-ignored (the Electron runtime is ~200 MB).
