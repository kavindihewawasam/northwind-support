import { createContext } from 'react';
import type { AuthUser } from '../types/api';

export interface AuthState {
  user?: AuthUser;
  isAuthenticated: boolean;
  /** Signs in and keeps the session. Rejects with an ApiError when the credentials are wrong. */
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthState | undefined>(undefined);