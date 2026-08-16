import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Moon, Sun } from 'lucide-react';

import { cn } from '@/lib/utils';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'oas.theme';

interface ThemeCtx {
  theme: Theme;
  setTheme: (t: Theme) => void;
  toggle: () => void;
}

const Ctx = createContext<ThemeCtx | null>(null);

function readInitial(): Theme {
  if (typeof window === 'undefined') return 'dark';
  const stored = window.localStorage.getItem(STORAGE_KEY);
  if (stored === 'light' || stored === 'dark') return stored;
  return window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

/** Single source of truth for light/dark across web, mobile and desktop shells. */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(readInitial);

  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle('dark', theme === 'dark');
    root.style.colorScheme = theme;
    window.localStorage.setItem(STORAGE_KEY, theme);
  }, [theme]);

  const setTheme = useCallback((t: Theme) => setThemeState(t), []);
  const toggle = useCallback(() => setThemeState((t) => (t === 'dark' ? 'light' : 'dark')), []);

  const value = useMemo(() => ({ theme, setTheme, toggle }), [theme, setTheme, toggle]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

function useTheme(): ThemeCtx {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useTheme must be used inside <ThemeProvider>');
  return ctx;
}

interface ToggleProps {
  className?: string;
  /** `icon` = square button (mobile headers), `row` = full-width labelled row (sidebars). */
  variant?: 'icon' | 'row';
  label?: string;
}

export function ThemeToggle({ className, variant = 'icon', label }: ToggleProps) {
  const { theme, toggle } = useTheme();
  const Icon = theme === 'dark' ? Sun : Moon;
  const a11y = label ?? (theme === 'dark' ? 'Light mode' : 'Dark mode');

  if (variant === 'row') {
    return (
      <button
        type="button"
        onClick={toggle}
        aria-label={a11y}
        className={cn(
          'flex h-8 w-full items-center gap-2 rounded-md px-2 text-xs text-sidebar-foreground/70 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground',
          className,
        )}
      >
        <Icon className="h-3.5 w-3.5 shrink-0" />
        {label && <span className="truncate">{label}</span>}
      </button>
    );
  }

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={a11y}
      title={a11y}
      className={cn(
        'inline-flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-card text-muted-foreground transition-colors hover:bg-muted hover:text-foreground',
        className,
      )}
    >
      <Icon className="h-4 w-4" />
    </button>
  );
}
