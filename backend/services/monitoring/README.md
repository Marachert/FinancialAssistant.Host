# Monitoring Service

Related Jira: FIN-37.

The Monitoring Service provides the privacy-safe operational data contract used
by the P8 admin experience. It is a technical utility service and is never an
authoritative source for transactions, balances, limits, scores, or other user
financial data.

## Capabilities

- `/health/live` reports process liveness.
- `/health/ready` validates the monitoring target and allowlist configuration.
- `GET /admin/monitoring` returns an admin-only aggregate snapshot through the
  Public API Gateway.
- configured probes record only service status, latency, safe error category,
  RabbitMQ queue/consumer counts, and Elasticsearch cluster counts.
- authenticated internal signal endpoints aggregate numeric AI usage/cost,
  parsing quality, and UI funnel counters.

The signal contracts deliberately have no arbitrary labels, user identifiers,
financial values, receipt/OCR text, prompts, provider responses, or request
payload fields. Sources and UI stages must match configuration allowlists.

## Required secrets

Set these through environment-backed configuration; do not store them in the
repository:

```text
Monitoring__Gateway__SharedSecret
Monitoring__Signals__SharedSecret
Monitoring__RabbitMq__Username
Monitoring__RabbitMq__Password
```

Both shared secrets must contain at least 32 characters. The gateway secret
must match `Gateway__DownstreamAuthentication__SharedSecret`. RabbitMQ
credentials are optional at startup; when absent, the dashboard reports that
probe as `not_configured` without exposing configuration detail.

## Boundaries

The initial store is process-local and suitable for PoC aggregation and tests.
Durable operational indices, retention, visual admin UI, alerting, and support
workflows belong to later P8 tickets. The API contract remains aggregate-only
when those adapters are added.

Full API and safety rules are in
`docs/api/monitoring-admin-v1.md`.
