# Shared Observability Baseline

`FinancialAssistant.Shared.Observability` is the provider-neutral ASP.NET Core
baseline for backend APIs. It owns technical logging and correlation behavior
only; it has no financial domain, business-state persistence, exporter, or billing ownership.

API hosts call `AddFinancialAssistantObservability()` before registering their
services and `UseFinancialAssistantCorrelation()` before authentication or
endpoint middleware. The public gateway retains its equivalent request
middleware while using the shared JSON logging and outbound propagation setup.

The baseline:

* emits UTC JSON console records with scopes enabled;
* validates `correlationId` and `X-Correlation-Id`, generating a safe value when
  neither input is valid;
* returns both correlation headers and `X-Trace-Id`;
* scopes request logs with `CorrelationId`, `TraceId`, `ServiceName`,
  `Environment`, and `RequestMethod`;
* propagates correlation and trace identifiers through registered `HttpClient`
  instances;
* exposes exception type only through `SafeErrorFields`.

No request/response body, credential, token, financial value, personal identity,
raw OCR content, or LLM prompt/response belongs in an operational log. RabbitMQ
publishers and consumers must preserve the existing envelope correlation and
causation identifiers.

FIN-277 adds the [private native diagnostic sink](../../../docs/engineering/windows-native-adapters.md).
It preserves console logging and writes only fixed UTC/event-ID/severity/W3C trace
metadata to a separate protected NTFS root. It never formats messages or serializes
state, exception text, scopes or categories. Queue/file/retention bounds and explicit
drop/storage-failure visibility do not replace Audit authority or the retained native
collector/Elasticsearch/alert qualification. Disabled/missing native diagnostics keep
readiness unhealthy without blocking financial HTTP or enabling a cloud exporter.
