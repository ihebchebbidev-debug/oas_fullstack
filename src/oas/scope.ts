/**
 * BL-012 — account perimeter (site / lines).
 *
 * A console account can be limited to a single production line
 * (`scopeLineId`, a real hierarchy line id) or cover the whole site. Every
 * console view runs its data through `inScope()`, so a line supervisor only
 * sees his own line while a director keeps the whole site. `lines` holds
 * real line names now — nothing here is i18n-keyed.
 */

import { useAuth } from './authStore';
import { useRefState } from './refStore';
import { useHierarchyState } from './hierarchyStore';

export interface Scope {
  /** Allowed line names — always a concrete list. */
  lines: string[];
  /** True when the account covers the whole site. */
  wholeSite: boolean;
  inScope: (lineName: string) => boolean;
}

export function useScope(): Scope {
  const auth = useAuth();
  const { users } = useRefState();
  const { lines: allLines } = useHierarchyState();
  const row = auth ? users.find((u) => u.id === auth.id) : null;
  const allNames = allLines.map((l) => l.name);
  const scopedLine = row?.scopeLineId ? allLines.find((l) => l.id === row.scopeLineId) : undefined;
  const lines = scopedLine ? [scopedLine.name] : allNames;
  const wholeSite = !scopedLine;
  return {
    lines,
    wholeSite,
    inScope: (lineName: string) => wholeSite || lines.includes(lineName),
  };
}
