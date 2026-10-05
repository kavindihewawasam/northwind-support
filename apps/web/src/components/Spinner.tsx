interface SpinnerProps {
  label?: string;
}

export function Spinner({ label = 'Loading' }: SpinnerProps) {
  return (
    <p className="spinner" role="status">
      <span className="spinner__dot" aria-hidden="true" />
      {label}...
    </p>
  );
}
