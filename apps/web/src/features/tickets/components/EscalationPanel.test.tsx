import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail, ticketEscalation } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
  },
}));

const escalateTicket = vi.mocked(ticketsApi.escalateTicket);

function fillAndSubmit(reason: string) {
  fireEvent.change(screen.getByLabelText(/reason/i), { target: { value: reason } });
  fireEvent.click(screen.getByRole('button', { name: /escalate to high/i }));
}

describe('EscalationPanel', () => {
  beforeEach(() => {
    escalateTicket.mockReset();
  });

  it('blocks a reason that is too short and does not call the API', async () => {
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    fillAndSubmit('abc');

    expect(await screen.findByText(/at least 5 characters/i)).toBeInTheDocument();
    expect(escalateTicket).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('sends only the trimmed reason (who escalates comes from the token) and reports the result', async () => {
    const onEscalated = vi.fn();
    const result = { ticket: ticketDetail({ priority: 'High' }), escalation: ticketEscalation() };
    escalateTicket.mockResolvedValue(result);
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    fillAndSubmit('  Customer is blocked  ');

    await waitFor(() => expect(onEscalated).toHaveBeenCalledWith(result));
    expect(escalateTicket).toHaveBeenCalledWith(12, { reason: 'Customer is blocked' });
  });

  it('shows a 409 from the server and leaves the ticket unchanged', async () => {
    const onEscalated = vi.fn();
    escalateTicket.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical; critical tickets cannot be escalated.'),
    );
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    fillAndSubmit('Customer is blocked');

    expect(await screen.findByRole('alert')).toHaveTextContent(/already Critical/);
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /escalate to high/i })).toBeEnabled();
  });

  it('offers no escalate button for a Critical ticket and says why', () => {
    render(<EscalationPanel ticket={ticketDetail({ priority: 'Critical' })} onEscalated={vi.fn()} />);

    expect(screen.queryByRole('button', { name: /escalate/i })).not.toBeInTheDocument();
    expect(screen.getByText(/critical tickets cannot be escalated/i)).toBeInTheDocument();
  });

  it.each(['Resolved', 'Closed'] as const)('offers no escalate button for a %s ticket', (status) => {
    render(<EscalationPanel ticket={ticketDetail({ status })} onEscalated={vi.fn()} />);

    expect(screen.queryByRole('button', { name: /escalate/i })).not.toBeInTheDocument();
    expect(screen.getByText(/resolved or closed tickets cannot be escalated/i)).toBeInTheDocument();
  });
});