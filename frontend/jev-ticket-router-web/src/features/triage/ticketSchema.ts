import { z } from 'zod';
import { REQUESTER_ROLES } from '../../api/types';

/**
 * Client-side validation. Mirrors the FluentValidation rules on the server so the user gets instant
 * feedback; the server remains the authority and its errors are surfaced if the two ever diverge.
 */
export const ticketSchema = z.object({
  title: z
    .string()
    .trim()
    .min(3, 'Title must be at least 3 characters.')
    .max(200, 'Title must be at most 200 characters.'),
  description: z
    .string()
    .trim()
    .min(10, 'Description must be at least 10 characters.')
    .max(5000, 'Description must be at most 5000 characters.'),
  requesterRole: z.enum(REQUESTER_ROLES),
});

/** The shape of the ticket form. */
export type TicketFormValues = z.infer<typeof ticketSchema>;

/** An empty form. */
export const emptyTicket: TicketFormValues = {
  title: '',
  description: '',
  requesterRole: 'BranchEmployee',
};
