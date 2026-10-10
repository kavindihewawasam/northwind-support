import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession, saveSession, type Session } from '../auth/session';
import { ApiError, request, setUnauthorizedHandler } from './client';

const session: Session = {
  token: 'abc.def.ghi',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@example.com' },
};

function reply(status: number, body: unknown = {}) {
  return vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  );
}

describe('request', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    setUnauthorizedHandler(undefined);
  });

  it('attaches the signed-in agent\'s token to the call', async () => {
    saveSession(session);
    const fetchMock = reply(200, { ok: true });
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/tickets'),
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer abc.def.ghi' }),
      }),
    );
  });

  it('sends no Authorization header when nobody is signed in', async () => {
    const fetchMock = reply(200, { ok: true });
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    const headers = fetchMock.mock.calls[0][1].headers as Record<string, string>;
    expect(headers.Authorization).toBeUndefined();
  });

  it('ends the session and tells the app when any call comes back 401', async () => {
    saveSession(session);
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    vi.stubGlobal('fetch', reply(401, { title: 'Unauthorized' }));

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(onUnauthorized).toHaveBeenCalledTimes(1);
    expect(getSession()).toBeUndefined();
  });

  it('does not treat a rejected login as an expired session', async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    vi.stubGlobal('fetch', reply(401, { detail: 'Invalid email or password.' }));

    await expect(request('/auth/login', { method: 'POST', body: '{}' })).rejects.toMatchObject({
      status: 401,
      message: 'Invalid email or password.',
    });

    expect(onUnauthorized).not.toHaveBeenCalled();
  });
});

describe('session storage', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it('returns a saved session and forgets it after logout', () => {
    saveSession(session);
    expect(getSession()?.user.fullName).toBe('Alex Turner');

    clearSession();
    expect(getSession()).toBeUndefined();
  });

  it('ignores and removes a session that has expired', () => {
    saveSession({ ...session, expiresAtUtc: new Date(Date.now() - 1000).toISOString() });

    expect(getSession()).toBeUndefined();
    expect(window.sessionStorage.length).toBe(0);
  });
});