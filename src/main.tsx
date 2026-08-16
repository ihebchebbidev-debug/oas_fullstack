import { createRoot } from 'react-dom/client';
import App from './App';
import './index.css';
import './oas/tokens.css';
import { applyTypography } from './config/typography.runtime';
import { initIonic } from './ionic/setup';

applyTypography();
initIonic();

createRoot(document.getElementById('root')!).render(<App />);

// PWA app shell — best-effort registration, safe to skip (SSR/tests/unsupported browsers).
// Skipped inside the Capacitor native shell: assets are already bundled in the APK and a
// stale SW cache would serve an outdated shell after an app update.
const isNative =
  typeof window !== 'undefined' &&
  (Boolean((window as unknown as { Capacitor?: { isNativePlatform?: () => boolean } }).Capacitor
    ?.isNativePlatform?.()) ||
    window.location.protocol === 'capacitor:' ||
    window.location.protocol === 'file:');

if (typeof window !== 'undefined' && 'serviceWorker' in navigator && !isNative) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      /* offline shell is best-effort; ignore registration failures */
    });
  });
}

