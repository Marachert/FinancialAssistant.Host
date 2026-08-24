# Monitoring Admin API v1

Related Jira: FIN-37.

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

- generated UTC timestamp and overall `healthy` or `degraded` state;
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
- an unavailable probe produces a degraded snapshot with a safe category such
  as `timeout`, `transport`, `http_status`, or `invalid_response`.

Admin responses must use `Cache-Control: no-store` at the deployment boundary.
Visual dashboard composition is owned by FIN-194; this contract supplies its
safe operational data.
