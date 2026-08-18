import { Fragment, useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, Plus, Save, Trash2 } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useT } from '@/i18n/I18nProvider';
import {
  qualityTemplatesApi,
  type OasQualityCheckTemplateDto, type OasQualityCheckTemplateItemDto,
} from '@/oas/api/operations';
import { pushToast } from '@/shared/lib/toast';
import { ApiError } from '@/oas/api/client';

const CHECK_TYPES = ['first_part', 'in_process', 'final'] as const;
const VALUE_TYPES = ['boolean', 'numeric', 'text'] as const;

const EMPTY_TEMPLATE = { code: '', name: '', checkType: 'in_process' as (typeof CHECK_TYPES)[number] };
const EMPTY_ITEM = { label: '', valueType: 'boolean' as (typeof VALUE_TYPES)[number], minValue: '', maxValue: '', isRequired: true };

/** BL — quality control templates (spec §6.1), the admin side of the mobile quality-check screen (`QualityCheckPage.tsx`). Was fully built server-side (`OasQualityCheckTemplatesController`) with zero frontend anywhere — this is that missing screen. */
export function QualityTemplates() {
  const t = useT();
  const [templates, setTemplates] = useState<OasQualityCheckTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [draft, setDraft] = useState(EMPTY_TEMPLATE);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [items, setItems] = useState<OasQualityCheckTemplateItemDto[]>([]);
  const [itemDraft, setItemDraft] = useState(EMPTY_ITEM);
  const [savingItems, setSavingItems] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const reload = () => {
    setLoading(true);
    void qualityTemplatesApi.list().then(setTemplates).catch(() => pushToast('common.actionFailed')).finally(() => setLoading(false));
  };
  useEffect(reload, []);

  async function submitTemplate() {
    if (!draft.code.trim() || !draft.name.trim()) return;
    setFormError(null);
    try {
      if (editingId) await qualityTemplatesApi.update(editingId, draft);
      else await qualityTemplatesApi.create(draft);
      setDraft(EMPTY_TEMPLATE);
      setEditingId(null);
      reload();
    } catch (e) {
      setFormError(e instanceof ApiError ? e.message : t('common.actionFailed'));
    }
  }

  async function removeTemplate(id: string) {
    try {
      await qualityTemplatesApi.delete(id);
      reload();
    } catch {
      pushToast('common.actionFailed');
    }
  }

  async function toggleExpand(tpl: OasQualityCheckTemplateDto) {
    if (expanded === tpl.id) { setExpanded(null); return; }
    setExpanded(tpl.id);
    setItemDraft(EMPTY_ITEM);
    try {
      const rows = await qualityTemplatesApi.items(tpl.id);
      setItems(rows.sort((a, b) => a.sortOrder - b.sortOrder));
    } catch {
      setItems([]);
      pushToast('common.actionFailed');
    }
  }

  async function saveItems(templateId: string, next: OasQualityCheckTemplateItemDto[]) {
    setSavingItems(true);
    try {
      await qualityTemplatesApi.putItems(templateId, next.map((it, i) => ({
        label: it.label, valueType: it.valueType,
        minValue: it.minValue ?? undefined, maxValue: it.maxValue ?? undefined,
        isRequired: it.isRequired, sortOrder: i,
      })));
      const rows = await qualityTemplatesApi.items(templateId);
      setItems(rows.sort((a, b) => a.sortOrder - b.sortOrder));
    } catch {
      pushToast('common.actionFailed');
    } finally {
      setSavingItems(false);
    }
  }

  function addItem(templateId: string) {
    if (!itemDraft.label.trim()) return;
    const next = [...items, {
      id: '', label: itemDraft.label.trim(), valueType: itemDraft.valueType,
      minValue: itemDraft.minValue ? Number(itemDraft.minValue) : null,
      maxValue: itemDraft.maxValue ? Number(itemDraft.maxValue) : null,
      isRequired: itemDraft.isRequired, sortOrder: items.length,
    }];
    setItemDraft(EMPTY_ITEM);
    void saveItems(templateId, next);
  }

  function removeItem(templateId: string, itemId: string) {
    void saveItems(templateId, items.filter((it) => it.id !== itemId));
  }

  return (
    <Card data-demo="quality-templates">
      <CardHeader>
        <CardTitle>{t('web.ref.qualityTemplates')}</CardTitle>
        <CardDescription>{t('web.ref.qualityTemplatesDesc')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-0 p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead />
              <TableHead>{t('web.ref.code')}</TableHead>
              <TableHead>{t('web.ref.name')}</TableHead>
              <TableHead>{t('web.quality.checkType')}</TableHead>
              <TableHead className="text-end">{t('web.ref.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {templates.map((tpl) => (
              <Fragment key={tpl.id}>
                <TableRow className="cursor-pointer transition-colors hover:bg-accent/50" onClick={() => void toggleExpand(tpl)}>
                  <TableCell className="w-8">
                    {expanded === tpl.id ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
                  </TableCell>
                  <TableCell className="font-mono">{tpl.code}</TableCell>
                  <TableCell>{tpl.name}{!tpl.isActive && <Badge variant="ghost" className="ms-2">{t('web.ref.inactive')}</Badge>}</TableCell>
                  <TableCell><Badge variant="ghost">{t(`web.quality.type.${tpl.checkType}`)}</Badge></TableCell>
                  <TableCell className="text-end" onClick={(e) => e.stopPropagation()}>
                    <div className="inline-flex gap-1">
                      <Button size="sm" variant="outline" onClick={() => { setEditingId(tpl.id); setDraft({ code: tpl.code, name: tpl.name, checkType: tpl.checkType as (typeof CHECK_TYPES)[number] }); }}>
                        {t('web.ref.edit')}
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => void removeTemplate(tpl.id)} aria-label={t('web.ref.delete')}>
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
                {expanded === tpl.id && (
                  <TableRow className="bg-muted/40">
                    <TableCell />
                    <TableCell colSpan={4} className="space-y-2 py-3">
                      <p className="text-caption font-medium text-muted-foreground">{t('web.quality.items')}</p>
                      {items.length === 0 && <p className="text-caption text-muted-foreground">{t('web.quality.itemsEmpty')}</p>}
                      {items.map((it) => (
                        <div key={it.id} className="flex items-center gap-2 text-body-sm">
                          <span className="flex-1">{it.label}</span>
                          <Badge variant="ghost">{t(`web.quality.valueType.${it.valueType}`)}</Badge>
                          {it.isRequired && <Badge variant="ghost">{t('web.ref.required')}</Badge>}
                          <Button size="sm" variant="ghost" disabled={savingItems} onClick={() => removeItem(tpl.id, it.id)} aria-label={t('web.ref.delete')}>
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      ))}
                      <div className="flex flex-wrap items-end gap-2 pt-2">
                        <div className="w-48">
                          <label className="text-caption text-muted-foreground">{t('web.quality.itemLabel')}</label>
                          <Input value={itemDraft.label} onChange={(e) => setItemDraft({ ...itemDraft, label: e.target.value })} className="h-8" />
                        </div>
                        <div className="w-32">
                          <label className="text-caption text-muted-foreground">{t('web.quality.valueType')}</label>
                          <select value={itemDraft.valueType}
                            onChange={(e) => setItemDraft({ ...itemDraft, valueType: e.target.value as (typeof VALUE_TYPES)[number] })}
                            className="h-8 w-full rounded-md border border-input bg-background px-2 text-body-sm">
                            {VALUE_TYPES.map((vt) => <option key={vt} value={vt}>{t(`web.quality.valueType.${vt}`)}</option>)}
                          </select>
                        </div>
                        <Button size="sm" disabled={!itemDraft.label.trim() || savingItems} onClick={() => addItem(tpl.id)}>
                          <Plus className="me-1.5 h-3.5 w-3.5" /> {t('web.quality.addItem')}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                )}
              </Fragment>
            ))}
            {!loading && !templates.length && (
              <TableRow>
                <TableCell colSpan={5} className="p-6 text-center text-body-sm text-muted-foreground">
                  {t('web.quality.templatesEmpty')}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>

        {formError && <p className="px-3 pt-2 text-caption text-state-technical">{formError}</p>}
        <div className="flex flex-wrap items-end gap-2 border-t border-border p-3">
          <div className="w-28">
            <label className="text-caption text-muted-foreground">{t('web.ref.code')}</label>
            <Input value={draft.code} onChange={(e) => setDraft({ ...draft, code: e.target.value })} placeholder="QC-01" className="h-8 font-mono" />
          </div>
          <div className="w-44">
            <label className="text-caption text-muted-foreground">{t('web.ref.name')}</label>
            <Input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className="h-8" />
          </div>
          <div className="w-36">
            <label className="text-caption text-muted-foreground">{t('web.quality.checkType')}</label>
            <select value={draft.checkType}
              onChange={(e) => setDraft({ ...draft, checkType: e.target.value as (typeof CHECK_TYPES)[number] })}
              className="h-8 w-full rounded-md border border-input bg-background px-2 text-body-sm">
              {CHECK_TYPES.map((ct) => <option key={ct} value={ct}>{t(`web.quality.type.${ct}`)}</option>)}
            </select>
          </div>
          <Button size="sm" disabled={!draft.code.trim() || !draft.name.trim()} onClick={() => void submitTemplate()}>
            {editingId
              ? <><Save className="me-1.5 h-3.5 w-3.5" /> {t('web.ref.save')}</>
              : <><Plus className="me-1.5 h-3.5 w-3.5" /> {t('web.quality.addTemplate')}</>}
          </Button>
          {editingId && (
            <Button size="sm" variant="ghost" onClick={() => { setEditingId(null); setDraft(EMPTY_TEMPLATE); }}>
              {t('common.cancel')}
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
