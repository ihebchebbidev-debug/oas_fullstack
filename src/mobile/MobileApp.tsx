import { Suspense, lazy, useEffect, useState } from 'react';
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { Home, Map, Inbox, ScanLine, BarChart3, WifiOff, Wifi, RefreshCw, Sparkles, LogOut, Menu, X, UserRoundCog } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useI18n } from '@/i18n/I18nProvider';
import { LangSwitcher } from '@/i18n/LangSwitcher';
import { ThemeToggle } from '@/theme/ThemeProvider';

const OperatorHome = lazy(() => import('@/modules/shopfloor/pages/OperatorHome'));
const DeclareStop = lazy(() => import('@/modules/declarations/pages/DeclareStop'));
const DeclareProduction = lazy(() => import('@/modules/declarations/pages/DeclareProduction'));
const ChangeoverPage = lazy(() => import('@/modules/declarations/pages/ChangeoverPage'));
const QualityCheckPage = lazy(() => import('@/modules/declarations/pages/QualityCheckPage'));
const ScanPage = lazy(() => import('@/modules/traceability/pages/ScanPage'));
const ShopFloorPage = lazy(() => import('@/modules/shopfloor/pages/ShopFloorPage'));
const NeighborStop = lazy(() => import('@/modules/declarations/pages/NeighborStop'));
const InterventionInbox = lazy(() => import('@/modules/interventions/pages/InterventionInbox'));
const MobileKpi = lazy(() => import('@/modules/dashboard/pages/MobileKpi'));
import MobileLogin from '@/modules/auth/pages/MobileLogin';
import { MobilePageSkeleton } from '@/components/loading/PageSkeletons';
const HistoryPage = lazy(() => import('@/modules/traceability/pages/HistoryPage'));
const ShiftEnd = lazy(() => import('@/modules/reporting/pages/ShiftEnd'));
import { computeMetrics, flushPending, switchOperator, useOnline, useSessionState, useSyncStatus } from '@/modules/auth/store/session';
import { MobileDemoTour } from '@/modules/demo/MobileDemoTour';
import { PinPanel } from '@/modules/auth/components/PinPanel';
import { getAuth, signOut, useAuth } from '@/oas/authStore';
import { PluginGate, useIsPluginEnabled } from '@/modules/shared/plugins';
import { ToastStack } from '@/shared/components/ToastStack';


/** Each tab declares the plugin that owns it — disabled plugins drop out. */
const TABS = [
  { to: '/mobile/home',  key: 'nav.mobile.poste', icon: Home,      plugin: 'OA0003SHOPFLOOR' },
  { to: '/mobile/map',   key: 'nav.mobile.map',   icon: Map,       plugin: 'OA0003SHOPFLOOR' },
  { to: '/mobile/scan',  key: 'nav.mobile.scan',  icon: ScanLine,  plugin: 'OA0012TRACEABILITY' },
  { to: '/mobile/inbox', key: 'nav.mobile.inbox', icon: Inbox,     plugin: 'OA0005INTERVENTIONS' },
  { to: '/mobile/kpi',   key: 'nav.mobile.kpi',   icon: BarChart3, plugin: 'OA0002DASHBOARD' },
] as const;

/** Slide-in side panel — language, theme, guided demo and sign-out. */
function MobileSidePanel({
  open,
  onClose,
  onDemo,
  onSwitchUser,
}: {
  open: boolean;
  onClose: () => void;
  onDemo: () => void;
  onSwitchUser: () => void;
}) {
  const { t } = useI18n();
  const auth = useAuth();

  useEffect(() => {
    if (!open) return;
    const esc = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', esc);
    return () => window.removeEventListener('keydown', esc);
  }, [open, onClose]);

  return (
    <div className={cn('absolute inset-0 z-40', open ? '' : 'pointer-events-none')} aria-hidden={!open}>
      <button
        type="button"
        tabIndex={open ? 0 : -1}
        aria-label={t('common.close')}
        onClick={onClose}
        className={cn(
          'absolute inset-0 bg-foreground/40 backdrop-blur-[2px] transition-opacity duration-200',
          open ? 'opacity-100' : 'opacity-0',
        )}
      />
      <aside
        role="dialog"
        aria-modal={open}
        aria-label={t('common.menu')}
        className={cn(
          'absolute inset-y-0 end-0 flex w-[78%] max-w-[300px] flex-col gap-4 border-s border-border bg-background p-4 shadow-2xl transition-transform duration-250 ease-out',
          open ? 'translate-x-0' : 'ltr:translate-x-full rtl:-translate-x-full',
        )}
      >
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="truncate text-[0.6875rem] font-medium uppercase tracking-[0.04em] text-muted-foreground">
              {t('common.menu')}
            </p>
            <p className="truncate text-sm font-semibold">{auth?.displayName ?? auth?.email ?? '—'}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border text-muted-foreground active:bg-muted"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-2 rounded-xl border border-border p-3">
          <p className="text-[0.6875rem] font-medium uppercase tracking-[0.04em] text-muted-foreground">
            {t('common.language')}
          </p>
          <LangSwitcher variant="segment" className="w-full justify-between" />
        </div>

        <div className="flex items-center justify-between gap-2 rounded-xl border border-border p-3">
          <p className="text-sm font-medium">{t('common.theme')}</p>
          <ThemeToggle className="h-9 w-9" />
        </div>

        <button
          type="button"
          onClick={() => {
            onClose();
            onDemo();
          }}
          className="flex min-h-[48px] items-center gap-2 rounded-xl border border-border px-3 text-sm font-medium text-primary active:bg-muted"
        >
          <Sparkles className="h-4 w-4" />
          {t('demo.start')}
        </button>

        {/* BL-017 — shared tablet: hand the post over to the next operator. */}
        <button
          data-demo="m-switch"
          type="button"
          onClick={() => {
            onClose();
            onSwitchUser();
          }}
          className="flex min-h-[48px] items-center gap-2 rounded-xl border border-border px-3 text-sm font-medium active:bg-muted"
        >
          <UserRoundCog className="h-4 w-4" />
          {t('mobile.switchUser')}
        </button>

        <button
          type="button"
          onClick={signOut}
          className="mt-auto flex min-h-[48px] items-center gap-2 rounded-xl border border-border px-3 text-sm font-medium text-muted-foreground active:bg-muted"
        >
          <LogOut className="h-4 w-4 rtl:rotate-180" />
          {t('common.signOut')}
        </button>
      </aside>
    </div>
  );
}

