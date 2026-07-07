# API Gateway Verification Checklist

## Purpose

This checklist verifies the Public API Gateway foundation implemented under FIN-15.

Use synthetic values only. Do not use real access tokens, user data, financial data, receipts, OCR output, AI prompts, or production destination addresses.

## Automated coverage

Test project:

```text
backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/
```

Run:

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```

Automated checks:

- gateway host starts through `WebApplicationFactory`;
- `/health` returns HTTP 200 and healthy status;
- `/gateway/routes` exposes the expected configured route groups;
- `/health/ready` confirms route and destination configuration is loaded;
- a missing correlation id is generated and returned in both supported headers;
- a supplied correlation id is returned unchanged in headers and payload;
- an incoming W3C `traceparent` trace id is visible through gateway diagnostics;
- a placeholder route returns HTTP 501 with safe route ownership metadata;
- route access-policy headers are emitted in placeholder mode;
- the gateway assembly has no direct Elasticsearch client reference.

## Build and formatting

From the repository root:

```bash
dotnet restore backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
dotnet build backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj --no-restore --configuration Release
dotnet format backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj --verify-no-changes --verbosity diagnostic
```

Expected:

- restore succeeds;
- build succeeds with no errors;
- format verification reports no changes;
- GitHub Actions detects the test project through `<IsTestProject>true</IsTestProject>`;
- CI test step runs instead of being skipped;
- a TRX test result artifact is uploaded.

## Manual endpoint verification

Run the gateway:

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

The local URL may differ depending on ASP.NET Core settings.

### Health and diagnostics

```bash
curl -i http://localhost:5000/health
curl -i http://localhost:5000/health/live
curl -i http://localhost:5000/health/ready
curl -i http://localhost:5000/gateway/info
curl -i http://localhost:5000/gateway/status
```

Verify:

- responses are successful;
- correlation response headers are present;
- diagnostics contain technical summaries only;
- no destination address, user data, financial data, receipt data, OCR output, or AI content is exposed.

### Route map

```bash
curl -i http://localhost:5000/gateway/routes
```

Verify route groups and intended owners:

- `/auth` -> Auth Service;
- `/users/me` -> Profile Service;
- `/categories` -> Category Service;
- `/transactions/intake` -> Transaction Intake Service;
- `/transactions/drafts/{id}/confirm` -> Transaction Intake Service;
- `/receipts` -> Receipt File Intake Service;
- `/analytics` -> Analytics Service;
- `/score` -> Financial Score Service;
- `/recommendations` -> Recommendation Service;
- `/notifications` -> Notification Service;
- `/admin/monitoring` -> Monitoring Admin Service.

### Correlation generation

```bash
curl -i http://localhost:5000/health/live
```

Verify:

- `correlationId` is present;
- `X-Correlation-Id` is present;
- both values match;
- generated value is a GUID.

### Correlation propagation

```bash
curl -i -H "correlationId: fin-265-synthetic-correlation" http://localhost:5000/gateway/info
```

Verify the same synthetic value appears in:

- `correlationId` response header;
- `X-Correlation-Id` response header;
- response payload.

### Trace context

```bash
curl -i -H "traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" http://localhost:5000/gateway/info
```

Verify the diagnostic `traceId` is:

```text
4bf92f3577b34da6a3ce929d0e0e4736
```

### Placeholder route

```bash
curl -i http://localhost:5000/categories
```

Verify:

- HTTP status is 501;
- route key is `categories`;
- service owner is `Category Service`;
- access policy is `authenticated`;
- `X-Gateway-Access-Policy` is `authenticated`;
- `X-Gateway-Security-Mode` is `placeholder`;
- response contains no domain data.

## Architecture boundary verification

The gateway must not reference or initialize Elasticsearch clients.

Automated assembly-reference checks cover the known .NET Elasticsearch client package names. During review, also confirm:

- no Elasticsearch client package is added to the gateway project;
- no Elasticsearch connection configuration is added to gateway settings;
- no gateway component reads or writes service-owned indices;
- storage access remains behind owning service APIs.

## Review result template

```text
Date:
Reviewer:
Branch/commit:
CI run:
Restore: PASS/FAIL
Build: PASS/FAIL
Format: PASS/FAIL
Automated tests: PASS/FAIL
Health endpoints: PASS/FAIL
Route map: PASS/FAIL
Correlation: PASS/FAIL
Trace context: PASS/FAIL
Storage boundary: PASS/FAIL
Sensitive-data exposure check: PASS/FAIL
Notes:
```

## Related documents

- `docs/engineering/api-gateway-routing-foundation.md`
- gateway-local `README.md`
- Jira FIN-15
- Jira FIN-265
- Jira FIN-266
