import { useMutation, useQuery } from '@tanstack/react-query';
import { fetchHealth, triageTicket } from './client';
import type { TriageTicketRequest } from './types';

/** Query keys used across the app. */
export const queryKeys = {
  health: ['health'] as const,
};

/**
 * Reads the service status that drives the Live Jev / Mock mode badge. Kept fresh for a minute; the
 * mode only changes when the server restarts.
 */
export function useHealth() {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: ({ signal }) => fetchHealth(signal),
    staleTime: 60_000,
    retry: 1,
  });
}

/** Submits a ticket for triage. Not retried: the caller decides when to resubmit. */
export function useTriageTicket() {
  return useMutation({
    mutationKey: ['triage'],
    mutationFn: (request: TriageTicketRequest) => triageTicket(request),
    retry: false,
  });
}
