# Gateway Route Groups and Service Destinations

## Purpose

FIN-71 defines the Public API Gateway route groups and the internal service destination configuration used by Android, iOS, Web, and admin clients.

The gateway is a technical routing boundary. It selects a configured route, applies gateway-level controls, and forwards the unchanged HTTP request to the service that owns the business capability.

It does not perform financial calculations, validate transaction-domain rules, read service-owned Elasticsearch indices, run OCR/LLM workflows, or persist domain data.

## Synchronous flow

```text
mobile / web / admin client
-> Public API Gateway
-> correlation and rate-limit middleware
-> route access-policy boundary
-> route catalog
-> destination catalog
-> owning service REST API
-> owning service validation and business logic
-> response through gateway
```

Asynchronous domain events remain service-to-service concerns through RabbitMQ. The gateway does not publish business events for proxied requests.

## Public route groups

| Route key | Public pattern | Methods | Access policy | Owning service | Destination key |
| --- | --- | --- | --- | --- | --- |
| `auth` | `/auth` and `/auth/{**gatewayPath}` | GET, POST | public | Identity/Auth Service | `auth-service` |
| `profile-me` | `/users/me` and `/users/me/{**gatewayPath}` | GET, PUT, PATCH | authenticated | Profile Service | `profile-service` |
| `categories` | `/categories` and `/categories/{**gatewayPath}` | GET, POST, PUT, PATCH | authenticated | Category Service | `category-service` |
| `transaction-intake` | `/transactions/intake` and child paths | POST | authenticated | Transaction Intake Service | `transaction-intake-service` |
| `transaction-draft-confirm` | `/transactions/drafts/{id}/confirm` | POST | authenticated | Transaction Intake Service | `transaction-intake-service` |
| `receipts` | `/receipts` and `/receipts/{**gatewayPath}` | GET, POST | authenticated | Receipt File Intake Service | `receipt-file-intake-service` |
| `analytics` | `/analytics` and child paths | GET | authenticated | Analytics Service | `analytics-service` |
| `score` | `/score` and child paths | GET | authenticated | Financial Score Service | `financial-score-service` |
| `recommendations` | `/recommendations` and child paths | GET | authenticated | Recommendation Service | `recommendation-service` |
| `notifications` | `/notifications` and child paths | GET, POST, PATCH | authenticated | Notification Service | `notification-service` |
| `admin-monitoring` | `/admin/monitoring` and child paths | GET | admin | Monitoring Admin Service | `monitoring-admin-service` |

Route methods are explicit. An empty method list is rejected during startup rather than silently enabling a broad default set.

## Configuration ownership

Routes are configured under:

```text
Gateway:RouteMap:Routes
```

Destinations are configured under:

```text
Gateway:DestinationMap:Destinations
```

A route definition owns only technical routing metadata:

```json
{
  "RouteKey": "auth",
  "PublicPattern": "/auth",
  "CatchAllPattern": "/auth/{**gatewayPath}",
  "ServiceOwner": "Auth Service",
  "InternalDestination": "auth-service",
  "AccessPolicy": "public",
  "Status": "placeholder",
  "Methods": [ "GET", "POST" ]
}
```

A destination definition owns connection metadata:

```json
{
  "DestinationKey": "auth-service",
  "BaseAddress": "http://identity-service:8080",
  "Enabled": true,
  "RequestTimeoutSeconds": 30
}
```

Destination addresses are deployment configuration. Production addresses, credentials, certificates, and secrets must not be committed to the repository or returned by public diagnostics.

## Environment overrides

.NET configuration providers can override the checked-in defaults.

Example environment variables for the first destination entry:

```text
Gateway__DestinationMap__Destinations__0__BaseAddress=http://identity-service:8080
Gateway__DestinationMap__Destinations__0__Enabled=true
Gateway__DestinationMap__Destinations__0__RequestTimeoutSeconds=15
Gateway__RouteMap__Routes__0__Status=active
```

Array indexes must match the environment-specific configuration file. For production, prefer an explicit environment-specific configuration source or deployment template so route and destination indexes are reviewed together.

## Activation rules

A route forwards traffic only when all conditions are true:

1. The route status is `active`.
2. The destination key exists.
3. The destination is enabled.
4. The base address is a valid absolute HTTP or HTTPS URI.
5. The route access-policy boundary allows the request.

