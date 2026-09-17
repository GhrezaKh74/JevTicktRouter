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
 * An API failure carrying the server's ProblemDetails when one was returned, so the UI can show a
 * useful message and per-field validation errors instead of a generic failure.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(message: string, status: number, problem: ProblemDetails | null = null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
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
    );
  }

  if (!response.ok) {
    throw new ApiError(
      await describeFailure(response),
      response.status,
      await readProblem(response),
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

async function describeFailure(response: Response): Promise<string> {
  const problem = await readProblem(response);

  if (problem?.detail) {
    return problem.detail;
  }

  if (problem?.title) {
    return problem.title;
  }

  return `The triage API returned an unexpected error (${String(response.status)}).`;
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
