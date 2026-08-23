import { Platform } from 'react-native';
import Constants from 'expo-constants';

import { getApiBaseUrl } from '@/config/environment';
import type { AuthSession, ClientContext, ProblemDetails } from '@/features/auth/authTypes';
import { getClientInstanceId } from '@/security/sessionStore';

type SessionAccess = {
  getSession: () => AuthSession | null;
  setSession: (session: AuthSession | null) => Promise<void>;
};

export type RequestOptions = RequestInit & {
  authenticated?: boolean;
  refreshOnUnauthorized?: boolean;
};

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

export function friendlyApiError(reason: unknown, fallback: string) {
  if (reason instanceof TypeError && /network request failed|failed to fetch|load failed/i.test(reason.message)) {
    return 'We could not connect. Check your internet connection and try again.';
  }

  if (!(reason instanceof ApiProblem)) return fallback;
  if ([401, 403].includes(reason.status)) return 'Your session has expired. Sign in again to continue.';
  if (reason.status === 404) return 'This information is not available yet. Try again shortly.';
  if (reason.status === 409) return 'This information changed. Refresh and try again.';
  if (reason.status === 429) return 'Too many requests were made. Wait a moment and try again.';
  if (reason.status >= 500) return 'The service is temporarily unavailable. Try again shortly.';
  return fallback;
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
  let refreshInFlight: Promise<AuthSession | null> | null = null;

  const request = async <T>(
    path: string,
    options: RequestOptions = {},
    allowRefresh = true,
    sessionOverride?: AuthSession,
  ): Promise<T> => {
    const { authenticated = true, refreshOnUnauthorized = true, ...requestInit } = options;
    const session = sessionOverride || sessionAccess.getSession();
    const headers = new Headers(requestInit.headers);
    headers.set('Accept', 'application/json');
    if (requestInit.body && !headers.has('Content-Type') && !(requestInit.body instanceof FormData)) {
      headers.set('Content-Type', 'application/json');
    }
    if (authenticated && session) {
      headers.set('Authorization', `${session.tokenType || 'Bearer'} ${session.accessToken}`);
    }

    const response = await fetch(`${getApiBaseUrl()}${path}`, { ...requestInit, headers });

    if (response.status === 401 && allowRefresh && refreshOnUnauthorized && session?.refreshToken) {
      refreshInFlight ??= refresh(session.refreshToken).finally(() => {
        refreshInFlight = null;
      });
      const refreshed = await refreshInFlight;
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
    if ([400, 401, 403].includes(response.status)) return null;
    if (!response.ok) {
      const problem = await readProblem(response);
      throw new ApiProblem(problem?.detail || problem?.title || 'Session refresh is temporarily unavailable.', response.status, problem);
    }
    return (await response.json()) as AuthSession;
  };

  return { request };
}
