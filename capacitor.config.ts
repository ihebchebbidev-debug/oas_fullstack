import type { CapacitorConfig } from '@capacitor/cli';

/**
 * Live-reload against the Lovable sandbox is opt-in:
 *   CAP_LIVE_RELOAD=1 npx cap sync
 * Without it the app ships fully bundled (`dist/`) — required for a
 * standalone, installable APK.
 */
const liveReload = process.env.CAP_LIVE_RELOAD === '1';

const config: CapacitorConfig = {
  appId: 'app.lovable.bbbae44cdf3e4deeb8b931739903efa0',
  appName: 'OAS Production',
  webDir: 'dist',
  android: {
    allowMixedContent: true,
  },
  server: {
    androidScheme: 'https',
    ...(liveReload
      ? {
          url: 'https://bbbae44c-df3e-4dee-b8b9-31739903efa0.lovableproject.com?forceHideBadge=true',
          cleartext: true,
        }
      : {}),
  },
  plugins: {
    Keyboard: {
      resizeOnFullScreen: true,
    },
    // NB: @capacitor/splash-screen is not installed — the launch screen comes
    // from the generated drawable-*/splash.png resources.
    StatusBar: {
      style: 'DARK',
      backgroundColor: '#0b1220',
    },
  },
};

export default config;
