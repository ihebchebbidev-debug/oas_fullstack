import { useState } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { removeProduct, upsertProduct, useRefState, type ProductRow } from '@/oas/refStore';
import { useT } from '@/i18n/I18nProvider';

const EMPTY: ProductRow = { id: '', ref: '', label: '', customer: '', unit: 'pcs' };

/** BL-003 — references (product catalogue) CRUD, with cadence coverage flag. */
export function ReferencesPanel() {
  const t = useT();
  const { products, cadences } = useRefState();
  const [draft, setDraft] = useState<ProductRow>(EMPTY);

  const submit = () => {
    void upsertProduct(draft);
    setDraft(EMPTY);
  };

  return (
    <Card data-demo="references">
      <CardHeader>
        <CardTitle>{t('web.ref.references')}</CardTitle>
        <CardDescription>{t('web.ref.referencesDesc')}</CardDescription>
      </CardHeader>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('web.ref.reference')}</TableHead>
              <TableHead>{t('web.ref.label')}</TableHead>
              <TableHead>{t('web.ref.customer')}</TableHead>
              <TableHead>{t('web.ref.cadence')}</TableHead>
              <TableHead className="text-end">{t('web.ref.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {products.map((p) => {
              const rate = cadences.find((c) => c.ref === p.ref)?.rate;
              return (
                <TableRow key={p.id}>
                  <TableCell className="font-mono">{p.ref}</TableCell>
                  <TableCell>{p.label}</TableCell>
                  <TableCell className="text-muted-foreground">{p.customer || '—'}</TableCell>
                  <TableCell>
                    {rate
                      ? <span dir="ltr" className="font-mono">{rate} p/h</span>
                      : <Badge variant="outline">{t('web.ref.noCadence')}</Badge>}
                  </TableCell>
                  <TableCell className="text-end">
                    <Button size="sm" variant="ghost" aria-label={t('web.ref.edit')}
                      onClick={() => setDraft(p)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button size="sm" variant="ghost" aria-label={t('web.ref.delete')}
                      onClick={() => void removeProduct(p.id)}>
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>

        <div className="flex flex-wrap items-center gap-2 border-t border-border p-3">
          <Input className="w-32" value={draft.ref} disabled={!!draft.id}
            onChange={(e) => setDraft({ ...draft, ref: e.target.value })} placeholder={t('web.ref.reference')} />
          <Input className="w-48" value={draft.label}
            onChange={(e) => setDraft({ ...draft, label: e.target.value })} placeholder={t('web.ref.label')} />
          <Input className="w-40" value={draft.customer}
            onChange={(e) => setDraft({ ...draft, customer: e.target.value })} placeholder={t('web.ref.customer')} />
          <Button size="sm" onClick={submit}>
            <Plus className="me-1 h-3.5 w-3.5" /> {draft.id ? t('web.ref.save') : t('web.ref.addReference')}
          </Button>
          {draft.id && (
            <Button size="sm" variant="ghost" onClick={() => setDraft(EMPTY)}>
              {t('common.cancel')}
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
