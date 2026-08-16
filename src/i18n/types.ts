export type Lang = 'fr' | 'en' | 'ar';

export type Dict = Record<string, string>;

export const LANGS: { code: Lang; label: string; native: string; dir: 'ltr' | 'rtl' }[] = [
  { code: 'fr', label: 'French',   native: 'Français',   dir: 'ltr' },
  { code: 'en', label: 'English',  native: 'English',    dir: 'ltr' },
  { code: 'ar', label: 'Tunisian', native: 'تونسي',      dir: 'rtl' },
];
