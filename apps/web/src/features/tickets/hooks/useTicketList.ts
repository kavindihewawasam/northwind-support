import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const SEARCH_DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  data?: PagedResult<TicketListItem>;
  error?: string;
  /** The query the data or error above belongs to. */
  settled?: TicketFilters;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  // What the filter inputs show. Updated on every change.
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  // What requests are built from. It trails `filters` only while a search is being typed.
  const [query, setQuery] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({});

  const latestFilters = useRef(filters);
  const searchTimer = useRef<number | undefined>(undefined);

  // Loading means the current query has not been answered yet.
  const isLoading = state.settled !== query;

  // Any change to `query` (which holds every filter) sends exactly one request.
  useEffect(() => {
    let ignore = false;

    const run = async () => {
      try {
        const data = await ticketsApi.getTickets(query);
        if (!ignore) setState({ data, settled: query });
      } catch (error) {
        if (ignore) return;
        const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
        setState((current) => ({ ...current, error: message, settled: query }));
      }
    };

    void run();

    // A newer query replaced this one: drop its response.
    return () => {
      ignore = true;
    };
  }, [query]);

  useEffect(() => () => window.clearTimeout(searchTimer.current), []);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const next = { ...latestFilters.current, ...patch, page: patch.page ?? 1 };
    latestFilters.current = next;
    setFilters(next);

    window.clearTimeout(searchTimer.current);

    const onlySearchChanged = Object.keys(patch).every((key) => key === 'search');
    if (onlySearchChanged) {
      // Typing: wait until it settles, then send whatever the filters are by then.
      searchTimer.current = window.setTimeout(
        () => setQuery(latestFilters.current),
        SEARCH_DEBOUNCE_MS,
      );
    } else {
      // Dropdowns, checkbox, sort, paging: apply immediately.
      setQuery(next);
    }
  };

  const resetFilters = () => {
    window.clearTimeout(searchTimer.current);
    latestFilters.current = emptyFilters;
    setFilters(emptyFilters);
    setQuery(emptyFilters);
  };

  // Retry: a copy of the query is a new identity, so the effect runs again.
  const reload = () => setQuery((current) => ({ ...current }));

  return {
    filters,
    updateFilters,
    resetFilters,
    reload,
    isLoading,
    data: state.data,
    error: state.error,
  };
}