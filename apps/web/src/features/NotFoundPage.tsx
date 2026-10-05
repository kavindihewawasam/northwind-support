import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <section className="empty">
      <h1>Page not found</h1>
      <p className="empty__description">
        That page does not exist. <Link to="/tickets">Back to the ticket list</Link>.
      </p>
    </section>
  );
}
