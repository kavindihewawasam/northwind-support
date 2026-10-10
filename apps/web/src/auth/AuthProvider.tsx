import { useCallback, useEffect, useMemo, useState } from 'react';
import { authApi } from '../api/auth';
import { setUnauthorizedHandler } from '../api/client';
import { AuthContext, type AuthState } from './AuthContext';
import { clearSession, getSession, saveSession, type Session } from './session';

/** Owns the signed-in session. The protected routes watch it and send visitors to the login page. */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session | undefined>(getSession);

  useEffect(() => {
    // Any 401 (for example an expired token) ends the session.
    setUnauthorizedHandler(() => setSession(undefined));

    return () => setUnauthorizedHandler(undefined);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const response = await authApi.login({ email, password });

    const next: Session = {
      token: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      user: response.user,
    };

    saveSession(next);
    setSession(next);
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setSession(undefined);
  }, []);

  const value = useMemo<AuthState>(
    () => ({ user: session?.user, isAuthenticated: session !== undefined, login, logout }),
    [session, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}