import { useEffect, useState } from 'react';
import { CheckCircle2, AlertTriangle, XCircle, Loader2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import { apiFetch, ApiError, OAS_TENANT_SLUG } from '@/oas/api/client';

type Phase = 'checking' | 'ok' | 'incomplete' | 'not_provisioned' | 'no_tenant' | 'error';

interface OasHealthDto {
  status: string;
  tenant?: string;
  message?: string;
  tablesMissing?: string[];
  /** Only present when this body arrived via `OasTenantMiddleware`'s pre-flight 503 (a different shape than the health handler's own JSON — see the effect below). */
  error?: string;
}

/**
 * Real connectivity indicator, not a static label — hits the actual
 * `GET /api/oas/health` (spec §11 criterion 14) with the tenant slug this
 * bundle derived from its own hostname (`OAS_TENANT_SLUG`, see client.ts),
 * and shows exactly what the backend reports: which database it resolved
 * to, and whether the schema is actually there. A green "connected" here
 * means the backend really reached that tenant's Postgres and found every
 * expected table — not just that the page loaded.
 */
export function DbStatusBanner() {
  const [phase, setPhase] = useState<Phase>('checking');
  const [missingCount, setMissingCount] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const apply = (dto: OasHealthDto) => {
      if (cancelled) return;
      setPhase(dto.status as Phase);
      setMissingCount(dto.tablesMissing?.length ?? 0);
    };
    (async () => {
      try {
        apply(await apiFetch<OasHealthDto>('/health', { auth: false }));
      } catch (e) {
        if (cancelled) return;
        // Two DIFFERENT 503 shapes can land here, both legitimate signal, not
        // failures to treat as "unreachable":
        //  - `OasTenantMiddleware`'s own pre-flight 503, body `{error:"oas_tenant_not_provisioned", tenant}`
        //    (thrown before the health handler ever runs, for any oas-suffixed slug with no DB configured);
        //  - the health handler's own 503 for `{status:"incomplete", tablesMissing}` (DB reachable, schema partial) —
        //    `apiFetch` treats that non-2xx as an error too even though the body is exactly what we want to show.
        if (e instanceof ApiError && e.body && typeof e.body === 'object') {
          const body = e.body as OasHealthDto;
          if (body.status === 'incomplete') { apply(body); return; }
          if (body.error === 'oas_tenant_not_provisioned' || body.status === 'not_provisioned') {
            setPhase('not_provisioned');
            return;
          }
        }
        setPhase('error');
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const byPhase: Record<Phase, { icon: JSX.Element; text: string; cls: string }> = {
    checking: {
      icon: <Loader2 className="h-3.5 w-3.5 animate-spin" />,
      text: 'Checking database…',
      cls: 'bg-muted text-muted-foreground',
    },
    ok: {
      icon: <CheckCircle2 className="h-3.5 w-3.5" />,
      text: `Database connected — ${OAS_TENANT_SLUG}`,
      cls: 'bg-state-production/10 text-state-production',
    },
    incomplete: {
      icon: <AlertTriangle className="h-3.5 w-3.5" />,
      text: `Database connected — ${OAS_TENANT_SLUG} (schema incomplete, ${missingCount} table(s) missing — run COMPLETE_SETUP.sql)`,
      cls: 'bg-state-material/10 text-state-material',
    },
    not_provisioned: {
      icon: <XCircle className="h-3.5 w-3.5" />,
      text: `Database not provisioned for tenant "${OAS_TENANT_SLUG}" — set TENANT_${OAS_TENANT_SLUG.toUpperCase()}_DATABASE_URL`,
      cls: 'bg-state-technical/10 text-state-technical',
    },
    no_tenant: {
      icon: <XCircle className="h-3.5 w-3.5" />,
      text: `No OAS tenant detected for this host ("${OAS_TENANT_SLUG}" does not end in "oas")`,
      cls: 'bg-state-technical/10 text-state-technical',
    },
    error: {
      icon: <XCircle className="h-3.5 w-3.5" />,
      text: 'Database unreachable',
      cls: 'bg-state-technical/10 text-state-technical',
    },
  };

  const { icon, text, cls } = byPhase[phase];

  return (
    <div dir="ltr" className={cn('flex items-center justify-center gap-1.5 px-2 py-1 text-caption font-mono', cls)}>
      {icon}
      <span className="truncate">{text}</span>
    </div>
  );
}
