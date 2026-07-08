# Gateway Access Control Middleware

## Purpose

FIN-73 protects private Public API Gateway route groups before requests reach internal services.

The gateway validates Identity Service access tokens, applies route access policy, and forwards a small trusted user context. Identity Service remains the source of truth for accounts, credentials, sessions, token issuance, refresh, logout, and revocation.

## Request flow

```text
mobile / web client
-> Public API Gateway
-> exact public endpoint allowlist
-> JWT validation for private routes
-> admin role check where required
-> trusted gateway user context
-> internal owning service
```

The gateway does not perform account lookup, credential validation, session refresh, financial calculations, or domain authorization.

## Security modes

### Placeholder

`Gateway:Security:Mode=placeholder`

This mode preserves local incremental development while internal destinations remain disabled. It does not enforce access tokens and must not be used for an internet-facing environment.

### Enforce

`Gateway:Security:Mode=enforce`

Enforce mode requires:

```text
Gateway__Security__AccessTokenSigningKey
Gateway__Security__AccessTokenIssuer
Gateway__Security__AccessTokenAudience
```

The signing key must contain at least 32 UTF-8 bytes. Gateway startup fails when enforce-mode configuration is missing or structurally invalid.

The signing key, issuer, and audience must match Identity Service authentication configuration:

```text
Identity__Authentication__AccessTokenSigningKey
Identity__Authentication__AccessTokenIssuer
Identity__Authentication__AccessTokenAudience
```

Secrets must be supplied through the deployment secret store. They must not be committed to repository configuration, logs, tickets, or client applications.

## Public endpoint allowlist

The `/auth` route group is deny-by-default and has the `authenticated` route policy.

Only exact method-and-path pairs configured in `Gateway:Security:PublicEndpoints` bypass access-token validation:

| Method | Path | Reason |
| --- | --- | --- |
| POST | `/auth/v1/register` | Create account and initial session |
| POST | `/auth/v1/sign-in` | Email/password authentication |
| POST | `/auth/v1/refresh` | Refresh token is validated by Identity Service |
| POST | `/auth/v1/providers/google/sign-in` | Provider authentication entry point |
| POST | `/auth/v1/providers/apple/sign-in` | Provider authentication entry point |
| POST | `/auth/v1/providers/phone/verifications` | Start phone verification |
| POST | `/auth/v1/providers/phone/verifications/confirm` | Confirm phone verification |

Matching is exact after removal of a trailing slash. Query strings do not participate in the match. A different HTTP method or a different path requires a valid access token.

`POST /auth/v1/logout` and `GET /auth/v1/me` are not public and require a bearer access token.

## Access token validation

The gateway validates the same HS256 JWT contract issued by Identity Service.

Required validation:

* signature uses HS256 and the configured symmetric key;
* issuer matches `AccessTokenIssuer`;
* audience matches `AccessTokenAudience`;
* token has not expired, with bounded configurable clock skew;
* token contains safe non-empty `sub` and `sid` claims;
* optional `role` claims contain only bounded role identifiers.

Expected claims:

```text
sub   opaque user identifier
sid   opaque session identifier
role  repeated role claim such as user or admin
amr   authentication method; retained in the token but not forwarded as trusted context
```

The gateway does not query Elasticsearch or call an LLM to decide whether a token is valid.

## Route policies

### Public

Public route policy bypasses access-token validation. Broad public catch-all routes should not be configured. Exact `PublicEndpoints` rules are preferred.

### Authenticated

A valid bearer token with safe `sub` and `sid` claims is required.

### Admin

A valid bearer token is required and at least one normalized `role` claim must match the configured admin role, default `admin`.

A client-provided `X-Gateway-Admin-Scope` header has no authority and is removed before downstream dispatch.

## Trusted user context

After successful validation, the gateway stores the validated context in `HttpContext.Items` and overwrites these internal request headers:

```text
X-Gateway-User-Id
X-Gateway-Session-Id
X-Gateway-Roles
```

Only opaque user ID, opaque session ID, and normalized roles are forwarded.

The gateway does not forward email, phone number, display name, password, refresh token, provider token, receipt data, transaction input, financial score, recommendation data, or LLM content as identity context.

Client attempts to supply the trusted gateway headers are ignored. `X-Gateway-Route-Key` is also overwritten from the validated route configuration.

The original bearer token continues to the owning internal service so the service can independently validate it where required. Trusted gateway context is an optimization and routing boundary, not a replacement for service-specific domain authorization.

## Error responses

Access-control failures use `application/problem+json` and `Cache-Control: no-store`.

| Status | Code | Meaning |
| --- | --- | --- |
| 401 | `authentication_required` | Bearer token is missing |
| 401 | `session_invalid` | Token is malformed, incorrectly signed, has wrong issuer/audience, or lacks required claims |
| 401 | `session_expired` | Token lifetime has expired |
| 403 | `forbidden` | Valid session lacks the required admin role |

401 responses include:

```text
WWW-Authenticate: Bearer
```

Public errors contain the correlation identifier but never contain the token, signature details, claims, internal destination, signing key, exception message, or stack trace.

## Logging policy

Safe access-control logs may contain:

```text
CorrelationId
TraceId
RouteKey
AccessPolicy
AuthenticationResult
HTTP status
```

They must not contain:

* Authorization header or token;
* user ID or session ID unless a future approved hashed diagnostic convention is introduced;
* email, phone number, password, OTP, provider token, or refresh token;
* receipt text, OCR content, raw notes, transaction input, or financial values;
* JWT signature or signing-key details.

## Responsibility boundaries

### Gateway owns

* exact public allowlist;
* access-token cryptographic validation;
* route-level public/authenticated/admin enforcement;
* safe technical 401/403 responses;
* trusted user-context header creation;
* removal of spoofed gateway identity headers.

### Identity Service owns

* account and credential state;
* token issuance and refresh;
* session revocation and logout;
* authoritative role assignment;
* provider authentication;
* Identity-owned Elasticsearch documents.

### Domain services own

* resource ownership checks;
* transaction/category/receipt access decisions;
* financial business rules and calculations;
* domain events and read-model authorization.

## Verification

Automated tests cover:

* exact public allowlist method/path matching;
* missing token rejection;
* valid HS256 token validation;
* invalid signature rejection;
* expired token rejection;
* required `sub` and `sid` context;
* admin role rejection and success;
* enforce-mode startup validation;
* overwriting spoofed user/session/role headers;
* removal of legacy admin-scope headers;
* forwarding only validated opaque user context.

Run:

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```
