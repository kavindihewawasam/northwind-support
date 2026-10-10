import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './useAuth';

/** Sends visitors who are not signed in to the login page, remembering where they wanted to go. */
export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />;
  }

  return <>{children}</>;
}