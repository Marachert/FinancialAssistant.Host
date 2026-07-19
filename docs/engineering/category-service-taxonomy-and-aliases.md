# Category Service Taxonomy and Aliases

## Scope

FIN-20 introduces the Category Service boundary used by Transaction Intake. Category owns stable category definitions, per-user aliases, deterministic lookup, and category update events. It does not classify transactions probabilistically and does not persist transaction or receipt data.

## Default taxonomy

Each registered user receives the same ordered baseline with stable identifiers such as `income.salary`, `expense.food`, and `expense.transportation`. Identifiers and sort order are deterministic and are never generated from display text or user data.

The current catalog contains two income categories and nine expense categories. Display names are English bootstrap values; future localization must map stable keys to localized resources without changing identifiers.

Registration is idempotent. Replaying `user.registered.v1` returns the existing catalog and does not reset aliases or timestamps.

## Aliases and matching

Aliases are scoped to one authenticated user. Input is whitespace-normalized, lowercased with invariant rules, de-duplicated, sorted, and constrained to 20 values of at most 80 characters each.

Search evaluates normalized category keys, display names, and aliases. Results are ordered by:

1. exact match;
2. prefix match;
3. substring match;
4. stable taxonomy sort order;
5. category identifier.

This makes repeated searches auditable and independent of LLM or OCR output. Transaction Intake may combine a deterministic match with probabilistic suggestions, but a suggestion cannot silently become authoritative financial state.

## Events

An actual alias change publishes `category.updated.v1` with user ID, category ID, version, change type, timestamp, and correlation ID. The event deliberately excludes aliases and merchant text to avoid broadcasting potentially sensitive user input. Replacing aliases with the same normalized set does not publish a duplicate event.

## Ownership and persistence

The current in-memory store and publisher are development adapters. Production adapters must be environment-configured, owned by Category Service, and preserve atomic version updates. RabbitMQ publication must use an outbox or equivalent reliable handoff when durable persistence is introduced.

No service may read Category tables or search indices directly. Consumers use Category API contracts or versioned events.

## Gateway authentication

All category and registration-event endpoints fail closed unless `X-Gateway-Authentication` matches the environment-provided `Category__Gateway__SharedSecret`. The service validates that configuration at startup and compares a fixed-size digest in constant time. User routes additionally require the trusted `X-Gateway-User-Id` established by the authenticated gateway.

Deployment must keep the service on an internal network, strip client attempts to supply either trusted header at the public gateway, and inject the configured authentication value only after gateway authentication succeeds. Health and service-information endpoints do not expose catalog data and remain available for platform probes.
