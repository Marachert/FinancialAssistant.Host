# Service Health and Readiness Baseline

Status: Implemented for the POC

Related Jira: FIN-192

## Purpose

Every Financial Assistant HTTP host exposes one predictable, privacy-safe
health contract. The contract is implemented by
`FinancialAssistant.Shared.Observability`, so service owners add only checks
for required dependencies that they own.

Health output is operational evidence. It is never a source of financial truth
and cannot mutate transactions, balances, limits, reports, scores, or confirmed
financial entities.

## Endpoints

| Endpoint | Meaning | Allowed work |
| --- | --- | --- |
| `GET /health/live` | The process and request pipeline can run | The shared `self` check only |
| `GET /health/ready` | The host can serve its core responsibility | `self` plus required configuration and required dependencies |
| `GET /health` | Compatibility and operator detail | All registered checks |

All hosts call `AddFinancialAssistantHealthChecks()` and
`MapFinancialAssistantHealthEndpoints()`. The shared registration tags `self`
with `live` and `ready`. Service-owned readiness checks use the `ready` tag.

Liveness must never call PostgreSQL, RabbitMQ, Elasticsearch, object storage,
another service, OCR, an LLM, or an exporter. A dependency belongs in readiness
only when the component cannot serve its core contract without it. Optional AI,
OCR, recommendation, notification, analytics, search, and telemetry paths are
reported through the readiness dashboard without making unrelated services
unready.

## Response contract

All three endpoints return the same JSON shape:

```json
{
  "status": "healthy",
  "service": "FinancialAssistant.Expense.Api",
  "environment": "Production",
  "checkedAtUtc": "2026-09-04T15:00:00Z",
  "durationMilliseconds": 1,
  "checks": [
    {
      "name": "self",
      "status": "healthy",
      "durationMilliseconds": 1,
      "errorCategory": null
    }
  ]
}
```

Statuses are `healthy`, `degraded`, or `unavailable`. Healthy and degraded
reports use HTTP 200; unavailable reports use HTTP 503. Responses set
`Cache-Control: no-store`.

Check descriptions, exception messages, exception data, dependency response
bodies, addresses, connection strings, credentials, identifiers, financial
values, receipt/OCR content, prompts, completions, and provider payloads are
never returned. Failures expose only `timeout` or `check_failed`.

## Required dependency rules

1. The owning service decides whether a dependency is required for its core
   responsibility.
2. Required configuration and dependencies are tagged `ready`; optional paths
   are observed by Monitoring Service and do not fail service readiness.
3. A readiness check has a bounded timeout and a bounded technical name.
4. A check verifies availability, not business data, queue messages, documents,
   user records, or financial correctness.
5. Readiness must not create accounts, provision infrastructure, send alerts,
   invoke paid providers, or repair data.

## Readiness dashboard

`GET /admin/monitoring` remains role-restricted and returns only
`aggregate-operational-only` data. It shows:

- generated time and overall status;
- component totals by healthy, degraded, unavailable, and not-configured state;
- allowlisted service readiness state, probe latency, check time, and safe error
  category;
- aggregate RabbitMQ queue depth and consumers;
- aggregate Elasticsearch color, node count, and active shards;
- existing bounded AI/OCR cost, parsing-quality, and UI-funnel counters.

Dashboard status uses explicit worst-state rules:

- `healthy`: every configured component is healthy;
- `degraded`: at least one component is degraded or not configured, and none is
  unavailable;
- `unavailable`: at least one configured component is unavailable, an unknown
  state is received, or no component can be evaluated.

The dashboard is diagnostic, not a release approval by itself. First-user POC
readiness still requires the repository release gates, approved-host evidence,
and the remaining P8/P9 work.

## Cost boundary

The baseline uses ASP.NET Core health checks, the existing Monitoring Service,
and local JSON output. It enables no paid monitoring account, exporter, hosted
dashboard, alert destination, or additional provider credit. Any future
external telemetry or alerting requires separate privacy, retention, credential,
availability, and maximum-spend approval.

## Verification

Regression coverage verifies the shared registration tags, response schema,
sensitive-detail suppression, status policy, gateway and service endpoints, and
repository-wide use of the shared extensions. Synthetic checks are sufficient;
verification does not contact live infrastructure or paid providers.
