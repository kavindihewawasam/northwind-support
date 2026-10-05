import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';

/** Loads one ticket, and refetches whenever the id changes. */
export function useTicket(id: number) {
  const load = useCallback(() => ticketsApi.getTicket(id), [id]);

  return useAsyncData(load, 'Unable to load this ticket.');
}
