import type { TriageTicketResponse } from '../api/types';

/** A confident, auto-routed result: no rule fired and nothing was redacted. */
export const cleanResult: TriageTicketResponse = {
  ticketId: 'abc123def456',
  category: {
    value: 'AccessRequest',
    modelValue: 'AccessRequest',
    confidence: 0.94,
    origin: 'JevModel',
    wasOverridden: false,
  },
  targetTeam: {
    value: 'IdentityAccess',
    modelValue: 'IdentityAccess',
    confidence: 0.91,
    origin: 'JevModel',
    wasOverridden: false,
  },
  priority: {
    value: 'Low',
    modelValue: 'Low',
    confidence: 0.88,
    origin: 'JevModel',
    wasOverridden: false,
  },
  containsSensitiveData: {
    value: false,
    modelValue: false,
    confidence: null,
    origin: 'JevModel',
    wasOverridden: false,
  },
  needsHumanReview: {
    value: false,
    modelValue: false,
    confidence: null,
    origin: 'JevModel',
    wasOverridden: false,
  },
  routingSummary: 'Auto-routed to IdentityAccess at Low priority.',
  appliedRules: [],
  jev: {
    mode: 'Mock',
    model: 'jev-mock-1.13.0',
    latencyMs: 42,
    priorityScore: 0.12,
    sensitiveDataProbability: 0.04,
    humanReviewProbability: 0.08,
    stateSummary: {
      title: 'Access to the reporting portal',
      description: 'A new analyst needs read-only access to the reporting portal.',
      requesterRole: 'InternalSupport',
      redacted: false,
    },
  },
};

/** A security result: escalated by rule, flagged sensitive, and redacted by the server. */
export const escalatedResult: TriageTicketResponse = {
  ...cleanResult,
  ticketId: 'sec9900aa11',
  category: {
    value: 'SecurityConcern',
    modelValue: 'SecurityConcern',
    confidence: 0.86,
    origin: 'JevModel',
    wasOverridden: false,
  },
  targetTeam: {
    value: 'Security',
    modelValue: 'Security',
    confidence: 0.84,
    origin: 'JevModel',
    wasOverridden: false,
  },
  priority: {
    value: 'Critical',
    modelValue: 'Critical',
    confidence: 0.61,
    origin: 'JevModel',
    wasOverridden: false,
  },
  containsSensitiveData: {
    value: true,
    modelValue: true,
    confidence: null,
    origin: 'JevModel',
    wasOverridden: false,
  },
  needsHumanReview: {
    value: true,
    modelValue: false,
    confidence: null,
    origin: 'BusinessRule',
    wasOverridden: true,
  },
  routingSummary:
    'Queued for Security at Critical priority, held for human review before assignment.',
  appliedRules: [
    {
      id: 'SECURITY_OR_CRITICAL_ESCALATION',
      description: 'Security concerns and critical tickets always require human review.',
      effect: 'needsHumanReview forced to true because category is SecurityConcern.',
    },
    {
      id: 'SENSITIVE_DATA_REDACTION',
      description: 'Tickets flagged as containing sensitive data have their description redacted.',
      effect: 'The ticket description is masked in structured logs.',
    },
  ],
  jev: {
    ...cleanResult.jev,
    priorityScore: 2.9,
    sensitiveDataProbability: 0.93,
    humanReviewProbability: 0.41,
    stateSummary: {
      title: '[REDACTED: sensitive data detected]',
      description: '[REDACTED: sensitive data detected]',
      requesterRole: 'InternalSupport',
      redacted: true,
    },
  },
};
