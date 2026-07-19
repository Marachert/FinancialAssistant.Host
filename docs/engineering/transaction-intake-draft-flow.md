# Transaction Intake Draft Flow

## Scope

FIN-21 implements the first half of the core single-input workflow. An authenticated user submits one natural-language statement and receives a structured draft containing type, amount, currency, category, merchant, date, confidence, and explicit ambiguities. The draft is review material only. No balance, transaction ledger, report, or score changes until the separate confirmation flow validates and persists authoritative state.

## Request boundary

`POST /api/v1/transactions/intake` is the canonical service route. `POST /transactions/intake` reaches the same handler and matches the existing gateway's unchanged forwarding path. Both require trusted gateway authentication, a gateway-established user ID, and an opaque 8-to-128 character `Idempotency-Key`. Input is whitespace-normalized and limited to 2,000 characters. The key must not contain identity, device, merchant, amount, or other financial data.

The service stores a SHA-256 fingerprint of normalized input with the user-scoped idempotency key and draft. It does not store raw input in the idempotency record. Repeating the same key and normalized input returns the original draft. Reusing the key for different input returns `409 idempotency_key_conflict`.

## Parser boundary

`ITransactionInputParser` is a replaceable input adapter. Parser output is never authoritative. `TransactionDraftValidator` independently enforces:

- supported transaction types: income, expense, transfer, or unknown;
- positive bounded amounts rounded to two decimal places;
- supported ISO currencies for the PoC;
- category identifier shape and transaction-type alignment;
- merchant length limits;
- date bounds;
- confidence range and the low-confidence review threshold.

Invalid candidate values are removed and represented by stable ambiguity codes. Unknown, low-confidence, or incomplete candidates remain drafts with `requiresReview = true`.

## Development adapters

The deterministic parser recognizes a bounded English keyword and amount/date subset so contracts remain executable without an external AI provider. It is not presented as general natural-language understanding. The in-memory store demonstrates user-scoped idempotency and atomic first-write behavior but is not durable.

Production adapters must be environment-selected, preserve the application interfaces, encrypt sensitive draft fields at rest, avoid raw financial input in logs, and use durable storage. A future AI adapter may improve extraction, but deterministic backend validation remains mandatory.

## Confirmation and authoritative records

`POST /api/v1/transactions/drafts/{draftId}/confirm` and the gateway-forwarded `/transactions/drafts/{draftId}/confirm` route confirm a draft owned by the authenticated user. Unknown, transfer, incomplete, or review-required drafts return `422 transaction_draft_not_confirmable` and cannot alter financial state.

The first valid confirmation creates a stable transaction ID and `transaction.confirmed.v1` event. Income and Expense consumers independently validate event type, positive amount, currency shape, category ownership, and identifiers before idempotently storing their service-owned source-of-truth record. Repeated and concurrent confirmation returns the original transaction without another event or record.

The in-memory publisher demonstrates the event boundary in local development. Production must atomically persist confirmation and an outbox message, publish through RabbitMQ, and let durable consumers deduplicate by transaction/event ID. Raw intake text and idempotency keys are absent from the event.

## Security

The service fails startup unless `TransactionIntake__Gateway__SharedSecret` contains at least 32 characters. Protected endpoints compare a fixed-size digest in constant time before trusting `X-Gateway-User-Id`. The gateway uses the matching `Gateway__DownstreamAuthentication__SharedSecret`, strips caller-supplied `X-Gateway-Authentication`, and injects its credential only for destinations marked `RequiresGatewayAuthentication`. A protected destination fails closed with 503 when the gateway credential is absent or invalid. Deployment must also keep the service listener on an internal network.
