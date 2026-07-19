# Financial Assistant Expense Service

FIN-22 establishes the Expense Service event-consumer boundary. `ExpenseTransactionConfirmedConsumer` accepts only valid `transaction.confirmed.v1` expense events and idempotently stores an Expense-owned authoritative record keyed by transaction ID.

The current store is an in-memory development adapter. Production persistence must be durable and encrypted, preserve transaction/event idempotency, and remain private to Expense Service.
