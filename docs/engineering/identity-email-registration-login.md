# Email Registration and Sign-In Flow

## Purpose

FIN-75 activates `POST /auth/v1/register` and `POST /auth/v1/sign-in` using deterministic server-side logic. FIN-76 subsequently implemented refresh, logout, access-token validation and current-session lookup; see [session lifecycle](identity-session-lifecycle.md).

## Component boundaries

- API maps HTTP contracts to application results and safe Problem Details responses.
- Application validates commands, normalizes email identities, coordinates account creation/sign-in, and publishes lifecycle events through abstractions.
- Domain owns account lifecycle state and the rule that only active accounts can authenticate.
- Infrastructure provides password protection, keyed email lookup, JWT access and opaque refresh values, development in-memory stores, clock, and outbox/event adapters.
- Contracts remain independent from storage documents and hashing metadata.

## Registration flow

1. Validate email, new-password policy, client context, and optional idempotency-key shape.
2. Normalize the email value in memory.
3. Derive a keyed lookup value; the normalized email is not stored.
4. Check for an existing credential.
5. Create an active account with the default `user` role.
6. Protect the supplied secret with the ASP.NET Core Identity password hasher.
7. Atomically store account and credential records through `IIdentityAccountStore`.
8. Create the server-side session and issue a signed access JWT and opaque refresh value.
9. Publish `user.registered.v1` through `IIdentityEventPublisher` with user ID and authentication method only.
10. Return HTTP 201.

Duplicate creation returns HTTP 409 with `identity_conflict` and no indication of internal storage state.

## Sign-in flow

1. Validate request shape and client context.
2. Normalize and key the email lookup value.
3. Load the credential and account through the storage abstraction.
4. Execute dummy password verification when the lookup is missing or the account cannot authenticate.
5. Verify the protected secret.
6. Replace the protected value when the framework reports that rehashing is required.
7. Issue a new initial session response.
8. Return HTTP 200.

Unknown identifiers and invalid secrets return the same HTTP 401 `authentication_failed` response.

## Sensitive data rules

- Raw email is used only during request processing and is not stored by the current adapter.
- The lookup key is an HMAC-SHA256 value produced with an environment-supplied key or an ephemeral development key.
- Passwords are never logged, returned, or stored in plaintext.
- Protected credential values are not exposed by API contracts.
- Issued opaque session values are returned once and are not logged.
- Event data contains no email, credential, token, client identifier, or financial data.

## Current persistence adapter

FIN-75 uses `InMemoryIdentityAccountStore` to make registration and sign-in executable in local development and CI while preserving the final application boundary.

This adapter is not a production persistence solution:

- state is lost on process restart;
- it is not shared across replicas;
- it does not create Elasticsearch documents or aliases;
- it provides no durable idempotency store.

Any production repository must implement `IIdentityAccountStore` without changing
API contracts, domain rules or use cases. The preferred durable target is
service-owned PostgreSQL; earlier Elasticsearch contracts remain historical or
adapter-specific. See [storage policy](../architecture/storage-policy.md).

## Session boundary

The original FIN-75 opaque-access issuer was replaced by the FIN-76 lifecycle.
The compatibility wrapper now creates server-side session records, signed access
JWTs and opaque refresh values with rotation, revocation and replay detection.
The current session store remains in-memory, not crash-durable.

## Event boundary

`user.registered.v1` is enqueued through the application event abstraction after
the authoritative account write. FIN-77 implemented the outbox dispatcher and
publisher-confirmed RabbitMQ transport. The active outbox is in-memory; state
and event intent are not one durable transaction. See [event publishing](identity-event-publishing.md).

## Verification

Automated tests cover:

- successful registration and sign-in;
- duplicate registration;
- invalid input;
- identical public failure semantics for unknown email and wrong password;
- protected lookup and credential storage;
- versioned registration event publication;
- OpenAPI continuity;
- session issuance continuity with the implemented FIN-76 handlers.
