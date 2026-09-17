/**
 * Mirrors the DTOs returned by the JevTicketRouter API. Enum-like values travel as their names, so
 * they are modelled as string unions rather than numbers.
 */

export const REQUESTER_ROLES = ['BranchEmployee', 'Customer', 'InternalSupport'] as const;
export type RequesterRole = (typeof REQUESTER_ROLES)[number];

export const TICKET_CATEGORIES = [
  'TechnicalIssue',
  'ServiceInquiry',
  'AccessRequest',
  'SecurityConcern',
  'GeneralQuestion',
] as const;
export type TicketCategory = (typeof TICKET_CATEGORIES)[number];

export const TARGET_TEAMS = [
  'ApplicationSupport',
  'Infrastructure',
  'IdentityAccess',
  'Security',
  'BusinessOperations',
] as const;
export type TargetTeam = (typeof TARGET_TEAMS)[number];

export const TICKET_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;
export type TicketPriority = (typeof TICKET_PRIORITIES)[number];

/** Where a final value came from. */
export type DecisionOrigin = 'JevModel' | 'BusinessRule';

/** A final field value together with what Jev proposed and who decided it. */
export interface DecidedField<T> {
  readonly value: T;
  readonly modelValue: T;
  /** Jev's confidence from 0 to 1, or null for noul-backed fields, which carry no confidence. */
  readonly confidence: number | null;
  readonly origin: DecisionOrigin;
  readonly wasOverridden: boolean;
}

/** A deterministic rule that fired during triage. */
export interface AppliedRule {
  readonly id: string;
  readonly description: string;
  readonly effect: string;
}

/** The state that was evaluated, redacted when sensitive data was detected. */
export interface JevStateSummary {
  readonly title: string;
  readonly description: string;
  readonly requesterRole: string;
  readonly redacted: boolean;
}

/** Sanitised diagnostics about the Jev call. */
export interface JevDiagnostics {
  readonly mode: 'Live' | 'Mock';
  readonly model: string;
  readonly latencyMs: number;
  readonly priorityScore: number;
  readonly sensitiveDataProbability: number;
  readonly humanReviewProbability: number;
  readonly stateSummary: JevStateSummary;
}

/** The request body of POST /api/tickets/triage. */
export interface TriageTicketRequest {
  readonly title: string;
  readonly description: string;
  readonly requesterRole: RequesterRole;
}

/** The response body of POST /api/tickets/triage. */
export interface TriageTicketResponse {
  readonly ticketId: string;
  readonly category: DecidedField<TicketCategory>;
  readonly targetTeam: DecidedField<TargetTeam>;
  readonly priority: DecidedField<TicketPriority>;
  readonly containsSensitiveData: DecidedField<boolean>;
  readonly needsHumanReview: DecidedField<boolean>;
  readonly routingSummary: string;
  readonly appliedRules: readonly AppliedRule[];
  readonly jev: JevDiagnostics;
}

/** The response body of GET /api/health. */
export interface HealthResponse {
  readonly status: string;
  readonly jevMode: 'Live' | 'Mock';
  readonly model: string;
  readonly minimumConfidence: number;
}

/** RFC 7807 problem details, optionally carrying per-field validation errors. */
export interface ProblemDetails {
  readonly title?: string;
  readonly detail?: string;
  readonly status?: number;
  readonly errors?: Record<string, string[]>;
  readonly traceId?: string;
}
