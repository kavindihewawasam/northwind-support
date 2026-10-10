import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail, TicketPriority } from '../../../types/api';

// The same limits the API enforces, so a request that would be rejected is never sent.
const REASON_MIN = 5;
const REASON_MAX = 500;

const priorityOrder: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalationResult) => void;
}

/**
 * Escalates a ticket one priority level. Shows why it cannot be escalated instead of the form
 * when that is already known, and still reports it if the server rejects an escalation.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [reasonError, setReasonError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  const blocked = blockedMessage(ticket);

  if (blocked) {
    return (
      <div className="card">
        <h2>Escalate</h2>
        <p>{blocked}</p>
      </div>
    );
  }

  const nextPriority = priorityOrder[priorityOrder.indexOf(ticket.priority) + 1];

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationError = validate(reason);
    setReasonError(validationError);
    setSubmitError(undefined);

    if (validationError) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalateTicket(ticket.id, { reason: reason.trim() });

      setReason('');
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError && caught.fieldErrors.Reason?.[0]) {
        setReasonError(caught.fieldErrors.Reason[0]);
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} noValidate>
      <h2>Escalate</h2>

      <Field
        id="escalation-reason"
        label="Reason"
        error={reasonError}
        hint={`${REASON_MIN} to ${REASON_MAX} characters (${reason.trim().length}/${REASON_MAX}). Recorded under your name.`}
      >
        {(fieldProps) => (
          <textarea
            {...fieldProps}
            rows={3}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
        )}
      </Field>

      {submitError && (
        <p className="field__error" role="alert">
          {submitError}
        </p>
      )}

      <div className="button-row">
        <button type="submit" className="button button--primary" disabled={isSaving}>
          {isSaving ? 'Escalating...' : `Escalate to ${nextPriority}`}
        </button>
      </div>
    </form>
  );
}

/** Why this ticket cannot be escalated, when that is already clear from the ticket itself. */
function blockedMessage(ticket: TicketDetail): string | undefined {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return 'Resolved or closed tickets cannot be escalated.';
  }

  if (ticket.priority === 'Critical') {
    return 'Critical tickets cannot be escalated.';
  }

  return undefined;
}

function validate(reason: string): string | undefined {
  const length = reason.trim().length;

  if (length < REASON_MIN) {
    return `Give a reason of at least ${REASON_MIN} characters.`;
  }

  if (length > REASON_MAX) {
    return `Keep the reason to ${REASON_MAX} characters or fewer.`;
  }

  return undefined;
}