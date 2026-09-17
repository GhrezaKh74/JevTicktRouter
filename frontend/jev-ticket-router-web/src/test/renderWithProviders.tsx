import type { ReactElement, ReactNode } from 'react';
import { render, type RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LocaleProvider } from '../providers/LocaleProvider';
import { LOCALE_STORAGE_KEY, type Locale } from '../i18n';

interface Options extends Omit<RenderOptions, 'wrapper'> {
  /** Interface language to start in. Defaults to English. */
  readonly locale?: Locale;
}

/**
 * Wraps a component in the same providers the real app uses, with retries disabled.
 *
 * The locale is seeded through localStorage rather than a prop, because that is the same path the
 * app itself takes on start-up — so a test asserting on Persian is exercising the real detection.
 */
export function renderWithProviders(ui: ReactElement, { locale = 'en', ...options }: Options = {}) {
  try {
    window.localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    // Storage is available in jsdom; a failure here would only weaken the locale seed.
  }

  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  });

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <LocaleProvider>{children}</LocaleProvider>
      </QueryClientProvider>
    );
  }

  return { queryClient, ...render(ui, { wrapper: Wrapper, ...options }) };
}
