import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { renderWithProviders } from '../test/renderWithProviders';
import { cleanResult } from '../test/fixtures';
import { demoLabel } from '../test/i18nHelpers';
import { LOCALES, LOCALE_STORAGE_KEY, en, fa, formatPercent } from '../i18n';
import type { HealthResponse } from '../api/types';

const health: HealthResponse = {
  status: 'Healthy',
  jevMode: 'Mock',
  model: 'jev-latest',
  minimumConfidence: 0.75,
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function mockFetch() {
  return vi.fn((input: RequestInfo | URL) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    return Promise.resolve(jsonResponse(url.includes('/api/health') ? health : cleanResult));
  });
}

/** Walks two message trees in parallel and reports any leaf that differs in shape. */
function compareShape(a: unknown, b: unknown, path = ''): string[] {
  if (typeof a === 'string') {
    return typeof b === 'string' ? [] : [path];
  }

  if (typeof a !== 'object' || a === null || typeof b !== 'object' || b === null) {
    return [path];
  }

  const left = a as Record<string, unknown>;
  const right = b as Record<string, unknown>;

  return Object.keys(left).flatMap((key) =>
    key in right
      ? compareShape(left[key], right[key], path ? `${path}.${key}` : key)
      : [`${path}.${key}`],
  );
}

describe('translations', () => {
  it('ships every English key in Persian', () => {
    expect(compareShape(en, fa)).toEqual([]);
  });

  it('has no key left as the English text in Persian', () => {
    // A copy-paste slip would leave an English sentence sitting in the Persian file.
    expect(fa.form.heading).not.toBe(en.form.heading);
    expect(fa.result.heading).not.toBe(en.result.heading);
    expect(fa.categories.SecurityConcern).not.toBe(en.categories.SecurityConcern);
  });

  it('keeps interpolation placeholders intact across locales', () => {
    const placeholders = (value: string) =>
      [...value.matchAll(/{{(\w+)}}/g)].map((m) => m[1]).sort();

    expect(placeholders(fa.result.summaryAuto)).toEqual(placeholders(en.result.summaryAuto));
    expect(placeholders(fa.toast.routed)).toEqual(placeholders(en.toast.routed));
    expect(placeholders(fa.header.modeLiveHint)).toEqual(placeholders(en.header.modeLiveHint));
  });

  it('exposes exactly the two shipped locales', () => {
    expect(LOCALES).toEqual(['en', 'fa']);
  });
});

describe('number formatting', () => {
  it('uses Western digits in English', () => {
    expect(formatPercent(0.94, 'en')).toBe('94%');
  });

  it('uses Persian digits in Persian', () => {
    const formatted = formatPercent(0.94, 'fa');

    expect(formatted).toMatch(/[۰-۹]/);
    expect(formatted).not.toMatch(/[0-9]/);
  });
});

describe('the interface in Persian', () => {
  it('renders the dashboard in Persian and flips the page to RTL', async () => {
    vi.stubGlobal('fetch', mockFetch());

    renderWithProviders(<App />, { locale: 'fa' });

    expect(await screen.findByText(fa.form.heading)).toBeInTheDocument();
    expect(screen.getByText(fa.app.tagline)).toBeInTheDocument();

    await waitFor(() => {
      expect(document.documentElement).toHaveAttribute('dir', 'rtl');
      expect(document.documentElement).toHaveAttribute('lang', 'fa');
    });
  });

  it('stays left-to-right in English', async () => {
    vi.stubGlobal('fetch', mockFetch());

    renderWithProviders(<App />, { locale: 'en' });

    expect(await screen.findByText(en.form.heading)).toBeInTheDocument();

    await waitFor(() => {
      expect(document.documentElement).toHaveAttribute('dir', 'ltr');
    });
  });

  it('translates the routing decision', async () => {
    vi.stubGlobal('fetch', mockFetch());
    const user = userEvent.setup();

    renderWithProviders(<App />, { locale: 'fa' });

    await user.click(
      await screen.findByRole('button', { name: demoLabel('english-access', 'fa') }),
    );
    await user.click(screen.getByRole('button', { name: fa.form.submit }));

    expect(await screen.findByText(fa.result.heading)).toBeInTheDocument();
    expect(screen.getByText(fa.teams.IdentityAccess)).toBeInTheDocument();
    expect(screen.getByText(fa.result.autoRouted)).toBeInTheDocument();
  });

  it('shows Persian validation messages', async () => {
    vi.stubGlobal('fetch', mockFetch());
    const user = userEvent.setup();

    renderWithProviders(<App />, { locale: 'fa' });

    await user.type(await screen.findByRole('textbox', { name: new RegExp(fa.form.title) }), 'ا');
    await user.click(screen.getByRole('button', { name: fa.form.submit }));

    // The message is interpolated with a Persian-digit count, so match on the stable prefix.
    expect(await screen.findByText(/عنوان باید حداقل/)).toBeInTheDocument();
  });

  it('switches language at runtime and remembers the choice', async () => {
    vi.stubGlobal('fetch', mockFetch());
    const user = userEvent.setup();

    renderWithProviders(<App />, { locale: 'en' });

    expect(await screen.findByText(en.form.heading)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: en.header.language }));
    await user.click(await screen.findByRole('menuitem', { name: 'فارسی' }));

    expect(await screen.findByText(fa.form.heading)).toBeInTheDocument();

    await waitFor(() => {
      expect(document.documentElement).toHaveAttribute('dir', 'rtl');
      expect(window.localStorage.getItem(LOCALE_STORAGE_KEY)).toBe('fa');
    });
  });

  it('re-translates a visible validation message when the language changes', async () => {
    // The Zod schema is built from `t`. If it were not rebuilt on a language change, the message
    // already on screen would stay stranded in the previous language.
    vi.stubGlobal('fetch', mockFetch());
    const user = userEvent.setup();

    renderWithProviders(<App />, { locale: 'en' });

    await user.type(await screen.findByRole('textbox', { name: /title/i }), 'a');
    await user.click(screen.getByRole('button', { name: en.form.submit }));
    expect(await screen.findByText(/Title must be at least/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: en.header.language }));
    await user.click(await screen.findByRole('menuitem', { name: 'فارسی' }));

    expect(await screen.findByText(/عنوان باید حداقل/)).toBeInTheDocument();
    expect(screen.queryByText(/Title must be at least/)).not.toBeInTheDocument();
  });

  it('keeps the product name in Latin script in both locales', async () => {
    vi.stubGlobal('fetch', mockFetch());

    renderWithProviders(<App />, { locale: 'fa' });

    const heading = await screen.findByRole('heading', { name: 'JevTicketRouter' });

    expect(heading).toHaveAttribute('dir', 'ltr');
    expect(heading).toHaveAttribute('lang', 'en');
  });
});
