# Financial Assistant Transaction Intake Service

.NET 8 Transaction Intake Service for FIN-21, FIN-91, and FIN-92.

Canonical engineering documentation:

```text
docs/engineering/transaction-intake-draft-flow.md
docs/engineering/async-ai-ocr-processing-flow.md
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

## Input sources

Every draft exposes one stable input source:

```text
text
voice_transcript
receipt_ocr
manual_form
```

Text and receipt OCR have active adapters. Voice transcript and manual form are contract placeholders in this baseline; later adapters must use the same review and validation boundary.

## Draft behavior

The parser is an interchangeable probabilistic-input boundary. Its output is validated by deterministic backend rules before a draft is returned. The response includes a nullable note placeholder; this baseline does not infer note content from free-form text. Unsupported or invalid values become explicit ambiguities instead of financial facts. Low-confidence drafts remain review-required.

FIN-21 ships an intentionally limited deterministic parser and in-memory idempotency store for local development and CI. Production work must provide a configured parser adapter and durable encrypted idempotency/draft persistence without changing the application contract.

## Draft-created event

The first successful text-draft request stores normalized source text behind an opaque
reference and publishes `transaction.draft-created.v1`. The event carries only identifiers
and timestamps; it excludes raw input, merchant, amount, note, idempotency material, and
parser output. Repeated or concurrent requests publish one logical event. If publication
fails, retrying the same request resumes the pending publication before returning the
stored draft.

The current source-payload and publication-state store is an in-memory development adapter.
Production delivery requires encrypted durable payload storage and a transactional outbox;
downstream consumers resolve the opaque reference through an authenticated owner boundary
and deduplicate by event and job identifiers.

## Confirmation

FIN-22 confirms only complete income or expense drafts that require no review. The first successful confirmation stores a stable transaction result, publishes `transaction.confirmed.v1`, and synchronously delivers the development event to independently validating Income and Expense consumers. Repeated or concurrent confirmation returns the original transaction and does not publish or persist a duplicate.

The current publisher and stores are in-memory development adapters. Production delivery requires a transactional outbox, durable encrypted draft and financial-record stores, and RabbitMQ consumers with the same idempotent event contract.

## Verification

```text
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
```
