# API Gateway Verification Checklist

## Purpose

Use this checklist to verify the Public API Gateway routing foundation and FIN-71 route/destination configuration.

Use synthetic data only. Never use production access tokens, personal data, financial input, receipts, OCR output, AI prompts, or production destination addresses.

## Automated verification

Run:

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```

Automated checks cover:

* gateway startup and `/health`;
* configured public route count and ownership;
* sanitized `/gateway/routes` output;
* absence of internal destination keys in public route metadata;
* readiness route/destination counts;
* correlation generation and propagation;
* W3C trace-context handling;
* gateway access-policy headers;
* route and destination configuration validation;
* duplicate route and unsafe destination rejection;
* explicit HTTP method requirements;
* active request forwarding of method, path, query, body, and correlation headers;
* trusted overwrite of `X-Gateway-Route-Key`;
* safe placeholder `501 route_not_active` responses;
* safe missing/disabled destination `503 destination_unavailable` responses;
* safe transport-failure responses without request or internal-host leakage;
* rate limiting and health exclusions;
* absence of direct Elasticsearch client references.

## Build and formatting

```bash
dotnet restore backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
dotnet build backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj --no-restore --configuration Release
dotnet format backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj --verify-no-changes --verbosity diagnostic
```

Verify:

* restore succeeds;
* Release build succeeds;
* format verification reports no changes;
* CI detects the test project;
* test execution is not skipped;
* TRX results are uploaded.

## Manual diagnostics

Run the gateway:

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Check:

```bash
curl -i http://localhost:5000/health
curl -i http://localhost:5000/health/live
curl -i http://localhost:5000/health/ready
curl -i http://localhost:5000/gateway/info
curl -i http://localhost:5000/gateway/status
curl -i http://localhost:5000/gateway/routes
```

Verify:

* health and diagnostic endpoints respond;
* correlation headers are present;
* diagnostics contain technical summaries only;
* route metadata includes public patterns, owners, policies, status, and methods;
* route metadata excludes `internalDestination` and all base addresses;
* no secrets, tokens, personal data, financial data, receipt content, OCR output, or AI content is exposed.

## Route groups

Confirm the public map contains:

* `/auth` -> Identity/Auth Service;
* `/users/me` -> Profile Service;
* `/categories` -> Category Service;
* `/transactions/intake` -> Transaction Intake Service;
* `/transactions/drafts/{id}/confirm` -> Transaction Intake Service;
* `/receipts` -> Receipt File Intake Service;
* `/analytics` -> Analytics Service;
* `/score` -> Financial Score Service;
* `/recommendations` -> Recommendation Service;
* `/notifications` -> Notification Service;
* `/admin/monitoring` -> Monitoring Admin Service.

## Placeholder route

```bash
curl -i http://localhost:5000/categories
```

Verify:

* HTTP status is 501;
* response code is `route_not_active`;
* `X-Gateway-Access-Policy` is `authenticated`;
* `X-Gateway-Security-Mode` is `placeholder`;
* response contains a correlation identifier;
* response does not contain `category-service`, an internal hostname, service implementation metadata, or domain data.

## Active route with unavailable destination

In a test-only configuration, set a route to `active` while leaving its destination missing or disabled.

Verify:

* HTTP status is 503;
* response code is `destination_unavailable`;
* response is `application/problem+json`;
* `Cache-Control` is `no-store`;
* response contains a correlation identifier;
* response does not expose the destination key, internal host, base address, request body, or credentials.

## Active dispatch

In a test-only configuration, activate a route and point it to a synthetic local HTTP handler.

Verify:

* HTTP method is preserved;
* path and query string are preserved;
* request body is preserved;
* hop-by-hop headers are removed;
* `X-Gateway-Route-Key` is overwritten with the configured route key;
* correlation headers are forwarded;
* downstream status, allowed headers, and body are returned;
* timeout configuration is applied.

## Configuration review

Routes must use:

* unique lower-case kebab-case keys;
* valid non-diagnostic public paths;
* valid catch-all patterns;
* explicit methods;
* known access policies;
* `placeholder` or `active` status;
* an owning service and destination key.

Destinations must use:

* unique lower-case kebab-case keys;
* HTTP/HTTPS absolute addresses;
* no user-info, query, or fragment in base addresses;
* a base address when enabled;
* timeouts from 1 to 300 seconds.

## Architecture boundaries

Confirm:

* no Elasticsearch package or connection belongs to the gateway;
* no gateway code reads or writes service-owned indices;
* no financial calculations or business validation are implemented in routing;
* no OCR/LLM call occurs during ordinary proxy dispatch;
* RabbitMQ domain-event handling remains in owning services;
* public errors do not expose internal configuration.

## Review result template

```text
Date:
Reviewer:
Branch/commit:
CI run:
Restore: PASS/FAIL
Release build: PASS/FAIL
Format: PASS/FAIL
Automated tests: PASS/FAIL
Route configuration validation: PASS/FAIL
Destination configuration validation: PASS/FAIL
Sanitized route catalog: PASS/FAIL
Placeholder failure: PASS/FAIL
Unavailable destination failure: PASS/FAIL
Active dispatch: PASS/FAIL
Correlation/trace propagation: PASS/FAIL
Storage boundary: PASS/FAIL
Sensitive-data exposure check: PASS/FAIL
Notes:
```

## Related documents

* `docs/engineering/gateway-route-groups-and-destinations.md`
* `docs/engineering/api-gateway-routing-foundation.md`
* gateway-local `README.md`
* Jira FIN-71
