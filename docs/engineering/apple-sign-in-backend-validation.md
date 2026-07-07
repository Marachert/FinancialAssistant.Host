# Apple Sign-In Backend Validation

## Purpose

FIN-79 prepares the backend path required for Sign in with Apple and iOS release readiness.

```text
POST /auth/v1/providers/apple/sign-in
```

The mobile client obtains an Apple identity token with a nonce and exchanges it for the standard Financial Assistant `AuthSessionResponse`.

## Release dependency

Financial Assistant already supports Google sign-in. Apple App Review Guideline 4.8 requires apps that use third-party or social login for the primary account to also provide an equivalent privacy-preserving login option unless an explicit exception applies.

Release checklist:

1. Enable Sign in with Apple capability for the iOS App ID.
2. Configure every allowed Bundle ID or Services ID as an Apple client ID.
3. Implement the native Apple authorization button and nonce flow.
4. Keep the Apple backend endpoint enabled and reachable during App Review.
5. Include working review credentials or a fully functional review path.

Official guideline:

```text
https://developer.apple.com/app-store/review/guidelines/#login-services
```

## Public contract

Request:

```json
{
  "identityToken": "apple-identity-token",
  "nonce": "client-generated-raw-nonce",
  "client": {
    "clientInstanceId": "installation-scoped-id",
    "platform": "ios",
    "appVersion": "0.1.0"
  }
}
```

The iOS client generates a cryptographically random raw nonce, sends its SHA-256 representation in the Apple authorization request, and sends the raw nonce to Identity Service. The backend accepts the common lowercase-hex and base64url SHA-256 encodings when validating the token `nonce` claim.

## Validation flow

```text
iOS client
-> AuthenticationServices obtains Apple credential
-> API Gateway
-> Identity Apple endpoint
-> IAppleIdentityTokenValidator
-> Apple OIDC discovery and JWKS
-> RS256 signature, issuer, audience, lifetime and nonce validation
-> hash Apple subject
-> find or create Identity provider link
-> issue Financial Assistant session
```

The validator uses Apple OIDC discovery and automatically refreshes signing keys after a key-not-found validation failure.

Required checks:

* token is signed with RS256;
* signing key comes from Apple OIDC metadata;
* issuer matches `https://appleid.apple.com`;
* audience matches a configured Bundle ID or Services ID;
* token is not expired and is within configured clock skew;
* `sub` is present;
* nonce claim matches SHA-256 of the raw client nonce.

## Provider identity mapping

Apple `sub` is the only provider identity key. It is stored only as a purpose-separated HMAC value.

Provider-link fields:

```text
accountId
provider = apple
providerSubjectHash
linkedAtUtc
lastAuthenticatedAtUtc
```

Identity Service does not persist:

* Apple identity tokens;
* authorization codes;
* raw Apple subjects;
* raw nonces;
* Apple email addresses;
* first/last names;
* private-relay flags.

Apple can provide email and name only during the initial authorization, and users can choose a private relay address. Those values are optional profile-enrichment inputs, not authentication truth.

## Account creation and linking

Rules:

1. Existing Apple provider link: reuse the linked Identity account.
2. Unknown Apple subject with no local credential collision: create a provider-only Identity account.
3. Unknown Apple subject whose verified email matches an existing local credential: return `provider_link_required`.
4. Never auto-link accounts solely by email, including private-relay email.
5. Future linking must require an authenticated existing Identity session.

## Safe errors

```text
validation_failed                 400
provider_authentication_failed   401
provider_link_required           409
provider_unavailable             503
```

Malformed tokens, signature failures, issuer/audience mismatch, expiry, nonce mismatch, and missing subjects use the same generic authentication failure. Responses and logs never contain the Apple token, nonce, subject, or email.

## Session and events

Successful authentication issues the standard server-side Identity session with:

```text
authenticationMethod = apple_oidc
```

New provider-only account:

```text
user.registered.v1
user.signed_in.v1
```

Returning provider account:

```text
user.signed_in.v1
```

Invalid provider credential:

```text
authentication.failed.v1
```

Failure events contain no subject or email.

## Configuration

```text
Identity:Providers:IdentifierHmacKey
Identity:Providers:Apple:Enabled
Identity:Providers:Apple:ClientIds
Identity:Providers:Apple:Issuer
Identity:Providers:Apple:DiscoveryEndpoint
Identity:Providers:Apple:ClockSkewSeconds
Identity:Providers:Apple:RequireNonce
```

When Apple sign-in is enabled, readiness requires:

* at least one allowed client ID;
* provider identifier HMAC key;
* HTTPS issuer and discovery endpoints;
* non-negative clock skew;
* nonce validation enabled.

## Apple key material

Identity-token validation uses Apple's public OIDC keys and does not require an Apple private key or generated client secret.

Future authorization-code exchange, token revocation, or account-transfer operations require Apple Developer configuration that may include:

```text
Team ID
Key ID
Services ID / Bundle ID
Sign in with Apple private key (.p8)
short-lived client secret generated server-side
```

Private keys and generated client secrets must live in a secret manager and must never be committed, logged, returned to clients, or stored in provider-link documents.

## Current persistence limitation

The active provider-link adapter is process-local. Production Elasticsearch persistence must enforce unique provider-subject ownership and atomic account-plus-provider-link creation through optimistic concurrency or an equivalent documented mechanism.

## Verification

Automated tests cover:

* provider-only Apple account creation;
* hashed Apple subject storage;
* repeated sign-in reusing the same account;
* explicit linking when verified email matches a local credential;
* invalid token or nonce handling;
* provider outage handling;
* request validation;
* safe lifecycle events;
* absence of identity-token, nonce, and email leakage.
