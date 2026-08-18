import { Platform } from 'react-native';
import Constants from 'expo-constants';

import { getApiBaseUrl } from '@/config/environment';
import type { AuthSession, ClientContext, ProblemDetails } from '@/features/auth/authTypes';
import { getClientInstanceId } from '@/security/sessionStore';

type SessionAccess = {
  getSession: () => AuthSession | null;
  setSession: (session: AuthSession | null) => Promise<void>;
};

type RequestOptions = RequestInit & { authenticated?: boolean };

export class ApiProblem extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly problem?: ProblemDetails,
  ) {
    super(message);
    this.name = 'ApiProblem';
  }
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const contentType = response.headers.get('content-type') || '';
  if (!contentType.includes('json')) return undefined;
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return undefined;
  }
}

export async function createClientContext(): Promise<ClientContext> {
  return {
    clientInstanceId: await getClientInstanceId(),
    platform: Platform.OS,
    appVersion: Constants.expoConfig?.version,
  };
}

export function createApiClient(sessionAccess: SessionAccess) {
  const request = async <T>(
    path: string,
    options: RequestOptions = {},
    allowRefresh = true,
    sessionOverride?: AuthSession,
  ): Promise<T> => {
    const { authenticated = true, ...requestInit } = options;
    const session = sessionOverride || sessionAccess.getSession();
    const headers = new Headers(requestInit.headers);
    headers.set('Accept', 'application/json');
    if (requestInit.body) headers.set('Content-Type', 'application/json');
    if (authenticated && session) {
      headers.set('Authorization', `${session.tokenType || 'Bearer'} ${session.accessToken}`);
    }

    const response = await fetch(`${getApiBaseUrl()}${path}`, { ...requestInit, headers });

    if (response.status === 401 && allowRefresh && session?.refreshToken) {
      const refreshed = await refresh(session.refreshToken);
      if (refreshed) {
        await sessionAccess.setSession(refreshed);
        return request<T>(path, options, false, refreshed);
      }
      await sessionAccess.setSession(null);
    }

    if (!response.ok) {
      const problem = await readProblem(response);
      throw new ApiProblem(problem?.detail || problem?.title || 'The request could not be completed.', response.status, problem);
    }

    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  };

  const refresh = async (refreshToken: string): Promise<AuthSession | null> => {
    const response = await fetch(`${getApiBaseUrl()}/auth/v1/refresh`, {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken, client: await createClientContext() }),
    });
    if (!response.ok) return null;
    return (await response.json()) as AuthSession;
  };

  return { request };
}
