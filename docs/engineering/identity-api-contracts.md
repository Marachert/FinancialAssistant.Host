# Identity API Contracts for Clients

## Purpose

This document defines the FIN-86 version 1 authentication contract used by React Native mobile clients, web clients, and the Public API Gateway.

The contract is intentionally defined before registration, login, and token logic are implemented. OpenAPI, DTOs, routes, status codes, and errors are stable enough for client API generation and mock-based UI work. The current server endpoints return HTTP 501 until FIN-75 and FIN-76 activate the deterministic use cases.

## Public boundary

The public versioned base path is:

```text
/auth/v1
```

The gateway already owns the `/auth` route group and forwards these paths to Identity Service. Identity Service remains authoritative for credentials and sessions. Gateway forwarding or pre-validation never replaces Identity Service validation.

All requests and responses use JSON unless a response has no body. Clients may send `X-Correlation-Id`; the gateway generates one when absent and returns the effective value.

## Endpoint catalog

| Method | Path | Authentication | Success | Purpose |
| --- | --- | --- | --- | --- |
| POST | `/auth/v1/register` | Public | 201 | Create account and initial session |
| POST | `/auth/v1/sign-in` | Public | 200 | Authenticate with email/password |
| POST | `/auth/v1/refresh` | Refresh token in body | 200 | Rotate refresh session and issue new tokens |
| POST | `/auth/v1/logout` | Bearer access token plus refresh token | 204 | Revoke current session |
| GET | `/auth/v1/me` | Bearer access token | 200 | Return current identity/session context |

`POST /auth/v1/register` accepts an optional `Idempotency-Key` header. Clients should reuse the same opaque key when retrying the same registration after a timeout. Keys must not contain email, device information, or other personal data.

## Shared client context

Registration, sign-in, refresh, and logout include:

```json
{
  "clientInstanceId": "opaque-installation-or-browser-id",
  "platform": "ios | android | web",
  "appVersion": "client-version"
}
```

Rules:

- `clientInstanceId` is an app-generated opaque identifier, not a hardware fingerprint;
- clients must not send device names, contacts, advertising IDs, IP addresses, or financial data;
- `platform` is a stable client category;
- `appVersion` is optional and supports compatibility diagnostics only.

## Register

### Request

```http
POST /auth/v1/register
Content-Type: application/json
Idempotency-Key: opaque-retry-key
```

```json
{
  "email": "person@example.com",
  "password": "client-supplied-secret",
  "client": {
    "clientInstanceId": "opaque-client-id",
    "platform": "android",
    "appVersion": "1.0.0"
  }
}
```

The client sends plaintext password only over TLS to the Identity Service boundary. Passwords are never logged, returned, or persisted in plaintext.

### Success

HTTP 201 with `AuthSessionResponse`.

Registration conflict uses the generic `identity_conflict` error. Public details must not disclose internal storage state.

## Sign in

### Request

```http
POST /auth/v1/sign-in
Content-Type: application/json
```

```json
{
  "email": "person@example.com",
  "password": "client-supplied-secret",
  "client": {
    "clientInstanceId": "opaque-client-id",
    "platform": "web",
    "appVersion": "1.0.0"
  }
}
```

### Success

HTTP 200 with `AuthSessionResponse`.

Invalid identifier, invalid password, unavailable account, and other credential failures return the same public `authentication_failed` code and HTTP 401. The API must not reveal whether an account exists.

## Refresh

### Request

```http
POST /auth/v1/refresh
Content-Type: application/json
```

```json
{
  "refreshToken": "opaque-refresh-token",
  "client": {
    "clientInstanceId": "opaque-client-id",
    "platform": "ios",
    "appVersion": "1.0.0"
  }
}
```

### Success

HTTP 200 with a new `AuthSessionResponse`.

A successful refresh rotates the refresh token. The client must atomically replace both access and refresh tokens and must never reuse the previous refresh token. Replay handling is defined by FIN-76.

