# .NET 8 Service Template

This folder contains the reusable Clean Architecture baseline for new Financial Assistant backend services.

FIN-51 establishes the five project boundaries and dependency direction. FIN-52 adds the mandatory liveness/readiness health endpoints, local-development OpenAPI, JSON structured logging, correlation identifier middleware, and validated typed configuration.

## Projects

```text
ServiceTemplate.Api/
ServiceTemplate.Application/
ServiceTemplate.Domain/
ServiceTemplate.Infrastructure/
ServiceTemplate.Contracts/
FinancialAssistant.ServiceTemplate.sln
```

| Project | Responsibility |
| --- | --- |
| `ServiceTemplate.Api` | HTTP host and composition root; maps transport concerns to application use cases |
| `ServiceTemplate.Application` | Commands, queries, use-case orchestration, ports, and validation flow |
| `ServiceTemplate.Domain` | Entities, value objects, invariants, and deterministic business rules |
| `ServiceTemplate.Infrastructure` | Elasticsearch, RabbitMQ, external providers, object storage, and other technical adapters |
| `ServiceTemplate.Contracts` | Stable request/response DTOs and versioned integration contracts |

## Dependency direction

Allowed project references:

```text
Api -> Application
Api -> Contracts
Api -> Infrastructure

Infrastructure -> Application
Infrastructure -> Contracts
Infrastructure -> Domain

Application -> Contracts
Application -> Domain

Domain -> no project references
Contracts -> no project references
```

The API project may reference Infrastructure only as the composition root that registers adapters. Business logic must not move into API or Infrastructure.

Forbidden examples:

- Domain referencing Application, Infrastructure, API, provider SDKs, or persistence packages;
- Application referencing Infrastructure or API;
- Contracts referencing service implementation projects;
- shared business repositories or cross-service Elasticsearch document models;
- LLM or OCR output becoming authoritative financial state.

## Build the template

From the repository root:

```bash
dotnet restore backend/templates/service-template/FinancialAssistant.ServiceTemplate.sln
dotnet build backend/templates/service-template/FinancialAssistant.ServiceTemplate.sln --no-restore --configuration Release
```

The five template projects are also included in `FinancialAssistant.Backend.sln`, so normal Backend CI compiles them.

Run the template API in local development:

### Bash, Git Bash, or WSL

```bash
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project backend/templates/service-template/ServiceTemplate.Api/ServiceTemplate.Api.csproj
```

