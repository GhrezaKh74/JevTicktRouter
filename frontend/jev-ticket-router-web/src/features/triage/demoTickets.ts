import type { TicketFormValues } from './ticketSchema';

/** Identifies a demo ticket. Doubles as its translation key under `demos`. */
export type DemoTicketId = 'persian-technical' | 'english-access' | 'security-review';

/** A one-click sample ticket. Its label and hint are translated; the ticket text is not. */
export interface DemoTicket {
  readonly id: DemoTicketId;
  readonly values: TicketFormValues;
}

/**
 * Three fictional tickets that exercise the interesting paths: clean auto-routing in Persian, clean
 * auto-routing in English, and a security case that the deterministic rules escalate and redact.
 *
 * The ticket bodies stay in their original language whatever the interface language is — the point
 * of the demo is that triage handles either, so translating them would defeat it.
 * All names, numbers, and systems here are invented.
 */
export const demoTickets: readonly DemoTicket[] = [
  {
    id: 'persian-technical',
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
