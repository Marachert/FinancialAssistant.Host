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
docs/engineering/apple-sign-in-backend-validation.md
```

## Responsibility

Identity Service owns account authentication, session lifecycle, provider links, access-token issuance, refresh rotation, revocation, and identity lifecycle event intents. Profile and financial data remain owned by their dedicated services.

## Client API

```text
POST /auth/v1/register
POST /auth/v1/sign-in
POST /auth/v1/providers/google/sign-in
POST /auth/v1/providers/apple/sign-in
POST /auth/v1/refresh
POST /auth/v1/logout
GET  /auth/v1/me
```

Registration and sign-in create an authoritative server-side session. Refresh rotates the session. Logout revokes it. Current-user context verifies both the signed access value and server-side session state.

Google and Apple sign-in validate provider-issued identity tokens server-side, map stable provider subjects through Identity-owned hashed provider links, and return the same Financial Assistant session contract. Provider tokens are never used as Financial Assistant API bearer tokens.

## Provider linking

- Provider `sub` is the stable provider key; email is not a provider primary key.
- Provider subjects and supported tenant identifiers are stored only as purpose-separated HMAC values.
- Provider links are stored separately from local email credentials.
- A verified provider email matching a local credential does not trigger automatic linking.
- Matching local accounts return `provider_link_required`; future linking must require an authenticated existing Identity session.
- ID tokens, raw provider subjects, raw nonces, email, names, and profile pictures are not persisted in provider-link records.
- Apple private-relay email is treated as optional profile data, not identity truth.

## Apple validation

- Apple OIDC discovery and JWKS provide signing keys.
- Identity tokens must use RS256, the configured Apple issuer, and an allowed Bundle ID or Services ID audience.
- The mobile client supplies a raw nonce; the token must contain the matching SHA-256 nonce representation.
- Signing-key rotation triggers one metadata refresh and validation retry.
- Apple sessions use `authenticationMethod = apple_oidc`.
- Apple private keys and client secrets are not required for identity-token validation; they are future requirements for authorization-code exchange or revocation.

## Session model

- Access values are short-lived signed JWTs. Default lifetime: 15 minutes.
- Refresh values are opaque, rotate on use, and default to 30 days.
- Only refresh hashes and session metadata are stored.
- Reuse of a rotated value revokes the entire token family.
- Logout publishes `token.revoked.v1` through the event publishing boundary.
- `GET /auth/v1/me` rejects sessions that are revoked, expired, missing, or associated with an unavailable account.
- Google sessions use `authenticationMethod = google_oidc`.
- Apple sessions use `authenticationMethod = apple_oidc`.

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
IAppleIdentityTokenValidator
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
Identity:Providers:Apple:Enabled
Identity:Providers:Apple:ClientIds
Identity:Providers:Apple:Issuer
Identity:Providers:Apple:DiscoveryEndpoint
Identity:Providers:Apple:ClockSkewSeconds
Identity:Providers:Apple:RequireNonce
Identity:Events:Mode
Identity:Events:Exchange
Identity:Events:ConnectionString
Identity:Events:UserIdHmacKey
Identity:Events:BatchSize
Identity:Events:DispatchIntervalMilliseconds
Identity:Events:MaximumRetryDelaySeconds
```

Keys, RabbitMQ credentials, and provider configuration belong in environment variables or a secret manager. Enabled providers require allowed client IDs and the provider identifier HMAC key. Apple readiness additionally requires HTTPS metadata endpoints and nonce validation.

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

Automated tests cover email registration/sign-in, Google and Apple provider authentication, explicit linking, refresh rotation, replay-family revocation, expiry, logout, current-user context, hash-only secret storage, shared event envelopes, safe lifecycle events, outbox retry, OpenAPI, and architecture boundaries.

## Boundaries

- Deterministic server logic is authoritative.
- Google and Apple validation are isolated behind application abstractions.
- Public contracts expose no storage implementation fields.
- Other services never read Identity Service indices directly.
- RabbitMQ is an integration transport, not the identity source of truth.
- API Gateway routes provider requests but does not own provider validation or provider links.
- Profile data from providers belongs to Profile Service, not Identity authentication truth.
- LLM and OCR are outside the authentication trust boundary.
