import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthState } from './AuthContext';
import { RequireAuth } from './RequireAuth';

function authState(isAuthenticated: boolean): AuthState {
  return {
    isAuthenticated,
    user: isAuthenticated
      ? { id: 1, fullName: 'Alex Turner', email: 'alex.turner@example.com' }
      : undefined,
    login: vi.fn(),
    logout: vi.fn(),
  };
}

/** Stands in for the login page and shows where the visitor wanted to go. */
function LoginProbe() {
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;

  return <p>Login page, wanted {from}</p>;
}

function renderAt(path: string, isAuthenticated: boolean) {
  render(
    <AuthContext.Provider value={authState(isAuthenticated)}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/login" element={<LoginProbe />} />
          <Route
            path="/tickets/:id"
            element={
              <RequireAuth>
                <p>Ticket page</p>
              </RequireAuth>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}

describe('RequireAuth', () => {
  it('sends a visitor who is not signed in to the login page and remembers the page they wanted', () => {
    renderAt('/tickets/12?tab=history', false);

    expect(screen.queryByText('Ticket page')).not.toBeInTheDocument();
    expect(screen.getByText('Login page, wanted /tickets/12?tab=history')).toBeInTheDocument();
  });

  it('shows the page to a signed-in agent', () => {
    renderAt('/tickets/12', true);

    expect(screen.getByText('Ticket page')).toBeInTheDocument();
  });
});