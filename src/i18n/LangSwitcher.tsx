import { useI18n } from './I18nProvider';
import { LANGS, type Lang } from './translations';
import { cn } from '@/lib/utils';

import flagFr from '@/assets/flags/flag-fr.png';
import flagEn from '@/assets/flags/flag-en.png';
import flagAr from '@/assets/flags/flag-ar.png';

const FLAGS: Record<Lang, string> = { fr: flagFr, en: flagEn, ar: flagAr };

type Variant = 'segment' | 'menu' | 'compact';

interface Props {
  variant?: Variant;
  className?: string;
}

/**
 * Three-way FR / EN / AR switcher rendered with national flags
 * (AR = Tunisian flag, the launch market). Variants:
 *   - segment: large flag buttons for login / mobile screens.
 *   - menu:    flag row for dense web sidebars.
 *   - compact: small flags for headers.
 */
export function LangSwitcher({ variant = 'segment', className }: Props) {
  const { lang, setLang, t } = useI18n();

  const size =
    variant === 'compact'
      ? 'h-6 w-9'
      : variant === 'menu'
        ? 'h-6 w-9'
        : 'h-8 w-12';

  return (
    <div
      role="group"
      aria-label={t('common.language')}
      className={cn(
        'flex items-center gap-1 rounded-lg border border-border bg-card p-1',
        className,
      )}
    >
      {LANGS.map((l) => {
        const active = lang === l.code;
        return (
          <button
            key={l.code}
            type="button"
            onClick={() => setLang(l.code)}
            aria-pressed={active}
            title={l.native}
            className={cn(
              'group relative flex items-center justify-center rounded-md p-0.5 transition-all',
              active
                ? 'ring-2 ring-ring ring-offset-1 ring-offset-card'
                : 'opacity-60 hover:opacity-100',
            )}
          >
            <img
              src={FLAGS[l.code]}
              alt={l.native}
              loading="lazy"
              width={992}
              height={672}
              className={cn('rounded-[3px] object-cover shadow-sm', size)}
            />
            <span className="sr-only">{l.native}</span>
          </button>
        );
      })}
    </div>
  );
}
