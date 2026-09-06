# Web Admin

Web workspace for internal Financial Assistant monitoring and administration tools.

Canonical monitoring application source boundary:

```text
web-admin/monitoring-ui/
```

## Responsibilities

- Operational service-health views.
- Sanitized gateway route diagnostics.
- Safe processing and failure summaries.
- Admin-protected support tools.

FIN-194 implements the React monitoring baseline under `monitoring-ui`.
User-support lookup is deliberately disabled pending its approved privacy scope.

The monitoring UI calls approved backend APIs through the Public API Gateway. It must not access Elasticsearch, RabbitMQ, Redis, MinIO, service storage, or sensitive user data directly.
