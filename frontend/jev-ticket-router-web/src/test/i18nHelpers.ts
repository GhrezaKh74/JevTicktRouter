import { en, fa, type Locale } from '../i18n';
import type { DemoTicketId } from '../features/triage/demoTickets';

const MESSAGES = { en, fa };

/** The label a demo ticket's button shows in a given locale. */
export function demoLabel(id: DemoTicketId, locale: Locale = 'en'): string {
  return MESSAGES[locale].demos[id].label;
}
