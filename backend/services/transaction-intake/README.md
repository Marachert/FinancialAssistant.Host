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
POST /transactions/intake
POST /api/v1/transactions/drafts/{draftId}/confirm
POST /transactions/drafts/{draftId}/confirm
```

Both paths use the same handler; `/transactions/intake` matches the existing gateway's unchanged forwarding path. The endpoint requires `X-Gateway-Authentication`, `X-Gateway-User-Id`, and an opaque `Idempotency-Key`. Configure `TransactionIntake__Gateway__SharedSecret` from the environment with at least 32 characters. Configure the gateway with the same environment-provided value in `Gateway__DownstreamAuthentication__SharedSecret`; it strips client attempts to supply this header and injects its own value only for protected destinations. Never place the shared secret, user input, or idempotency values in source control or logs.

Receipt Processing delivers `ocr.completed.v1` to the internal `/internal/events/ocr-completed` endpoint. Configure both services with the same environment-provided `ReceiptProcessing__Events__SharedSecret` of 32 to 256 characters. The endpoint is not routed through the public gateway and rejects requests without the dedicated service credential.

## Draft behavior

The parser is an interchangeable probabilistic-input boundary. Its output is validated by deterministic backend rules before a draft is returned. Unsupported or invalid values become explicit ambiguities instead of financial facts. Low-confidence drafts remain review-required.

FIN-21 ships an intentionally limited deterministic parser and in-memory idempotency store for local development and CI. Production work must provide a configured parser adapter and durable encrypted idempotency/draft persistence without changing the application contract.

## Confirmation

FIN-22 confirms only complete income or expense drafts that require no review. The first successful confirmation stores a stable transaction result, publishes `transaction.confirmed.v1`, and synchronously delivers the development event to independently validating Income and Expense consumers. Repeated or concurrent confirmation returns the original transaction and does not publish or persist a duplicate.

The current publisher and stores are in-memory development adapters. Production delivery requires a transactional outbox, durable encrypted draft and financial-record stores, and RabbitMQ consumers with the same idempotent event contract.

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
```
