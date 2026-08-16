/**
 * Guided product demo — the ordered script the auto-play tour follows.
 *
 * Each step navigates the web app to `route`, optionally spotlights the first
 * element matching `selector`, and narrates `textKey` with the browser voice.
 * `run` fires once when the step opens (open a drawer, switch a tab, …) so the
 * tour can also show modals and panels, not just pages.
 */

export interface TourStep {
  id: string;
  /** Route to navigate to before narrating. */
  route: string;
  /** CSS selector of the element to spotlight (first match). */
  selector?: string;
  /** i18n keys — short title + narrated paragraph. */
  titleKey: string;
  textKey: string;
  /** Optional UI action played when the step starts (clicks a button, …). */
  run?: () => void;
}

/** Click the first element matching one of the selectors, if present. */
const click = (...selectors: string[]) => () => {
  for (const sel of selectors) {
    const el = document.querySelector<HTMLElement>(sel);
    if (el) {
      el.click();
      return;
    }
  }
};

/** Close any open detail drawer so the next step starts from a clean screen. */
const closeDrawer = () => {
  document.querySelector<HTMLElement>('[data-demo="drawer-close"]')?.click();
};

export const TOUR_STEPS: TourStep[] = [
  {
    id: 'welcome',
    route: '/web/dashboard',
    titleKey: 'demo.step.welcome.title',
    textKey: 'demo.step.welcome.text',
  },
  {
    id: 'nav',
    route: '/web/dashboard',
    selector: 'nav ul',
    titleKey: 'demo.step.nav.title',
    textKey: 'demo.step.nav.text',
  },
  {
    id: 'kpis',
    route: '/web/dashboard',
    selector: '[data-demo="dash-kpis"]',
    titleKey: 'demo.step.kpis.title',
    textKey: 'demo.step.kpis.text',
  },
  {
    id: 'roles',
    route: '/web/dashboard',
    selector: '[data-demo="dash-role"]',
    titleKey: 'demo.step.roles.title',
    textKey: 'demo.step.roles.text',
  },
  {
    id: 'charts',
    route: '/web/dashboard',
    selector: '[data-demo="dash-charts"]',
    titleKey: 'demo.step.charts.title',
    textKey: 'demo.step.charts.text',
  },
  {
    id: 'shopfloor',
    route: '/web/shopfloor',
    selector: '[data-demo="floor-map"]',
    titleKey: 'demo.step.shopfloor.title',
    textKey: 'demo.step.shopfloor.text',
  },
  {
    id: 'post',
    route: '/web/shopfloor',
    selector: '[data-demo="post-drawer"]',
    titleKey: 'demo.step.post.title',
    textKey: 'demo.step.post.text',
    run: click('[data-demo="post-card"]'),
  },
  {
    id: 'alerts',
    route: '/web/alerts',
    titleKey: 'demo.step.alerts.title',
    textKey: 'demo.step.alerts.text',
    run: closeDrawer,
  },
  {
    id: 'event',
    route: '/web/alerts',
    selector: '[data-demo="event-panel"]',
    titleKey: 'demo.step.event.title',
    textKey: 'demo.step.event.text',
    run: click('[data-demo="event-row"]'),
  },
  {
    id: 'eventActions',
    route: '/web/alerts',
    selector: '[data-demo="event-actions"]',
    titleKey: 'demo.step.eventActions.title',
    textKey: 'demo.step.eventActions.text',
    // Keep the panel open when the step is opened directly from the progress dots.
    run: click('[data-demo="event-panel"] [data-demo="event-actions"]', '[data-demo="event-row"]'),
  },
  {
    id: 'assignments',
    route: '/web/assignments',
    titleKey: 'demo.step.assignments.title',
    textKey: 'demo.step.assignments.text',
    run: closeDrawer,
  },
  {
    id: 'assignActions',
    route: '/web/assignments',
    selector: '[data-demo="assign-actions"]',
    titleKey: 'demo.step.assignActions.title',
    textKey: 'demo.step.assignActions.text',
    run: click('[data-demo="assign-actions"] button'),
  },
  {
    id: 'assignBoard',
    route: '/web/assignments',
    selector: '[data-demo="assign-board"]',
    titleKey: 'demo.step.assignBoard.title',
    textKey: 'demo.step.assignBoard.text',
  },
  {
    id: 'assignRoster',
    route: '/web/assignments',
    selector: '[data-demo="assign-roster"]',
    titleKey: 'demo.step.assignRoster.title',
    textKey: 'demo.step.assignRoster.text',
  },
  {
    id: 'reports',
    route: '/web/reports',
    selector: '[data-demo="reports-filter"]',
    titleKey: 'demo.step.reports.title',
    textKey: 'demo.step.reports.text',
  },
  {
    id: 'sla',
    route: '/web/reports',
    selector: '[data-demo="sla-service"]',
    titleKey: 'demo.step.sla.title',
    textKey: 'demo.step.sla.text',
  },
  {
    id: 'cadenceGap',
    route: '/web/reports',
    selector: '[data-demo="cadence-gap"]',
    titleKey: 'demo.step.cadenceGap.title',
    textKey: 'demo.step.cadenceGap.text',
  },
  {
    id: 'adoption',
    route: '/web/reports',
    selector: '[data-demo="adoption"]',
    titleKey: 'demo.step.adoption.title',
    textKey: 'demo.step.adoption.text',
  },
  {
    id: 'rawExport',
    route: '/web/reports',
    selector: '[data-demo="raw-export"]',
    titleKey: 'demo.step.rawExport.title',
    textKey: 'demo.step.rawExport.text',
  },
  {
    id: 'shiftReport',
    route: '/web/shift-report',
    selector: '[data-demo="shift-report"]',
    titleKey: 'demo.step.shiftReport.title',
    textKey: 'demo.step.shiftReport.text',
  },
  {
    id: 'andon',
    route: '/web/andon',
    titleKey: 'demo.step.andon.title',
    textKey: 'demo.step.andon.text',
  },
  {
    id: 'andonBoard',
    route: '/web/andon',
    selector: '[data-demo="andon-board"]',
    titleKey: 'demo.step.andonBoard.title',
    textKey: 'demo.step.andonBoard.text',
  },
  {
    id: 'andonEvents',
    route: '/web/andon',
    selector: '[data-demo="andon-events"]',
    titleKey: 'demo.step.andonEvents.title',
    textKey: 'demo.step.andonEvents.text',
  },
  {
    id: 'referentials',
    route: '/web/referentials',
    titleKey: 'demo.step.referentials.title',
    textKey: 'demo.step.referentials.text',
  },
  {
    id: 'hierarchy',
    route: '/web/referentials',
    selector: '[data-demo="hierarchy"]',
    titleKey: 'demo.step.hierarchy.title',
    textKey: 'demo.step.hierarchy.text',
  },
  {
    id: 'references',
    route: '/web/referentials',
    selector: '[data-demo="references"]',
    titleKey: 'demo.step.references.title',
    textKey: 'demo.step.references.text',
  },
  {
    id: 'causeTree',
    route: '/web/referentials',
    selector: '[data-demo="cause-tree"]',
    titleKey: 'demo.step.causeTree.title',
    textKey: 'demo.step.causeTree.text',
  },
  {
    id: 'cadences',
    route: '/web/referentials',
    selector: '[data-demo="cadences"]',
    titleKey: 'demo.step.cadences.title',
    textKey: 'demo.step.cadences.text',
  },
  {
    id: 'causeReview',
    route: '/web/referentials',
    selector: '[data-demo="cause-review"]',
    titleKey: 'demo.step.causeReview.title',
    textKey: 'demo.step.causeReview.text',
  },
  {
    id: 'qrCodes',
    route: '/web/admin?tab=qr',
    selector: '[data-demo="qr-panel"]',
    titleKey: 'demo.step.qrCodes.title',
    textKey: 'demo.step.qrCodes.text',
  },
  {
    id: 'shifts',
    route: '/web/admin?tab=shifts',
    selector: '[data-demo="shifts-panel"]',
    titleKey: 'demo.step.shifts.title',
    textKey: 'demo.step.shifts.text',
  },
  {
    id: 'import',
    route: '/web/admin?tab=import',
    selector: '[data-demo="import-panel"]',
    titleKey: 'demo.step.import.title',
    textKey: 'demo.step.import.text',
  },
  {
    id: 'admin',
    route: '/web/admin?tab=users',
    titleKey: 'demo.step.admin.title',
    textKey: 'demo.step.admin.text',
  },
  {
    id: 'usersAdmin',
    route: '/web/admin?tab=users',
    selector: '[data-demo="users-panel"]',
    titleKey: 'demo.step.usersAdmin.title',
    textKey: 'demo.step.usersAdmin.text',
  },
  {
    id: 'userCreate',
    route: '/web/admin?tab=users',
    selector: '[data-demo="user-create"]',
    titleKey: 'demo.step.userCreate.title',
    textKey: 'demo.step.userCreate.text',
  },
  {
    id: 'corrections',
    route: '/web/admin?tab=corrections',
    selector: '[data-demo="corrections"]',
    titleKey: 'demo.step.corrections.title',
    textKey: 'demo.step.corrections.text',
  },
  {
    id: 'audit',
    route: '/web/admin?tab=audit',
    selector: '[data-demo="audit-panel"]',
    titleKey: 'demo.step.audit.title',
    textKey: 'demo.step.audit.text',
  },
  {
    id: 'mobile',
    route: '/web/dashboard',
    titleKey: 'demo.step.mobile.title',
    textKey: 'demo.step.mobile.text',
  },
  {
    id: 'end',
    route: '/web/dashboard',
    titleKey: 'demo.step.end.title',
    textKey: 'demo.step.end.text',
  },
];