## Logout

### Request

```http
POST /auth/v1/logout
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "refreshToken": "opaque-refresh-token",
  "client": {
    "clientInstanceId": "opaque-client-id",
    "platform": "web",
    "appVersion": "1.0.0"
  }
}
```

### Success

HTTP 204 with no body.

Logout revokes the current refresh session. The client deletes local access and refresh tokens whether the server returns 204 or indicates that the session is already invalid.

## Current identity context

```http
GET /auth/v1/me
Authorization: Bearer <access-token>
```

HTTP 200 response:

```json
{
  "userId": "opaque-user-id",
  "sessionId": "opaque-session-id",
  "roles": ["user"],
  "authenticationMethod": "email_password",
  "authenticatedAtUtc": "2026-01-01T12:00:00Z",
  "sessionExpiresAtUtc": "2026-01-31T12:00:00Z"
}
```

This response contains identity/session context only. Display name, preferences, financial settings, transactions, scores, and recommendations belong to other services.

## Session response

Registration, sign-in, and refresh return:

```json
{
  "tokenType": "Bearer",
  "accessToken": "opaque-access-token",
  "accessTokenExpiresAtUtc": "2026-01-01T12:15:00Z",
  "refreshToken": "opaque-refresh-token",
  "refreshTokenExpiresAtUtc": "2026-01-31T12:00:00Z",
  "user": {
    "userId": "opaque-user-id",
    "sessionId": "opaque-session-id",
    "roles": ["user"],
    "authenticationMethod": "email_password",
    "authenticatedAtUtc": "2026-01-01T12:00:00Z",
    "sessionExpiresAtUtc": "2026-01-31T12:00:00Z"
  }
}
```

Clients store tokens only in platform-appropriate secure storage. Tokens must not be written to analytics, crash reports, URLs, logs, clipboard, or general application state persistence.

## Error convention

Errors use a Problem Details-compatible JSON object with content type `application/problem+json`:

```json
{
  "type": "https://errors.financial-assistant.app/identity/authentication-failed",
  "title": "Authentication failed.",
  "status": 401,
  "code": "authentication_failed",
  "detail": "The supplied credentials could not be accepted.",
  "traceId": "safe-correlation-or-trace-id",
  "errors": null,
  "retryAfterSeconds": null
}
```

Stable error codes:

| Code | Typical status | Client behavior |
| --- | --- | --- |
| `validation_failed` | 400 | Show field errors from `errors` |
| `authentication_failed` | 401 | Show generic credentials message |
| `session_invalid` | 401 | Clear local session and re-authenticate |
| `session_expired` | 401 | Try refresh when appropriate, otherwise sign in |
| `session_revoked` | 401 | Clear local session and sign in |
| `identity_conflict` | 409 | Stop automatic retry; show safe conflict guidance |
| `rate_limited` | 429 | Wait for `retryAfterSeconds` or `Retry-After` |
| `service_unavailable` | 503 | Retry with bounded backoff |

`detail` is safe user-facing context, not an internal exception. `traceId` can be shown in support flows. Field errors never include passwords, tokens, hashes, or storage details.

## Compatibility rules

- `/auth/v1` contracts are backward compatible within v1;
- fields may be added only when clients can safely ignore them;
- existing field meaning and error-code meaning must not change;
- breaking route, required-field, or semantic changes require `/auth/v2`;
- storage documents and Elasticsearch fields never appear in public contracts;
- OpenAPI is the machine-readable source for generated clients;
- this document is the human-readable behavioral source.

## Current implementation state

FIN-86 publishes DTOs, routes, OpenAPI metadata, synthetic examples, and safety tests. Endpoints return HTTP 501 with `not_implemented` until their use cases are activated.

Implementation sequence:

1. FIN-75 activates register and sign-in.
2. FIN-76 activates refresh, logout, token validation, and current context.
3. FIN-77 publishes versioned identity lifecycle events after authoritative writes.
