# Financial Assistant Identity Service

.NET 8 Identity Service for FIN-16.

Canonical engineering documentation:

```text
docs/engineering/identity-service-baseline.md
docs/engineering/identity-data-model-and-storage.md
docs/engineering/identity-api-contracts.md
docs/engineering/identity-email-registration-login.md
docs/engineering/identity-session-lifecycle.md
```

## Responsibility

Identity Service owns account authentication, session lifecycle, provider links, and identity lifecycle events. Profile and financial data remain owned by their dedicated services.

## Client API

```text
POST /auth/v1/register
POST /auth/v1/sign-in
POST /auth/v1/refresh
POST /auth/v1/logout
GET  /auth/v1/me
```

Registration and sign-in create an authoritative server-side session. Refresh rotates the session. Logout revokes it. Current-user context verifies both the signed access value and server-side session state.

## Session model

- Access values are short-lived signed JWTs. Default lifetime: 15 minutes.
- Refresh values are opaque, rotate on use, and default to 30 days.
- Only refresh hashes and session metadata are stored.
- Reuse of a rotated value revokes the entire token family.
- Logout publishes `token.revoked.v1` through the event abstraction.
- `GET /auth/v1/me` rejects sessions that are revoked, expired, missing, or associated with an unavailable account.

## Storage boundary

Application services depend on:

```text
IIdentityAccountStore
IIdentitySessionStore
IEmailLookupHasher
IPasswordCredentialHasher
IRefreshTokenService
IAccessTokenService
IIdentityEventPublisher
```

The current `InMemoryDevelopment` adapters support local execution and CI. They are not production persistence: state is lost on restart and is not shared between replicas. Production Elasticsearch adapters must preserve the same duplicate, rotation, replay, and revocation semantics.

## Configuration

```text
Identity:Authentication:RuntimeAdapter
Identity:Authentication:LookupHmacKey
Identity:Authentication:RefreshTokenHashKey
Identity:Authentication:AccessTokenSigningKey
Identity:Authentication:AccessTokenIssuer
Identity:Authentication:AccessTokenAudience
Identity:Authentication:AccessTokenLifetimeMinutes
Identity:Authentication:RefreshTokenLifetimeDays
Identity:Events:Mode
Identity:Events:Exchange
```

Keys and provider credentials belong in environment variables or a secret manager. Development uses process-local keys when configured values are absent.

## Runtime and verification

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet run --project backend/services/identity/FinancialAssistant.Identity.Api/FinancialAssistant.Identity.Api.csproj
```

OpenAPI is available in Development and Testing:

```text
/openapi/v1.json
```

Automated tests cover registration, sign-in, refresh rotation, replay-family revocation, expiry, logout, current-user context, hash-only refresh storage, OpenAPI, and architecture boundaries.

## Boundaries

- Deterministic server logic is authoritative.
- Public contracts expose no storage implementation fields.
- Other services never read Identity Service indices directly.
- RabbitMQ is an integration transport, not the identity source of truth.
- Durable RabbitMQ delivery remains FIN-77.
- LLM and OCR are outside the authentication trust boundary.
