# Financial Assistant Identity Service

.NET 8 Identity Service for FIN-16 and FIN-17.

Canonical engineering documentation:

```text
docs/engineering/identity-service-baseline.md
docs/engineering/identity-data-model-and-storage.md
docs/engineering/identity-api-contracts.md
docs/engineering/identity-email-registration-login.md
docs/engineering/identity-session-lifecycle.md
docs/engineering/identity-event-publishing.md
docs/engineering/google-sign-in-backend-validation.md
```

## Responsibility

Identity Service owns account authentication, session lifecycle, provider links, access-token issuance, refresh rotation, revocation, and identity lifecycle event intents. Profile and financial data remain owned by their dedicated services.

## Client API

```text
POST /auth/v1/register
POST /auth/v1/sign-in
POST /auth/v1/providers/google/sign-in
POST /auth/v1/refresh
POST /auth/v1/logout
GET  /auth/v1/me
```

Registration and sign-in create an authoritative server-side session. Refresh rotates the session. Logout revokes it. Current-user context verifies both the signed access value and server-side session state.

Google sign-in accepts a Google ID token, validates it server-side, maps the stable provider subject through an Identity-owned hashed provider link, and returns the same Financial Assistant session contract. The Google token is never used as a Financial Assistant API bearer token.

## Provider linking

- Google `sub` is the stable provider key; email is not a provider primary key.
- Provider subjects and hosted-domain identifiers are stored only as purpose-separated HMAC values.
- Provider links are stored separately from local email credentials.
- A verified Google email matching a local credential does not trigger automatic linking.
- Matching local accounts return `provider_link_required`; future linking must require an authenticated existing Identity session.
- ID tokens, raw provider subjects, email, names, and profile pictures are not persisted in provider-link records.

## Session model

- Access values are short-lived signed JWTs. Default lifetime: 15 minutes.
- Refresh values are opaque, rotate on use, and default to 30 days.
- Only refresh hashes and session metadata are stored.
- Reuse of a rotated value revokes the entire token family.
- Logout publishes `token.revoked.v1` through the event publishing boundary.
- `GET /auth/v1/me` rejects sessions that are revoked, expired, missing, or associated with an unavailable account.
- Google sessions use `authenticationMethod = google_oidc`.

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
IIdentityFederatedAccountStore
IIdentitySessionStore
IEmailLookupHasher
IIdentityProviderIdentifierHasher
IGoogleIdentityTokenValidator
IPasswordCredentialHasher
IRefreshTokenService
IAccessTokenService
IIdentityEventPublisher
IIdentityEventOutbox
IIdentityEventTransport
```

The current `InMemoryDevelopment` adapters support local execution and CI. They are not production persistence: state is lost on restart and is not shared between replicas. Production Elasticsearch adapters must preserve duplicate, provider-subject uniqueness, account-link, rotation, replay, revocation, and outbox semantics.

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
Identity:Providers:IdentifierHmacKey
Identity:Providers:Google:Enabled
Identity:Providers:Google:ClientIds
Identity:Providers:Google:HostedDomain
Identity:Providers:Google:IssuedAtClockToleranceSeconds
Identity:Providers:Google:ExpirationClockToleranceSeconds
Identity:Events:Mode
Identity:Events:Exchange
Identity:Events:ConnectionString
Identity:Events:UserIdHmacKey
Identity:Events:BatchSize
Identity:Events:DispatchIntervalMilliseconds
Identity:Events:MaximumRetryDelaySeconds
```

Keys, RabbitMQ credentials, and provider configuration belong in environment variables or a secret manager. Google readiness requires at least one allowed client ID and a provider identifier HMAC key when enabled.

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

Automated tests cover email registration/sign-in, Google provider authentication, explicit linking, refresh rotation, replay-family revocation, expiry, logout, current-user context, hash-only secret storage, shared event envelopes, safe lifecycle events, outbox retry, OpenAPI, and architecture boundaries.

## Boundaries

- Deterministic server logic is authoritative.
- Google validation is isolated behind an application abstraction.
- Public contracts expose no storage implementation fields.
- Other services never read Identity Service indices directly.
- RabbitMQ is an integration transport, not the identity source of truth.
- API Gateway routes provider requests but does not own provider validation or provider links.
- Profile data from providers belongs to Profile Service, not Identity authentication truth.
- LLM and OCR are outside the authentication trust boundary.
