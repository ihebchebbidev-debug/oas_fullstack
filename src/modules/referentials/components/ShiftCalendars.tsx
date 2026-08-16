import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useT } from '@/i18n/I18nProvider';
import {
  dailyOpenMin,
  removeShift,
  shiftOpenMin,
  shiftSpanMin,
  upsertShift,
  useRefState,
  type ShiftRow,
} from '@/oas/refStore';

const BLANK: ShiftRow = { id: '', siteId: '', code: '', name: '', start: '06:00', end: '14:00', breakMinutes: 30, active: true };

const hhmm = (min: number) => `${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')}`;

/**
 * BL-009 — shift calendars (2×8 / 3×8 / 4×8 / journée).
 * Breaks are deducted from the span, so the theoretical opening time
 * shown here is exactly the base used by the OEE computation.
 */
export function ShiftCalendars() {
  const t = useT();
  const state = useRefState();
  const { shifts } = state;
  const [draft, setDraft] = useState<ShiftRow>(BLANK);
  const total = dailyOpenMin(state);

  const patch = (row: ShiftRow, next: Partial<ShiftRow>) => void upsertShift({ ...row, ...next });

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('web.ref.shifts')}</CardTitle>
        <CardDescription>{t('web.ref.shiftsDesc')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="space-y-2">
          {shifts.map((s) => (
            <div key={s.id} className="flex flex-wrap items-center gap-2 rounded-lg border border-border p-2">
              <div className="min-w-[10rem] flex-1">
                <Input
                  value={s.name}
                  aria-label={t('web.ref.shift.name')}
                  onChange={(e) => patch(s, { name: e.target.value })}
                  className="h-8"
                />
                <p className="mt-1 font-mono text-caption text-muted-foreground" dir="ltr">
                  {s.code} · {t('web.ref.shift.open')} {hhmm(shiftOpenMin(s))}
                  {shiftSpanMin(s) !== shiftOpenMin(s) && ` (−${s.breakMinutes} min)`}
                </p>
              </div>
              <Input
                type="time"
                value={s.start}
                aria-label={t('web.ref.shift.start')}
                onChange={(e) => patch(s, { start: e.target.value })}
                className="h-8 w-28"
              />
              <Input
                type="time"
                value={s.end}
                aria-label={t('web.ref.shift.end')}
                onChange={(e) => patch(s, { end: e.target.value })}
                className="h-8 w-28"
              />
              <Input
                type="number"
                min={0}
                value={s.breakMinutes}
                aria-label={t('web.ref.shift.break')}
                onChange={(e) => patch(s, { breakMinutes: Number(e.target.value) })}
                className="h-8 w-20"
              />
              <div className="flex items-center gap-1">
                <Button
                  size="sm"
                  variant={s.active ? 'default' : 'outline'}
                  onClick={() => patch(s, { active: !s.active })}
                >
                  {s.active ? t('common.active') : t('common.inactive')}
                </Button>
                <Button size="icon" variant="ghost" aria-label={t('common.delete')} onClick={() => void removeShift(s.id)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </div>

        <div className="flex flex-wrap items-center gap-2 rounded-lg border border-dashed border-border p-2">
          <Input
            value={draft.code}
            placeholder={t('web.ref.shift.code')}
            onChange={(e) => setDraft({ ...draft, code: e.target.value })}
            className="h-8 w-24"
          />
          <Input
            value={draft.name}
            placeholder={t('web.ref.shift.name')}
            onChange={(e) => setDraft({ ...draft, name: e.target.value })}
            className="h-8 w-40"
          />
          <Input type="time" value={draft.start} onChange={(e) => setDraft({ ...draft, start: e.target.value })} className="h-8 w-28" />
          <Input type="time" value={draft.end} onChange={(e) => setDraft({ ...draft, end: e.target.value })} className="h-8 w-28" />
          <Input
            type="number"
            min={0}
            value={draft.breakMinutes}
            onChange={(e) => setDraft({ ...draft, breakMinutes: Number(e.target.value) })}
            className="h-8 w-20"
          />
          <Button
            size="sm"
            onClick={() => {
              void upsertShift(draft);
              setDraft(BLANK);
            }}
          >
            <Plus className="me-1 h-4 w-4" />
            {t('common.add')}
          </Button>
        </div>

        <p className="text-caption text-muted-foreground">
          {t('web.ref.shift.total', { h: hhmm(total) })}
        </p>
      </CardContent>
    </Card>
  );
}
