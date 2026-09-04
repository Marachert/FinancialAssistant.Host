# Structured Logging and Correlation Baseline

Status: Implemented for the POC
Owner: Platform and service owners
Related Jira: FIN-191

## Purpose

This is the implementation contract for privacy-safe structured logging and
correlation in every Financial Assistant backend HTTP host. It implements the
[backend observability strategy](../architecture/backend-observability-strategy.md)
without enabling an external collector or paid telemetry service.

The shared implementation lives in
`backend/shared/observability/FinancialAssistant.Shared.Observability`.
Business services retain ownership of their operational events, event-ID ranges,
RabbitMQ envelopes, metrics, dashboards, alerts, and runbooks.

## Host bootstrap

Every service API registers the baseline before its service dependencies:

```csharp
builder.AddFinancialAssistantObservability();
```

Every service API installs correlation before authentication, authorization,
custom request auditing, or endpoint execution:

```csharp
app.UseFinancialAssistantCorrelation();
```

The public gateway keeps its stricter gateway request middleware because it also
owns routing duration and gateway-specific diagnostics. It still calls
`AddFinancialAssistantObservability()` for the same JSON console format and
outbound `HttpClient` behavior.

## JSON and scope contract

The local and POC sink is UTC JSON console output with scopes enabled. Each
request scope contains:

| Field | Source and constraint |
| --- | --- |
| `CorrelationId` | Validated opaque value, maximum 128 characters |
| `TraceId` | W3C 32-character trace identifier |
| `ServiceName` | Controlled application/assembly name |
| `Environment` | Controlled host environment |
| `RequestMethod` | HTTP method only |

Service-owned operational events use source-generated `LoggerMessage`
definitions, stable numeric `EventId` values, stable PascalCase event names,
constant templates, explicit levels, and allowlisted low-cardinality fields.
Do not interpolate or serialize arbitrary objects into a log message.

## HTTP correlation

Accepted inbound headers are:

```text
correlationId
X-Correlation-Id
```

The primary header wins. A value is accepted only when it is non-empty, no more
than 128 characters, and contains no control or whitespace characters. Invalid
or missing input is replaced with a 32-character opaque GUID. Application code
receives the resolved value through both request headers,
`HttpContext.TraceIdentifier`, and the
`FinancialAssistant.CorrelationId` context item.

Every response contains both correlation headers and:

```text
X-Trace-Id
```

The registered `CorrelationPropagationHandler` applies the resolved correlation
identifier and current W3C trace identifier to every `HttpClient` created by
`IHttpClientFactory`. A caller must not put a user, account, session,
transaction, receipt, merchant, or provider identifier into these headers.

## Asynchronous correlation

RabbitMQ publishers preserve `CorrelationId`, `CausationId`, and W3C trace
context in the versioned integration-event envelope. Consumers start or
continue a trace from that envelope, establish the same safe log scope, and
create a new causation link when they publish follow-up work. Message bodies are
never copied into operational logs.

## Safe failures

Operational failure events may record:

```text
FailureType
AttemptCount
RetryDelaySeconds
DependencyKey
StatusCode
```

`SafeErrorFields.From(exception)` returns only `FailureType`, using the runtime
type name. Exception messages, stack traces, inner exceptions, request bodies,
response bodies, arbitrary headers, credentials, tokens, personal identities,
financial values, receipts, raw OCR text, prompts, completions, and provider
payloads are prohibited.

## Cost and exporter boundary

This baseline adds no external exporter, collector account, notification
destination, production credential, or paid service. Export, retention,
sampling, and alert delivery require explicit privacy, security, availability,
and cost approval under OBS-005. Core financial behavior must remain available
when optional telemetry export is disabled.

## Verification

`FinancialAssistant.Shared.Observability.Tests` verifies safe inbound
correlation, generated identifiers, canonical response headers, request scopes,
outbound propagation, W3C trace identifiers, and type-only error fields.
Repository conformance tests verify that every API host references and registers
the shared package, with the documented gateway middleware exception.
