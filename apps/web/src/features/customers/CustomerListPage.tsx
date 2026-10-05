import { Link } from 'react-router-dom';
import { customersApi } from '../../api/customers';
import { EmptyState } from '../../components/EmptyState';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useAsyncData } from '../../hooks/useAsyncData';

const loadCustomers = () => customersApi.getCustomers();

export function CustomerListPage() {
  const { data, isLoading, error, reload } = useAsyncData(loadCustomers, 'Unable to load customers.');

  return (
    <section>
      <header className="page-header">
        <div>
          <h1>Customers</h1>
          <p className="page-header__subtitle">Who we support, and how much is open for them.</p>
        </div>
      </header>

      {isLoading && !data && <Spinner label="Loading customers" />}

      {error && <ErrorMessage message={error} onRetry={() => void reload()} />}

      {data && data.length === 0 && <EmptyState title="No customers yet" />}

      {data && data.length > 0 && (
        <div className="table-wrapper">
          <table className="table">
            <caption className="visually-hidden">Customers</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Tier</th>
                <th scope="col">Email</th>
                <th scope="col">Phone</th>
                <th scope="col">Open tickets</th>
              </tr>
            </thead>
            <tbody>
              {data.map((customer) => (
                <tr key={customer.id}>
                  <td>
                    <Link to={`/customers/${customer.id}`}>{customer.name}</Link>
                  </td>
                  <td>{customer.tier}</td>
                  <td>{customer.email}</td>
                  <td>{customer.phone ?? '-'}</td>
                  <td>{customer.openTicketCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
