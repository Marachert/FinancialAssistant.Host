# FinancialAssistant.PublicApiGateway

Initial .NET 8 public API Gateway host for Financial Assistant.

## Responsibility

This gateway is the public REST entry point for mobile, web, and admin clients.

It is responsible for hosting the public HTTP boundary, basic health checks, and later routing/correlation middleware.

It is not responsible for business calculations, service-owned storage access, or full authentication flows.

## Current endpoints

```text
GET /
GET /health
GET /gateway/info
```

## Local run

From the repository root:

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Then verify:

```bash
curl http://localhost:5000/health
curl http://localhost:5000/gateway/info
```

The actual local URL can differ depending on local ASP.NET Core settings.

## Follow-up subtasks

- FIN-259 — configure gateway route map;
- FIN-260 — implement forwarding foundation;
- FIN-261 — add correlation and tracing propagation;
- FIN-262 — add gateway security boundary placeholders;
- FIN-263 — add gateway health and diagnostics endpoints;
- FIN-264 — document gateway routing foundation;
- FIN-265 — add gateway tests and verification checklist;
- FIN-266 — review API Gateway foundation.
