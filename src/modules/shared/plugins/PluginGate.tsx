/**
 * PluginGate — render-level gate for module routes.
 *
 *   <PluginGate code="OA0010ANDON"><AndonTv /></PluginGate>
 *
 * When the plugin is disabled it renders an explanatory panel, or a caller
 * supplied `fallback`, or redirects when `redirectTo` is set.
 */
import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { PackageX } from 'lucide-react';

import { useI18n } from '@/i18n/I18nProvider';
import { useIsPluginEnabled } from './usePlugins';
import { getPluginByCode } from './registry';

interface PluginGateProps {
  code: string;
  children: ReactNode;
  fallback?: ReactNode;
  redirectTo?: string;
}

export function PluginDisabled({ code }: { code: string }) {
  const { t } = useI18n();
  const manifest = getPluginByCode(code);
  return (
    <div
      role="status"
      className="mx-auto grid max-w-md place-items-center gap-3 rounded-xl border border-dashed border-border p-8 text-center"
    >
      <PackageX className="h-8 w-8 text-muted-foreground" />
      <p className="text-sm font-semibold">{t('plugins.disabledTitle')}</p>
      <p className="text-caption text-muted-foreground">
        {t('plugins.disabledBody')} <span className="font-mono">{manifest?.code ?? code}</span>
      </p>
    </div>
  );
}

export function PluginGate({ code, children, fallback, redirectTo }: PluginGateProps) {
  const enabled = useIsPluginEnabled(code);

  if (enabled) return <>{children}</>;
  if (fallback !== undefined) return <>{fallback}</>;
  if (redirectTo) return <Navigate to={redirectTo} replace />;
  return <PluginDisabled code={code} />;
}

export default PluginGate;
