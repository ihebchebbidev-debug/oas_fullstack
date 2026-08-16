import { Fragment, useState } from 'react';
import { ChevronDown, ChevronRight, Plus, Power, Trash2 } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { STATES, kindToState, type EventKind } from '@/oas/demo';
import {
  addCauseChild, causeLabelIn, removeCause, removeCauseChild, setCauseCriticality,
  setCauseKind, toggleCauseActive, upsertCause, useRefState, type CauseNode,
} from '@/oas/refStore';
import { useI18n } from '@/i18n/I18nProvider';

const KINDS: EventKind[] = ['technical', 'quality', 'material', 'changeover'];
const CRITS: CauseNode['criticality'][] = ['low', 'medium', 'high'];

/**
 * BL-007 — editable 2-level cause tree.
 * Level 1 = family (mapped to a service + a criticality that drives the SLA),
 * level 2 = the detail the operator picks on the machine.
 */
export function CauseTreeEditor() {
  const { t, lang } = useI18n();
  const { causeTree } = useRefState();
  const [open, setOpen] = useState<string | null>(null);
  const [child, setChild] = useState('');
  const [childAr, setChildAr] = useState('');
  const [draft, setDraft] = useState({ code: '', label: '', labelAr: '', kind: 'technical' as EventKind, criticality: 'medium' as CauseNode['criticality'] });

  const submit = () => {
    void upsertCause(draft);
    setDraft({ code: '', label: '', labelAr: '', kind: 'technical', criticality: 'medium' });
  };

  return (
    <Card data-demo="cause-tree">
      <CardHeader>
        <CardTitle>{t('web.ref.causeTree')}</CardTitle>
        <CardDescription>{t('web.ref.causeTreeDesc')}</CardDescription>
      </CardHeader>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-8" />
              <TableHead>{t('web.ref.code')}</TableHead>
              <TableHead>{t('web.ref.label')}</TableHead>
              <TableHead>{t('web.ref.service')}</TableHead>
              <TableHead>{t('web.ref.criticality')}</TableHead>
              <TableHead className="text-end">{t('web.ref.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {causeTree.map((c) => {
              const expanded = open === c.id;
              return (
                <Fragment key={c.id}>
                  <TableRow className={c.active ? undefined : 'opacity-50'}>
                    <TableCell className="pe-0">
                      <button type="button" aria-label={causeLabelIn(c, lang)} onClick={() => setOpen(expanded ? null : c.id)}>
                        {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                      </button>
                    </TableCell>
                    <TableCell className="font-mono">{c.code}</TableCell>
                    <TableCell>
                      {causeLabelIn(c, lang)}
                      <Badge variant="outline" className="ms-2">{c.children.length}</Badge>
                    </TableCell>
                    <TableCell>
                      <Select className="min-w-[8.5rem]" value={c.kind} onChange={(e) => void setCauseKind(c.id, e.target.value as EventKind)}>
                        {KINDS.map((k) => (
                          <option key={k} value={k}>{t(STATES[kindToState[k]].labelKey)}</option>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell>
                      <Select className="min-w-[7rem]" value={c.criticality} onChange={(e) => void setCauseCriticality(c.id, e.target.value as CauseNode['criticality'])}>
                        {CRITS.map((k) => (
                          <option key={k} value={k}>{t(`web.ref.crit.${k}`)}</option>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell className="text-end">
                      <Button size="sm" variant="ghost" onClick={() => void toggleCauseActive(c.id)} aria-label={t('web.ref.toggleActive')}>
                        <Power className="h-3.5 w-3.5" />
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => void removeCause(c.id)} aria-label={t('web.ref.delete')}>
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </TableCell>
                  </TableRow>
                  {expanded && (
                    <TableRow>
                      <TableCell colSpan={6} className="bg-muted/40">
                        <ul className="space-y-1">
                          {c.children.map((ch) => (
                            <li key={ch.id} className="flex items-center gap-2 text-body">
                              <span className="font-mono text-caption text-muted-foreground">{ch.code}</span>
                              <span className="flex-1">{causeLabelIn(ch, lang)}</span>
                              <Button size="sm" variant="ghost" onClick={() => void removeCauseChild(c.id, ch.id)} aria-label={t('web.ref.delete')}>
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            </li>
                          ))}
                          {!c.children.length && (
                            <li className="text-caption text-muted-foreground">{t('web.ref.noDetail')}</li>
                          )}
                        </ul>
                        <div className="mt-2 flex gap-2">
                          <Input
                            value={open === c.id ? child : ''}
                            onChange={(e) => setChild(e.target.value)}
                            placeholder={t('web.ref.addDetail')}
                          />
                          <Input
                            value={open === c.id ? childAr : ''}
                            onChange={(e) => setChildAr(e.target.value)}
                            placeholder={t('web.ref.labelAr')}
                          />
                          <Button size="sm" onClick={() => { void addCauseChild(c.id, child, childAr); setChild(''); setChildAr(''); }}>
                            <Plus className="me-1 h-3.5 w-3.5" /> {t('web.ref.add')}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  )}
                </Fragment>
              );
            })}
          </TableBody>
        </Table>

        <div className="flex flex-wrap items-center gap-2 border-t border-border p-3">
          <Input className="w-28" value={draft.code} onChange={(e) => setDraft({ ...draft, code: e.target.value })} placeholder={t('web.ref.code')} />
          <Input className="w-40" value={draft.label} onChange={(e) => setDraft({ ...draft, label: e.target.value })} placeholder={t('web.ref.label')} />
          <Input className="w-40" value={draft.labelAr} onChange={(e) => setDraft({ ...draft, labelAr: e.target.value })} placeholder={t('web.ref.labelAr')} />
          <Select className="w-40 shrink-0" value={draft.kind} onChange={(e) => setDraft({ ...draft, kind: e.target.value as EventKind })}>
            {KINDS.map((k) => <option key={k} value={k}>{t(STATES[kindToState[k]].labelKey)}</option>)}
          </Select>
          <Select className="w-32 shrink-0" value={draft.criticality} onChange={(e) => setDraft({ ...draft, criticality: e.target.value as CauseNode['criticality'] })}>
            {CRITS.map((k) => <option key={k} value={k}>{t(`web.ref.crit.${k}`)}</option>)}
          </Select>
          <Button size="sm" onClick={submit}><Plus className="me-1 h-3.5 w-3.5" /> {t('web.ref.addCause')}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
