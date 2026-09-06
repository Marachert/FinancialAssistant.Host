# Monitoring Admin API v1

Related Jira: FIN-37, FIN-192, FIN-194.

## Purpose

The Monitoring Admin API gives authorized operators a single aggregate view of
PoC service readiness, dependency health, AI/OCR operational cost and quality,
and client funnel counts. It never returns user-level or financial records.

## Admin snapshot

```http
GET /admin/monitoring
X-Gateway-Authentication: <environment secret>
X-Gateway-Roles: admin
```

The Public API Gateway validates the access token and `admin` role, removes
client-controlled gateway headers, and injects its environment-provided shared
secret. Monitoring Service validates the secret with a fixed-time comparison
and independently requires the forwarded admin role.

The response contains:

- generated UTC timestamp and overall `healthy`, `degraded`, or `unavailable`
  state;
- component totals by healthy, degraded, unavailable, and not-configured state;
- allowlisted service name, health state, probe latency, timestamp, and safe
  error category;
- RabbitMQ aggregate queue depth and consumer count;
- Elasticsearch color, node count, and active-shard count;
- aggregate numeric AI request/token/cost counters;
- aggregate parsing success/review/failure counters;
- allowlisted UI funnel stage totals and completion percentages;
- fixed data classification `aggregate-operational-only`.

No raw dependency response body is retained or returned. Hostnames, connection
strings, credentials, exception messages, raw queue messages, Elasticsearch
documents, user identifiers, financial amounts, notes, receipt/OCR content,
prompts, or model/provider responses are prohibited.

## Internal signals

Approved services submit bounded non-negative counters using an independent
environment secret:

```http
POST /internal/monitoring/signals/ai-usage
POST /internal/monitoring/signals/parsing-quality
POST /internal/monitoring/signals/ui-funnel
X-Monitoring-Authentication: <environment secret>
```

Source services and UI stages must match explicit configuration allowlists.
There is no arbitrary dimension, label, message, metadata, or payload field.
Rejected signals return a generic safe problem response without echoing input.

## Failure behavior

- `401`: trusted gateway or service authentication is missing/invalid;
- `403`: gateway request lacks the admin role;
- `400`: source, stage, relationship, or numeric bound is invalid;
- degraded or not-configured probes produce a degraded snapshot when no
  component is unavailable;
- an unavailable or unknown probe produces an unavailable snapshot with a safe
  category such as `timeout`, `transport`, `http_status`, or
  `invalid_response`.

Admin responses must use `Cache-Control: no-store` at the deployment boundary.
Visual dashboard composition is owned by FIN-194; this contract supplies its
safe operational data.

The shared endpoint schema, required-dependency rules, and full dashboard
requirements are defined in
[Service Health and Readiness Baseline](../engineering/service-health-and-readiness.md).

## Recent processing jobs (FIN-194)

`GET /admin/monitoring/jobs` uses the same trusted-gateway and admin-role checks
and returns `Cache-Control: no-store`. It is available through the existing
gateway catch-all. Response classification is `bounded-operational-metadata-only`;
the aggregate snapshot above retains its existing classification and schema.

The response contains generated UTC time, capacity (200), retention hours (24),
and recent job observations. Fields are an opaque random operation UUID, service,
kind (`ai`, `ocr`, `notification`), state (`queued`, `running`, `succeeded`,
`failed`), positive monotonic revision, server-observed UTC time, and an optional
closed failure category. This UUID must be generated for operations; it must not
reuse user, receipt, financial record, token, or other private identifiers.

`POST /internal/monitoring/signals/jobs` requires the independent service secret.
Its request is `operationId`, `sourceService`, `kind`, `state`, `revision`, and
`errorCategory`. Source must be allowlisted and match kind ownership:
AI = `ai-orchestration`, OCR = `receipt-processing`, notification =
`recommendations-notifications`. Categories are `timeout`, `transport`,
`provider_unavailable`, `invalid_result`, or `policy_rejected`; a category is
required exactly for failed state. Invalid input is 400, missing service trust
is 401, conflicting same-revision evidence is 409, accepted signals are 202.

Duplicate revisions are idempotent without extending retention; older revisions
are ignored. Later revisions replace the last observation. At capacity the oldest
observation is evicted; entries expire 24 hours after the last accepted update.
The store is concurrency-protected and process-local: restart loses observations,
replicas are independent, and this is not a durable operational or audit history.

The signal ingestion contract and UI are executable; producer wiring is not
automatically enabled by this ticket. No job or provider response is fetched from
another service's storage. Missing observations remain empty, never demo records.
User-support lookup stays disabled pending a separately approved privacy scope.

Web client setup and security behavior:
[Monitoring Web UI](../../web-admin/monitoring-ui/README.md).
