# Financial Assistant Expense Service

.NET 8 Expense Service for FIN-22, FIN-94, and FIN-95.

## Responsibility

Expense Service owns confirmed expense records and deterministic active-expense totals. It does not own transaction parsing, draft review, income records, categories, balances, reports, OCR, or LLM output.

The normative rules for authoritative records, ownership, lifecycle, corrections, and per-currency calculations are defined in `docs/architecture/financial-source-of-truth.md`.

Authoritative records have one of two origins:

- `confirmed_transaction`, created idempotently from a validated `transaction.confirmed.v1` expense event;
- `manual`, created by the authenticated owner through the Expense API.

Raw AI and OCR output remains suggestion-only and is never accepted as Expense source of truth.

## API

Canonical routes:

```text
POST /api/v1/expenses
GET  /api/v1/expenses?from=YYYY-MM-DD&to=YYYY-MM-DD&includeArchived=false
GET  /api/v1/expenses/{expenseId}
PUT  /api/v1/expenses/{expenseId}
POST /api/v1/expenses/{expenseId}/archive
POST /api/v1/expenses/{expenseId}/restore
GET  /expense/info
GET  /health
GET  /health/live
GET  /health/ready
```

Equivalent `/expenses` gateway routes reach the same handlers. Financial routes require one trusted `X-Gateway-Authentication` value and an `X-Gateway-User-Id` established by the gateway. A record owned by another user is indistinguishable from a missing record.

Development and Testing expose OpenAPI at `/openapi/v1.json`.

## Validation

Application validation is deterministic:

- amount must be positive, bounded, and rounds to two decimal places;
- currency must be EUR, GBP, UAH, or USD;
- category identifiers must match the `expense.*` namespace and stable identifier shape;
- merchant text is whitespace-normalized and limited to 120 characters;
- dates must be within ten years before today and 366 days after today;
- list periods must be ordered and no longer than ten years.

Invalid requests return `400 invalid_expense_request` before storage changes. Expense validates category contract shape locally and must use a stable Category Service contract or versioned projection when user-created category validation is introduced; it never reads Category storage directly.

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

The durable adapter will use Expense-owned PostgreSQL storage with logical `expense_records`, `expense_event_inbox`, and `expense_event_outbox` tables. Other services must use Expense APIs or events and must not query these tables.

## Planned events

FIN-99 owns publication of:

```text
expense.created.v1
expense.updated.v1
expense.archived.v1
expense.restored.v1
```

Events will contain minimum required stable data and exclude raw intake text, prompts, OCR payloads, credentials, and idempotency keys.

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet run --project backend/services/expense/FinancialAssistant.Expense.Api/FinancialAssistant.Expense.Api.csproj
```

Set `Expense__Gateway__SharedSecret` to an environment-backed value of at least 32 characters before starting the API.
