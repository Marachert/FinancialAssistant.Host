# FinancialAssistant.PublicApiGateway

Initial .NET 8 public API Gateway host for Financial Assistant.

## Responsibility

This gateway is the public REST entry point for mobile, web, and admin clients.

It is responsible for hosting the public HTTP boundary, basic health checks, route catalog, and later forwarding/correlation middleware.

It is not responsible for business calculations, service-owned storage access, or full authentication flows.

## Current endpoints

```text
GET /
GET /health
GET /gateway/info
GET /gateway/routes
```

## Public route groups

The route catalog is configured in:

```text
appsettings.json
Gateway:RouteMap:Routes
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

The placeholder endpoints return HTTP 501 until FIN-260 adds request handling to configured internal service destinations.

## Local run

From the repository root:

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Then verify:

```bash
curl http://localhost:5000/health
curl http://localhost:5000/gateway/info
curl http://localhost:5000/gateway/routes
curl http://localhost:5000/categories
```

The actual local URL can differ depending on local ASP.NET Core settings.

## Boundary rules

- Gateway route map defines public API shape and intended service ownership.
- Gateway placeholders must not implement domain behavior.
- Gateway must not read or write service-owned storage directly.
- Auth, profile, transaction, receipt, analytics, score, recommendation, notification, and monitoring behavior belongs to dedicated services.

## Follow-up subtasks

- FIN-260 — implement forwarding foundation;
- FIN-261 — add correlation and tracing propagation;
- FIN-262 — add gateway security boundary placeholders;
- FIN-263 — add gateway health and diagnostics endpoints;
- FIN-264 — document gateway routing foundation;
- FIN-265 — add gateway tests and verification checklist;
- FIN-266 — review API Gateway foundation.
