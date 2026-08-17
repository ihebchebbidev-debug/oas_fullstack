import { useNavigate } from 'react-router-dom';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/shared/components/PageHeader';
import { EquipmentCadences } from '../components/EquipmentCadences';
import { CauseReviewQueue } from '../components/CauseReviewQueue';
import { CauseTreeEditor } from '../components/CauseTreeEditor';
import { ReferencesPanel } from '../components/ReferencesPanel';
import { completeness, useRefState } from '@/oas/refStore';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { HierarchyManager } from '../components/HierarchyManager';
import { ShiftCalendars } from '../components/ShiftCalendars';
import { useT } from '@/i18n/I18nProvider';


export default function Referentials() {
  const t = useT();
  const navigate = useNavigate();
  useRefState();
  /** BL-011 — completeness computed server-side, per family. */
  const rows = completeness();
  // Real counts — was a hardcoded "Site Sousse · 1 zone · 3 lignes · 15 postes"
  // regardless of the tenant's actual hierarchy.
  const { sites, zones, lines, posts } = useHierarchyState();
  const activePosts = posts.filter((p) => !p.archived).length;
  return (
    <>
      <PageHeader
        title={t('web.ref.title')}
        subtitle={t('web.ref.subtitle')}
        actions={
          <Button size="sm" onClick={() => navigate('/web/admin?tab=import')}>
            {t('web.ref.import')}
          </Button>
        }
      />
      <div className="p-4 pb-0">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle>{t('web.ref.completeness')}</CardTitle>
            <CardDescription>{t('web.ref.completenessDesc')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {rows.map((c) => (
              <div key={c.key} className="flex items-center gap-3">
                <span className="w-40 shrink-0 text-body">{t(c.key)}</span>
                <span className="h-2 flex-1 overflow-hidden rounded-full bg-muted">
                  <span
                    className={`block h-full rounded-full transition-all duration-500 ${c.pct === 100 ? 'bg-state-production' : c.pct >= 60 ? 'bg-state-changeover' : 'bg-state-technical'}`}
                    style={{ width: `${c.pct}%` }}
                  />
                </span>
                <span dir="ltr" className="w-16 text-end font-mono text-caption text-muted-foreground">{c.detail}</span>
                <span dir="ltr" className="w-12 text-end font-mono text-body">{c.pct}%</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid min-w-0 grid-cols-1 gap-3 p-3 sm:p-4 lg:grid-cols-2 [&>*]:min-w-0">
        <Card className="min-w-0">
          <CardHeader>
            <CardTitle>{t('web.ref.hierarchy')}</CardTitle>
            <CardDescription>
              {t('web.ref.hierarchyDesc', { sites: sites.length, zones: zones.length, lines: lines.length, posts: activePosts })} · {t('web.ref.criticalHint')}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {/* BL-001 — Site → Zone → Line → Post CRUD (create / rename / archive),
                plus the BL-036 critical-post flag on each leaf. */}
            <HierarchyManager />
          </CardContent>
        </Card>

        {/* BL-007 — editable 2-level cause tree (service mapping + criticality). */}
        <CauseTreeEditor />
      </div>

      <div className="grid min-w-0 grid-cols-1 gap-3 px-3 pb-4 sm:px-4 [&>*]:min-w-0">
        {/* BL-003 — references CRUD. */}
        <ReferencesPanel />

      </div>

      <div className="grid min-w-0 grid-cols-1 gap-3 px-3 pb-4 sm:px-4 lg:grid-cols-2 [&>*]:min-w-0">
        <EquipmentCadences />
        {/* BL-009 — shift calendars: breaks deducted from the theoretical time. */}
        <div className="lg:col-span-2">
          <ShiftCalendars />
        </div>
      </div>

      <div className="min-w-0 px-3 pb-6 sm:px-4">
        <CauseReviewQueue />
      </div>
    </>
  );
}
