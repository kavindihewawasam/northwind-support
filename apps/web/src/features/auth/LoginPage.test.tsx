import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { AuthContext, type AuthState } from '../../auth/AuthContext';
import { LoginPage } from './LoginPage';

function renderLogin(login: AuthState['login']) {
  render(
    <AuthContext.Provider value={{ isAuthenticated: false, login, logout: vi.fn() }}>
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}

function fillAndSubmit(email: string, password: string) {
  fireEvent.change(screen.getByLabelText(/email/i), { target: { value: email } });
  fireEvent.change(screen.getByLabelText(/password/i), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: /sign in/i }));
}

describe('LoginPage', () => {
  it('asks for both fields and does not call the API when one is missing', async () => {
    const login = vi.fn();
    renderLogin(login);

    fillAndSubmit('', '');

    expect(await screen.findByText(/enter your email/i)).toBeInTheDocument();
    expect(screen.getByText(/enter your password/i)).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('signs in with the trimmed email and the password as typed', async () => {
    const login = vi.fn().mockResolvedValue(undefined);
    renderLogin(login);

    fillAndSubmit('  alex.turner@example.com  ', 'LocalDevOnly!123');

    await waitFor(() => expect(login).toHaveBeenCalledWith('alex.turner@example.com', 'LocalDevOnly!123'));
  });

  it('shows one clear message when the credentials are rejected', async () => {
    const login = vi.fn().mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
    renderLogin(login);

    fillAndSubmit('alex.turner@example.com', 'wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(screen.getByRole('button', { name: /sign in/i })).toBeEnabled();
  });
});