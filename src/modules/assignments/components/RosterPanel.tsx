import { Clock, UserCheck, UserX } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useT } from '@/i18n/I18nProvider';
import { cn } from '@/lib/utils';
import {
  PRESENCES, ROSTER, presenceOf, setPresence,
  type AssignmentState, type Presence,
} from '@/oas/assignmentStore';

const ICONS: Record<Presence, typeof UserCheck> = {
  confirmed: UserCheck,
  pending: Clock,
  absent: UserX,
};

/** Colour per list, so the three states read at a glance on the board. */
const TONES: Record<Presence, string> = {
  confirmed: 'border-state-production bg-state-production/15 text-state-production',
  pending: 'border-state-material bg-state-material/15 text-state-material',
  absent: 'border-destructive bg-destructive/10 text-destructive',
};

/** BL-019 / BL-020 — presence triage in three lists: confirmed / awaited / absent. */
export function RosterPanel({ state }: { state: AssignmentState }) {
  const t = useT();
  const postOf = (name: string) =>
    Object.keys(state.assignments).find((code) => state.assignments[code] === name) ?? null;

  return (
    <Card data-demo="assign-roster">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <UserCheck className="h-4 w-4" /> {t('web.assign.roster')}
        </CardTitle>
        <CardDescription>{t('web.assign.rosterDesc')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {PRESENCES.map((presence) => {
          const names = ROSTER.filter((n) => presenceOf(state, n) === presence);
          const Icon = ICONS[presence];
          return (
            <section key={presence} className="space-y-2">
              <h3 className="flex items-center gap-2 text-caption font-semibold uppercase text-muted-foreground">
                <Icon className="h-3.5 w-3.5" />
                {t(`web.assign.presence.${presence}`)}
                <span className="font-mono">{names.length}</span>
              </h3>
              {names.length === 0 && (
                <p className="text-caption text-muted-foreground">{t('web.assign.presence.empty')}</p>
              )}
              {names.map((name) => {
                const post = postOf(name);
                return (
                  <div key={name} className="flex flex-wrap items-center gap-3 rounded-md border border-border p-2.5">
                    <span className="text-body font-medium">{name}</span>
                    <span className="font-mono text-caption text-muted-foreground">
                      {post ?? t('web.assign.unassigned')}
                    </span>
                    <div className="ms-auto flex gap-1.5" role="group" aria-label={name}>
                      {PRESENCES.map((target) => {
                        const TargetIcon = ICONS[target];
                        const on = target === presence;
                        return (
                          <button
                            key={target}
                            type="button"
                            aria-pressed={on}
                            onClick={() => setPresence(name, target)}
                            title={t(`web.assign.presence.${target}`)}
                            className={cn(
                              'inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-caption font-medium transition-colors',
                              on ? TONES[target] : 'border-border bg-muted text-muted-foreground hover:bg-accent',
                            )}
                          >
                            <TargetIcon className="h-3.5 w-3.5" />
                            <span className="hidden sm:inline">{t(`web.assign.presence.${target}`)}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </section>
          );
        })}
      </CardContent>
    </Card>
  );
}
