# Financial Assistant Transaction Intake Service

.NET 8 Transaction Intake Service for FIN-21.

Canonical engineering documentation:

```text
docs/engineering/transaction-intake-draft-flow.md
```

## Responsibility

Transaction Intake owns natural-language intake, idempotent draft creation, deterministic validation of parser output, and the review contract consumed before confirmation. It does not persist authoritative income, expense, transfer, balance, or reporting state; FIN-22 owns confirmation and authoritative persistence.

```text
POST /api/v1/transactions/intake
```

The endpoint requires `X-Gateway-Authentication`, `X-Gateway-User-Id`, and an opaque `Idempotency-Key`. Configure `TransactionIntake__Gateway__SharedSecret` from the environment with at least 32 characters. Never place the shared secret, user input, or idempotency values in source control or logs.

## Draft behavior

The parser is an interchangeable probabilistic-input boundary. Its output is validated by deterministic backend rules before a draft is returned. Unsupported or invalid values become explicit ambiguities instead of financial facts. Low-confidence drafts remain review-required.

FIN-21 ships an intentionally limited deterministic parser and in-memory idempotency store for local development and CI. Production work must provide a configured parser adapter and durable encrypted idempotency/draft persistence without changing the application contract.

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
```