/** One bottom-bar tab, hidden when its owning plugin is deactivated. */
function MobileTab({
  to,
  labelKey,
  icon: Icon,
  plugin,
  active,
}: {
  to: string;
  labelKey: string;
  icon: typeof Home;
  plugin: string;
  active: boolean;
}) {
  const { t } = useI18n();
  const enabled = useIsPluginEnabled(plugin);
  if (!enabled) return null;
  return (
    <li className="flex-1">
      <NavLink
        to={to}
        className={cn(
          'relative flex min-h-[56px] flex-col items-center justify-center gap-1 text-[0.6875rem] font-medium transition-colors',
          active ? 'text-foreground' : 'text-muted-foreground active:text-foreground',
        )}
      >
        <span
          aria-hidden
          className={cn(
            'absolute top-0 h-0.5 rounded-full bg-foreground transition-all duration-300',
            active ? 'w-8 opacity-100' : 'w-0 opacity-0',
          )}
        />
        <Icon className={cn('h-5 w-5 transition-transform', active && 'stroke-[2.25] -translate-y-0.5')} />
        {t(labelKey)}
      </NavLink>
    </li>
  );
}

function MobileHeader({ onMenu }: { onMenu: () => void }) {
  const { t } = useI18n();
  const auth = useAuth();
  const state = useSessionState();
  const metrics = computeMetrics(state);
  const online = useOnline();
  const status = useSyncStatus();
  const syncing = status === 'syncing';

  const pending = metrics.pendingSync;

  const flush = () => {
    if (!online || !pending || syncing) return;
    void flushPending();
  };

  return (
    <header className="sticky top-0 z-20 border-b border-border bg-background/95 px-4 py-2.5 backdrop-blur">
      <div className="flex items-center gap-2">
        <div className="min-w-0 flex-1">
          <p className="truncate text-[0.6875rem] font-medium uppercase tracking-[0.04em] text-muted-foreground">
            {state.session ? `${t('mobile.shift')} · ${state.session.postCode}` : t('mobile.shift')}
          </p>
          <p className="truncate text-sm font-semibold">{state.session?.operator ?? auth?.displayName ?? auth?.email ?? '—'}</p>
        </div>
        <button
          type="button"
          data-demo="m-sync"
          onClick={flush}
          disabled={!online || !pending || syncing}
          title={online ? t('mobile.sync.now') : t('mobile.sync.offline')}
          className={cn(
            'inline-flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1 text-[0.6875rem] transition-colors',
            status === 'error'
              ? 'border-state-technical/50 bg-state-technical/10 text-state-technical'
              : pending && online
              ? 'border-state-material/50 bg-state-material/10 text-state-material'
              : 'border-border bg-muted text-muted-foreground',
          )}
        >
          {syncing ? (
            <RefreshCw className="h-3 w-3 animate-spin" />
          ) : online ? (
            <Wifi className="h-3 w-3" />
          ) : (
            <WifiOff className="h-3 w-3" />
          )}
          <span dir="ltr">
            {!online
              ? t('mobile.queue', { n: pending })
              : status === 'error' && pending
              ? t('mobile.sync.retry', { n: pending })
              : pending
              ? t('mobile.queue', { n: pending })
              : t('mobile.sync.upToDate')}
          </span>
        </button>
        <button
          type="button"
          onClick={onMenu}
          data-demo="m-menu"
          title={t('common.menu')}
          aria-label={t('common.menu')}
          className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border text-foreground active:bg-muted"
        >
          <Menu className="h-4 w-4" />
        </button>
      </div>
    </header>
  );
}


