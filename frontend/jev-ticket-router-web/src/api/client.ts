import type {
  HealthResponse,
  ProblemDetails,
  TriageTicketRequest,
  TriageTicketResponse,
} from './types';

/**
 * Base URL for the API. Empty in development so requests go to the Vite dev server, which proxies
 * `/api` to the backend.
 */
const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '';

/**
 * Why a request failed, in a form the UI can branch on.
 *
 * `message` is an English fallback suitable for a console or a log. Failures the client itself
 * diagnoses (`network`, `unexpected-status`) are translated for display; `server` means the message
 * came from the API's ProblemDetails and is shown exactly as the server wrote it.
 */
export type ApiErrorReason = 'network' | 'server' | 'unexpected-status';

/**
 * An API failure carrying the server's ProblemDetails when one was returned, so the UI can show a
 * useful message and per-field validation errors instead of a generic failure.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly reason: ApiErrorReason;
  readonly problem: ProblemDetails | null;

  constructor(
    message: string,
    status: number,
    reason: ApiErrorReason,
    problem: ProblemDetails | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.reason = reason;
    this.problem = problem;
  }

  /** Field-level validation messages, keyed by field name, or null when this was not a 400. */
  get validationErrors(): Record<string, string[]> | null {
    return this.problem?.errors ?? null;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;

  try {
    const headers = new Headers(init?.headers);
    headers.set('Content-Type', 'application/json');

    response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });
  } catch {
    throw new ApiError(
      'Could not reach the triage API. Check that the backend is running and try again.',
      0,
      'network',
    );
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    const detail = problem?.detail ?? problem?.title;

    throw new ApiError(
      detail ?? `The triage API returned an unexpected error (${String(response.status)}).`,
      response.status,
      detail ? 'server' : 'unexpected-status',
      problem,
    );
  }

  return (await response.json()) as T;
}

/** Reads a ProblemDetails body, tolerating a non-JSON error page. */
async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.clone().json()) as ProblemDetails;
  } catch {
    return null;
  }
}

/** Submits a ticket for triage. */
export function triageTicket(
  body: TriageTicketRequest,
  signal?: AbortSignal,
): Promise<TriageTicketResponse> {
  return request<TriageTicketResponse>('/api/tickets/triage', {
    method: 'POST',
    body: JSON.stringify(body),
    ...(signal ? { signal } : {}),
  });
}

/** Reads service status, including whether Jev is live or mocked. */
export function fetchHealth(signal?: AbortSignal): Promise<HealthResponse> {
  return request<HealthResponse>('/api/health', { ...(signal ? { signal } : {}) });
}
