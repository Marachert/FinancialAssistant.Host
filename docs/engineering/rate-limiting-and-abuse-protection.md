# Rate Limiting and Abuse Protection Baseline

## Purpose

FIN-81 adds a deterministic PoC protection layer for public Identity and intake routes.

Rate limiting is applied twice:

```text
client
-> Public API Gateway rate limit
-> gateway security and routing
-> Identity Service endpoint rate limit
-> deterministic authentication logic
```

The gateway absorbs broad public abuse. Identity applies defense-in-depth limits to sensitive endpoints in case it is reached through an internal route or gateway policy is misconfigured.

Rate limiting is technical protection. It does not make authentication decisions, calculate financial values, or replace provider fraud controls.

## Policy groups

### Gateway policies

| Policy | Default limit | Window | Routes |
| --- | ---: | ---: | --- |
| `general` | 300 | 60 seconds | routes without a more specific rule |
| `identity-registration` | 5 | 10 minutes | `POST /auth/v1/register` |
| `identity-sign-in` | 10 | 5 minutes | `POST /auth/v1/sign-in` |
| `identity-provider` | 10 | 5 minutes | Google and Apple sign-in |
| `phone-start` | 5 | 60 seconds | phone verification start |
| `phone-confirm` | 20 | 5 minutes | phone verification confirmation |
| `intake` | 30 | 60 seconds | transaction and receipt intake POST routes |

Health and safe gateway diagnostic endpoints are excluded so orchestration and monitoring are not blocked by client traffic.

### Identity policies

| Policy | Default limit | Window | Endpoints |
| --- | ---: | ---: | --- |
| registration | 5 | 10 minutes | account registration |
| sign-in | 10 | 5 minutes | email/password sign-in |
| provider sign-in | 10 | 5 minutes | Google and Apple sign-in |
| phone start | 5 | 60 seconds | phone verification dispatch |
| phone confirm | 20 | 5 minutes | phone verification checks |
| session | 60 | 60 seconds | refresh, logout, and current-user context |

Phone verification retains its domain-specific challenge cooldown, attempt lockout, and per-phone/per-client counters. Endpoint rate limiting complements those controls; it does not replace them.

## Partition strategy

The current in-process limiter partitions by:

```text
policy name
+ remote connection address
+ optional X-Client-Instance-Id
```

The material is SHA-256 hashed before it becomes an in-memory partition key. Raw IP addresses and client instance identifiers are not logged or included in public responses.

`X-Client-Instance-Id` is accepted only when it contains 8–128 characters and no control characters. Missing or invalid values use an IP-only partition.

The gateway and Identity Service do not trust `X-Forwarded-For` directly. Production deployment must configure ASP.NET Core Forwarded Headers with an explicit trusted proxy/network allowlist before using forwarded addresses for throttling.

## Request flow

### Accepted request

```text
classify route or endpoint
-> derive privacy-safe partition key
-> acquire one fixed-window permit
-> continue to authorization, routing, or authentication logic
```

### Rejected request

```text
permit unavailable
-> HTTP 429
-> Retry-After header
-> no-store cache policy
-> generic problem response
-> no backend service call
```

Public response:

```json
{
  "type": "https://errors.financial-assistant.app/identity/rate-limited",
  "title": "Request rate limit exceeded.",
  "status": 429,
  "code": "rate_limited",
  "detail": "Too many requests were received. Wait before trying again.",
  "traceId": "correlation-id",
  "retryAfterSeconds": 60
}
```

Gateway responses use the equivalent gateway error URI and `correlationId` field.

The response never reveals:

* whether an account exists;
* whether an email or phone is registered;
* whether a password, provider token, or code was correct;
* the active policy name or partition identifier;
* raw request values.

## Retry behavior

Clients must honor `Retry-After` and avoid automatic immediate retries.

Recommended client behavior:

1. Disable the submit action until the indicated retry time.
2. Show a neutral message such as “Too many attempts. Try again shortly.”
3. Do not distinguish known and unknown accounts.
4. Apply exponential backoff for repeated transport or `429` responses.
5. Do not silently switch authentication methods without user intent.

