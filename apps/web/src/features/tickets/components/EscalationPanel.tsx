import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail, TicketPriority } from '../../../types/api';

// The same limits the API enforces, so a request that would be rejected is never sent.
const REASON_MIN = 5;
const REASON_MAX = 500;
const ACTOR_MAX = 100;

const priorityOrder: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalationResult) => void;
}

type EscalationErrors = Partial<Record<'reason' | 'escalatedBy', string>>;

/**
 * Escalates a ticket one priority level. Shows why it cannot be escalated instead of the form
 * when that is already known, and still reports it if the server rejects an escalation.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('');
  const [errors, setErrors] = useState<EscalationErrors>({});
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

    const validationErrors = validate(reason, escalatedBy);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalateTicket(ticket.id, {
        reason: reason.trim(),
        escalatedBy: escalatedBy.trim(),
      });

      setReason('');
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError && Object.keys(caught.fieldErrors).length > 0) {
        setErrors(fromApiErrors(caught.fieldErrors));
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
        id="escalated-by"
        label="Escalated by"
        error={errors.escalatedBy}
        hint="Your name, until sign-in replaces this."
      >
        {(fieldProps) => (
          <input
            {...fieldProps}
            type="text"
            value={escalatedBy}
            onChange={(event) => setEscalatedBy(event.target.value)}
          />
        )}
      </Field>

      <Field
        id="escalation-reason"
        label="Reason"
        error={errors.reason}
        hint={`${REASON_MIN} to ${REASON_MAX} characters (${reason.trim().length}/${REASON_MAX}).`}
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

function validate(reason: string, escalatedBy: string): EscalationErrors {
  const errors: EscalationErrors = {};
  const trimmedReason = reason.trim();
  const trimmedActor = escalatedBy.trim();

  if (trimmedReason.length < REASON_MIN) {
    errors.reason = `Give a reason of at least ${REASON_MIN} characters.`;
  } else if (trimmedReason.length > REASON_MAX) {
    errors.reason = `Keep the reason to ${REASON_MAX} characters or fewer.`;
  }

  if (trimmedActor.length === 0) {
    errors.escalatedBy = 'Enter who is escalating this ticket.';
  } else if (trimmedActor.length > ACTOR_MAX) {
    errors.escalatedBy = `Keep the name to ${ACTOR_MAX} characters or fewer.`;
  }

  return errors;
}

/** The API names fields in PascalCase; the form uses camelCase. */
function fromApiErrors(fieldErrors: Record<string, string[]>): EscalationErrors {
  const errors: EscalationErrors = {};

  for (const [field, messages] of Object.entries(fieldErrors)) {
    const key = field.charAt(0).toLowerCase() + field.slice(1);

    if (key === 'reason' || key === 'escalatedBy') {
      errors[key] = messages[0];
    }
  }

  return errors;
}