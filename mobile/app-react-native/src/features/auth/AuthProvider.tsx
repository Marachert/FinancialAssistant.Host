import { createContext, useCallback, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import * as Crypto from 'expo-crypto';

import { ApiProblem, createApiClient, createClientContext } from '@/api/client';
import { clearSession, readSession, writeSession } from '@/security/sessionStore';

import type { AuthCredentials, AuthSession } from './authTypes';

type AuthState = 'loading' | 'anonymous' | 'authenticated';

type AuthContextValue = {
  state: AuthState;
  session: AuthSession | null;
  signIn: (credentials: AuthCredentials) => Promise<void>;
  register: (credentials: AuthCredentials) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [state, setState] = useState<AuthState>('loading');
  const [session, setSessionValue] = useState<AuthSession | null>(null);

  const setSession = useCallback(async (next: AuthSession | null) => {
    if (next) {
      await writeSession(next);
      setSessionValue(next);
      setState('authenticated');
      return;
    }

    try {
      await clearSession();
    } finally {
      setSessionValue(null);
      setState('anonymous');
    }
  }, []);

  const api = useMemo(
    () => createApiClient({ getSession: () => session, setSession }),
    [session, setSession],
  );

  useEffect(() => {
    let active = true;
    void readSession()
      .then(async (stored) => {
        if (!active) return;
        setSessionValue(stored);
        if (!stored) {
          setState('anonymous');
          return;
        }
        try {
          const restoreApi = createApiClient({ getSession: () => stored, setSession });
          await restoreApi.request('/auth/v1/me');
          if (active) setState('authenticated');
        } catch (reason) {
          if (!active) return;
          if (reason instanceof ApiProblem && [400, 401, 403].includes(reason.status)) await setSession(null);
          else setState('authenticated');
        }
      })
      .catch(() => {
        if (active) {
          setSessionValue(null);
          setState('anonymous');
        }
      });
    return () => {
      active = false;
    };
  }, [setSession]);

  const authenticate = useCallback(
    async (path: '/auth/v1/sign-in' | '/auth/v1/register', credentials: AuthCredentials) => {
      const { idempotencyKey, ...requestCredentials } = credentials;
      const response = await api.request<AuthSession>(path, {
        method: 'POST',
        authenticated: false,
        headers: path.endsWith('/register') ? { 'Idempotency-Key': idempotencyKey || Crypto.randomUUID() } : undefined,
        body: JSON.stringify({ ...requestCredentials, client: await createClientContext() }),
      });
      await setSession(response);
    },
    [api, setSession],
  );

  const signOut = useCallback(async () => {
    const current = session;
    try {
      if (current) {
        await api.request<void>('/auth/v1/logout', {
          method: 'POST',
          refreshOnUnauthorized: false,
          body: JSON.stringify({ refreshToken: current.refreshToken, client: await createClientContext() }),
        });
      }
    } finally {
      await setSession(null);
    }
  }, [api, session, setSession]);

  const value = useMemo<AuthContextValue>(
    () => ({
      state,
      session,
      signIn: (credentials) => authenticate('/auth/v1/sign-in', credentials),
      register: (credentials) => authenticate('/auth/v1/register', credentials),
      signOut,
    }),
    [authenticate, session, signOut, state],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider.');
  return context;
}
