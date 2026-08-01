# Shared Testing Utilities

This folder is reserved for deterministic test helpers that can be reused across backend test projects.

## Testcontainers baseline

`FinancialAssistant.Shared.Testing` references the official Testcontainers for .NET 4.13.0 modules for Elasticsearch, RabbitMQ, Redis, and MinIO. `FinancialAssistant.Shared.Testing.Tests` verifies the factory contract, pinned image policy, and synthetic credential policy without starting Docker.

The factory creates a configured container but does not start it. A service test owns the lifecycle of only the dependencies it needs:

```csharp
var factory = new FinancialAssistantTestcontainerFactory();
await using var redis = factory.CreateRedis();

await redis.StartAsync();
var connectionString = redis.GetConnectionString();
```

Use the returned connection string or endpoint only inside the owning service's Infrastructure integration tests. Put xUnit fixture or collection lifecycle code in the service test project so unrelated suites do not share mutable container state.

Pinned baseline images:

* `elasticsearch:8.15.3`
* `rabbitmq:3.13.7-management`
* `redis:7.4.7-alpine`
* `minio/minio:RELEASE.2025-09-07T16-13-09Z`

A developer machine or CI runner must provide a Docker-compatible runtime. Testcontainers uses random host ports and removes its resources after disposal. Tests must use public development images, synthetic data, and the built-in synthetic credentials; they must never connect to production networks or paid external providers. A failed container startup is an integration-test failure, not a reason to fall back to a production endpoint.

Allowed examples:

* synthetic test-data builders;
* deterministic clocks and identifier generators;
* fake message transports and provider adapters;
* reusable assertion helpers;
* privacy-safe log and contract test utilities;
* local integration-test infrastructure helpers.

Not allowed:

* production runtime dependencies;
* real user, credential, transaction, receipt, OCR, or LLM data;
* shared mutable fixtures that make tests order-dependent;
* service-owned domain logic hidden inside test helpers;
* network calls to production providers;
* secrets or environment-specific credentials.

Test helpers must preserve service ownership. A helper may provide a test container, fake transport, or synthetic fixture, but each service test suite remains responsible for verifying its own domain rules and persistence behavior.
