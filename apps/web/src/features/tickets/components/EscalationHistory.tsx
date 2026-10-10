import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  escalations: TicketEscalation[];
}

const agentName = (agent: AgentSummary | null) => agent?.fullName ?? 'Unassigned';

/** A ticket's escalations, newest first, with what changed, why, who and when. */
export function EscalationHistory({ escalations }: EscalationHistoryProps) {
  if (escalations.length === 0) {
    return <p>This ticket has not been escalated.</p>;
  }

  return (
    <ol className="history">
      {escalations.map((escalation) => (
        <li key={escalation.id} className="history__item">
          <p>
            <strong>
              {escalation.fromPriority} to {escalation.toPriority}
            </strong>
          </p>
          <p className="summary__note">
            Owner: {agentName(escalation.fromAgent)} to {agentName(escalation.toAgent)}
          </p>
          <p className="summary__note">
            Due: {formatDateTime(escalation.fromDueAtUtc)} to {formatDateTime(escalation.toDueAtUtc)}
          </p>
          <p>{escalation.reason}</p>
          <p className="summary__note">
            {escalation.escalatedBy}, {formatDateTime(escalation.escalatedAtUtc)}
          </p>
        </li>
      ))}
    </ol>
  );
}