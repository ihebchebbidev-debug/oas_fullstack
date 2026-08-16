/**
 * Ionic bootstrap — kept in one place so the design-token layer stays authoritative.
 *
 * We deliberately import only Ionic's core + structural CSS and skip
 * `typography.css` / `padding.css`, which would override the app's 13px root
 * font size and Tailwind spacing.
 */
import { setupIonicReact } from '@ionic/react';

import '@ionic/react/css/core.css';
import '@ionic/react/css/normalize.css';
import '@ionic/react/css/structure.css';
import '@ionic/react/css/display.css';
import '@ionic/react/css/flex-utils.css';

/** Ionic theme bridge: map Ionic CSS variables onto our design tokens. */
import './ionic-theme.css';

export function initIonic() {
  setupIonicReact({
    mode: 'md',
    // Ripple/animations stay on; they respect prefers-reduced-motion.
  });
}
