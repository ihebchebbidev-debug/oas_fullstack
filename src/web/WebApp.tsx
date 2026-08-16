import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, Map, Bell, Database, PanelLeft, PanelLeftClose, Tv,
  BarChart3, Settings, Users, PlayCircle, FileText, LogOut, Menu, X,
} from 'lucide-react';
import { Suspense, lazy, useEffect, useState } from 'react';

import { cn } from '@/lib/utils';
import { useI18n } from '@/i18n/I18nProvider';
import { LangSwitcher } from '@/i18n/LangSwitcher';
import { ThemeToggle } from '@/theme/ThemeProvider';
import { DemoTour } from '@/modules/demo/DemoTour';
import { ToastStack } from '@/shared/components/ToastStack';
import { signOut, useAuth } from '@/oas/authStore';
import { PluginGate, useIsPluginEnabled } from '@/modules/shared/plugins';
const ManagerDashboard = lazy(() => import('@/modules/dashboard/pages/ManagerDashboard'));
const ShopFloorBoard = lazy(() => import('@/modules/shopfloor/pages/ShopFloorBoard'));
const AlertsQueue = lazy(() => import('@/modules/alerts/pages/AlertsQueue'));
const Referentials = lazy(() => import('@/modules/referentials/pages/Referentials'));
import WebLogin from '@/modules/auth/pages/WebLogin';
import { WebPageSkeleton } from '@/components/loading/PageSkeletons';
const AndonTv = lazy(() => import('@/modules/andon/pages/AndonTv'));
const Reports = lazy(() => import('@/modules/reporting/pages/Reports'));
const Admin = lazy(() => import('@/modules/console/pages/Admin'));
const Assignments = lazy(() => import('@/modules/assignments/pages/Assignments'));
const ShiftReport = lazy(() => import('@/modules/reporting/pages/ShiftReport'));


/** Nav is derived from the plugin catalogue: each item declares its owner. */
const NAV = [
  { to: '/web/dashboard',    key: 'nav.web.dashboard',    icon: LayoutDashboard, plugin: 'OA0002DASHBOARD' },
  { to: '/web/shopfloor',    key: 'nav.web.shopfloor',    icon: Map,             plugin: 'OA0003SHOPFLOOR' },
  { to: '/web/alerts',       key: 'nav.web.alerts',       icon: Bell,            plugin: 'OA0006ALERTS' },
  { to: '/web/assignments',  key: 'nav.web.assignments',  icon: Users,           plugin: 'OA0007ASSIGNMENTS' },
  { to: '/web/reports',      key: 'nav.web.reports',      icon: BarChart3,       plugin: 'OA0009REPORTING' },
  { to: '/web/shift-report', key: 'nav.web.shiftReport',  icon: FileText,        plugin: 'OA0009REPORTING' },
  { to: '/web/andon',        key: 'nav.web.andon',        icon: Tv,              plugin: 'OA0010ANDON' },
  { to: '/web/referentials', key: 'nav.web.referentials', icon: Database,        plugin: 'OA0008REFERENTIALS' },
  { to: '/web/admin',        key: 'nav.web.admin',        icon: Settings,        plugin: 'OA0011CONSOLE' },
] as const;

/** One nav row, hidden when its owning plugin is deactivated. */
function NavItem({
  to,
  labelKey,
  icon: Icon,
  plugin,
  collapsed,
}: {
  to: string;
  labelKey: string;
  icon: typeof Map;
  plugin: string;
  collapsed: boolean;
}) {
  const { t } = useI18n();
  const enabled = useIsPluginEnabled(plugin);
  if (!enabled) return null;
  return (
    <li>
      <NavLink
        to={to}
        className={({ isActive }) =>
          cn(
            'flex h-9 items-center gap-2 rounded-md px-2 text-sm text-sidebar-foreground/80 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground',
            isActive && 'bg-sidebar-accent font-semibold text-sidebar-foreground',
          )
        }
        title={collapsed ? t(labelKey) : undefined}
      >
        <Icon className="h-4 w-4 shrink-0" />
        {!collapsed && <span className="truncate">{t(labelKey)}</span>}
      </NavLink>
    </li>
  );
}



