# Identity Access and Refresh Session Lifecycle

## Purpose

FIN-76 activates the complete version 1 session lifecycle for mobile and web clients:

```text
POST /auth/v1/refresh
POST /auth/v1/logout
GET  /auth/v1/me
```

Registration and sign-in now create server-side session records through the same lifecycle service. Identity Service remains authoritative for access issuance, refresh rotation, revocation, replay handling, and current session state.

## Recommended token model

The implementation separates access and refresh responsibilities:

- access token: short-lived signed JWT;
- refresh token: high-entropy opaque value;
- server storage: refresh hash and session metadata only;
- access validation: JWT middleware plus server-side session-state verification for protected Identity Service operations.

The default lifetime configuration is:

```text
access token: 15 minutes
refresh session: 30 days
```

Both values are configuration-driven.

## Access token

Access tokens are signed with HMAC-SHA256 and contain only identity/session claims required by the gateway and Identity Service:

```text
sub   account identifier
sid   session identifier
amr   authentication method
iat   issued-at timestamp
exp   expiry timestamp
role  trusted role assignment
```

Issuer, audience, signature, and lifetime are validated by ASP.NET Core JwtBearer middleware.

Access tokens are not persisted. They are never used as the source of truth for account or refresh-session state. Protected Identity Service endpoints additionally load the session record to reject revoked, expired, or missing sessions.

## Refresh token

Refresh values use this opaque format:

```text
rt1.{sessionId}.{randomSecret}
```

The random part is generated from 48 cryptographically secure random bytes and encoded with base64url.

The raw value is returned to the client once. Identity Service stores only a purpose-specific HMAC-SHA256 hash. The hash key belongs in a secret manager.

The embedded session identifier selects the candidate record. The full presented value must still match the stored hash using constant-time comparison.

## Session record

The application session model contains:

```text
session ID
account ID
token-family hash
refresh-value hash
client-instance hash
status
authentication method
issued timestamp
access expiry
refresh expiry
rotation timestamp
revocation timestamp
replacement session ID
```

Session states are:

```text
Active
Rotated
Revoked
Expired
```

Raw access values, refresh values, email addresses, passwords, and client instance identifiers are not stored in the record.

## Initial issuance

Registration and sign-in follow this synchronous flow:

1. Validate the credential operation.
2. Load or create the authoritative account.
3. Generate a new session ID and token-family identifier.
4. Generate the opaque refresh value.
5. Store only its hash with the session metadata.
6. Create the signed access token.
7. Return both values to the client.

The current FIN-75 compatibility interface remains as a scoped wrapper around the asynchronous lifecycle service. This avoids changing the public API while keeping all new sessions in the authoritative session store. A later internal refactor can remove the synchronous wrapper without changing contracts.

## Refresh rotation

Refresh is an atomic server-side operation:

1. Validate request and client context.
2. Parse the candidate session ID from the opaque value.
3. Load the current session and account.
4. Reject unavailable accounts.
5. Generate a replacement session and refresh value in the same token family.
6. Compare the presented value hash with the stored hash.
7. Mark the old session `Rotated` and record `ReplacedBySessionId`.
8. Store the new active session.
9. Return a new access token and refresh value.

Clients must atomically replace both values after a successful refresh.

## Replay and reuse detection

A correctly matching refresh value presented after its session has already been rotated is treated as replay.

Identity Service then:

1. revokes every session in the token family;
2. publishes `token.revoked.v1` with reason `refresh_reuse` through the event abstraction;
3. returns `session_revoked`;
4. rejects the replacement refresh value because its family is now revoked.

A malformed or non-matching value returns `session_invalid` and does not reveal whether the candidate session exists.

## Logout

Logout requires:

- a valid bearer access token;
- the current refresh value in the request body;
- matching access-token and refresh-token session IDs.

Successful logout marks the session revoked and publishes:

```text
token.revoked.v1
reason: logout
```

After logout:

- the refresh value cannot rotate the session;
- `/auth/v1/me` rejects the still-cryptographically-valid access JWT because server-side session state is revoked.

## Current identity context

`GET /auth/v1/me` requires JwtBearer authentication and then validates:

- account exists and can authenticate;
- session exists;
- session belongs to the account in the JWT;
- session is not revoked or expired;
- refresh-session lifetime has not elapsed.

The response contains identity/session context only. Profile data and financial data belong to their own services.

## Authentication challenge contract

Protected session endpoints preserve the Identity API error contract even when authorization middleware rejects the request before the endpoint handler runs.

Missing or invalid bearer credentials return:

```text
HTTP 401
Content-Type: application/problem+json
WWW-Authenticate: Bearer
code: session_invalid
```

The response is the versioned `IdentityApiErrorResponse` and includes the supplied correlation ID when one is present. It never includes token details, validation exceptions, or credential data.

## Events

FIN-76 publishes `token.revoked.v1` through `IIdentityEventPublisher` after authoritative in-memory state changes.

Event data contains only:

```text
userId
sessionId
reason
```

Durable RabbitMQ publication, delivery guarantees, and final event-envelope integration remain FIN-77.

## Configuration

```text
Identity:Authentication:RuntimeAdapter
Identity:Authentication:RefreshTokenHashKey
Identity:Authentication:AccessTokenSigningKey
Identity:Authentication:AccessTokenIssuer
Identity:Authentication:AccessTokenAudience
Identity:Authentication:AccessTokenLifetimeMinutes
Identity:Authentication:RefreshTokenLifetimeDays
```

For the `InMemoryDevelopment` adapter, missing key values generate process-local ephemeral keys. Hosted environments must provide stable keys through environment variables or a secret manager. Keys must never be committed to source control.

## Persistence boundary

The application depends on `IIdentitySessionStore`. FIN-76 includes an atomic in-memory adapter for local development and CI.

Known limitations of this adapter:

- state is lost on process restart;
- state is not shared between replicas;
- session records are not yet stored in the FIN-85 Elasticsearch session index;
- no distributed atomicity is provided.

A production Elasticsearch adapter must preserve the same rotation and family-revocation semantics, use optimistic concurrency, and remain owned exclusively by Identity Service.

## Synchronous and asynchronous flows

Synchronous REST flows:

```text
client -> gateway -> Identity API -> application service -> session store -> response
```

Asynchronous integration flow:

```text
Identity authoritative state change -> event abstraction -> RabbitMQ implementation in FIN-77
```

RabbitMQ never replaces the session store as the source of truth.

## Verification

Automated tests cover:

- JWT-shaped access values;
- successful `/me` with a current session;
- refresh rotation;
- old refresh reuse detection;
- token-family revocation;
- logout revocation;
- rejection of `/me` after logout;
- rejection of refresh after logout;
- hash-only persisted refresh values;
- expiry rejection in the atomic store;
- `token.revoked.v1` publication for logout and replay;
- structured identity problem responses for missing and invalid bearer credentials;
- generated OpenAPI continuity.
