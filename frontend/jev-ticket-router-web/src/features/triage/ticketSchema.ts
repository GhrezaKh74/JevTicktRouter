import { z } from 'zod';
import type { TFunction } from 'i18next';
import { REQUESTER_ROLES } from '../../api/types';

/** Length limits, mirroring the FluentValidation rules on the server. */
export const TICKET_LIMITS = {
  titleMin: 3,
  titleMax: 200,
  descriptionMin: 10,
  descriptionMax: 5000,
} as const;

/**
 * Builds the client-side validation schema in the reader's language.
 *
 * A factory rather than a module constant because the messages are translated: a schema built once
 * at import time would keep whichever language happened to be active then, and stop matching the UI
 * after a language switch.
 */
export function createTicketSchema(t: TFunction) {
  return z.object({
    title: z
      .string()
      .trim()
      .min(TICKET_LIMITS.titleMin, t('validation.titleMin', { count: TICKET_LIMITS.titleMin }))
      .max(TICKET_LIMITS.titleMax, t('validation.titleMax', { count: TICKET_LIMITS.titleMax })),
    description: z
      .string()
      .trim()
      .min(
        TICKET_LIMITS.descriptionMin,
        t('validation.descriptionMin', { count: TICKET_LIMITS.descriptionMin }),
      )
      .max(
        TICKET_LIMITS.descriptionMax,
        t('validation.descriptionMax', { count: TICKET_LIMITS.descriptionMax }),
      ),
    requesterRole: z.enum(REQUESTER_ROLES, { message: t('validation.roleInvalid') }),
  });
}

/** The shape of the ticket form. */
export type TicketFormValues = z.infer<ReturnType<typeof createTicketSchema>>;

/** An empty form. */
export const emptyTicket: TicketFormValues = {
  title: '',
  description: '',
  requesterRole: 'BranchEmployee',
};
