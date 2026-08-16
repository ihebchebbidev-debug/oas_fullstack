import { useState } from 'react';
import { Check, X } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { reviewProposal, useRefState } from '@/oas/refStore';
import { useT } from '@/i18n/I18nProvider';

/** Slug a free-text label into a cause code candidate, e.g. "Fuite d'air" → "FUITE-DAIR". */
function slugCode(label: string): string {
  return label
    .trim()
    .toUpperCase()
    .normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/[^A-Z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 24) || 'NC';
}

/**
 * BL-008 — operator "missing cause" proposals queued for engineering/quality.
 * Approving one creates a real cause (server-side), immediately usable by operators.
 */
export function CauseReviewQueue() {
  const t = useT();
  const { proposals } = useRefState();
  const pending = proposals.filter((r) => r.status === 'pending').length;
  const [codes, setCodes] = useState<Record<string, string>>({});

  return (
    <Card data-demo="cause-review">
      <CardHeader>
        <CardTitle>{t('web.ref.review.title')}</CardTitle>
        <CardDescription>{t('web.ref.review.desc', { n: pending })}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2">
        {proposals.map((r) => (
          <div key={r.id} className="flex flex-wrap items-center gap-3 rounded-md border border-border p-2.5">
            <span className="text-body font-medium">{r.label}</span>
            <span className="text-caption text-muted-foreground">{new Date(r.at).toLocaleString()}</span>
            <div className="ms-auto flex items-center gap-2">
              {r.status === 'pending' ? (
                <>
                  <Input
                    value={codes[r.id] ?? slugCode(r.label)}
                    onChange={(e) => setCodes((prev) => ({ ...prev, [r.id]: e.target.value }))}
                    className="h-8 w-40 font-mono"
                    aria-label={t('web.ref.code')}
                  />
                  <Button
                    size="sm"
                    onClick={() => void reviewProposal(r.id, 'approved', codes[r.id] ?? slugCode(r.label))}
                  >
                    <Check className="me-1.5 h-3.5 w-3.5" /> {t('web.ref.review.approve')}
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => void reviewProposal(r.id, 'rejected')}>
                    <X className="me-1.5 h-3.5 w-3.5" /> {t('web.ref.review.reject')}
                  </Button>
                </>
              ) : (
                <span className={r.status === 'approved' ? 'text-body-sm text-state-production' : 'text-body-sm text-muted-foreground'}>
                  {t(`web.ref.review.${r.status}`)}
                </span>
              )}
            </div>
          </div>
        ))}
        {!proposals.length && <p className="text-body-sm text-muted-foreground">{t('web.alerts.empty')}</p>}
      </CardContent>
    </Card>
  );
}
