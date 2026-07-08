# Rate Limiting and Abuse Protection Baseline

## Purpose

FIN-81 adds deterministic protection for public Identity and intake routes.

```text
client
-> Public API Gateway rate limit
-> gateway security and routing
-> Identity Service endpoint rate limit
-> deterministic authentication logic
```

The gateway absorbs broad public abuse. Identity applies defense-in-depth limits to sensitive endpoints when it is reached through internal routing or when gateway policy is misconfigured.

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

Phone verification retains its challenge cooldown, attempt lockout, per-phone/per-client counters, and one-time completion. Endpoint throttling complements those controls; it does not replace them.

## Partition strategy

The active PoC partition is deliberately IP-wide:

```text
policy name
+ remote connection address
```

The material is SHA-256 hashed before it becomes an in-memory partition key. Raw IP addresses are not logged or included in public responses.

Caller-controlled headers such as `X-Client-Instance-Id` are not part of the enforcement key. Changing a client header must not create a fresh rate-limit bucket.

A future device-aware partition may be added only when the device signal is authenticated or attested. Even then, an IP-wide limiter must remain in the chain so rotating device identifiers cannot bypass public protection.

The gateway and Identity Service do not trust `X-Forwarded-For` directly. Production deployment must configure ASP.NET Core Forwarded Headers with an explicit trusted proxy/network allowlist before forwarded addresses are used for throttling.

## Gateway partition cache

Gateway partition state is held in a bounded, sliding-expiration cache:

```text
MaximumPartitionCount = 10000
PartitionIdleExpirationSeconds = 900
```

When the cache cannot admit a new partition, requests use a shared overflow limiter for the policy. Saturating the cache therefore does not create unlimited fresh buckets.

Cached fixed-window limiters use request-driven replenishment and do not own background timers. Idle entries can be evicted without leaving timer resources behind.

## Request flow

### Accepted request

```text
classify route or endpoint
-> derive privacy-safe IP-wide partition key
-> acquire one fixed-window permit
-> continue to authorization, routing, or authentication logic
```

### Rejected request

```text
permit unavailable
-> HTTP 429
-> Retry-After header
-> Cache-Control: no-store
-> generic problem response
-> no backend service call from the gateway
```

Identity response:

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
* the active partition identifier;
* raw request values.

## Retry behavior

Clients must honor `Retry-After` and avoid immediate automatic retries.

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
Gateway:RateLimiting:DefaultPolicy
Gateway:RateLimiting:MaximumPartitionCount
Gateway:RateLimiting:PartitionIdleExpirationSeconds
Gateway:RateLimiting:Policies
Gateway:RateLimiting:Rules
Gateway:RateLimiting:ExcludedPathPrefixes
```

Identity:

```text
Identity:RateLimiting:Enabled
Identity:RateLimiting:Registration
Identity:RateLimiting:SignIn
Identity:RateLimiting:ProviderSignIn
Identity:RateLimiting:PhoneStart
Identity:RateLimiting:PhoneConfirm
Identity:RateLimiting:Session
```

Configuration is environment-driven. Production values must be tuned from observed traffic, provider quotas, security review, and customer-support impact rather than copied blindly from the PoC defaults.

## Responsibility boundaries

### Public API Gateway owns

* broad route-group throttling;
* early rejection before downstream dispatch;
* IP-wide partitioning at the public edge;
* bounded partition-cache behavior;
* safe gateway `429` responses.

It does not own account existence, credential validation, phone challenge truth, provider fraud decisions, or financial-domain rules.

### Identity Service owns

* stricter endpoint-level defense-in-depth limits;
* authentication-neutral error behavior;
* phone challenge cooldown, attempt count, and one-time completion;
* session and account truth.

### External providers own

* provider-side quotas;
* SMS/email delivery protection;
* provider token abuse signals;
* delivery fraud and spend controls.

## Future distributed architecture

The current limiter is process-local. Each replica owns independent fixed-window state, and counters are lost on restart.

Production options:

1. Edge/WAF rate limiting for broad IP, ASN, country, and bot controls.
2. API Gateway distributed limiting backed by Redis or another low-latency atomic counter store.
3. Identity-owned Elasticsearch or Redis state for account/provider-specific controls that require durable ownership.
4. Provider-native service limits for SMS and external authentication operations.

Distributed keys must use purpose-separated HMAC values when persisted or shared. They must not contain raw IP addresses, phone numbers, emails, access tokens, provider subjects, or device identifiers.

A production design must define atomic increment and expiry behavior, replica consistency, fail-open versus fail-closed rules, trusted proxy handling, IPv6 normalization, NAT fairness, retention, metric cardinality, emergency overrides, and provider cost caps.

## Abuse signals for later phases

Future controls may combine rate limits with bot detection, IP/ASN reputation, country velocity, device attestation, SIM-swap signals, breached-password intelligence, provider risk signals, account/session velocity, receipt-upload limits, and step-up authentication.

Device fingerprints must not become hidden permanent identity keys. Collection must be proportionate, documented, privacy-reviewed, and replaceable when a device changes.

## Observability

Allowed metrics:

```text
rate_limit_requests_total{service,policy,outcome}
rate_limit_rejections_total{service,policy}
rate_limit_retry_after_seconds{service,policy}
rate_limit_partition_cache_entries{service}
rate_limit_partition_overflow_total{service,policy}
```

Do not use raw IP, email, phone, token, provider subject, receipt text, transaction note, or caller-provided device value as a metric label.

Logs may contain correlation ID, route key, HTTP status, normalized policy name, and duration. Partition keys must not be logged.

## Verification coverage

Automated tests verify:

* stricter sign-in and registration limits;
* separate operation policies;
* changing `X-Client-Instance-Id` does not reset the IP-wide bucket;
* bounded gateway partition storage;
* shared overflow limiting when cache capacity is reached;
* safe generic `429` response;
* `Retry-After` propagation;
* no email or password leakage;
* health endpoint exclusions;
* gateway rejection before service dispatch.

## Limitations

* Counters are in-memory and process-local.
* No distributed store or WAF integration is included.
* No trusted-forwarded-header configuration is enabled by this task.
* IP-wide limits can affect users behind shared NAT and require production tuning.
* Caller-provided client instance headers are not device attestation and are not used for enforcement.
* Policy and cache values are PoC defaults and require production tuning.
