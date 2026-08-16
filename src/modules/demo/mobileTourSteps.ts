/**
 * Guided product demo — mobile operator app.
 *
 * Same contract as the web tour: navigate to `route`, optionally run a UI
 * action, spotlight `selector` and narrate `textKey`. Steps are ordered as a
 * real shift: scan → presence → post → production → stop → changeover →
 * map → inbox → KPI → history → shift end.
 */

import { getSessionState, openSession } from '@/modules/auth/store/session';

export interface MobileTourStep {
  id: string;
  route: string;
  selector?: string;
  titleKey: string;
  textKey: string;
  run?: () => void;
}

/** Make sure a session is open so the operator screens are reachable. */
const ensureSession = () => {
  if (!getSessionState().session) openSession('PO-104');
};

/** BL-017 — open the shared-tablet operator switch sheet from the side menu. */
const openSwitchUser = () => {
  ensureSession();
  if (document.querySelector('[data-demo="m-switch-sheet"]')) return;
  document.querySelector<HTMLElement>('[data-demo="m-menu"]')?.click();
  window.setTimeout(() => document.querySelector<HTMLElement>('[data-demo="m-switch"]')?.click(), 250);
};

export const MOBILE_TOUR_STEPS: MobileTourStep[] = [
  {
    id: 'welcome',
    route: '/mobile/home',
    titleKey: 'mdemo.step.welcome.title',
    textKey: 'mdemo.step.welcome.text',
    run: ensureSession,
  },
  {
    id: 'scan',
    route: '/mobile/scan',
    selector: '[data-demo="m-scan"]',
    titleKey: 'mdemo.step.scan.title',
    textKey: 'mdemo.step.scan.text',
  },
  {
    id: 'post',
    route: '/mobile/home',
    selector: '[data-demo="m-post"]',
    titleKey: 'mdemo.step.post.title',
    textKey: 'mdemo.step.post.text',
    run: ensureSession,
  },
  {
    id: 'presence',
    route: '/mobile/home',
    selector: '[data-demo="m-presence"]',
    titleKey: 'mdemo.step.presence.title',
    textKey: 'mdemo.step.presence.text',
    run: ensureSession,
  },
  {
    id: 'actions',
    route: '/mobile/home',
    selector: '[data-demo="m-actions"]',
    titleKey: 'mdemo.step.actions.title',
    textKey: 'mdemo.step.actions.text',
  },
  {
    id: 'production',
    route: '/mobile/production',
    selector: '[data-demo="m-prod-anchor"]',
    titleKey: 'mdemo.step.production.title',
    textKey: 'mdemo.step.production.text',
  },
  {
    id: 'stop',
    route: '/mobile/stop',
    selector: '[data-demo="m-stop-causes"]',
    titleKey: 'mdemo.step.stop.title',
    textKey: 'mdemo.step.stop.text',
  },
  {
    id: 'changeover',
    route: '/mobile/changeover',
    selector: '[data-demo="m-changeover"]',
    titleKey: 'mdemo.step.changeover.title',
    textKey: 'mdemo.step.changeover.text',
  },
  {
    id: 'neighbor',
    route: '/mobile/neighbor?post=PO-105',
    selector: '[data-demo="m-neighbor"]',
    titleKey: 'mdemo.step.neighbor.title',
    textKey: 'mdemo.step.neighbor.text',
  },
  {
    id: 'map',
    route: '/mobile/map',
    selector: '[data-demo="m-map"]',
    titleKey: 'mdemo.step.map.title',
    textKey: 'mdemo.step.map.text',
  },
  {
    id: 'inbox',
    route: '/mobile/inbox',
    selector: '[data-demo="m-inbox"]',
    titleKey: 'mdemo.step.inbox.title',
    textKey: 'mdemo.step.inbox.text',
  },
  {
    id: 'kpi',
    route: '/mobile/kpi',
    selector: '[data-demo="m-kpi"]',
    titleKey: 'mdemo.step.kpi.title',
    textKey: 'mdemo.step.kpi.text',
  },
  {
    id: 'history',
    route: '/mobile/history',
    selector: '[data-demo="m-history"]',
    titleKey: 'mdemo.step.history.title',
    textKey: 'mdemo.step.history.text',
  },
  {
    id: 'sync',
    route: '/mobile/history',
    selector: '[data-demo="m-sync"]',
    titleKey: 'mdemo.step.sync.title',
    textKey: 'mdemo.step.sync.text',
  },
  {
    id: 'nav',
    route: '/mobile/home',
    selector: '[data-demo="m-nav"]',
    titleKey: 'mdemo.step.nav.title',
    textKey: 'mdemo.step.nav.text',
  },
  {
    id: 'switchUser',
    route: '/mobile/home',
    selector: '[data-demo="m-switch-sheet"]',
    titleKey: 'mdemo.step.switchUser.title',
    textKey: 'mdemo.step.switchUser.text',
    run: openSwitchUser,
  },
  {
    id: 'shiftEnd',
    route: '/mobile/shift-end',
    run: () => document.querySelector<HTMLElement>('[data-demo="m-switch-sheet"] button[aria-label]')?.click(),
    selector: '[data-demo="m-shiftend"]',
    titleKey: 'mdemo.step.shiftEnd.title',
    textKey: 'mdemo.step.shiftEnd.text',
  },
  {
    id: 'end',
    route: '/mobile/home',
    titleKey: 'mdemo.step.end.title',
    textKey: 'mdemo.step.end.text',
    run: ensureSession,
  },
];
