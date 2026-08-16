/**
 * OAS i18n dictionary — FR (default), EN, AR (Tunisian Arabic).
 * Flat keys: `namespace.key`. Missing keys fall back to FR.
 *
 * Per-locale dictionaries live in `./dicts/<lang>.ts` so each file stays
 * small and easy to review; this module is a barrel over them.
 *
 * Tunisian Arabic notes:
 *   - Industrial terms are kept in MSA when the shop-floor uses them daily
 *     (توقّف / إنتاج / تنبيهات / نوبة), everyday actions use Darija forms
 *     (سكّر, صادق, رجوع, أبدا).
 */

import { fr } from './dicts/fr';
import { en } from './dicts/en';
import { ar } from './dicts/ar';
import type { Dict, Lang } from './types';

export { LANGS } from './types';
export type { Lang, Dict } from './types';

export const DICTS: Record<Lang, Dict> = { fr, en, ar };
