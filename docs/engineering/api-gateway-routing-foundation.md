# API Gateway Routing Foundation

## Purpose

The Financial Assistant Public API Gateway is the single public REST entry point for mobile, web, and admin clients.

This document describes the routing foundation implemented under FIN-15: route ownership, request dispatch, correlation and trace propagation, access-policy placeholders, health/status endpoints, developer verification, and responsibility boundaries.

The gateway is a technical boundary. It is not a business capability service and must not become a shared business-data layer.

## Location

```text
backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/
```

Main areas:

```text
Program.cs
appsettings.json
Routing/
Observability/
Security/
Diagnostics/
```

Gateway-specific operational details also remain in the project README.

## Responsibility

The gateway owns:

- the public HTTP boundary;
- route matching and route metadata;
- forwarding to configured internal service destinations;
- request correlation handling;
- trace-context propagation;
- central route access-policy hooks;
- lightweight health and safe technical status endpoints;
- consistent gateway-level error responses.

The gateway does not own:

- identity or profile persistence;
- transaction, income, expense, category, receipt, analytics, score, recommendation, or notification data;
- financial calculations;
- transaction validation rules;
- OCR processing;
- LLM processing or recommendation logic;
- service-owned Elasticsearch indices;
- business reporting;
- domain events.

Architecture rule:

> The API Gateway must not query or update Elasticsearch directly. It must call the owning service API.

## Synchronous request flow

1. A client sends an HTTP request to the Public API Gateway.
2. Correlation middleware resolves or generates `correlationId`.
3. The access boundary evaluates the configured route classification.
4. The route catalog selects the public route.
5. The dispatcher resolves the internal destination.
6. The dispatcher forwards the request when the route and destination are active.
7. The owning service performs validation, business logic, persistence, and calculations.
8. The gateway copies the downstream status, allowed headers, and response body back to the client.

The gateway does not transform financial domain payloads or calculate financial values.

## Route map

Routes are configured under:

```text
Gateway:RouteMap:Routes
```

| Public route | Intended owner | Access policy | Foundation status |
| --- | --- | --- | --- |
| `/auth` | Auth Service | `public` | placeholder |
| `/users/me` | Profile Service | `authenticated` | placeholder |
| `/categories` | Category Service | `authenticated` | placeholder |
| `/transactions/intake` | Transaction Intake Service | `authenticated` | placeholder |
| `/transactions/drafts/{id}/confirm` | Transaction Intake Service | `authenticated` | placeholder |
| `/receipts` | Receipt File Intake Service | `authenticated` | placeholder |
| `/analytics` | Analytics Service | `authenticated` | placeholder |
| `/score` | Financial Score Service | `authenticated` | placeholder |
| `/recommendations` | Recommendation Service | `authenticated` | placeholder |
| `/notifications` | Notification Service | `authenticated` | placeholder |
| `/admin/monitoring` | Monitoring Admin Service | `admin` | placeholder |

A route definition contains a route key, public pattern, optional catch-all pattern, intended service owner, internal destination key, access policy, status, and allowed HTTP methods.

Placeholder routes return HTTP 501 and identify the intended service owner without implementing domain behavior.

## Destination map

Destinations are configured under:

```text
Gateway:DestinationMap:Destinations
```

A route can be forwarded only when:

- the route status is `active`;
- the destination exists;
- the destination is enabled;
- the destination address is valid.

Environment-specific destination addresses are configuration details and are not exposed through public status responses.

## Forwarding behavior

For an active route, the dispatcher:

- preserves the HTTP method;
- preserves path and query string;
- streams request content when present;
- excludes hop-by-hop headers;
- forwards normal request headers, including incoming `traceparent`;
- adds `X-Gateway-Route-Key`;
- adds `correlationId` and `X-Correlation-Id`;
- uses the request cancellation signal;
- returns the downstream status, allowed headers, and body.

Destination failures return safe HTTP 503 responses with route and correlation context only.

## Correlation and tracing

Correlation behavior:

- accept `correlationId` when valid;
- accept `X-Correlation-Id` as a compatibility fallback;
- generate a GUID when no valid value is present;
- store the value in `HttpContext.Items`;
- return both correlation headers to the caller;
- propagate both correlation headers downstream;
- add the value to the current .NET `Activity` as tag and baggage;
- include `CorrelationId` and `TraceId` in logger scopes.

