interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, pageSize, totalCount, totalPages, onPageChange }: PaginationProps) {
  const firstOnPage = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastOnPage = Math.min(page * pageSize, totalCount);

  return (
    <nav className="pagination" aria-label="Pagination">
      <p className="pagination__summary">
        {firstOnPage}-{lastOnPage} of {totalCount}
      </p>

      <div className="pagination__controls">
        <button
          type="button"
          className="button button--ghost"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
        >
          Previous
        </button>

        <span className="pagination__page">
          Page {page} of {Math.max(totalPages, 1)}
        </span>

        <button
          type="button"
          className="button button--ghost"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
        >
          Next
        </button>
      </div>
    </nav>
  );
}
