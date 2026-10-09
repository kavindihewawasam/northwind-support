import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

const advance = (ms: number) =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  const renderLoaded = async () => {
    const hook = renderHook(() => useTicketList());
    await advance(0);
    expect(getTickets).toHaveBeenCalledTimes(1);

    const change = (patch: Partial<TicketFilters>) =>
      act(async () => {
        hook.result.current.updateFilters(patch);
      });

    return { ...hook, change };
  };

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result, change } = await renderLoaded();

    await change({ search: 'invoice' });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await advance(300);

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  // DEFECT-117: these fail on the original code, which never re-ran the effect for them.
  it.each<[string, Partial<TicketFilters>]>([
    ['status', { status: 'InProgress' }],
    ['priority', { priority: 'High' }],
    ['category', { categoryId: 2 }],
    ['customer', { customerId: 3 }],
    ['assigned agent', { assignedAgentId: 4 }],
    ['unassigned only', { unassignedOnly: true }],
    ['sort order', { sortBy: 'priority', sortDirection: 'desc' }],
  ])('sends a request immediately when %s changes', async (_name, patch) => {
    const { change } = await renderLoaded();

    await change(patch);

    // No timers advanced: a dropdown change must not wait for a debounce.
    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ ...patch, page: 1 }));
  });

  it('keeps an earlier filter in the request when the search text changes later', async () => {
    const { change } = await renderLoaded();

    await change({ status: 'InProgress' });
    await change({ search: 'invoice' });
    await advance(300);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', search: 'invoice' }),
    );
  });

  it('sends one search request after typing stops, not one per keystroke', async () => {
    const { change } = await renderLoaded();

    for (const text of ['i', 'in', 'inv']) {
      await change({ search: text });
      await advance(100);
    }

    expect(getTickets).toHaveBeenCalledTimes(1);

    await advance(300);

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'inv' }));
  });
});