### PowerShell

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/templates/service-template/ServiceTemplate.Api/ServiceTemplate.Api.csproj
```

Use the URL printed by `dotnet run`.

## Cross-cutting baseline

### Health checks

The API exposes:

```text
GET /health/live
GET /health/ready
```

`/health/live` checks that the process and request pipeline are running. It must not depend on Elasticsearch, RabbitMQ, OCR, LLM, or another remote dependency because a liveness failure normally causes a restart.

`/health/ready` validates the mandatory service configuration and is the extension point for dependencies required to serve traffic. A real service should add only its own required readiness checks, such as its service-owned Elasticsearch alias or RabbitMQ connection.

Do not make readiness depend on optional recommendation, analytics, OCR, or LLM providers unless that service cannot perform its core responsibility without them.

### OpenAPI

OpenAPI services are registered for the host, but Swagger middleware is enabled only when:

```text
ASPNETCORE_ENVIRONMENT=Development
```

Local endpoints:

```text
/swagger
/swagger/v1/swagger.json
```

Production exposure requires an explicit security and delivery decision; it must not be enabled by removing the environment guard casually.

### Structured logging

The template clears default providers and configures JSON console logging with:

- UTC timestamps;
- structured scopes;
- a `CorrelationId` scope value for every request;
- log levels controlled by normal .NET configuration.

Logs must remain privacy-safe. Do not log access tokens, raw receipts, OCR text, LLM prompts/responses, personal identities, or financial payloads.

### Correlation identifier

`CorrelationIdMiddleware` accepts the request header:

```text
X-Correlation-ID
```

A supplied value is preserved when it is non-empty, contains no control characters, and is at most 128 characters. Otherwise the middleware generates a 32-character identifier, assigns it to `HttpContext.TraceIdentifier`, adds it to the logging scope, and returns it in the response header.

The correlation identifier is operational metadata, not authentication, authorization, idempotency, or a business transaction identifier.

### Typed options

The API binds and validates:

```json
{
  "Service": {
    "Name": "FinancialAssistant.ServiceTemplate",
    "Version": "1.0.0"
  }
}
```

`ServiceOptions` uses `ValidateOnStart`, so required configuration fails fast during startup instead of causing a later ambiguous runtime failure.

The example endpoint:

```text
GET /service/info
```

returns service name, version, and current environment from typed configuration. New services should replace the template values during generation.

Secrets must use environment variables or an approved secret store, never committed `appsettings` files.

## Create a new service

1. Copy `backend/templates/service-template/` to `backend/services/<service-name>/`.
2. Remove template-only documentation that does not belong in the service folder.
3. Replace `ServiceTemplate` in directory names, project filenames, assembly names, root namespaces, project references, and the copied solution file.
4. Use the product naming convention `FinancialAssistant.<Capability>.<Layer>` for assemblies and root namespaces.
5. Replace the `Service:Name` and `Service:Version` values.
6. Add the renamed projects to `FinancialAssistant.Backend.sln`.
7. Restore and build the root solution.
8. Add service-owned domain behavior before technical adapters.
9. Add REST endpoints only for synchronous operations and RabbitMQ events only after owned state changes.
10. Add service-owned readiness checks for dependencies required to serve traffic.
11. Keep Elasticsearch indices/aliases, repositories, mappings, and business events owned by the service.
12. Use synthetic test data and privacy-safe logs.

Example commands after renaming the copied projects:

```bash
dotnet sln FinancialAssistant.Backend.sln add backend/services/<service-name>/**/*.csproj
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
```

Review the generated solution entries before committing; shell glob expansion differs between Bash, PowerShell, and Windows Command Prompt.

## Layer rules in Financial Assistant

### Domain

- Owns deterministic financial invariants and business decisions.
- Has no infrastructure, transport, OCR, LLM, Elasticsearch, RabbitMQ, or provider dependencies.
- Does not depend on Application.

### Application

- Coordinates domain behavior through use cases.
- Defines ports/interfaces required from infrastructure.
- May depend on Domain and stable Contracts.
- Does not perform provider-specific persistence or messaging work.

### Infrastructure

- Implements application ports.
- Owns technical adapters and service-owned persistence mappings.
- May use Elasticsearch, RabbitMQ, MinIO, Redis, OCR providers, LLM providers, and notification providers when the service requires them.
- Must not become the source of financial business rules.

### Api

- Is the service HTTP host and dependency-composition boundary.
- Performs authentication/authorization integration, transport validation, mapping, health routing, correlation, and response handling.
- Does not own domain calculations or read another service's storage directly.

### Contracts

- Contains transport DTOs and versioned integration contracts.
- Must avoid provider SDK types and service implementation details.
- Event payloads contain only the minimum privacy-safe information required by consumers.

## Verification

`ServiceTemplateArchitectureTests` enforces the five-layer dependency graph and solution inclusion.

`ServiceTemplateCrossCuttingTests` starts the template API and verifies:

- liveness and readiness responses;
- Development-only OpenAPI behavior;
- supplied and generated correlation identifiers;
- typed options exposed through `/service/info`;
- JSON structured logging, health tags, middleware registration, and startup validation source boundaries.

FIN-51 and FIN-52 together complete the reusable .NET 8 service-template baseline. Provider-specific adapters, domain models, service-owned persistence, and business APIs remain the responsibility of each generated service.