/** BL-017 — shared tablet: matricule + PIN of the incoming operator, ≤3 taps. */
function SwitchUserSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useI18n();
  const state = useSessionState();
  if (!open) return null;
  return (
    <div data-demo="m-switch-sheet" className="absolute inset-0 z-50 flex flex-col bg-background">
      <div className="flex items-start justify-between gap-2 border-b border-border px-4 py-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{t('mobile.switchUser')}</p>
          <p className="truncate text-xs text-muted-foreground">
            {state.session ? t('mobile.switchUser.keepPost', { post: state.session.postCode }) : t('mobile.switchUser.noSession')}
          </p>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label={t('common.close')}
          className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border text-muted-foreground active:bg-muted"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto p-4">
        <PinPanel
          onSuccess={() => {
            const next = getAuth();
            switchOperator(next?.displayName ?? next?.email ?? '');
            onClose();
          }}
        />
      </div>
    </div>
  );
}

export default function MobileApp() {
  const { pathname } = useLocation();
  const { t, dir } = useI18n();
  const auth = useAuth();
  const [demo, setDemo] = useState(false);
  const [menu, setMenu] = useState(false);
  const [switchUser, setSwitchUser] = useState(false);

  if (pathname.endsWith('/login')) {
    return (
      <Routes>
        <Route path="login" element={<MobileLogin />} />
      </Routes>
    );
  }

  if (!auth || auth.workspace !== 'mobile') return <Navigate to="/mobile/login" replace />;

  return (
    <div dir={dir} className="oas-mobile-root flex h-[100dvh] justify-center overflow-hidden bg-muted/40">
      <div className="relative flex h-full w-full max-w-[430px] flex-col overflow-hidden bg-background text-foreground shadow-2xl">
        <MobileHeader onMenu={() => setMenu(true)} />
        <MobileSidePanel
          open={menu}
          onClose={() => setMenu(false)}
          onDemo={() => setDemo(true)}
          onSwitchUser={() => setSwitchUser(true)}
        />
        <SwitchUserSheet open={switchUser} onClose={() => setSwitchUser(false)} />


        <main data-demo="mobile-scroll" className="min-h-0 flex-1 overflow-y-auto px-4 py-4 pb-6">

          <Suspense fallback={<MobilePageSkeleton />}>
          <Routes>
            <Route index element={<Navigate to="home" replace />} />
            <Route path="home" element={<OperatorHome />} />
            <Route path="stop" element={<PluginGate code="OA0004DECLARATIONS"><DeclareStop /></PluginGate>} />
            <Route path="production" element={<PluginGate code="OA0004DECLARATIONS"><DeclareProduction /></PluginGate>} />
            <Route path="changeover" element={<PluginGate code="OA0004DECLARATIONS"><ChangeoverPage /></PluginGate>} />
            <Route path="quality" element={<PluginGate code="OA0004DECLARATIONS"><QualityCheckPage /></PluginGate>} />
            <Route path="scan" element={<PluginGate code="OA0012TRACEABILITY"><ScanPage /></PluginGate>} />
            <Route path="neighbor" element={<PluginGate code="OA0004DECLARATIONS"><NeighborStop /></PluginGate>} />
            <Route path="map" element={<PluginGate code="OA0003SHOPFLOOR"><ShopFloorPage /></PluginGate>} />
            <Route path="inbox" element={<PluginGate code="OA0005INTERVENTIONS"><InterventionInbox /></PluginGate>} />
            <Route path="kpi" element={<PluginGate code="OA0002DASHBOARD"><MobileKpi /></PluginGate>} />
            <Route path="history" element={<PluginGate code="OA0012TRACEABILITY"><HistoryPage /></PluginGate>} />
            <Route path="shift-end" element={<PluginGate code="OA0009REPORTING"><ShiftEnd /></PluginGate>} />
            <Route path="*" element={<Navigate to="/mobile/home" replace />} />
          </Routes>
          </Suspense>
        </main>

        <nav className="z-20 shrink-0 border-t border-border bg-background/95 pb-[env(safe-area-inset-bottom)] backdrop-blur">
          <ul data-demo="m-nav" className="flex">
            {TABS.map(({ to, key, icon, plugin }) => (
              <MobileTab key={to} to={to} labelKey={key} icon={icon} plugin={plugin} active={pathname.startsWith(to)} />
            ))}

          </ul>
        </nav>

        <MobileDemoTour open={demo} onClose={() => setDemo(false)} />
      </div>
      <ToastStack />
    </div>
  );
}
