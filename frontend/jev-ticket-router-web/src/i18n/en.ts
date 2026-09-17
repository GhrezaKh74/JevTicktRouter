/**
 * English messages. This object is the source of truth for the message-key type: every other locale
 * is typed against it, so a missing or misspelled key is a compile error rather than a blank label.
 */
export const en = {
  app: {
    name: 'JevTicketRouter',
    tagline: 'Structured ticket triage with TypeSafe Jev',
    footer: 'Jev makes the structured calls; deterministic .NET rules make the final decision.',
    apiDocs: 'API documentation',
  },

  header: {
    github: 'View the source on GitHub',
    language: 'Language',
    modeLive: 'Live Jev',
    modeMock: 'Mock mode',
    modeUnknown: 'API offline',
    modeLiveHint: 'Calling the TypeSafe API with model {{model}}.',
    modeMockHint:
      'No TYPESAFE_API_KEY is configured, so triage returns deterministic sample answers.',
    modeUnknownHint: 'The API could not be reached, so its mode is unknown.',
  },

  form: {
    heading: 'Submit a ticket',
    subheading: 'Persian or English. Nothing you type here leaves your machine in mock mode.',
    title: 'Title',
    titlePlaceholder: 'Short summary of the problem',
    description: 'Description',
    descriptionPlaceholder: 'What happened, what you expected, and anything you already tried',
    descriptionHint: 'Avoid pasting real credentials or account numbers.',
    requesterRole: 'Requester role',
    submit: 'Triage ticket',
    submitting: 'Analysing…',
    reset: 'Clear the form',
    samplesDivider: 'or try a sample',
  },

  roles: {
    BranchEmployee: 'Branch employee',
    Customer: 'Customer',
    InternalSupport: 'Internal support',
  },

  demos: {
    'persian-technical': {
      label: 'Persian · technical issue',
      hint: 'A branch teller reporting an application fault, in Persian.',
    },
    'english-access': {
      label: 'English · access request',
      hint: 'A routine, pre-approved onboarding request.',
    },
    'security-review': {
      label: 'Security · needs review',
      hint: 'A phishing report that the rules escalate and redact.',
    },
  },

  result: {
    heading: 'Routing decision',
    ticketId: 'Ticket {{id}}',
    escalated: 'Held for human review',
    autoRouted: 'Ready to auto-route',
    summaryAuto: 'Auto-routed to {{team}} at {{priority}} priority.',
    summaryEscalated:
      'Queued for {{team}} at {{priority}} priority, held for human review before assignment.',
    category: 'Category',
    targetTeam: 'Target team',
    priority: 'Priority',
    sensitiveData: 'Sensitive data',
    humanReview: 'Human review',
    sensitiveDetected: 'Detected',
    sensitiveNone: 'None detected',
    reviewRequired: 'Required',
    reviewNotRequired: 'Not required',
    confidenceHeading: 'Jev confidence',
    sensitiveWarning:
      'Sensitive data was detected. The ticket text has been redacted from the structured logs and from the developer details below.',
    loadingHeading: 'Analysing ticket…',
    loadingSubheading: 'Asking Jev five questions in a single batched call',
    loadingAria: 'Analysing ticket',
    errorHeading: 'Triage failed',
    errorTitle: 'Could not triage this ticket',
    emptyHeading: 'No ticket analysed yet',
    emptyBody:
      'Submit a ticket, or pick one of the samples. Jev answers five scoped questions in one call, then deterministic .NET rules decide the final routing.',
  },

  decision: {
    byJev: 'Jev',
    byRule: 'rule',
    jevHint: 'This value is exactly what Jev returned.',
    ruleOverrodeHint: 'A business rule set this to {{value}}. Jev proposed {{modelValue}}.',
    ruleConfirmedHint: 'A business rule confirmed this value. Jev also proposed {{modelValue}}.',
  },

  confidence: {
    notReported: 'not reported',
    belowThreshold: '{{percent}} · below threshold',
    noneAria: '{{label}}: no confidence reported',
    noulHint: 'Yes/no (noul) answers return a probability but no confidence value.',
    meterHint: 'Jev reported {{percent}} confidence. The escalation threshold is {{threshold}}.',
    meterAria: '{{label}} confidence',
  },

  devDetails: {
    heading: 'Developer details',
    redactedBanner:
      'This ticket was flagged as containing sensitive data, so the server redacted the ticket text before sending this response. The raw description is not available here.',
    jevResponse: 'Jev structured response (sanitised)',
    mode: 'mode: {{value}}',
    model: 'model: {{value}}',
    latency: 'latency: {{value}} ms',
    rulesApplied: 'Deterministic rules applied ({{count}})',
    noRules:
      'No rule changed Jev’s proposal. The model was confident and nothing required escalation.',
    finalResponse: 'Final API response',
  },

  categories: {
    TechnicalIssue: 'Technical issue',
    ServiceInquiry: 'Service inquiry',
    AccessRequest: 'Access request',
    SecurityConcern: 'Security concern',
    GeneralQuestion: 'General question',
  },

  teams: {
    ApplicationSupport: 'Application Support',
    Infrastructure: 'Infrastructure',
    IdentityAccess: 'Identity & Access',
    Security: 'Security',
    BusinessOperations: 'Business Operations',
  },

  priorities: {
    Low: 'Low',
    Medium: 'Medium',
    High: 'High',
    Critical: 'Critical',
  },

  rules: {
    SECURITY_OR_CRITICAL_ESCALATION:
      'Security concerns and critical tickets always require human review.',
    LOW_CONFIDENCE_ESCALATION:
      'Jev confidence below the threshold on any routing field requires human review.',
    SENSITIVE_DATA_REDACTION:
      'Tickets flagged as containing sensitive data have their description redacted.',
    MODEL_REQUESTED_REVIEW: 'Jev estimated a high probability that this ticket needs a human.',
  },

  validation: {
    titleMin: 'Title must be at least {{count}} characters.',
    titleMax: 'Title must be at most {{count}} characters.',
    descriptionMin: 'Description must be at least {{count}} characters.',
    descriptionMax: 'Description must be at most {{count}} characters.',
    roleInvalid: 'Choose a requester role.',
  },

  toast: {
    routed: 'Triaged. Routed to {{team}}.',
    needsReview: 'Triaged. This ticket needs a human review.',
    failed: 'Triage failed. See the details on the right.',
  },

  errors: {
    unreachable: 'Could not reach the triage API. Check that the backend is running and try again.',
    unexpected: 'The triage API returned an unexpected error ({{status}}).',
  },
} as const;

/**
 * Widens the literal string types produced by `as const` back to `string`, while keeping the key
 * structure exact. Without this a translation could only ever be assigned the English text itself.
 */
type Widen<T> = T extends string ? string : { [K in keyof T]: Widen<T[K]> };

/** The shape every locale must satisfy: the same keys as {@link en}, with any string values. */
export type Messages = Widen<typeof en>;
