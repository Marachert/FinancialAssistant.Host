# Google Sign-In Backend Validation

## Purpose

FIN-78 prepares a production-shaped backend path for mobile and web clients that obtain a Google ID token and exchange it for a Financial Assistant Identity session.

The Google ID token is external evidence. Identity Service validates it, maps the stable Google subject to an Identity-owned provider link, and remains authoritative for the local account and session.

## Public API

```text
POST /auth/v1/providers/google/sign-in
```

Request:

```json
{
  "idToken": "google-id-token",
  "client": {
    "clientInstanceId": "installation-scoped-id",
    "platform": "ios",
    "appVersion": "0.1.0"
  }
}
```

Success returns the existing `AuthSessionResponse`. First provider registration and later provider sign-in both return HTTP 200 so the transport contract does not reveal whether the provider subject was already known.

## Validation flow

```text
mobile/web client
-> Google SDK obtains ID token
-> API Gateway
-> Identity Google endpoint
-> IGoogleIdentityTokenValidator
-> Google signature, issuer, audience, issued-at, expiry and optional hosted-domain validation
-> hash Google subject
-> find Identity-owned provider link
-> create or reuse Identity account
-> issue Financial Assistant access and refresh session
-> publish safe lifecycle events
```

The Infrastructure adapter uses the official `Google.Apis.Auth` package and `GoogleJsonWebSignature.ValidateAsync`.

Required checks:

* signature uses Google certificates;
* issuer is a trusted Google issuer;
* audience matches one configured OAuth client ID;
* token is within issued-at and expiry tolerances;
* optional hosted-domain claim matches when configured;
* `sub` is present.

## Identity mapping

The Google `sub` claim is the only provider identity key. Email is not used as a provider primary key.

Stored provider link fields:

```text
accountId
provider = google
providerSubjectHash
providerTenantHash when applicable
linkedAtUtc
lastAuthenticatedAtUtc
```

Raw Google subject, ID token, email, display name, and picture URL are not persisted in the provider link.

`providerSubjectHash` and `providerTenantHash` use purpose-separated HMAC values from `Identity:Providers:IdentifierHmacKey`.

## Account creation and linking

Rules:

1. Existing Google provider link: reuse its Identity account and issue a new session.
2. Unknown Google subject with no matching local email credential: create a provider-only Identity account and link.
3. Unknown Google subject whose verified email matches an existing local credential: return `provider_link_required`.
4. Never auto-link solely because Google reports the same email.
5. A future authenticated link endpoint must require proof of the existing Identity session before adding the Google link.

This prevents account takeover through implicit email correlation.

## Safe errors

```text
validation_failed                 400
provider_authentication_failed   401
provider_link_required           409
provider_unavailable             503
```

Invalid signature, issuer, audience, expiry, malformed token, and unknown subject conditions use generic public wording. Responses and logs never include the Google token or its claims.

## Session and events

Successful Google authentication issues the same server-side session lifecycle used by email/password authentication, with:

```text
authenticationMethod = google_oidc
```

A new provider-only account publishes:

```text
user.registered.v1
user.signed_in.v1
```

A returning account publishes:

```text
user.signed_in.v1
```

Invalid provider credentials publish privacy-safe `authentication.failed.v1` with no subject identifier.

## Configuration

```text
Identity:Providers:IdentifierHmacKey
Identity:Providers:Google:Enabled
Identity:Providers:Google:ClientIds
Identity:Providers:Google:HostedDomain
Identity:Providers:Google:IssuedAtClockToleranceSeconds
Identity:Providers:Google:ExpirationClockToleranceSeconds
```

`ClientIds` must include every allowed Android, iOS, and web OAuth audience used by the product. Google sign-in readiness fails when the provider is enabled without client IDs or the provider identifier HMAC key.

Secrets and client configuration must come from environment variables or a secret manager. No client secret is required for ID-token validation itself.

## Ownership boundaries

* Identity Service owns provider links, local accounts, and sessions.
* Google owns the external identity assertion and signing keys.
* API Gateway routes the request but does not validate provider identities or store provider tokens.
* Profile Service owns display names, avatars, preferences, and profile enrichment.
* LLM and OCR are not involved in authentication decisions.
* Other services must not read Identity provider-link storage directly.

## Current persistence limitation

The active account/provider-link adapter is `InMemoryIdentityAccountStore`. It supports local execution and automated tests but loses state on restart and is not shared between replicas.

A production Elasticsearch adapter must preserve unique provider-subject ownership and atomic account-plus-provider-link creation through optimistic concurrency or an equivalent documented mechanism.

## Mobile integration sequence

1. Configure Google OAuth clients for each platform.
2. Mobile obtains a Google ID token for the backend audience.
3. Mobile calls the Financial Assistant Google endpoint with the token and normal client context.
4. Backend returns the standard Financial Assistant session.
5. Mobile stores only the Financial Assistant refresh value using platform-secure storage.
6. Google ID tokens are not used as Financial Assistant API bearer tokens.

## Verification

Automated tests cover:

* new Google provider account creation;
* provider link stored separately from local credentials;
* hashed Google subject;
* repeat sign-in reusing the same account;
* explicit-link requirement for a matching local email credential;
* invalid provider token response;
* provider outage response;
* request validation;
* Google lifecycle event metadata;
* absence of token and email leakage.
