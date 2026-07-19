# Financial Assistant Income Service

FIN-22 establishes the Income Service event-consumer boundary. `IncomeTransactionConfirmedConsumer` accepts only valid `transaction.confirmed.v1` income events and idempotently stores an Income-owned authoritative record keyed by transaction ID.

The current store is an in-memory development adapter. Production persistence must be durable and encrypted, preserve transaction/event idempotency, and remain private to Income Service.