Repository defaults keep routes in `placeholder` state and destinations disabled until the owning service contract and security integration are ready.

Activation should be performed in one reviewed deployment change:

```text
verify service contract
-> configure internal DNS/base address
-> enable destination
-> activate route
-> run gateway verification tests
-> deploy owning service before or with gateway
```

## Startup validation

The gateway fails fast for structurally unsafe configuration.

Route validation rejects:

* missing or duplicate route keys;
* route keys outside lower-case kebab-case;
* missing public patterns;
* route patterns under reserved `/health` or `/gateway` prefixes;
* malformed catch-all patterns;
* missing service owner or destination key;
* unknown access policies;
* unknown route statuses;
* empty, unsupported, or duplicate HTTP methods;
* duplicate method and route-pattern combinations.

Destination validation rejects:

* missing or duplicate destination keys;
* destination keys outside lower-case kebab-case;
* enabled destinations without a base address;
* non-HTTP/HTTPS schemes;
* addresses containing user credentials, query strings, or fragments;
* request timeouts outside 1–300 seconds.

Both catalogs are resolved while the gateway maps endpoints, so invalid route or destination configuration prevents startup.

## Request forwarding

For an active route, the dispatcher:

* preserves the HTTP method;
* preserves the public path and query string;
* streams the request body;
* excludes hop-by-hop headers;
* overwrites `X-Gateway-Route-Key` with the trusted configured route key;
* forwards canonical `correlationId` and `X-Correlation-Id` values;
* forwards the downstream status, allowed headers, and response body;
* applies the destination-specific timeout;
* uses the client cancellation signal.

The gateway does not deserialize or transform financial domain payloads.

## Safe failure behavior

### Placeholder route

```text
HTTP 501
code = route_not_active
```

### Missing or disabled destination

```text
HTTP 503
code = destination_unavailable
```

### Downstream transport failure

```text
HTTP 503
code = destination_unavailable
```

### Downstream timeout

```text
HTTP 504
code = destination_timeout
```

Public errors include only a stable error type, title, status, code, generic detail, and correlation identifier.

They do not expose:

* internal destination keys;
* internal host names or base addresses;
* service implementation names;
* request bodies;
* passwords, access tokens, provider tokens, or phone codes;
* transaction amounts, notes, receipt text, OCR output, or LLM content.

The gateway may log the normalized route and destination keys for operational diagnosis, but it must not log the proxied request body or raw query values.

## Public route catalog

`GET /gateway/routes` returns a sanitized client/developer view:

* route key;
* public and catch-all patterns;
* service owner;
* access policy;
* route status;
* allowed methods.

It does not return internal destination keys or destination addresses.

## Responsibility boundaries

### Gateway owns

* public route matching;
* explicit method constraints;
* route/destination configuration validation;
* correlation and technical headers;
* gateway access-policy enforcement hooks;
* rate limiting;
* safe forwarding and technical failure responses.

### Owning services own

* request contract validation;
* account, profile, category, transaction, receipt, analytics, score, recommendation, notification, and monitoring business logic;
* service-owned Elasticsearch data;
* financial calculations;
* domain events and RabbitMQ outbox/inbox handling;
* deterministic business truth.

### AI/OCR boundaries

OCR and LLM providers are never called by the gateway for normal routing. Receipt extraction, free-form parsing, and recommendation generation belong to their owning backend capabilities. LLM output is not transaction truth.

## Verification

Automated coverage verifies:

* configured route group count and ownership;
* sanitized public route metadata;
* duplicate and malformed route rejection;
* explicit HTTP method requirements;
* unsafe destination URI rejection;
* enabled destination address requirements;
* method, path, query, body, and correlation forwarding;
* trusted gateway route-key overwrite;
* placeholder `501` behavior;
* missing destination `503` behavior;
* transport failure privacy;
* no internal host, destination key, service name, or credential leakage.

Run:

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```

## Current limitations

* Repository defaults do not activate downstream services.
* Destination availability is not proactively probed; an enabled but unreachable service fails safely during dispatch.
* Service discovery, retries, circuit breaking, load balancing, and distributed tracing exporters are future work.
* Array-index environment overrides require disciplined deployment configuration.
* Final JWT and role validation is handled by the gateway access-control task, not by route configuration.
