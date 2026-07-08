# Shared Backend Source Boundaries

`backend/shared` contains narrowly scoped technical assets that may be reused by multiple backend services without creating shared business ownership.

## Canonical folders

```text
backend/shared/building-blocks/  Small technical helpers with no domain or storage ownership
backend/shared/contracts/        Stable versioned integration contracts
backend/shared/elasticsearch/    Reusable Elasticsearch client and mapping utilities
backend/shared/testing/          Deterministic test helpers and synthetic fixtures
```

## Ownership rules

Shared source may contain technical utilities only. It must not contain:

* financial business rules or calculations;
* shared repositories for service-owned data;
* cross-service domain entities used as persistence models;
* direct access to another service's Elasticsearch indices;
* RabbitMQ handlers that own a business capability;
* OCR or LLM decisions treated as authoritative data.

Each microservice owns its domain model, persistence mappings, index aliases, write operations, and business events. Shared components may simplify technical integration but may not move ownership out of the service.

## Gateway and service layout

The canonical gateway root is plural:

```text
backend/gateways/
```

Do not create the obsolete parallel path `backend/gateway/`. Backend business capabilities live under:

```text
backend/services/
```

## Adding shared code

Before adding a shared project or helper:

1. confirm that at least two services need the same technical behavior;
2. verify that the code has no service-owned business rules or persistence ownership;
3. keep the public API minimal and versioned when it is a contract;
4. add deterministic tests;
5. document consumers and upgrade expectations.

Prefer duplication over a misleading shared abstraction when ownership is unclear.
