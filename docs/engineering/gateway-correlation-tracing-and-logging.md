# Gateway Correlation, Tracing, and Request Logging

## Purpose

FIN-72 defines privacy-safe request observability at the Public API Gateway boundary.

Every external request receives correlation and trace metadata that can be followed across synchronous backend calls without logging raw personal or financial payloads.

## Request flow

```text
client
-> correlation middleware
-> safe exception boundary
-> rate limiting and access policy
-> route dispatch
-> owning service REST API
```

The gateway does not log request or response bodies.

## Correlation metadata

Accepted request headers:

```text
correlationId
X-Correlation-Id
traceparent
tracestate
```

Resolution order:

1. valid `correlationId`;
2. valid `X-Correlation-Id`;
3. generated GUID.

Correlation identifiers are limited in length and control characters are rejected.

Response headers:

```text
correlationId
X-Correlation-Id
X-Trace-Id
Server-Timing: gateway;dur=<milliseconds>
```

The same correlation identifier is stored in `HttpContext.Items`, added to the logging scope, and forwarded to downstream services.

## Trace forwarding

Incoming W3C `traceparent` and `tracestate` headers are preserved for downstream dispatch. Standard .NET HTTP diagnostics create or continue the outgoing activity.

The gateway also forwards canonical correlation headers and overwrites `X-Gateway-Route-Key` with the trusted configured route key.

## Request duration logging

The correlation middleware measures the full gateway request duration.

Structured fields:

```text
CorrelationId
TraceId
RequestMethod
StatusCode
ElapsedMilliseconds
```

The middleware deliberately does not log:

* request path or query values;
* request or response body;
* email, phone, password, access token, refresh token, provider token, or OTP;
* transaction amount, merchant, category input, or raw note;
* receipt image, receipt text, OCR output, or file metadata;
* LLM prompt or response;
* downstream response payload.

Route-level operational logging may use the configured route key, but never raw path parameters or query values.

## Safe error boundary

Unhandled gateway exceptions return:

```json
{
  "type": "https://errors.financial-assistant.app/gateway/internal-error",
  "title": "The request could not be completed.",
  "status": 500,
  "code": "internal_error",
  "detail": "An unexpected gateway error occurred.",
  "correlationId": "..."
}
```

The response uses `application/problem+json` and `Cache-Control: no-store`.

The public response never includes exception messages, stack traces, internal hostnames, destination keys, credentials, request bodies, receipt text, or financial values.

Operational error logs contain only the exception type together with the existing correlation/trace scope. Exception messages and stack traces are intentionally excluded from this PoC policy because they may contain user or provider data.

## Client cancellation

When the client disconnects, the gateway records a neutral cancellation event. It does not attempt to create a new error response after the request is aborted.

Destination-specific timeouts remain separate from client cancellation and return the safe `504 destination_timeout` response when possible.

## Troubleshooting workflow

1. Obtain the `correlationId` or `X-Trace-Id` from the client response.
2. Search gateway operational logs using the exact correlation or trace identifier.
3. Check request method, status code, duration, route key, and failure type.
4. Follow the same trace/correlation values in the owning backend service.
5. Do not request screenshots or copies of passwords, tokens, raw receipts, transaction notes, OCR output, or LLM prompts.
6. Use synthetic data for reproduction.

Example safe investigation record:

```text
CorrelationId: 9be5c332-7b92-4cc8-945b-3a97e31f1880
TraceId: 4bf92f3577b34da6a3ce929d0e0e4736
RequestMethod: POST
RouteKey: transaction-intake
StatusCode: 503
ElapsedMilliseconds: 1512.8
FailureType: HttpRequestException
```

## Responsibility boundaries

### Gateway owns

* correlation identifier generation and propagation;
* W3C trace-context preservation;
* request duration measurement;
* privacy-safe gateway request lifecycle logs;
* safe technical error responses.

### Owning services own

* domain validation and business errors;
* financial calculations;
* service-specific tracing spans;
* service-owned Elasticsearch data;
* RabbitMQ event correlation;
* OCR/LLM operation telemetry without raw content.

FIN-82 will define the broader operational log policy shared by Gateway and Identity. FIN-72 implements the gateway request boundary without pre-empting that cross-service policy.

## Verification

Automated tests verify:

* generated correlation identifiers;
* propagated correlation identifiers;
* incoming W3C trace ID continuity;
* `X-Trace-Id` response metadata;
* `Server-Timing` duration metadata;
* safe unhandled `500 internal_error` responses;
* absence of password, receipt text, and financial input in public errors;
* existing downstream correlation and trace forwarding.

Run:

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```
