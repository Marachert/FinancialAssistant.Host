# Financial Assistant Category Service

.NET 8 Category Service for FIN-20 and FIN-90.

Canonical engineering documentation:

```text
docs/engineering/category-service-taxonomy-and-aliases.md
```

## Responsibility

Category Service owns the deterministic category taxonomy used by transaction intake:

- stable default income and expense category identifiers;
- stable localization, icon, and color keys derived from category identifiers;
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

Category endpoints require both `X-Gateway-Authentication` and, for user requests, `X-Gateway-User-Id`. The authentication value is compared against `Category__Gateway__SharedSecret`, which must be supplied through environment-backed configuration and contain at least 32 characters. The service fails to start when this secret is absent or invalid. The gateway must strip caller-provided trusted headers before adding its own values; never store the secret in source control.

Presentation keys are data, not localized text or visual values:

```text
localizationKey = categories.{categoryId}
iconKey = category.icon.{categoryId}
colorKey = category.color.{categoryId}
```

Clients map these stable keys to locale resources, icons, and color tokens. Alias replacement is the supported user-specific override in this increment. User-created categories and direct presentation-key overrides are explicitly deferred.

Search is deterministic: exact matches precede prefix matches, which precede substring matches; ties use stable taxonomy order.

## Current adapters

Development and Testing retain in-memory catalog/event adapters. FIN-283 selects
separate Category-owned PostgreSQL storage in WindowsNative. Alias updates and
encrypted outbox intents commit atomically; no-op/replayed aliases do not increment
versions or emit duplicate intents. Pending messages are not marked published by
the development publisher. Bounded cleanup removes only published messages after
30 days. Native transport/inbox qualification and readiness remain pending FIN-276,
not implemented RabbitMQ delivery. Other services must not read Category tables.
See [native persistence/configuration/verification](../../../docs/engineering/profile-category-native-persistence.md).

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
```
