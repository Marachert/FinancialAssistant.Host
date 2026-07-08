# FinancialAssistant.PublicApiGateway

.NET 8 public REST entry point for Financial Assistant mobile, web, and admin clients.

Canonical routing documentation:

```text
docs/engineering/gateway-route-groups-and-destinations.md
docs/engineering/api-gateway-routing-foundation.md
docs/engineering/api-gateway-verification-checklist.md
docs/engineering/safe-operational-log-policy.md
```

## Responsibility

The gateway owns the public HTTP boundary, route matching, destination dispatch, correlation, gateway security hooks, rate limiting, and safe technical errors.

It does not own identity/profile persistence, transaction or receipt business logic, financial calculations, OCR, LLM workflows, service-owned Elasticsearch data, or domain events.

## Technical endpoints

```text
GET /
GET /health
GET /health/live
GET /health/ready
GET /gateway/info
GET /gateway/status
GET /gateway/routes
```

Diagnostics expose safe technical summaries only. They must not return destination URLs, secrets, tokens, personal data, financial data, receipt text, OCR output, or AI prompt/response content.

`GET /gateway/routes` returns sanitized route descriptors. Internal destination keys and base addresses are deliberately excluded.

## Route and destination configuration

```text
Gateway:RouteMap:Routes
Gateway:DestinationMap:Destinations
```

Each route explicitly defines:

* public pattern and optional catch-all pattern;
* HTTP methods;
* owning backend service;
* internal destination key;
* access policy: `public`, `authenticated`, or `admin`;
* status: `placeholder` or `active`.

Each destination defines:

* stable destination key;
* internal HTTP/HTTPS base address;
* enabled flag;
* request timeout between 1 and 300 seconds.

Repository defaults keep routes as placeholders and destinations disabled until the owning service contract and security integration are ready.

## Startup validation

Startup fails for duplicate or malformed route keys, reserved public paths, malformed catch-all patterns, missing owners/destinations, unknown policies/statuses, missing/duplicate methods, duplicate endpoint signatures, unsafe destination addresses, or invalid timeout values.

This validation happens while endpoint mapping is built, before the gateway accepts traffic.

## Dispatch flow

```text
request
-> correlation middleware
-> rate limiting
-> route access-policy boundary
-> route catalog
-> destination catalog
-> owning service REST API
```

For active routes, the dispatcher preserves method, path, query, body, normal headers, and correlation context. It removes hop-by-hop headers and overwrites `X-Gateway-Route-Key` with the trusted configured route key.

The gateway forwards payloads without adding domain transformations.

## Safe failures

| Condition | Status | Code |
| --- | ---: | --- |
| route is still a placeholder | 501 | `route_not_active` |
| destination missing or disabled | 503 | `destination_unavailable` |
| downstream transport failure | 503 | `destination_unavailable` |
| downstream timeout | 504 | `destination_timeout` |

Public errors do not include internal destination keys, internal hosts, service implementation metadata, request bodies, credentials, transaction input, receipt text, OCR content, or LLM content.

## Correlation

The gateway resolves or creates `correlationId`, returns both `correlationId` and `X-Correlation-Id`, and forwards both downstream. Incoming W3C trace context remains a technical header and can be handled by standard .NET diagnostics.

## Operational logging

Gateway runtime events use the source-generated `GatewayOperationalLog` catalog with stable event IDs in the `1000–1999` range.

The correlation scope contains only `CorrelationId`, `TraceId`, and `RequestMethod`. Event fields are limited to route/destination keys, access policy, normalized authentication result, HTTP status, latency, timeout, failure type name, and response-start state.

Never log raw request or response bodies, query values, headers, passwords, tokens, user/session IDs, role lists, transaction data, receipt content, OCR output, Elasticsearch documents, RabbitMQ payloads, or AI prompt/response data. Exception objects are not passed to operational logs; only `exception.GetType().Name` may be recorded as `FailureType`.

The complete policy and review checklist are in `docs/engineering/safe-operational-log-policy.md`.

## Local run

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Useful checks:

```bash
curl -i http://localhost:5000/health
curl -i http://localhost:5000/health/ready
curl -i http://localhost:5000/gateway/status
curl -i http://localhost:5000/gateway/routes
curl -i http://localhost:5000/categories
```

Default route calls return safe `501 route_not_active` responses until a route is activated.

## Tests

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```

Coverage includes startup/configuration validation, sanitized route descriptors, correlation behavior, route security hooks, rate limiting, safe placeholder/destination failures, active downstream dispatch, and operational log event-contract enforcement.

## Boundary rules

* The gateway must never query service-owned Elasticsearch indices.
* The gateway must not store business entities.
* The gateway must not calculate financial values.
* Business validation belongs to the owning service.
* RabbitMQ domain events are produced and consumed by services, not by the gateway proxy.
* OCR and LLM integrations stay outside normal route dispatch.
