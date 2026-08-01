# Transaction Intake Draft Flow

## Scope

FIN-21 implements the first half of the core single-input workflow. FIN-91 completes the service baseline with explicit input-source metadata. FIN-92 adds the nullable note placeholder and the recoverable draft-created event boundary. FIN-93 adds owner-scoped review, deterministic draft updates, rejection, and a revision-claimed confirmation lifecycle. An authenticated user submits one natural-language statement and receives a structured draft containing type, amount, currency, category, merchant, date, confidence, and explicit ambiguities. The draft is review material only. No balance, transaction ledger, report, or score changes until the separate confirmation flow validates and persists authoritative state.

## Input sources

Drafts identify one of four stable sources: `text`, `voice_transcript`, `receipt_ocr`, or `manual_form`. Text and receipt OCR are active adapters. Voice transcript and manual form are placeholders for later adapters and cannot bypass deterministic validation, review requirements, or confirmation rules.

The draft lifecycle is explicit: `draft` output is suggestion-only, ambiguous or low-confidence values require review, and only the separate confirmation flow can create an authoritative income or expense record. Confirmation atomically claims one draft revision before publishing and then records `confirmed`; rejection records the terminal `rejected` status without publishing.

## Request boundary

`POST /api/v1/transactions/intake` is the canonical service route. `POST /transactions/intake` reaches the same handler and matches the existing gateway's unchanged forwarding path. Both require trusted gateway authentication, a gateway-established user ID, and an opaque 8-to-128 character `Idempotency-Key`. Input is whitespace-normalized and limited to 2,000 characters. The key must not contain identity, device, merchant, amount, or other financial data.

The service stores a SHA-256 fingerprint of normalized input with the user-scoped idempotency key and draft. It does not store raw input in the idempotency record. For downstream AI work, normalized source text is stored separately behind an opaque payload reference; that reference, never the text, is carried by `transaction.draft-created.v1`. Repeating the same key and normalized input returns the original draft. Reusing the key for different input returns `409 idempotency_key_conflict`.

## Parser boundary

`ITransactionInputParser` is a replaceable input adapter. Parser output is never authoritative. `TransactionDraftValidator` independently enforces:

- supported transaction types: income, expense, transfer, or unknown;
- positive bounded amounts rounded to two decimal places;
- supported ISO currencies for the PoC;
- category identifier shape and transaction-type alignment;
- merchant length limits;
- date bounds;
- confidence range and the low-confidence review threshold.

Invalid candidate values are removed and represented by stable ambiguity codes. Unknown, low-confidence, or incomplete candidates remain drafts with `requiresReview = true`. A full draft update revalidates every user-reviewed value, normalizes the optional note, and keeps invalid updates review-required rather than allowing them into financial state.

## Development adapters

The deterministic parser recognizes a bounded English keyword and amount/date subset so contracts remain executable without an external AI provider. It is not presented as general natural-language understanding. The in-memory stores demonstrate user-scoped idempotency, atomic first-write behavior, opaque payload lookup, and retryable publication state but are not durable.

Production adapters must be environment-selected, preserve the application interfaces, encrypt sensitive draft and source-payload fields at rest, avoid raw financial input in logs or events, and use durable storage plus a transactional outbox. A future AI adapter may improve extraction, but deterministic backend validation remains mandatory.

## Review, rejection, and authoritative records

The canonical owner-scoped draft routes are:

- `GET /api/v1/transactions/drafts/{draftId}` to review current values and status;
- `PUT /api/v1/transactions/drafts/{draftId}` to replace and deterministically revalidate editable values;
- `POST /api/v1/transactions/drafts/{draftId}/reject` to reject idempotently;
- `POST /api/v1/transactions/drafts/{draftId}/confirm` to create authoritative state.

Equivalent `/transactions/drafts/{draftId}` gateway routes reach the same handlers. A draft belonging to another user is indistinguishable from a missing draft. Confirming or rejected drafts cannot be edited, and rejected drafts cannot be confirmed.

Unknown, transfer, incomplete, review-required, or rejected drafts return `422 transaction_draft_not_confirmable` from confirmation and cannot alter financial state.

The first valid confirmation creates a stable transaction ID and `transaction.confirmed.v1` event. Income and Expense consumers independently validate event type, positive amount, currency shape, category ownership, and identifiers before idempotently storing their service-owned source-of-truth record. Repeated and concurrent confirmation returns the original transaction without another event or record.

The in-memory publisher demonstrates the event boundary in local development. Production must atomically persist confirmation and an outbox message, publish through RabbitMQ, and let durable consumers deduplicate by transaction/event ID. Raw intake text and idempotency keys are absent from the event.

## Security

The service fails startup unless `TransactionIntake__Gateway__SharedSecret` contains at least 32 characters. Protected endpoints compare a fixed-size digest in constant time before trusting `X-Gateway-User-Id`. The gateway uses the matching `Gateway__DownstreamAuthentication__SharedSecret`, strips caller-supplied `X-Gateway-Authentication`, and injects its credential only for destinations marked `RequiresGatewayAuthentication`. A protected destination fails closed with 503 when the gateway credential is absent or invalid. Deployment must also keep the service listener on an internal network.
