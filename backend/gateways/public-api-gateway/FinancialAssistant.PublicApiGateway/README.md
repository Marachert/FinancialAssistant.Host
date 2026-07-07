# FinancialAssistant.PublicApiGateway

Initial .NET 8 public API Gateway host for Financial Assistant.

## Responsibility

This gateway is the public REST entry point for mobile, web, and admin clients.

It is responsible for hosting the public HTTP boundary, basic health checks, route catalog, request dispatch foundation, correlation middleware, and security boundary placeholders.

It is not responsible for business calculations, service-owned storage access, identity persistence, or full authentication flows.

## Current endpoints

```text
GET /
GET /health
GET /gateway/info
GET /gateway/routes
```

`/gateway/info` returns a safe operational summary with service name, environment, route count, security mode, correlation id, and trace id. It must not return user, financial, OCR, AI prompt, or secret data.

## Public route groups

The route catalog is configured in:

```text
appsettings.json
Gateway:RouteMap:Routes
```

Destination settings are configured in:

```text
appsettings.json
Gateway:DestinationMap:Destinations
```

Correlation settings are configured in:

```text
appsettings.json
Gateway:Correlation
```

Security boundary settings are configured in:

```text
appsettings.json
Gateway:Security
```

| Public route | Service owner | Access policy | Current status |
| --- | --- | --- | --- |
| `/auth` | Auth Service | public | placeholder |
| `/users/me` | Profile Service | authenticated | placeholder |
| `/categories` | Category Service | authenticated | placeholder |
| `/transactions/intake` | Transaction Intake Service | authenticated | placeholder |
| `/transactions/drafts/{id}/confirm` | Transaction Intake Service | authenticated | placeholder |
| `/receipts` | Receipt File Intake Service | authenticated | placeholder |
| `/analytics` | Analytics Service | authenticated | placeholder |
| `/score` | Financial Score Service | authenticated | placeholder |
| `/recommendations` | Recommendation Service | authenticated | placeholder |
| `/notifications` | Notification Service | authenticated | placeholder |
| `/admin/monitoring` | Monitoring Admin Service | admin | placeholder |

Placeholder routes return HTTP 501. To enable a route later, set its status to `active` and enable its destination entry.

## Correlation and tracing

The gateway creates one request correlation boundary for every incoming request:

- accepts `correlationId` when provided by the caller;
- accepts `X-Correlation-Id` as a compatibility header;
- generates a new GUID correlation id when neither header is present or the provided value is invalid;
- writes both `correlationId` and `X-Correlation-Id` to the response;
- stores the resolved correlation id in `HttpContext.Items` for gateway components;
- adds the correlation id to the current .NET `Activity` as tag and baggage;
- uses logger scopes with `CorrelationId` and `TraceId` only.

`traceparent` is not transformed by gateway business code. If the caller sends it, the dispatcher copies it as a normal non-hop-by-hop request header. Standard .NET HTTP diagnostics can also create outgoing trace context when runtime instrumentation is enabled.

Logging rule: gateway logs must stay operational. Do not log raw user input, transaction amounts, receipt text, OCR output, AI prompt/response content, tokens, secrets, or personal financial data.

## Security boundary placeholders

The gateway has a central security boundary hook before route dispatch. Its purpose is to keep public/authenticated/admin route policy handling in one place so FIN-16 and FIN-17 can integrate real authentication and authorization without changing every route endpoint.

Current security mode:

```text
Gateway:Security:Mode = placeholder
```

Placeholder mode behavior:

- evaluates every route access policy before dispatch;
- adds safe route policy response headers when enabled;
- does not validate tokens;
- does not block requests;
- keeps real authentication and authorization implementation out of FIN-15.

Prepared enforcement behavior:

- `public` routes are allowed;
- `authenticated` routes can return HTTP 401 when enforcement is enabled and no authentication header is present;
- `admin` routes can return HTTP 403 when enforcement is enabled and admin placeholder scope is missing;
- responses include only route key, access policy, correlation id, and safe status information.

Important: `enforce` mode is a temporary integration hook only. It is not a production authentication model. Real JWT validation, issuer/audience/signature checks, claim mapping, refresh tokens, and identity persistence belong to Auth/Identity tasks, not this gateway routing story.

## Request dispatch behavior

For active routes, the gateway request dispatcher:

- builds the destination URI from `Gateway:DestinationMap` plus the incoming path and query string;
- preserves HTTP method;
- passes request payload for methods that include a body;
- copies non-hop-by-hop request headers, including incoming `traceparent`;
- adds `X-Gateway-Route-Key`;
- adds canonical `correlationId` and `X-Correlation-Id` headers;
- returns the destination status code, response headers, and response payload.

This is a technical dispatch foundation only. It must not add domain-specific transformations.

## Local run

From the repository root:

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Then verify:

```bash
curl -i http://localhost:5000/health
curl -i http://localhost:5000/gateway/info
curl -i -H "correlationId: demo-correlation-id" http://localhost:5000/gateway/info
curl -i -H "traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00" http://localhost:5000/categories
```

Expected verification:

- responses include `correlationId` and `X-Correlation-Id`;
- `/gateway/info` includes `securityMode`;
- incoming `correlationId` is reused when valid;
- missing correlation id is generated;
- route responses include safe access policy headers when enabled;
- placeholder routes still return HTTP 501 in placeholder security mode;
- logs include correlation and trace scope fields without sensitive payload data.

The actual local URL can differ depending on local ASP.NET Core settings.

## Boundary rules

- Gateway route map defines public API shape and intended service ownership.
- Gateway placeholders must not implement domain behavior.
- Gateway must not read or write service-owned storage directly.
- Gateway must not store identity, profile, transaction, category, receipt, analytics, score, recommendation, or notification data.
- Auth, profile, transaction, receipt, analytics, score, recommendation, notification, and monitoring behavior belongs to dedicated services.
- LLM is not a source of truth for transaction data, calculations, or persistence.

## Follow-up subtasks

- FIN-263 — add gateway health and diagnostics endpoints;
- FIN-264 — document gateway routing foundation;
- FIN-265 — add gateway tests and verification checklist;
- FIN-266 — review API Gateway foundation.
