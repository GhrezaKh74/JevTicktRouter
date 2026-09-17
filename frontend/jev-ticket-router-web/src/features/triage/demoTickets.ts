import type { TicketFormValues } from './ticketSchema';

/** A one-click sample ticket. */
export interface DemoTicket {
  readonly id: string;
  readonly label: string;
  readonly hint: string;
  readonly values: TicketFormValues;
}

/**
 * Three fictional tickets that exercise the interesting paths: clean auto-routing in Persian, clean
 * auto-routing in English, and a security case that the deterministic rules escalate and redact.
 * All names, numbers, and systems here are invented.
 */
export const demoTickets: readonly DemoTicket[] = [
  {
    id: 'persian-technical',
    label: 'Persian · technical issue',
    hint: 'A branch teller reporting an application fault, in Persian.',
    values: {
      title: 'خطا هنگام ثبت تراکنش در سامانه شعبه',
      description:
        'از امروز صبح هنگام ثبت تراکنش در سامانه شعبه با خطا مواجه می‌شوم و صفحه لود نمی‌شود. ' +
        'مجبورم هر تراکنش را دو بار وارد کنم و کار باجه کند شده است. لطفاً بررسی کنید.',
      requesterRole: 'BranchEmployee',
    },
  },
  {
    id: 'english-access',
    label: 'English · access request',
    hint: 'A routine, pre-approved onboarding request.',
    values: {
      title: 'Access to the reporting portal for a new analyst',
      description:
        'Our new business analyst started on Monday and needs read-only access to the quarterly ' +
        'reporting portal. Their manager has already approved the request. Please provision the ' +
        'account and the standard analyst role.',
      requesterRole: 'InternalSupport',
    },
  },
  {
    id: 'security-review',
    label: 'Security · needs review',
    hint: 'A phishing report that the rules escalate and redact.',
    values: {
      title: 'Suspicious email asking staff to confirm their password',
      description:
        'Several colleagues received a suspicious email that looks like phishing. It links to a fake ' +
        'login page and asks them to confirm their password. One colleague replied before realising, ' +
        'and the message quoted their internal reference 4400123400567800. Please investigate urgently.',
      requesterRole: 'InternalSupport',
    },
  },
];
