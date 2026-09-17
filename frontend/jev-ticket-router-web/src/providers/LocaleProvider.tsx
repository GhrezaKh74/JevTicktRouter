import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import createCache from '@emotion/cache';
import { CacheProvider } from '@emotion/react';
import { prefixer } from 'stylis';
import rtlPlugin from 'stylis-plugin-rtl';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import {
  LOCALE_DIRECTIONS,
  LOCALE_STORAGE_KEY,
  detectInitialLocale,
  i18n,
  type Locale,
} from '../i18n';
import { createAppTheme } from '../theme';

interface LocaleContextValue {
  readonly locale: Locale;
  readonly direction: 'ltr' | 'rtl';
  readonly setLocale: (locale: Locale) => void;
}

// eslint-disable-next-line react-refresh/only-export-components
export const LocaleContext = createContext<LocaleContextValue | null>(null);

/**
 * Owns the interface language for the whole app.
 *
 * Switching to Persian has to do three separate things, and missing any one of them leaves the page
 * half-flipped: translate the strings (i18next), mirror MUI's own layout logic (`theme.direction`),
 * and mirror the emitted CSS itself (an Emotion cache with the RTL stylis plugin, since MUI writes
 * physical properties like `margin-left` that the theme alone will not flip). The `dir` and `lang`
 * attributes on `<html>` are set too, so the browser handles bidirectional text correctly and
 * screen readers announce the right language.
 */
export function LocaleProvider({ children }: { readonly children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(detectInitialLocale);
  const direction = LOCALE_DIRECTIONS[locale];

  // Keep i18next in step with the state that drives the theme, so a language change is one render.
  useEffect(() => {
    if (i18n.language !== locale) {
      void i18n.changeLanguage(locale);
    }
  }, [locale]);

  useEffect(() => {
    const root = document.documentElement;
    root.setAttribute('lang', locale);
    root.setAttribute('dir', direction);
  }, [locale, direction]);

  const setLocale = useCallback((next: Locale) => {
    setLocaleState(next);

    try {
      window.localStorage.setItem(LOCALE_STORAGE_KEY, next);
    } catch {
      // A blocked or full store only costs the preference, never the language change itself.
    }
  }, []);

  // A separate cache per direction: the RTL plugin rewrites declarations as they are serialised, so
  // the two directions must not share a style sheet.
  const cache = useMemo(
    () =>
      createCache({
        key: direction === 'rtl' ? 'mui-rtl' : 'mui',
        stylisPlugins: direction === 'rtl' ? [prefixer, rtlPlugin] : [prefixer],
      }),
    [direction],
  );

  const theme = useMemo(() => createAppTheme(direction), [direction]);

  const value = useMemo<LocaleContextValue>(
    () => ({ locale, direction, setLocale }),
    [locale, direction, setLocale],
  );

  return (
    <LocaleContext.Provider value={value}>
      <I18nextProvider i18n={i18n}>
        <CacheProvider value={cache}>
          <ThemeProvider theme={theme}>
            <CssBaseline />
            {children}
          </ThemeProvider>
        </CacheProvider>
      </I18nextProvider>
    </LocaleContext.Provider>
  );
}
