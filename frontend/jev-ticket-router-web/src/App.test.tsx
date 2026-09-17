import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from './App';
import { renderWithProviders } from './test/renderWithProviders';
import { cleanResult, escalatedResult } from './test/fixtures';
import { demoLabel } from './test/i18nHelpers';
import { en } from './i18n';
import type { HealthResponse } from './api/types';

const health: HealthResponse = {
  status: 'Healthy',
  jevMode: 'Mock',
  model: 'jev-latest',
  minimumConfidence: 0.75,
};

/** Reads the target URL from any of the shapes fetch accepts. */
function requestUrl(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }

  return input instanceof URL ? input.href : input.url;
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

/** Routes fetch by URL so each test only states the triage outcome it cares about. */
function mockFetch(triage: () => Response) {
  return vi.fn((input: RequestInfo | URL) => {
    const url = requestUrl(input);

    if (url.includes('/api/health')) {
      return Promise.resolve(jsonResponse(health));
    }

    if (url.includes('/api/tickets/triage')) {
      return Promise.resolve(triage());
    }

    return Promise.reject(new Error(`Unexpected request to ${url}`));
  });
}

describe('App', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      mockFetch(() => jsonResponse(cleanResult)),
    );
  });

  it('shows the project name and the mock-mode badge', async () => {
    renderWithProviders(<App />);

    expect(screen.getByRole('heading', { name: 'JevTicketRouter' })).toBeInTheDocument();
    expect(await screen.findByText('Mock mode')).toBeInTheDocument();
  });

  it('shows a GitHub link', () => {
    renderWithProviders(<App />);

    expect(screen.getByRole('link', { name: /github/i })).toHaveAttribute('href');
  });

  it('triages a demo ticket end to end and shows the decision', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />);

    await user.click(screen.getByRole('button', { name: demoLabel('english-access') }));
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    expect(await screen.findByText('Routing decision')).toBeInTheDocument();
    expect(screen.getByText(en.teams.IdentityAccess)).toBeInTheDocument();
    expect(screen.getByText(en.result.autoRouted)).toBeInTheDocument();
  });

  it('confirms the outcome in a snackbar', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />);

    await user.click(screen.getByRole('button', { name: demoLabel('english-access') }));
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    expect(await screen.findByText('Triaged. Routed to Identity & Access.')).toBeInTheDocument();
  });

  it('reports an escalated ticket as needing review', async () => {
    vi.stubGlobal(
      'fetch',
      mockFetch(() => jsonResponse(escalatedResult)),
    );

    const user = userEvent.setup();
    renderWithProviders(<App />);

    await user.click(screen.getByRole('button', { name: demoLabel('security-review') }));
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    expect(await screen.findByText(en.result.escalated)).toBeInTheDocument();
    expect(await screen.findByText(en.toast.needsReview)).toBeInTheDocument();
  });

  it('shows a readable error when the API fails', async () => {
    vi.stubGlobal(
      'fetch',
      mockFetch(() =>
        jsonResponse(
          { title: 'Service unavailable', detail: 'The TypeSafe API is temporarily overloaded.' },
          503,
        ),
      ),
    );

    const user = userEvent.setup();
    renderWithProviders(<App />);

    await user.click(screen.getByRole('button', { name: demoLabel('persian-technical') }));
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    expect(await screen.findByText('Could not triage this ticket')).toBeInTheDocument();
    expect(screen.getByText('The TypeSafe API is temporarily overloaded.')).toBeInTheDocument();
  });

  it('shows server validation errors inline on the form', async () => {
    vi.stubGlobal(
      'fetch',
      mockFetch(() =>
        jsonResponse(
          {
            title: 'The ticket could not be accepted.',
            errors: { Title: ['The server rejected this title.'] },
          },
          400,
        ),
      ),
    );

    const user = userEvent.setup();
    renderWithProviders(<App />);

    await user.click(screen.getByRole('button', { name: demoLabel('persian-technical') }));
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    expect(await screen.findByText('The server rejected this title.')).toBeInTheDocument();
    // A field-level problem stays on the form rather than replacing the result panel.
    expect(screen.queryByText('Could not triage this ticket')).not.toBeInTheDocument();
  });

  it('reports an unreachable API', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = requestUrl(input);
        if (url.includes('/api/health')) {
          return Promise.resolve(jsonResponse(health));
        }
        return Promise.reject(new TypeError('Failed to fetch'));
      }),
    );

    const user = userEvent.setup();
    renderWithProviders(<App />);

    await user.click(screen.getByRole('button', { name: demoLabel('persian-technical') }));
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    expect(await screen.findByText(/Could not reach the triage API/i)).toBeInTheDocument();
  });

  it('shows an API offline badge when health cannot be read', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))),
    );

    renderWithProviders(<App />);

    await waitFor(
      () => {
        expect(screen.getByText('API offline')).toBeInTheDocument();
      },
      { timeout: 4000 },
    );
  });
});