Trace behavior:

- incoming `traceparent` is not rewritten by gateway business code;
- it is forwarded as a normal non-hop-by-hop header;
- standard .NET HTTP diagnostics may create outgoing trace context when instrumentation is enabled.

Logging must remain operational and must not include raw user input, transaction amounts, receipt content, OCR output, AI request/response content, or personal financial data.

## Access-policy placeholders

Route access policies are classified as:

- `public`;
- `authenticated`;
- `admin`.

The gateway contains a central `GatewaySecurityBoundary` hook before route dispatch.

Default mode:

```text
Gateway:Security:Mode = placeholder
```

Placeholder mode evaluates the configured policy but does not perform the final identity check. This allows later Auth/Identity work to integrate without changing every route endpoint.

The current hook can produce consistent HTTP 401 and HTTP 403 responses when enforcement is enabled. Production identity validation remains outside FIN-15.

## Health and safe status endpoints

| Endpoint | Purpose | Output boundary |
| --- | --- | --- |
| `GET /health` | framework health baseline | framework health status |
| `GET /health/live` | process liveness | service name, start time, uptime, correlation id |
| `GET /health/ready` | gateway readiness summary | route/destination counts, enabled destination count, security mode, correlation id |
| `GET /gateway/info` | compact gateway information | service, environment, route count, security mode, correlation id, trace id |
| `GET /gateway/status` | safe technical summary | uptime, route summary, destination summary, environment, security mode, correlation id, trace id |
| `GET /gateway/routes` | configured route metadata | route definitions only |

These endpoints are technical diagnostics, not business reporting. They do not expose user data, financial data, receipt content, OCR output, AI content, or environment-specific destination addresses.

Prometheus/OpenTelemetry exporter configuration is outside the FIN-15 routing-foundation scope.

## Configuration sections

```text
Gateway:Correlation
Gateway:Security
Gateway:DestinationMap:Destinations
Gateway:RouteMap:Routes
```

Configuration rules:

- keep production values outside repository defaults;
- use stable route and destination keys;
- do not activate a route before the owning service contract is ready;
- keep access-policy classification explicit;
- keep placeholder behavior safe and predictable.

## Developer verification

From the repository root:

```bash
dotnet restore backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
dotnet build backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj --no-restore --configuration Release
dotnet format backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj --verify-no-changes --verbosity diagnostic
```

Run locally:

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Example checks:

```bash
curl -i http://localhost:5000/health
curl -i http://localhost:5000/health/live
curl -i http://localhost:5000/health/ready
curl -i http://localhost:5000/gateway/info
curl -i http://localhost:5000/gateway/status
curl -i http://localhost:5000/gateway/routes
curl -i -H "correlationId: demo-correlation-id" http://localhost:5000/gateway/status
curl -i -H "traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00" http://localhost:5000/categories
```

Expected results:

- health/status endpoints respond without sensitive domain data;
- responses contain `correlationId` and `X-Correlation-Id`;
- a valid incoming correlation id is reused;
- a missing correlation id is generated;
- placeholder routes return HTTP 501;
- route ownership and access-policy metadata match configuration;
- logs contain correlation and trace scope fields without sensitive payloads.

## Change rules

Update this document when public route groups, ownership, destination activation rules, propagation behavior, access-policy classification, health/status endpoints, responsibility boundaries, or verification commands change.

Do not add transient experiments or environment-specific addresses to this architecture document.

## Out of scope

- production authentication and authorization;
- identity persistence;
- service business logic;
- financial calculations;
- direct Elasticsearch access;
- domain event publication from the gateway;
- OCR or LLM processing;
- production traffic-limit policy;
- production monitoring exporters;
- complete automated gateway test coverage, which is handled by FIN-265.

## Related Jira work

- FIN-15 — Implement API Gateway routing and request foundation
- FIN-258 — Create API Gateway project skeleton
- FIN-259 — Configure gateway route map
- FIN-260 — Implement gateway forwarding foundation
- FIN-261 — Add correlation and tracing propagation
- FIN-262 — Add gateway security boundary placeholders
- FIN-263 — Add gateway health and diagnostics endpoints
- FIN-264 — Document gateway routing foundation
- FIN-265 — Add gateway tests and verification checklist
- FIN-266 — Review API Gateway foundation
