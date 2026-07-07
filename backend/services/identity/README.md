# Financial Assistant Identity Service

.NET 8 Identity Service for FIN-16.

Canonical engineering documentation:

```text
docs/engineering/identity-service-baseline.md
docs/engineering/identity-data-model-and-storage.md
docs/engineering/identity-api-contracts.md
docs/engineering/identity-email-registration-login.md
docs/engineering/identity-session-lifecycle.md
docs/engineering/identity-event-publishing.md
```

## Responsibility

Identity Service owns account authentication, session lifecycle, provider links, access-token issuance, refresh rotation, revocation, and identity lifecycle event intents. Profile and financial data remain owned by their dedicated services.

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
- Logout publishes `token.revoked.v1` through the event publishing boundary.
- `GET /auth/v1/me` rejects sessions that are revoked, expired, missing, or associated with an unavailable account.

## Integration events

Identity Service currently produces:

```text
user.registered.v1
user.signed_in.v1
authentication.failed.v1
token.revoked.v1
```

All events use the shared versioned envelope with event ID, event type, occurrence and publication timestamps, producer, schema version, correlation ID, causation ID, purpose-specific user hash when applicable, and a minimal payload.

Application code enqueues event intents through `IIdentityEventPublisher`. `IdentityOutboxDispatcher` publishes pending messages through `IIdentityEventTransport`. RabbitMQ mode uses the durable `fa.events` topic exchange, persistent mandatory messages, and publisher confirmations.

The active outbox is development-only and process-local. It retries transport failures but is not crash durable and is not shared between replicas. Production Elasticsearch outbox persistence must close the state-to-event-intent gap explicitly.

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
IIdentityEventOutbox
IIdentityEventTransport
```

The current `InMemoryDevelopment` adapters support local execution and CI. They are not production persistence: state is lost on restart and is not shared between replicas. Production Elasticsearch adapters must preserve the same duplicate, rotation, replay, revocation, and outbox semantics.

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
Identity:Events:ConnectionString
Identity:Events:UserIdHmacKey
Identity:Events:BatchSize
Identity:Events:DispatchIntervalMilliseconds
Identity:Events:MaximumRetryDelaySeconds
```

Keys, RabbitMQ credentials, and provider credentials belong in environment variables or a secret manager. Development uses process-local keys when configured values are absent.

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

Automated tests cover registration, sign-in, refresh rotation, replay-family revocation, expiry, logout, current-user context, hash-only refresh storage, shared event envelopes, safe lifecycle events, outbox retry, OpenAPI, and architecture boundaries.

## Boundaries

- Deterministic server logic is authoritative.
- Public contracts expose no storage implementation fields.
- Other services never read Identity Service indices directly.
- RabbitMQ is an integration transport, not the identity source of truth.
- LLM and OCR are outside the authentication trust boundary.
