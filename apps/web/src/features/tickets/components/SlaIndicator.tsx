import { Badge } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';
import { slaPresentation } from './slaPresentation';

/** Where a ticket stands against its SLA: a word and a symbol, with colour as a bonus. */
export function SlaIndicator({ status }: { status: SlaStatus }) {
  const { label, symbol, tone } = slaPresentation[status];

  return (
    <Badge tone={tone}>
      <span aria-hidden="true">{symbol} </span>
      {label}
    </Badge>
  );
}