export type AuthUser = {
  userId: string;
  sessionId: string;
  roles: string[];
  authenticationMethod: string;
  authenticatedAtUtc: string;
  sessionExpiresAtUtc: string;
};

export type AuthSession = {
  tokenType: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: AuthUser;
};

export type ClientContext = {
  clientInstanceId: string;
  platform: string;
  appVersion?: string;
};

export type AuthCredentials = {
  email: string;
  password: string;
  idempotencyKey?: string;
};

export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  code?: string;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  retryAfterSeconds?: number;
};
