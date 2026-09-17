import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { en } from './en';
import { fa } from './fa';

/** The locales the interface ships with. */
export const LOCALES = ['en', 'fa'] as const;

/** A supported interface locale. */
export type Locale = (typeof LOCALES)[number];

/** How each locale names itself, for the language switcher. */
export const LOCALE_LABELS: Record<Locale, string> = {
  en: 'English',
  fa: 'فارسی',
};

/** Text direction per locale. */
export const LOCALE_DIRECTIONS: Record<Locale, 'ltr' | 'rtl'> = {
  en: 'ltr',
  fa: 'rtl',
};

/** BCP 47 tags used for number and date formatting. */
export const LOCALE_TAGS: Record<Locale, string> = {
  en: 'en-GB',
  fa: 'fa-IR',
};

/** localStorage key holding the reader's chosen locale. */
export const LOCALE_STORAGE_KEY = 'jevticketrouter.locale';

/** Narrows an arbitrary string to a supported locale. */
export function isLocale(value: unknown): value is Locale {
  return typeof value === 'string' && (LOCALES as readonly string[]).includes(value);
}

/**
 * Picks the starting locale: a previous choice, then the browser's preference, then English.
 * Storage access is guarded because it throws in a private window with site data blocked.
 */
export function detectInitialLocale(): Locale {
  try {
    const stored = window.localStorage.getItem(LOCALE_STORAGE_KEY);
    if (isLocale(stored)) {
      return stored;
    }
  } catch {
    // Storage unavailable; fall through to the browser preference.
  }

  for (const language of navigator.languages) {
    const base = language.split('-')[0];
    if (isLocale(base)) {
      return base;
    }
  }

  return 'en';
}

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    fa: { translation: fa },
  },
  lng: 'en',
  fallbackLng: 'en',
  supportedLngs: LOCALES,
  // React already escapes rendered values, so i18next must not escape them a second time.
  interpolation: { escapeValue: false },
  returnNull: false,
});

export default i18n;