## Configuration

Gateway:

```text
Gateway:RateLimiting:Enabled
Gateway:RateLimiting:ClientInstanceHeaderName
Gateway:RateLimiting:DefaultPolicy
Gateway:RateLimiting:Policies
Gateway:RateLimiting:Rules
Gateway:RateLimiting:ExcludedPathPrefixes
```

Identity:

```text
Identity:RateLimiting:Enabled
Identity:RateLimiting:ClientInstanceHeaderName
Identity:RateLimiting:Registration
Identity:RateLimiting:SignIn
Identity:RateLimiting:ProviderSignIn
Identity:RateLimiting:PhoneStart
Identity:RateLimiting:PhoneConfirm
Identity:RateLimiting:Session
```

Configuration is environment-driven. Production values must be tuned from observed traffic, provider quotas, security review, and customer-support impact rather than copied blindly from the PoC defaults.

## Responsibility boundaries

### Public API Gateway

Owns:

* broad route-group throttling;
* early rejection before downstream dispatch;
* IP/client-instance partitioning at the public edge;
* safe gateway `429` responses.

Does not own:

* account existence;
* credential validation;
* phone challenge truth;
* provider fraud decisions;
* financial-domain rules.

### Identity Service

Owns:

* stricter endpoint-level defense-in-depth limits;
* authentication-neutral error behavior;
* phone challenge cooldown, attempt count, and one-time completion;
* session and account truth.

### External providers

Own:

* provider-side quotas;
* SMS/email delivery protection;
* provider token abuse signals;
* delivery fraud and spend controls.

## Future distributed architecture

The current limiter is process-local. Each replica owns independent fixed-window state, and counters are lost on restart.

Production options:

1. Edge/WAF rate limiting for broad IP, ASN, country, and bot controls.
2. API Gateway distributed limiter backed by Redis or another low-latency atomic counter store.
3. Identity-owned Elasticsearch or Redis state for account/provider-specific controls that require durable ownership.
4. Provider-native service limits for SMS and external authentication operations.

Distributed keys must use purpose-separated HMAC values when persisted or shared. They must not contain raw IP addresses, phone numbers, emails, access tokens, provider subjects, or device identifiers.

A production design must define:

* atomic increment and expiry behavior;
* replica consistency;
* fail-open versus fail-closed rules per route;
* trusted proxy handling;
* IPv6 normalization;
* NAT and shared-household fairness;
* privacy retention;
* metric cardinality limits;
* emergency policy overrides;
* provider cost caps.

## Abuse signals for later phases

Future controls may combine rate limits with:

* bot detection;
* IP and ASN reputation;
* impossible travel and country velocity;
* device attestation;
* SIM swap and number-porting signals;
* breached-password intelligence;
* provider risk signals;
* account and session velocity;
* receipt-upload size and frequency;
* per-user financial-action step-up rules.

Device fingerprints must not become hidden permanent identity keys. Collection must be proportionate, documented, privacy-reviewed, and replaceable when a device changes.

## Observability

Allowed metrics:

```text
rate_limit_requests_total{service,policy,outcome}
rate_limit_rejections_total{service,policy}
rate_limit_retry_after_seconds{service,policy}
```

Do not use raw IP, email, phone, token, provider subject, receipt text, transaction note, or client-instance value as a metric label.

Logs may contain correlation ID, route key, HTTP status, normalized policy name, and duration. Partition keys must not be logged.

## Verification coverage

Automated tests verify:

* stricter sign-in and registration limits;
* separate operation policies;
* independent client-instance partitions;
* safe generic `429` response;
* `Retry-After` propagation;
* no email or password leakage;
* health endpoint exclusions;
* gateway rejection before service dispatch.

## Limitations

* Counters are in-memory and process-local.
* No distributed store or WAF integration is included.
* No trusted-forwarded-header configuration is enabled by this task.
* Client instance headers are caller-provided and are not device attestation.
* Policy values are PoC defaults and require production tuning.
