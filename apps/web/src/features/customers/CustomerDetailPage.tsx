import { useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { customersApi } from '../../api/customers';
import { EmptyState } from '../../components/EmptyState';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useAsyncData } from '../../hooks/useAsyncData';
import { formatDate } from '../../lib/format';
import { TicketTable } from '../tickets/components/TicketTable';

export function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const customerId = Number(id);

  const load = useCallback(() => customersApi.getCustomer(customerId), [customerId]);
  const { data: customer, isLoading, error, reload } = useAsyncData(load, 'Unable to load this customer.');

  if (isLoading && !customer) {
    return <Spinner label="Loading customer" />;
  }

  if (error && !customer) {
    return <ErrorMessage message={error} onRetry={() => void reload()} />;
  }

  if (!customer) {
    return null;
  }

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/customers">Customers</Link> / {customer.name}
          </p>
          <h1>{customer.name}</h1>
        </div>
      </header>

      <div className="card">
        <dl className="summary">
          <div>
            <dt>Tier</dt>
            <dd>{customer.tier}</dd>
          </div>
          <div>
            <dt>Email</dt>
            <dd>{customer.email}</dd>
          </div>
          <div>
            <dt>Phone</dt>
            <dd>{customer.phone ?? '-'}</dd>
          </div>
          <div>
            <dt>Customer since</dt>
            <dd>{formatDate(customer.createdAtUtc)}</dd>
          </div>
        </dl>
      </div>

      <h2>Tickets</h2>

      {customer.tickets.length === 0 ? (
        <EmptyState title="No tickets for this customer yet" />
      ) : (
        <div className="table-wrapper">
          <TicketTable tickets={customer.tickets} />
        </div>
      )}
    </section>
  );
}
