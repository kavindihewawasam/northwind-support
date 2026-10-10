import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SlaIndicator } from './SlaIndicator';
import { slaPresentation, slaStatuses } from './slaPresentation';

describe('SlaIndicator', () => {
  it('renders every SLA state with its own text', () => {
    render(
      <>
        {slaStatuses.map((status) => (
          <SlaIndicator key={status} status={status} />
        ))}
      </>,
    );

    for (const status of slaStatuses) {
      expect(screen.getByText(slaPresentation[status].label)).toBeInTheDocument();
    }
  });

  it('tells the states apart by word and by symbol, not by colour alone', () => {
    const labels = slaStatuses.map((status) => slaPresentation[status].label);
    const symbols = slaStatuses.map((status) => slaPresentation[status].symbol);

    expect(new Set(labels).size).toBe(slaStatuses.length);
    expect(new Set(symbols).size).toBe(slaStatuses.length);
  });
});