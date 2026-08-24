# Backend

Backend workspace.

## Structure

```text
templates/service-template/
shared/building-blocks/
shared/contracts/
services/
```

## Rules

- .NET 8 services.
- REST for public API calls.
- RabbitMQ for async events.
- Elasticsearch namespaces are service-owned.

Operational visibility is owned by
`backend/services/monitoring/` and exposes aggregate operational data only.

Append-only privacy-safe event traces are owned by `backend/services/audit/`.

Allowlisted role-controlled internal tools are owned by
`backend/services/mcp/`. MCP uses service APIs only and has no direct database
or Elasticsearch access.
