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
docs/engineering/phone-verification-authentication.md
```

## Responsibility

Identity Service owns account authentication, session lifecycle, provider links, verification challenges, access-token issuance, refresh rotation, revocation, and identity lifecycle event intents. Profile and financial data remain owned by their dedicated services.

## Client API

```text
POST /auth/v1/register
POST /auth/v1/sign-in
POST /auth/v1/providers/google/sign-in
POST /auth/v1/providers/apple/sign-in
POST /auth/v1/providers/phone/verifications
POST /auth/v1/providers/phone/verifications/confirm
POST /auth/v1/refresh
POST /auth/v1/logout
GET  /auth/v1/me
```

Registration and sign-in create an authoritative server-side session. Refresh rotates the session. Logout revokes it. Current-user context verifies both the signed access value and server-side session state.

Google and Apple sign-in validate provider-issued identity tokens server-side. Phone sign-in uses a provider-neutral two-step challenge. All successful methods map stable provider identifiers through Identity-owned HMAC-protected links and return the same Financial Assistant session contract.

## Provider linking

- Provider identifiers are stored only as purpose-separated HMAC values.
- Provider links are stored separately from local email credentials.
- Email, phone, Google, Apple, and Profile attributes never silently link accounts.
- Existing-account linking requires a future authenticated link endpoint.
- Account recovery requires an existing provider link and a separate recovery workflow.
- Provider tokens, raw subjects, raw nonces, verification codes, and raw phone numbers are not persisted in provider-link records or integration events.

## Phone verification

- Phone input must be E.164.
- The public purpose in v1 is `sign_in`.
- Identity applies challenge TTL, resend cooldown, per-phone and per-client start limits, failed-attempt lockout, client-instance binding, and one-time completion.
- The external provider owns code delivery and checking through `IPhoneVerificationProvider`.
- Identity never stores or logs verification codes.
- Default policy is six digits, ten-minute lifetime, thirty-second resend cooldown, five failed checks, five starts per phone per hour, and ten starts per client per hour.
- Phone sessions use `authenticationMethod = phone_otp`.
- SMS is a restricted out-of-band method and should not be the only method for high-risk financial actions.

## Apple validation

- Apple OIDC discovery and JWKS provide signing keys.
- Identity tokens must use RS256, the configured Apple issuer, and an allowed Bundle ID or Services ID audience.
- The mobile client supplies a raw nonce; the token must contain the matching SHA-256 nonce representation.
- Signing-key rotation triggers one metadata refresh and validation retry.
- Apple sessions use `authenticationMethod = apple_oidc`.

## Session model

- Access values are short-lived signed JWTs. Default lifetime: 15 minutes.
- Refresh values are opaque, rotate on use, and default to 30 days.
- Only refresh hashes and session metadata are stored.
- Reuse of a rotated value revokes the entire token family.
- Logout publishes `token.revoked.v1` through the event publishing boundary.
- `GET /auth/v1/me` rejects sessions that are revoked, expired, missing, or associated with an unavailable account.
- Google sessions use `authenticationMethod = google_oidc`.
- Apple sessions use `authenticationMethod = apple_oidc`.
- Phone sessions use `authenticationMethod = phone_otp`.

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
IPhoneVerificationChallengeStore
IPhoneVerificationProvider
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

The current in-memory adapters support local execution and CI. They are not production persistence: state is lost on restart and is not shared between replicas. Production Elasticsearch adapters must preserve duplicate, provider-subject uniqueness, account-link, challenge reservation, attempt increment, one-time completion, rotation, replay, revocation, and outbox semantics.

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
Identity:Providers:Phone:Enabled
Identity:Providers:Phone:Adapter
Identity:Providers:Phone:CodeLength
Identity:Providers:Phone:ChallengeLifetimeMinutes
Identity:Providers:Phone:ResendCooldownSeconds
Identity:Providers:Phone:MaximumAttempts
Identity:Providers:Phone:StartWindowMinutes
Identity:Providers:Phone:MaximumStartsPerPhone
Identity:Providers:Phone:MaximumStartsPerClient
Identity:Events:Mode
Identity:Events:Exchange
Identity:Events:ConnectionString
Identity:Events:UserIdHmacKey
Identity:Events:BatchSize
Identity:Events:DispatchIntervalMilliseconds
Identity:Events:MaximumRetryDelaySeconds
```

Keys, RabbitMQ credentials, provider credentials, and provider configuration belong in environment variables or a secret manager. Phone readiness fails when the feature is enabled while the adapter remains disabled or policy values are unsafe.

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

Automated tests cover email registration/sign-in, Google and Apple authentication, phone challenge and confirmation, rate limits, lockout, client binding, provider-link hashing, explicit linking, session rotation, replay-family revocation, expiry, logout, current-user context, safe lifecycle events, outbox retry, OpenAPI, and architecture boundaries.

## Boundaries

- Deterministic server logic is authoritative.
- Provider validation and delivery are isolated behind application abstractions.
- Public contracts expose no storage implementation fields.
- Other services never read Identity Service indices directly.
- RabbitMQ is an integration transport, not the identity source of truth.
- API Gateway routes and applies perimeter controls but does not own provider validation, challenges, or provider links.
- Profile data belongs to Profile Service, not Identity authentication truth.
- LLM and OCR are outside the authentication trust boundary.
