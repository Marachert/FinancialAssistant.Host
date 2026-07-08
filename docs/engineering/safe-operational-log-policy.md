# Safe Operational Log Policy

## Purpose

This policy defines what the Financial Assistant Public API Gateway and Identity Service may write to operational logs.

Operational logs exist to diagnose availability, routing, authentication, authorization, latency, and asynchronous delivery failures. They are not a transaction journal, audit database, analytics store, OCR archive, or LLM prompt history.

The default rule is **omit user and financial data**. Masking is a fallback for an approved integration constraint, not permission to log a sensitive value.

## Scope

The policy applies to:

- Public API Gateway request lifecycle, routing, access control, and transport failures;
- Identity Service account/session infrastructure and outbox publishing;
- structured application logs emitted through `ILogger`;
- log scopes, event templates, exception handling, and background workers;
- future gateway and identity code reviews.

Domain services should adopt the same principles, but service-specific event catalogs belong to the owning service.

## Responsibility boundary

The gateway may log technical routing and security outcomes. It must not log or infer financial domain content.

Identity may log technical account/session workflow outcomes. It must not log credentials, provider tokens, personal identifiers, or complete session/token values.

LLM and OCR content is never operational-log data. AI output is probabilistic UX assistance and must not be treated as financial truth or copied into logs.

## Structured event requirements

Every production operational event must use a source-generated `LoggerMessage` definition with:

1. a stable numeric `EventId`;
2. a stable PascalCase `EventName`;
3. a constant message template;
4. an explicit log level;
5. an allowlisted set of structured fields;
6. no `Exception` object parameter unless a separate security review explicitly approves it.

Do not build log messages with string interpolation, concatenation, serialized objects, anonymous request objects, or arbitrary dictionaries.

### Event ID ranges

| Range | Owner | Purpose |
| --- | --- | --- |
| `1000–1099` | Gateway | request lifecycle and correlation |
| `1100–1199` | Gateway | destination dispatch and transport |
| `1200–1299` | Gateway | authentication and authorization |
| `1900–1999` | Gateway | unexpected technical failures |
| `2000–2099` | Identity | event outbox and messaging |
| `2100–2999` | Identity | reserved for future identity operational events |

Event IDs and names are contracts used by dashboards and alerts. Renaming or reusing an ID requires review as an observability contract change.

## Allowed fields

The following fields are safe when they contain normalized technical values only.

### Common metadata

- `EventId` and `EventName`;
- timestamp and log level supplied by the logging provider;
- service/component name and environment;
- `CorrelationId` and `TraceId` after validation;
- `CausationId` for asynchronous event chains;
- request method;
- HTTP status code;
- elapsed milliseconds;
- retry attempt count and retry delay;
- exception **type name only**, exposed as `FailureType`.

### Gateway metadata

- configured `RouteKey`;
- configured `DestinationKey`;
- normalized `AccessPolicy`;
- normalized authentication result enum;
- configured timeout seconds;
- `ResponseStarted` boolean.

### Identity messaging metadata

- integration `EventId`;
- registered integration `EventType`;
- validated `CorrelationId` and `CausationId` from the envelope;
- retry attempt count and retry delay;
- transport failure type name.

Allowed identifiers are technical, bounded, and non-secret. They must not be replaced with raw payload fields.

## Data that must not be logged

The following values are prohibited in message templates, structured properties, scopes, exception objects, or serialized payloads:

- `Authorization` headers, bearer tokens, refresh tokens, JWTs, provider tokens, API keys, cookies, signing keys, secrets;
- passwords, password hashes, OTP codes, verification codes, credential material;
- raw user, account, profile, device, or session identifiers;
- email addresses, phone numbers, names, addresses, locale-derived personal data;
- request or response bodies;
- query-string values;
- arbitrary request headers;
- complete URLs containing query strings or user-controlled segments;
- transaction amounts, currency, merchant names, categories, notes, income, expenses, balances, limits, or financial score inputs;
- receipt images, object-storage keys, OCR text, normalized receipt fields, OCR confidence details tied to a user;
- LLM prompts, completions, recommendations, embeddings, tool inputs, or model reasoning;
- Elasticsearch documents, search queries containing user data, RabbitMQ payloads, or serialized domain events;
- exception messages, `Exception.ToString()`, stack traces, inner exceptions, or provider response bodies;
- internal destination base addresses, credentials, connection strings, or deployment secrets;
- role lists or authorization headers supplied by the client.

`FailureType = exception.GetType().Name` is permitted. Passing the exception object to the logger is not permitted by default because provider exceptions can contain request URLs, tokens, payload fragments, or personal data.

## Masking and minimization

Prefer omission over masking.

When a value is explicitly approved for diagnostic use:

1. normalize it before logging;
2. enforce a maximum length;
3. remove control characters;
4. use an irreversible keyed hash only when cross-event correlation is genuinely required;
5. use a dedicated property name such as `UserReferenceHash`;
6. document retention and access restrictions;
7. add an automated test proving the raw value is absent.

Do not use reversible encoding, partial JWTs, token prefixes, email domains, phone suffixes, or “last four” financial identifiers as a shortcut.

## Correlation metadata

### Gateway synchronous flow

`CorrelationMiddleware` owns request correlation metadata.

It validates or generates the correlation identifier and creates a structured scope containing:

- `CorrelationId`;
- `TraceId`;
- `RequestMethod`.

Downstream events inherit the scope. Individual log calls must not duplicate headers, path, query, token, user ID, or session ID.

### Identity asynchronous flow

Outbox events use the envelope `CorrelationId` and `CausationId`. The dispatcher logs those identifiers with technical event metadata when publishing fails.

The event payload, metadata dictionary, user reference hash, and transport exception object are not logged.

## Event catalog

### Gateway

| Event ID | Event name | Level | Fields |
| --- | --- | --- | --- |
| 1000 | `GatewayRequestStarted` | Information | correlation scope only |
| 1001 | `GatewayRequestCompleted` | Information | `StatusCode`, `ElapsedMilliseconds` |
| 1002 | `GatewayClientRequestCancelled` | Information | correlation scope only |
| 1100 | `GatewayDestinationUnavailable` | Warning | `RouteKey`, `DestinationKey` |
| 1101 | `GatewayDispatchCancelled` | Information | `RouteKey` |
| 1102 | `GatewayDestinationTimedOut` | Warning | `RouteKey`, `DestinationKey`, `TimeoutSeconds` |
| 1103 | `GatewayDestinationCallFailed` | Warning | `RouteKey`, `DestinationKey`, `FailureType` |
| 1200 | `GatewaySecurityPlaceholderEvaluated` | Debug | `RouteKey`, `AccessPolicy` |
| 1201 | `GatewayPublicRequestAllowed` | Debug | `RouteKey` |
| 1202 | `GatewayAuthenticationRejected` | Warning | `RouteKey`, `AuthenticationResult` |
| 1203 | `GatewayAdminRoleRejected` | Warning | `RouteKey` |
| 1204 | `GatewayRequestAuthorized` | Debug | `RouteKey`, `AccessPolicy` |
| 1900 | `GatewayUnhandledFailure` | Error | `FailureType`, `ResponseStarted` |

### Identity

| Event ID | Event name | Level | Fields |
| --- | --- | --- | --- |
| 2000 | `IdentityOutboxPublishFailed` | Warning | `EventId`, `EventType`, `CorrelationId`, `CausationId`, `FailureType`, `AttemptCount`, `RetryDelaySeconds` |

## Safe examples

### Gateway timeout

```json
{
  "eventId": 1102,
  "eventName": "GatewayDestinationTimedOut",
  "level": "Warning",
  "correlationId": "5b2f3cb6-46db-4dc5-9d24-e7ca75df7e65",
  "traceId": "25b70e2e2cdd69f7e84fa1e665d993e1",
  "requestMethod": "POST",
  "routeKey": "transactions-intake",
  "destinationKey": "transaction-intake-service",
  "timeoutSeconds": 30
}
```

### Identity outbox retry

```json
{
  "eventId": 2000,
  "eventName": "IdentityOutboxPublishFailed",
  "level": "Warning",
  "eventType": "identity.user.registered.v1",
  "correlationId": "5b2f3cb6-46db-4dc5-9d24-e7ca75df7e65",
  "causationId": "1f32d9f6-553f-49e6-bbe3-6eec420cc2e2",
  "failureType": "BrokerUnreachableException",
  "attemptCount": 2,
  "retryDelaySeconds": 8
}
```

## Unsafe examples

Do not write events like these:

```csharp
logger.LogWarning("Login failed for {Email} with token {Token}", email, token);
logger.LogError(exception, "Receipt OCR failed for body {Payload}", requestBody);
logger.LogInformation("Transaction {Transaction}", JsonSerializer.Serialize(transaction));
logger.LogDebug("Request URL {Url}", context.Request.GetDisplayUrl());
```

The safe replacement records the technical outcome only:

```csharp
GatewayOperationalLog.AuthenticationRejected(logger, routeKey, authenticationResult);
GatewayOperationalLog.DestinationCallFailed(logger, routeKey, destinationKey, exception.GetType().Name);
```

## Code-review checklist

A reviewer must verify that every new or changed operational log:

- uses the service event catalog rather than direct `logger.Log*` templates;
- has a unique stable event ID and name in the correct range;
- uses only allowlisted structured fields;
- inherits correlation metadata from the approved scope or event envelope;
- does not accept an `Exception` parameter;
- does not serialize an object, request, response, event payload, or document;
- does not log path/query/header/body data;
- does not log user/session/account IDs, credentials, financial data, OCR content, or LLM content;
- logs enum/status values rather than free-form provider messages;
- includes a regression test when a new field or event is introduced;
- keeps public error responses generic and correlation-based.

## Automated enforcement

The gateway and identity test projects inspect `LoggerMessageAttribute` metadata and fail when:

- event IDs are duplicated or outside the assigned range;
- event names do not follow the service naming convention;
- a template or method parameter contains a prohibited field;
- a structured field is not in the service allowlist;
- an operational event accepts an exception object.

These tests are guardrails, not a substitute for review. New services should create their own event catalog and policy test using this document as the baseline.

## Retention and access

Production log retention, export, and access controls are deployment concerns, but they must follow data minimization:

- shortest retention compatible with incident response;
- least-privilege access;
- encrypted transport and storage;
- no production logs copied into tickets or chat without redaction;
- no unrestricted developer access to production logs;
- alerts and dashboards use stable event IDs/names, not full-text searches for user data.
