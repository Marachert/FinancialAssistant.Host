# Backend

Backend workspace.

## Structure

```text
templates/service-template/
gateways/public-api-gateway/
shared/building-blocks/
shared/contracts/
services/
```

## Rules

- .NET 8 services.
- REST for public API calls.
- RabbitMQ for async events.
- Elasticsearch namespaces are service-owned.
- PostgreSQL is the preferred authoritative durability target; existing in-memory
  adapters and Elasticsearch templates are not a completed migration.

Read the [current implementation map](../docs/architecture/current-implementation.md)
and [storage policy](../docs/architecture/storage-policy.md) before changing persistence.

Operational visibility is owned by
`backend/services/monitoring/` and exposes aggregate operational data only.

Append-only privacy-safe event traces are owned by `backend/services/audit/`.

Allowlisted role-controlled internal tools are owned by
`backend/services/mcp/`. MCP uses service APIs only and has no direct database
or Elasticsearch access.
