import { useState } from 'react';
import { KeyRound, UserPlus, Power } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  ROLE_KEYS, addUser, regeneratePin, setUserRole, setUserScope, toggleUserActive, useRefState, type RoleKey,
} from '@/oas/refStore';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { useT } from '@/i18n/I18nProvider';

/** BL-012 / BL-013 / BL-015 — directory: create, interim express, deactivate, one-shot PIN. */
export default function UsersPanel() {
  const t = useT();
  const { users } = useRefState();
  const { lines } = useHierarchyState();
  const [name, setName] = useState('');
  const [role, setRole] = useState<RoleKey>('operator');
  const [interim, setInterim] = useState(false);
  /** One-time reveal (spec decision v12) — never persisted, cleared on next fetch. */
  const [revealedPins, setRevealedPins] = useState<Record<string, string>>({});

  const create = () => {
    void addUser(name, role, interim);
    setName('');
    setInterim(false);
  };

  const regen = async (id: string) => {
    const pin = await regeneratePin(id);
    if (pin) setRevealedPins((prev) => ({ ...prev, [id]: pin }));
  };

  return (
    <Card data-demo="users-panel">
      <CardHeader>
        <CardTitle>{t('web.admin.users.title')}</CardTitle>
        <CardDescription>{t('web.admin.users.desc')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex flex-wrap items-end gap-2 rounded-md border border-border p-3" data-demo="user-create">
          <div className="w-56">
            <label className="text-caption text-muted-foreground">{t('web.admin.users.name')}</label>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder={t('web.admin.users.namePlaceholder')} />
          </div>
          <div className="w-48">
            <label className="text-caption text-muted-foreground">{t('web.admin.users.role')}</label>
            <Select value={role} onChange={(e) => setRole(e.target.value as RoleKey)}>
              {ROLE_KEYS.map((r) => (
                <option key={r} value={r}>{t(`role.${r}`)}</option>
              ))}
            </Select>
          </div>
          <label className="flex h-9 items-center gap-2 text-body-sm">
            <input type="checkbox" checked={interim} onChange={(e) => setInterim(e.target.checked)} />
            {t('web.admin.users.interim')}
          </label>
          <Button size="sm" onClick={create} disabled={!name.trim()}>
            <UserPlus className="me-1.5 h-3.5 w-3.5" /> {t('web.admin.users.create')}
          </Button>
        </div>

        <div className="rounded-md border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('web.admin.users.name')}</TableHead>
                <TableHead>{t('web.admin.users.code')}</TableHead>
                <TableHead>{t('web.admin.users.role')}</TableHead>
                <TableHead>{t('web.admin.users.scope')}</TableHead>
                <TableHead>{t('web.admin.users.status')}</TableHead>
                <TableHead className="text-end">{t('web.admin.users.pin')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.map((p) => (
                <TableRow key={p.id}>
                  <TableCell className="font-medium">
                    {p.name}
                    {p.interim && <Badge variant="ghost" className="ms-2">{t('web.admin.users.interim')}</Badge>}
                  </TableCell>
                  <TableCell className="font-mono">{p.code}</TableCell>
                  <TableCell>
                    <Select
                      value={p.role}
                      onChange={(e) => void setUserRole(p.id, e.target.value as RoleKey)}
                      className="h-8 w-44"
                      aria-label={t('web.admin.users.role')}
                    >
                      {ROLE_KEYS.map((r) => (
                        <option key={r} value={r}>{t(`role.${r}`)}</option>
                      ))}
                    </Select>
                  </TableCell>
                  {/* BL-012 — perimeter: a single line, empty = whole site. */}
                  <TableCell>
                    <Select
                      value={p.scopeLineId ?? ''}
                      onChange={(e) => void setUserScope(p.id, e.target.value || null)}
                      className="h-8 w-40"
                      aria-label={t('web.admin.users.scope')}
                    >
                      <option value="">{t('web.admin.users.scopeAll')}</option>
                      {lines.map((line) => (
                        <option key={line.id} value={line.id}>{line.code} · {line.name}</option>
                      ))}
                    </Select>
                  </TableCell>
                  <TableCell>
                    <Badge variant="ghost">{p.active ? t('web.admin.users.active') : t('web.admin.users.inactive')}</Badge>
                  </TableCell>
                  <TableCell className="text-end">
                    <div className="inline-flex items-center gap-2">
                      {revealedPins[p.id] && <span dir="ltr" className="font-mono text-body-sm text-state-production">{revealedPins[p.id]}</span>}
                      <Button size="sm" variant="outline" onClick={() => void regen(p.id)}>
                        <KeyRound className="me-1.5 h-3.5 w-3.5" /> {t('web.admin.users.regenerate')}
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => void toggleUserActive(p.id)}>
                        <Power className="me-1.5 h-3.5 w-3.5" />
                        {p.active ? t('web.admin.users.deactivate') : t('web.admin.users.reactivate')}
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
        <p className="text-caption text-muted-foreground">{t('web.admin.users.pinHint')}</p>
      </CardContent>
    </Card>
  );
}
