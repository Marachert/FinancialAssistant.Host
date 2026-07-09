# .NET 8 Service Template

This folder contains the reusable Clean Architecture project skeleton for new Financial Assistant backend services.

FIN-51 establishes the project boundaries and dependency direction. FIN-52 adds the mandatory health checks, OpenAPI, structured logging, correlation, and typed configuration baseline.

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

The five template projects are also included in `FinancialAssistant.Backend.sln`, so the normal Backend CI compiles them.

## Create a new service

1. Copy `backend/templates/service-template/` to `backend/services/<service-name>/`.
2. Remove template-only documentation that does not belong in the service folder.
3. Replace `ServiceTemplate` in directory names, project filenames, assembly names, root namespaces, project references, and the copied solution file.
4. Use the product naming convention `FinancialAssistant.<Capability>.<Layer>` for assemblies and root namespaces.
5. Add the renamed projects to `FinancialAssistant.Backend.sln`.
6. Restore and build the root solution.
7. Add service-owned domain behavior before technical adapters.
8. Add REST endpoints only for synchronous operations and RabbitMQ events only after owned state changes.
9. Keep Elasticsearch indices/aliases, repositories, mappings, and business events owned by the service.
10. Use synthetic test data and privacy-safe logs.

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
- Performs authentication/authorization integration, transport validation, mapping, and response handling.
- Does not own domain calculations or read another service's storage directly.

### Contracts

- Contains transport DTOs and versioned integration contracts.
- Must avoid provider SDK types and service implementation details.
- Event payloads contain only the minimum privacy-safe information required by consumers.

## Current scope boundary

FIN-51 intentionally delivers only the compileable Clean Architecture skeleton and dependency graph.

The following belong to FIN-52 and must not be considered complete yet:

- liveness and readiness health endpoints;
- local-development OpenAPI;
- structured logging baseline;
- correlation identifier middleware;
- typed options/configuration pattern.
