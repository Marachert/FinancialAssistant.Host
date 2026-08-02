# Financial Assistant Expense Service

.NET 8 Expense Service baseline for FIN-22 and FIN-96.

## Responsibility

Expense Service owns confirmed expense records and deterministic spending totals derived from them. It does not own transaction parsing, draft review, income records, category definitions, reports, limits, OCR, or LLM output.

The current aggregate records:

- transaction and source-draft identifiers;
- owning user identifier;
- positive amount and ISO currency;
- Expense-category identifier;
- optional merchant;
- effective date;
- confirmation timestamp.

Only a validated `transaction.confirmed.v1` event with transaction type `expense` may create the initial authoritative record. Raw AI or OCR output is suggestion-only and is never accepted as Expense source of truth.

## Service projects

```text
FinancialAssistant.Expense.Api
FinancialAssistant.Expense.Application
FinancialAssistant.Expense.Contracts
FinancialAssistant.Expense.Domain
FinancialAssistant.Expense.Infrastructure
FinancialAssistant.Expense.Tests
```

The API project is the HTTP composition root. Application owns use-case ports and confirmation consumption, Domain owns the aggregate, Contracts owns stable transport models, and Infrastructure owns storage and event adapters.

## Baseline API

```text
GET /expense/info
GET /health
GET /health/live
GET /health/ready
```

Development and Testing expose OpenAPI at `/openapi/v1.json`. FIN-96 intentionally does not expose expense CRUD routes; FIN-97 owns those commands, validation responses, authentication integration, and user-facing resource contracts.

## Lifecycle definition

The planned command lifecycle is deterministic:

1. **Create** accepts only a confirmed transaction request or trusted confirmed event and deduplicates by transaction identifier.
2. **Update** preserves owner and source transaction identity, validates every replacement financial value, and advances optimistic concurrency state.
3. **Archive** is a reversible soft transition. Archived records remain auditable but are excluded from active spending totals.
4. **Restore** returns an archived record to active totals after current category and financial values pass validation.
5. Physical deletion is not part of the normal financial lifecycle.

FIN-97 will implement the CRUD command surface. Until then, the existing confirmed-event consumer is the only write path and the in-memory record is active by definition.

## Category validation

Expense category identifiers must use the `expense.` namespace. The application will validate category ownership through a stable Category Service contract or a service-owned validated projection. Expense must never read Category storage directly.

A category failure must reject the command before an Expense write. Temporary Category Service unavailability must fail closed or use an explicitly versioned local projection; it must not silently accept an unknown category.

## Storage layout

The development adapter stores records in memory so CI can exercise the confirmed-event boundary without production infrastructure.

The durable adapter will use Expense-owned PostgreSQL storage with logical tables for:

- `expense_records`, including owner, financial values, lifecycle status, revision, and audit timestamps;
- `expense_event_inbox` for idempotent confirmed-event consumption;
- `expense_event_outbox` for atomic publication after owned state changes.

Indexes will support owner/date queries, owner/status totals, category reporting, and unique transaction/source identifiers. Other services must use Expense APIs or events and must not query these tables.

Spending totals include only active, confirmed records for the requested owner and currency. Drafts, rejected drafts, archived records, raw AI or OCR output, and failed event deliveries contribute nothing.

## Planned events

FIN-99 owns implementation of service-owned change publication. The planned versioned events are:

```text
expense.created.v1
expense.updated.v1
expense.archived.v1
expense.restored.v1
```

Events will contain stable identifiers and the minimum consumer data required. They will not include raw intake text, prompts, OCR payloads, credentials, or idempotency keys.

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet run --project backend/services/expense/FinancialAssistant.Expense.Api/FinancialAssistant.Expense.Api.csproj
```