/** Web shell — supervisor / manager / admin surfaces at 13px dense scale. */
export default function WebApp() {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [tourOpen, setTourOpen] = useState(false);

  const { pathname } = useLocation();
  const { t, dir } = useI18n();
  const auth = useAuth();

  /** Navigating always dismisses the phone drawer. */
  useEffect(() => setMobileOpen(false), [pathname]);

  /** Labels stay visible inside the phone drawer even when collapsed on desktop. */
  const sidebarCollapsed = collapsed && !mobileOpen;



  if (pathname.endsWith('/login')) {
    return (
      <Routes>
        <Route path="login" element={<WebLogin />} />
      </Routes>
    );
  }

  if (!auth || auth.workspace !== 'web') return <Navigate to="/web/login" replace />;

  return (
    <div dir={dir} className={cn('flex h-screen w-full overflow-hidden bg-background text-foreground')}>
      {/* Scrim — phones/tablets only, closes the drawer. */}
      {mobileOpen && (
        <button
          type="button"
          aria-label={t('nav.web.toggleMenu')}
          onClick={() => setMobileOpen(false)}
          className="fixed inset-0 z-40 bg-black/50 lg:hidden"
        />
      )}

      <aside
        className={cn(
          'fixed inset-y-0 z-50 flex h-full shrink-0 flex-col border-e border-sidebar-border bg-sidebar transition-transform duration-200',
          'lg:static lg:z-auto lg:translate-x-0 lg:rtl:translate-x-0 lg:transition-[width]',
          'start-0 w-56',
          mobileOpen ? 'translate-x-0' : '-translate-x-full rtl:translate-x-full',
          collapsed ? 'lg:w-14' : 'lg:w-56',
        )}
      >
        <div className="flex h-12 items-center gap-2 border-b border-sidebar-border px-3">
          <span className="flex h-6 w-6 items-center justify-center rounded bg-sidebar-primary font-mono text-[0.625rem] font-bold text-sidebar-primary-foreground">
            OAS
          </span>
          {!sidebarCollapsed && (
            <span className="truncate text-sm font-semibold text-sidebar-foreground">Production</span>
          )}
          <button
            type="button"
            onClick={() => (mobileOpen ? setMobileOpen(false) : setCollapsed((c) => !c))}
            className="ms-auto rounded p-1 text-sidebar-foreground/70 hover:bg-sidebar-accent"
            aria-label={t('nav.web.toggleMenu')}
          >
            {mobileOpen ? (
              <X className="h-4 w-4" />
            ) : collapsed ? (
              <PanelLeft className="h-4 w-4" />
            ) : (
              <PanelLeftClose className="h-4 w-4" />
            )}
          </button>
        </div>

        <nav className="flex-1 overflow-y-auto p-2" onClick={() => setMobileOpen(false)}>
          <ul className="space-y-1">
            {NAV.map(({ to, key, icon, plugin }) => (
              <NavItem key={to} to={to} labelKey={key} icon={icon} plugin={plugin} collapsed={sidebarCollapsed} />
            ))}
          </ul>
        </nav>

        <div className="space-y-2 border-t border-sidebar-border p-2">
          {!sidebarCollapsed && <LangSwitcher variant="menu" className="w-full justify-center bg-sidebar-accent" />}
          <ThemeToggle variant="row" label={sidebarCollapsed ? undefined : t('nav.web.theme')} />
        </div>

      </aside>

      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <div className="z-30 flex h-11 shrink-0 items-center gap-2 border-b border-border bg-background/95 px-3 backdrop-blur sm:px-4">
          <button
            type="button"
            onClick={() => setMobileOpen(true)}
            aria-label={t('nav.web.toggleMenu')}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-md border border-border text-muted-foreground transition-colors hover:bg-muted hover:text-foreground lg:hidden"
          >
            <Menu className="h-4 w-4" />
          </button>
          <span className="hidden truncate text-caption text-muted-foreground sm:inline">{t('demo.tagline')}</span>
          <div className="ms-auto flex items-center gap-2">
            <span className="hidden text-caption text-muted-foreground sm:inline">
              {auth.displayName ?? auth.email} · {t(`role.${auth.role}`)}
            </span>
            <button
              type="button"
              onClick={signOut}
              title={t('common.signOut')}
              aria-label={t('common.signOut')}
              className="grid h-8 w-8 place-items-center rounded-md border border-border text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            >
              <LogOut className="h-4 w-4 rtl:rotate-180" />
            </button>
          </div>
          <button
            type="button"
            onClick={() => setTourOpen(true)}
            className="flex h-8 items-center gap-2 rounded-md bg-primary px-3 text-xs font-semibold text-primary-foreground transition-colors hover:bg-primary/90"
          >
            <PlayCircle className="h-4 w-4" />
            {t('demo.start')}
          </button>
        </div>

        <main data-demo="scroll-area" className="min-h-0 flex-1 overflow-y-auto">
        <Suspense fallback={<WebPageSkeleton />}>
        <Routes>
          <Route index element={<Navigate to="/web/dashboard" replace />} />
          <Route path="dashboard"    element={<PluginGate code="OA0002DASHBOARD"><ManagerDashboard /></PluginGate>} />
          <Route path="shopfloor"    element={<PluginGate code="OA0003SHOPFLOOR"><ShopFloorBoard /></PluginGate>} />
          <Route path="alerts"       element={<PluginGate code="OA0006ALERTS"><AlertsQueue /></PluginGate>} />
          <Route path="assignments"  element={<PluginGate code="OA0007ASSIGNMENTS"><Assignments /></PluginGate>} />
          <Route path="reports"      element={<PluginGate code="OA0009REPORTING"><Reports /></PluginGate>} />
          <Route path="shift-report" element={<PluginGate code="OA0009REPORTING"><ShiftReport /></PluginGate>} />
          <Route path="andon"        element={<PluginGate code="OA0010ANDON"><AndonTv /></PluginGate>} />
          <Route path="admin"        element={<PluginGate code="OA0011CONSOLE"><Admin /></PluginGate>} />
          <Route path="referentials" element={<PluginGate code="OA0008REFERENTIALS"><Referentials /></PluginGate>} />
          <Route path="*"            element={<Navigate to="/web/dashboard" replace />} />

        </Routes>
        </Suspense>
        </main>
      </div>

      <DemoTour open={tourOpen} onClose={() => setTourOpen(false)} />
      <ToastStack />
    </div>
  );
}
