# Web Admin

Web workspace for internal Financial Assistant monitoring and administration tools.

Canonical monitoring application source boundary:

```text
web-admin/monitoring-ui/
```

## Planned responsibilities

- Operational service-health views.
- Sanitized gateway route diagnostics.
- Safe processing and failure summaries.
- Admin-protected support tools.

The monitoring UI calls approved backend APIs through the Public API Gateway. It must not access Elasticsearch, RabbitMQ, Redis, MinIO, service storage, or sensitive user data directly.
