import { describe, expect, it, vi } from 'vitest';
import { ApiError, fetchHealth, triageTicket } from './client';
import { cleanResult } from '../test/fixtures';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('api client', () => {
  it('posts a ticket to the triage endpoint and returns the decision', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(jsonResponse(cleanResult)));
    vi.stubGlobal('fetch', fetchMock);

    const result = await triageTicket({
      title: 'A title',
      description: 'A description long enough to be valid.',
      requesterRole: 'Customer',
    });

    expect(result.ticketId).toBe(cleanResult.ticketId);

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toContain('/api/tickets/triage');
    expect(init.method).toBe('POST');
  });

  it('reads the health endpoint', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          jsonResponse({
            status: 'Healthy',
            jevMode: 'Live',
            model: 'jev-latest',
            minimumConfidence: 0.75,
          }),
        ),
      ),
    );

    await expect(fetchHealth()).resolves.toMatchObject({ jevMode: 'Live' });
  });

  it('surfaces the ProblemDetails detail as the error message', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(jsonResponse({ detail: 'Upstream is overloaded.' }, 503))),
    );

    await expect(fetchHealth()).rejects.toThrow('Upstream is overloaded.');
  });

  it('exposes field-level validation errors', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(jsonResponse({ errors: { Title: ['Title is too short.'] } }, 400)),
      ),
    );

    const error = await fetchHealth().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).validationErrors).toEqual({ Title: ['Title is too short.'] });
  });

  it('falls back to a generic message for a non-JSON error body', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response('<html>oops</html>', { status: 500 }))),
    );

    await expect(fetchHealth()).rejects.toThrow(/unexpected error \(500\)/);
  });

  it('reports a network failure without leaking the underlying error', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))),
    );

    const error = await fetchHealth().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(0);
    expect((error as ApiError).message).toMatch(/Could not reach the triage API/);
  });
});
