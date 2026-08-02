# Financial Assistant Income Service

.NET 8 Income Service for FIN-22, FIN-94, and FIN-95.

## Responsibility

Income Service owns confirmed income records and deterministic active-income totals. It does not own transaction parsing, draft review, expense records, categories, balances, reports, OCR, or LLM output.

Authoritative records have one of two origins:

- `confirmed_transaction`, created idempotently from a validated `transaction.confirmed.v1` income event;
- `manual`, created by the authenticated owner through the Income API.

Raw AI and OCR output remains suggestion-only and is never accepted as Income source of truth.

## API

Canonical routes:

```text
POST /api/v1/incomes
GET  /api/v1/incomes?from=YYYY-MM-DD&to=YYYY-MM-DD&includeArchived=false
GET  /api/v1/incomes/{incomeId}
PUT  /api/v1/incomes/{incomeId}
POST /api/v1/incomes/{incomeId}/archive
POST /api/v1/incomes/{incomeId}/restore
GET  /income/info
GET  /health
GET  /health/live
GET  /health/ready
```

Equivalent `/incomes` gateway routes reach the same handlers. Financial routes require one trusted `X-Gateway-Authentication` value and an `X-Gateway-User-Id` established by the gateway. A record owned by another user is indistinguishable from a missing record.

Development and Testing expose OpenAPI at `/openapi/v1.json`.

## Validation

Application validation is deterministic:

- amount must be positive, bounded, and rounds to two decimal places;
- currency must be EUR, GBP, UAH, or USD;
- category identifiers must match the `income.*` namespace and stable identifier shape;
- merchant text is whitespace-normalized and limited to 120 characters;
- dates must be within ten years before today and 366 days after today;
- list periods must be ordered and no longer than ten years.

Invalid requests return `400 invalid_income_request` before storage changes. Income validates category contract shape locally and must use a stable Category Service contract or versioned projection when user-created category validation is introduced; it never reads Category storage directly.

## Lifecycle and totals

Manual creation stores an active authoritative record. Updates preserve owner, record identity, origin, source-draft reference, and confirmation timestamp while advancing the revision.

Archive and restore are idempotent reversible transitions:

- archived records remain auditable and cannot be updated;
- archived records are excluded from list results by default;
- `includeArchived=true` includes them in records but never in active totals;
- restore revalidates the stored financial fields before returning the record to active totals;
- physical deletion is not part of the first-release financial lifecycle.

List results group active totals by currency so unlike currencies are never summed together.

## Storage

The development adapter is an owner-scoped in-memory store with atomic create and compare-and-replace updates. Confirmed events remain idempotent by transaction identifier.

The durable adapter will use Income-owned PostgreSQL storage with logical `income_records`, `income_event_inbox`, and `income_event_outbox` tables. Other services must use Income APIs or events and must not query these tables.

## Planned events

FIN-99 owns publication of:

```text
income.created.v1
income.updated.v1
income.archived.v1
income.restored.v1
```

Events will contain minimum required stable data and exclude raw intake text, prompts, OCR payloads, credentials, and idempotency keys.

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet run --project backend/services/income/FinancialAssistant.Income.Api/FinancialAssistant.Income.Api.csproj
```

Set `Income__Gateway__SharedSecret` to an environment-backed value of at least 32 characters before starting the API.
