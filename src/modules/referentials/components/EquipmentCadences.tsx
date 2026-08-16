import { Fragment, useState } from 'react';
import { History, Plus, Save, Trash2 } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useHierarchyState } from '@/oas/hierarchyStore';
import {
  addCadence, removeEquipment, saveCadence, upsertEquipment, useRefState,
  type CadenceRow, type EquipmentRow,
} from '@/oas/refStore';
import { useT } from '@/i18n/I18nProvider';

const EMPTY_EQ: EquipmentRow = { id: '', code: '', postId: null, post: '', name: '', manufacturer: '', criticality: 'medium' };
const CRITS: EquipmentRow['criticality'][] = ['low', 'medium', 'high', 'critical'];

/** BL-004 — a cadence is stored in parts/hour; the cycle time is derived. */
const cycleSec = (rate: number) => Math.round((3600 / rate) * 10) / 10;

export function EquipmentCadences() {
  const t = useT();
  const { cadences, products, equipment } = useRefState();
  const { posts } = useHierarchyState();
  const [editing, setEditing] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [openHistory, setOpenHistory] = useState<string | null>(null);
  const [newProductId, setNewProductId] = useState('');
  const [newPostId, setNewPostId] = useState('');
  const [newRate, setNewRate] = useState('');
  // BL-002 — equipment rows are editable and live in the referential store.
  const [eqDraft, setEqDraft] = useState<EquipmentRow>(EMPTY_EQ);

  const startEqEdit = (row: EquipmentRow) => setEqDraft(row);
  const submitEq = () => {
    void upsertEquipment(eqDraft);
    setEqDraft(EMPTY_EQ);
  };

  const startEdit = (r: CadenceRow) => {
    setEditing(r.id);
    setDraft(String(r.rate));
  };

  const uncovered = products.filter((p) => !cadences.some((c) => c.ref === p.ref));

  return (
    <>
      <Card data-demo="cadences">
        <CardHeader>
          <CardTitle>{t('web.ref.equipment')}</CardTitle>
          <CardDescription>{t('web.ref.equipmentDesc')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-0 p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('web.ref.code')}</TableHead>
                <TableHead>{t('web.ref.model')}</TableHead>
                <TableHead>{t('web.ref.post')}</TableHead>
                <TableHead>{t('web.ref.criticality')}</TableHead>
                <TableHead className="text-end">{t('web.ref.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {equipment.map((e) => (
                <TableRow key={e.id} className="transition-colors hover:bg-accent/50">
                  <TableCell className="font-mono">{e.code}</TableCell>
                  <TableCell>{e.name}{e.manufacturer && <span className="ms-1.5 text-caption text-muted-foreground">· {e.manufacturer}</span>}</TableCell>
                  <TableCell className="font-mono">{e.post || '—'}</TableCell>
                  <TableCell><Badge variant="ghost">{t(`web.ref.crit.${e.criticality}`)}</Badge></TableCell>
                  <TableCell className="text-end">
                    <div className="inline-flex gap-1">
                      <Button size="sm" variant="outline" onClick={() => startEqEdit(e)}>
                        {t('web.ref.edit')}
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => void removeEquipment(e.id)}
                        aria-label={t('web.ref.delete')}>
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
              {!equipment.length && (
                <TableRow>
                  <TableCell colSpan={5} className="p-6 text-center text-body-sm text-muted-foreground">
                    {t('web.ref.equipmentEmpty')}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>

          <div className="flex flex-wrap items-end gap-2 border-t border-border p-3">
            <div className="w-28">
              <label className="text-caption text-muted-foreground">{t('web.ref.code')}</label>
              <Input value={eqDraft.code} onChange={(e) => setEqDraft({ ...eqDraft, code: e.target.value })}
                placeholder="MC-04" className="h-8 font-mono" disabled={Boolean(eqDraft.id)} />
            </div>
            <div className="w-44">
              <label className="text-caption text-muted-foreground">{t('web.ref.model')}</label>
              <Input value={eqDraft.name} onChange={(e) => setEqDraft({ ...eqDraft, name: e.target.value })} className="h-8" />
            </div>
            <div className="w-32">
              <label className="text-caption text-muted-foreground">{t('web.ref.post')}</label>
              <select value={eqDraft.postId ?? ''}
                onChange={(e) => setEqDraft({ ...eqDraft, postId: e.target.value || null })}
                className="h-8 w-full rounded-md border border-input bg-background px-2 font-mono text-body-sm">
                <option value="">—</option>
                {posts.map((p) => <option key={p.id} value={p.id}>{p.code}</option>)}
              </select>
            </div>
            <div className="w-32">
              <label className="text-caption text-muted-foreground">{t('web.ref.criticality')}</label>
              <select value={eqDraft.criticality}
                onChange={(e) => setEqDraft({ ...eqDraft, criticality: e.target.value as EquipmentRow['criticality'] })}
                className="h-8 w-full rounded-md border border-input bg-background px-2 text-body-sm">
                {CRITS.map((c) => <option key={c} value={c}>{t(`web.ref.crit.${c}`)}</option>)}
              </select>
            </div>
            <Button size="sm" disabled={!eqDraft.code.trim()} onClick={submitEq}>
              {eqDraft.id
                ? <><Save className="me-1.5 h-3.5 w-3.5" /> {t('web.ref.save')}</>
                : <><Plus className="me-1.5 h-3.5 w-3.5" /> {t('web.ref.addEquipment')}</>}
            </Button>
            {eqDraft.id && (
              <Button size="sm" variant="ghost" onClick={() => setEqDraft(EMPTY_EQ)}>
                {t('common.cancel')}
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('web.ref.cadences')}</CardTitle>
          <CardDescription>{t('web.ref.cadencesDesc')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3 p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('web.ref.reference')}</TableHead>
                <TableHead>{t('web.ref.post')}</TableHead>
                <TableHead>{t('web.ref.rate')}</TableHead>
                <TableHead>{t('web.ref.version')}</TableHead>
                <TableHead className="text-end">{t('web.ref.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {cadences.map((r) => (
                <Fragment key={r.id}>
                  <TableRow className="transition-colors hover:bg-accent/50">
                    <TableCell className="font-mono">{r.ref}</TableCell>
                    <TableCell className="font-mono">{posts.find((p) => p.id === r.postId)?.code ?? '—'}</TableCell>
                    <TableCell>
                      {editing === r.id ? (
                        <Input
                          value={draft}
                          onChange={(e) => setDraft(e.target.value)}
                          inputMode="numeric"
                          className="h-8 w-24 font-mono"
                          autoFocus
                        />
                      ) : (
                        <span dir="ltr" className="font-mono">
                          {r.rate} {t('web.ref.perHour')}
                          <span className="ms-2 text-caption text-muted-foreground">
                            {t('web.ref.cycleSec', { sec: cycleSec(r.rate) })}
                          </span>
                        </span>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge variant="ghost" dir="ltr">v{r.version} · {r.since}</Badge>
                    </TableCell>
                    <TableCell className="text-end">
                      <div className="inline-flex gap-1">
                        {editing === r.id ? (
                          <Button
                            size="sm"
                            onClick={() => { void saveCadence(r.id, Number(draft)); setEditing(null); }}
                          >
                            <Save className="me-1.5 h-3.5 w-3.5" /> {t('web.ref.saveVersion')}
                          </Button>
                        ) : (
                          <Button size="sm" variant="outline" onClick={() => startEdit(r)}>
                            {t('web.ref.edit')}
                          </Button>
                        )}
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={!r.history.length}
                          onClick={() => setOpenHistory(openHistory === r.id ? null : r.id)}
                        >
                          <History className="me-1.5 h-3.5 w-3.5" /> {r.history.length}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                  {openHistory === r.id &&
                    r.history.map((h) => (
                      <TableRow key={`${r.id}-${h.version}`} className="bg-muted/40">
                        <TableCell />
                        <TableCell className="text-caption text-muted-foreground">
                          {t('web.ref.previousVersion')}
                        </TableCell>
                        <TableCell dir="ltr" className="font-mono text-muted-foreground">
                          {h.rate} {t('web.ref.perHour')}
                        </TableCell>
                        <TableCell dir="ltr" className="font-mono text-muted-foreground">
                          v{h.version} · {h.since}
                        </TableCell>
                        <TableCell />
                      </TableRow>
                    ))}
                </Fragment>
              ))}
            </TableBody>
          </Table>

          {/* BL-006 — references without a cadence run in TRS-lite mode. */}
          {uncovered.length > 0 && (
            <p className="px-4 text-caption text-state-changeover">
              {t('web.ref.noCadenceList', { list: uncovered.map((p) => p.ref).join(', ') })}
            </p>
          )}

          <div className="flex flex-wrap items-end gap-2 border-t border-border p-3">
            <div className="w-40">
              <label className="text-caption text-muted-foreground">{t('web.ref.reference')}</label>
              <select
                value={newProductId}
                onChange={(e) => setNewProductId(e.target.value)}
                className="h-8 w-full rounded-md border border-input bg-background px-2 font-mono text-body-sm"
              >
                <option value="">—</option>
                {products.map((p) => <option key={p.id} value={p.id}>{p.ref}</option>)}
              </select>
            </div>
            <div className="w-36">
              <label className="text-caption text-muted-foreground">{t('web.ref.post')}</label>
              <select
                value={newPostId}
                onChange={(e) => setNewPostId(e.target.value)}
                className="h-8 w-full rounded-md border border-input bg-background px-2 font-mono text-body-sm"
              >
                <option value="">—</option>
                {posts.map((p) => <option key={p.id} value={p.id}>{p.code}</option>)}
              </select>
            </div>
            <div className="w-28">
              <label className="text-caption text-muted-foreground">{t('web.ref.rate')}</label>
              <Input value={newRate} onChange={(e) => setNewRate(e.target.value)} inputMode="numeric" className="h-8 font-mono" />
            </div>
            <Button
              size="sm"
              disabled={!newProductId || !newPostId || !Number(newRate)}
              onClick={() => { void addCadence(newProductId, newPostId, Number(newRate)); setNewProductId(''); setNewPostId(''); setNewRate(''); }}
            >
              <Plus className="me-1.5 h-3.5 w-3.5" /> {t('web.ref.addCadence')}
            </Button>
          </div>
        </CardContent>
      </Card>
    </>
  );
}
