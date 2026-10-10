import type { BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

interface SlaPresentation {
  label: string;
  /** A shape as well as a colour, so the state is never conveyed by colour alone. */
  symbol: string;
  tone: BadgeTone;
}

/** The one place the SLA states are named, used by the indicator and by the filter bar. */
export const slaPresentation: Record<SlaStatus, SlaPresentation> = {
  AtRisk: { label: 'At risk', symbol: '▲', tone: 'warning' },
  Breached: { label: 'Breached', symbol: '✕', tone: 'danger' },
  WithinSla: { label: 'Within SLA', symbol: '●', tone: 'info' },
  Met: { label: 'Met', symbol: '✓', tone: 'success' },
  NotApplicable: { label: 'No SLA', symbol: '–', tone: 'neutral' },
};

/** In the order a person scanning for trouble wants them. */
export const slaStatuses: SlaStatus[] = ['AtRisk', 'Breached', 'WithinSla', 'Met', 'NotApplicable'];