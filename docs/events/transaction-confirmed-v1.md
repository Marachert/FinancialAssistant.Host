# transaction.confirmed.v1

`transaction.confirmed.v1` is emitted once when Transaction Intake successfully confirms a complete income or expense draft.

## Fields

| Field | Purpose |
| --- | --- |
| `eventId` | Unique event identity for delivery deduplication |
| `transactionId` | Stable authoritative transaction identity |
| `userId` | Owner identity established by the authenticated gateway |
| `draftId` | Source draft identity |
| `transactionType` | `income` or `expense` |
| `amount` | Positive backend-validated decimal amount |
| `currency` | Validated ISO currency code |
| `categoryId` | Category aligned with transaction type |
| `merchant` | Optional confirmed merchant value |
| `date` | Confirmed financial date |
| `confirmedAtUtc` | Confirmation timestamp |
| `correlationId` | Bounded operational correlation identity |

The event excludes raw intake input, parser confidence, ambiguities, and idempotency keys. It contains private financial data and therefore belongs only on authenticated encrypted internal transport; payloads must never be logged.

Consumers validate the event independently and store idempotently by transaction/event identity. Production publication uses a transactional outbox and RabbitMQ. Duplicate delivery must not create another Income or Expense record.
