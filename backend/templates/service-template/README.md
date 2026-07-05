# Service Template

Clean Architecture template for new .NET 8 backend services.

## Layers

```text
ServiceTemplate.Api
ServiceTemplate.Application
ServiceTemplate.Domain
ServiceTemplate.Infrastructure
ServiceTemplate.Contracts
```

## Dependency direction

Api -> Application -> Domain

Infrastructure implements technical adapters used by Application.

Contracts contains DTOs and integration contracts.
