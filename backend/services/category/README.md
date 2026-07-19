# Financial Assistant Category Service

.NET 8 Category Service for FIN-20.

Canonical engineering documentation:

```text
docs/engineering/category-service-taxonomy-and-aliases.md
```

## Responsibility

Category Service owns the deterministic category taxonomy used by transaction intake:

- stable default income and expense category identifiers;
- per-user category catalogs seeded after `user.registered.v1`;
- user-specific aliases used for deterministic merchant and free-text matching;
- category search ordering and validation;
- `category.updated.v1` publication after an actual alias change.

The service does not own transactions, balances, OCR output, LLM prompts, or financial calculations. Consumers may use category matches as candidates, but authoritative transaction state remains in the financial core services.

## API

```text
GET  /categories?query={text}
PUT  /categories/{categoryId}/aliases
POST /internal/category/v1/events/user-registered
GET  /category/info
GET  /health/live
GET  /health/ready
```

Public routes require the trusted `X-Gateway-User-Id` header. Search is deterministic: exact matches precede prefix matches, which precede substring matches; ties use stable taxonomy order.

## Current adapters

FIN-20 uses in-memory catalog storage and an in-memory event publisher so the domain and HTTP contracts are executable in local development and CI without production infrastructure. Future increments must replace these adapters with Category-owned persistence and RabbitMQ publication. Other services must not read Category storage directly.

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
```
