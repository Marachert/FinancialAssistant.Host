# Phone Verification Authentication Path

## Purpose

FIN-80 prepares a provider-neutral phone verification path for mobile authentication and future account recovery.

Public endpoints:

```text
POST /auth/v1/providers/phone/verifications
POST /auth/v1/providers/phone/verifications/confirm
```

Identity Service owns challenge lifecycle, anti-abuse policy, account mapping, and Financial Assistant sessions. An external provider owns message delivery and verification-code checking behind `IPhoneVerificationProvider`.

## Flow

```text
mobile/web client
-> request verification with E.164 phone number and client context
-> Identity validates and HMACs phone/client identifiers
-> challenge store atomically applies cooldown and rate limits
-> provider sends verification message
-> client submits verification ID and code
-> provider checks code
-> Identity completes challenge once
-> Identity creates/reuses phone provider link
-> Identity issues Financial Assistant session
```

The current API purpose is:

```text
sign_in
```

Future account recovery must consume a successful verification through a separate recovery grant and must only recover an account that already owns the phone link. Recovery must not create or silently link an account.

## Contracts

Start request:

```json
{
  "phoneNumber": "+15551234567",
  "purpose": "sign_in",
  "client": {
    "clientInstanceId": "installation-scoped-id",
    "platform": "android",
    "appVersion": "0.1.0"
  }
}
```

Start response:

```json
{
  "verificationId": "opaque-challenge-id",
  "maskedDestination": "+1*****4567",
  "expiresAtUtc": "2026-07-07T19:00:00Z",
  "resendAvailableAtUtc": "2026-07-07T18:50:30Z"
}
```

Confirm request:

```json
{
  "verificationId": "opaque-challenge-id",
  "code": "six-digit-code",
  "client": {
    "clientInstanceId": "installation-scoped-id",
    "platform": "android",
    "appVersion": "0.1.0"
  }
}
```

Successful confirmation returns the standard `AuthSessionResponse` with:

```text
authenticationMethod = phone_otp
```

## Phone identity mapping

Phone numbers must be normalized to E.164 before use. Identity Service stores only a purpose-separated HMAC:

```text
provider = phone
providerSubjectHash = HMAC(phone:number:E.164)
```

Raw phone numbers are passed only to the configured delivery provider for dispatch. They are not stored in the Identity provider link, challenge record, event payload, or application log.

Provider links remain separate from email/password credentials and social-provider links.

## Account creation and linking

Rules:

1. Approved verification for an existing phone provider link reuses the linked account.
2. Approved verification for an unknown phone creates a provider-only Identity account.
3. Phone verification never links to an email, Google, Apple, or profile record by matching attributes.
4. Linking a phone to an existing account requires a future authenticated link endpoint.
5. Account recovery requires an existing phone link and a separate recovery workflow.
6. Profile Service may store a user-visible phone attribute, but Profile data is not authentication truth.

## Challenge policy

Default policy:

```text
code length                  6 digits
challenge lifetime           10 minutes
resend cooldown              30 seconds
maximum failed checks        5
rate-limit window            60 minutes
maximum starts per phone     5
maximum starts per client    10
```

A new challenge after the cooldown cancels the prior active challenge for the same phone. Completed, cancelled, locked, or expired challenges cannot be replayed. Confirmation is bound to the client instance that started the challenge.

A `429 rate_limited` response includes `Retry-After` and `retryAfterSeconds` when the next retry time is known.

NIST SP 800-63B requires out-of-band secrets to expire within ten minutes, be accepted only once, use at least six decimal digits, and be protected by rate limiting. It also classifies PSTN-based authentication as restricted and recommends considering SIM change, device swap, and number-porting risk indicators.

Reference:

```text
https://pages.nist.gov/800-63-4/sp800-63b.html
```

Provider guidance also recommends E.164 normalization and a resend buffer. Twilio suggests one verification request per 30 seconds per phone as a starting point.

Reference:

```text
https://www.twilio.com/docs/verify/developer-best-practices
```

## Provider abstraction

```csharp
public interface IPhoneVerificationProvider
{
    Task<PhoneVerificationDispatchResult> StartAsync(...);
    Task<PhoneVerificationCheckResult> CheckAsync(...);
}
```

Application and domain projects do not reference Twilio, AWS, Azure, or another SMS vendor SDK.

The default adapter is `DisabledPhoneVerificationProvider`. Production readiness fails if phone verification is enabled while the adapter remains disabled.

A production adapter must:

* send a provider-generated or provider-managed one-time code;
* return an opaque provider reference;
* check codes without exposing them to application logs;
* map provider throttling to `RateLimited`;
* map temporary network/provider failures to `Unavailable`;
* use provider credentials from a secret manager;
* provide delivery and fraud metrics without raw phone-number labels.

## Challenge storage

The development adapter is `InMemoryPhoneVerificationChallengeStore`. It atomically handles:

* active challenge per phone hash;
* resend cooldown;
* start counters by phone hash and client-instance hash;
* failed attempt count;
* lockout;
* one-time completion.

Production Elasticsearch persistence must use service-owned indices and optimistic concurrency for reservation, attempt increments, lockout, and completion. It must not store verification codes.

## Safe errors

```text
validation_failed                 400
provider_authentication_failed   401
rate_limited                     429
provider_unavailable             503
```

Missing, expired, locked, wrong-client, replayed, and rejected-code challenges all return the same generic authentication failure. This avoids revealing challenge state or account existence.

## Logging and events

Never log or emit:

* verification codes;
* raw phone numbers;
* provider request payloads containing phone numbers;
* provider references in integration events;
* whether a phone already has an account.

Successful new phone authentication publishes:

```text
user.registered.v1
user.signed_in.v1
```

Returning authentication publishes:

```text
user.signed_in.v1
```

Rejected confirmation publishes privacy-safe:

```text
authentication.failed.v1
```

with `authenticationMethod = phone_otp` and no phone identifier.

## Fraud and abuse controls

Identity-level controls are necessary but not sufficient. Production should additionally apply:

* API Gateway/WAF limits by IP, device, ASN, country, and velocity;
* bot detection for suspicious start traffic;
* country allow/deny policy based on product rollout;
* number intelligence and line-type checks where legally appropriate;
* SIM-swap and number-porting risk signals for sensitive recovery;
* spend caps and provider-side service rate limits;
* anomaly alerts for delivery failures and toll-fraud patterns;
* alternative authentication methods for users without reliable SMS access.

Phone OTP should not be the only authentication method for high-risk financial operations. Step-up authentication should prefer phishing-resistant methods when available.

## Configuration

```text
Identity:Providers:IdentifierHmacKey
Identity:Providers:Phone:Enabled
Identity:Providers:Phone:Adapter
Identity:Providers:Phone:CodeLength
Identity:Providers:Phone:ChallengeLifetimeMinutes
Identity:Providers:Phone:ResendCooldownSeconds
Identity:Providers:Phone:MaximumAttempts
Identity:Providers:Phone:StartWindowMinutes
Identity:Providers:Phone:MaximumStartsPerPhone
Identity:Providers:Phone:MaximumStartsPerClient
```

## Verification coverage

Automated tests cover:

* E.164 validation;
* masked response without raw phone or code leakage;
* provider-only account creation;
* hashed phone provider link;
* returning sign-in reusing the account;
* resend cooldown and `Retry-After`;
* failed-attempt lockout;
* client-instance binding;
* provider outage handling;
* privacy-safe lifecycle events.
