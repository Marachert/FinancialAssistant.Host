# API Gateway Routing Foundation

## Purpose

The Financial Assistant Public API Gateway is the single public REST entry point for Android, iOS, Web, and admin clients.

This document defines the stable routing foundation: responsibility boundaries, synchronous flow, route/destination activation, correlation, safe diagnostics, and failure behavior.

Detailed FIN-71 configuration is documented in:

```text
docs/engineering/gateway-route-groups-and-destinations.md
```

## Responsibility

The gateway owns:

* the public HTTP boundary;
* route matching and explicit method constraints;
* environment-driven destination resolution;
* correlation and trace-context propagation;
* gateway access-policy hooks;
* rate limiting;
* lightweight health and safe technical diagnostics;
* consistent gateway-level technical errors.

The gateway does not own:

* identity or profile persistence;
* income, expense, transaction, category, receipt, analytics, score, recommendation, or notification data;
* financial calculations or domain validation;
* OCR or LLM processing;
* service-owned Elasticsearch indices;
* business reports or domain events.

Architecture rule:

> The API Gateway must call the owning service REST API. It must never query or update service-owned storage directly.

## Synchronous request flow

```text
client
-> gateway correlation and rate limiting
-> gateway route access policy
-> route catalog
-> destination catalog
-> owning service REST API
-> owning service deterministic business logic and storage
-> gateway response copy
```

RabbitMQ remains the asynchronous service-to-service mechanism. The gateway does not publish business events for ordinary proxy requests.

## Public route map

Configured route groups cover:

* `/auth`;
* `/users/me`;
* `/categories`;
* `/transactions/intake`;
* `/transactions/drafts/{id}/confirm`;
* `/receipts`;
* `/analytics`;
* `/score`;
* `/recommendations`;
* `/notifications`;
* `/admin/monitoring`.

Routes are configured under:

```text
Gateway:RouteMap:Routes
```

Every route declares a stable key, public pattern, optional catch-all pattern, service owner, destination key, access policy, route status, and explicit methods.

## Destination map

Destinations are configured under:

```text
Gateway:DestinationMap:Destinations
```

Every destination declares a stable key, internal HTTP/HTTPS base address, enabled flag, and request timeout.

A route can be forwarded only when:

1. its status is `active`;
2. its destination exists;
3. the destination is enabled;
4. the destination URI is valid;
5. the access-policy boundary allows the request.

Repository defaults keep routes in `placeholder` state and destinations disabled.

## Configuration validation

Route and destination catalogs are resolved during endpoint mapping. Structurally invalid configuration prevents startup.

Validation includes:

* unique lower-case kebab-case route and destination keys;
* valid public and catch-all patterns;
* reserved diagnostic prefix protection;
* explicit supported HTTP methods;
* unique endpoint method/pattern combinations;
* known access policies and statuses;
* enabled destination address requirements;
* HTTP/HTTPS-only destination URIs;
* no destination user-info, query, or fragment components;
* destination timeouts between 1 and 300 seconds.

A structurally valid but missing/disabled destination fails safely at request time with a generic `503 destination_unavailable` response.

## Forwarding behavior

For an active route, the gateway:

* preserves method, path, query, and body;
* excludes hop-by-hop headers;
* forwards regular headers and trace context;
* overwrites `X-Gateway-Route-Key` with the configured route key;
* forwards canonical correlation headers;
* applies the destination timeout;
* returns downstream status, allowed headers, and body.

The gateway does not deserialize financial payloads or calculate values.

## Safe public errors

| Condition | HTTP | Code |
| --- | ---: | --- |
| route placeholder | 501 | `route_not_active` |
| destination unavailable | 503 | `destination_unavailable` |
| transport failure | 503 | `destination_unavailable` |
| destination timeout | 504 | `destination_timeout` |

Errors include a stable type, title, status, code, generic detail, and correlation identifier.

They exclude internal destination keys, internal addresses, request bodies, credentials, transaction input, receipt/OCR content, and LLM data.

## Correlation and tracing

The gateway:

* accepts a valid `correlationId`;
* accepts `X-Correlation-Id` as compatibility input;
* generates a GUID when needed;
* stores the resolved value in `HttpContext.Items`;
* returns both correlation headers;
* forwards both correlation headers downstream;
* adds correlation and trace values to technical logging scope.

Incoming `traceparent` is not transformed by business code. Standard .NET HTTP diagnostics can manage outgoing trace context.

## Access policies

Route access policies are:

* `public`;
* `authenticated`;
* `admin`.

The central security boundary evaluates the route policy before dispatch. Final token validation and role enforcement belong to the gateway access-control task.

## Health and diagnostics

| Endpoint | Purpose |
| --- | --- |
| `GET /health` | framework health baseline |
| `GET /health/live` | process liveness |
| `GET /health/ready` | route/destination configuration summary |
| `GET /gateway/info` | compact gateway information |
| `GET /gateway/status` | safe route and destination counts |
| `GET /gateway/routes` | sanitized public route descriptors |

`/gateway/routes` excludes internal destination keys and addresses. Diagnostics never expose user, financial, receipt, OCR, AI, token, or secret data.

## Verification

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```

Tests cover route/destination validation, safe route descriptors, correlation, access-policy hooks, rate limiting, placeholder behavior, missing destination behavior, and downstream forwarding.

## Change rules

Update the routing documents when public route groups, service ownership, access-policy classification, activation rules, destination validation, forwarding behavior, or safe errors change.

Do not place environment-specific production addresses or secrets in architecture documentation.

## Out of scope

* domain business logic;
* direct Elasticsearch access;
* OCR/LLM execution;
* production service discovery and load balancing;
* retries and circuit breakers;
* final JWT validation;
* monitoring exporter configuration.
