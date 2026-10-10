import type { AuthUser } from '../types/api';

const STORAGE_KEY = 'northwind.session';

export interface Session {
  token: string;
  expiresAtUtc: string;
  user: AuthUser;
}

function isSession(value: unknown): value is Session {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<Session>;

  return (
    typeof candidate.token === 'string' &&
    typeof candidate.expiresAtUtc === 'string' &&
    typeof candidate.user?.fullName === 'string'
  );
}

/**
 * The saved session, or undefined when there is none or it has expired. It lives in
 * sessionStorage: it survives a reload and is gone when the tab closes. The trade-off is that
 * a script injected into the page could read it; an httpOnly cookie would avoid that, at the
 * cost of handling CSRF, which is more than this app needs.
 */
export function getSession(): Session | undefined {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);

    if (!raw) {
      return undefined;
    }

    const parsed: unknown = JSON.parse(raw);

    if (!isSession(parsed) || new Date(parsed.expiresAtUtc).getTime() <= Date.now()) {
      clearSession();
      return undefined;
    }

    return parsed;
  } catch {
    // Storage can be unavailable or hold something unreadable; treat both as "signed out".
    return undefined;
  }
}

export function saveSession(session: Session): void {
  window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession(): void {
  try {
    window.sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Nothing to clear if storage is unavailable.
  }
}