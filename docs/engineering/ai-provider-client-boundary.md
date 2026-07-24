# AI provider client boundary

FIN-108 keeps external LLM calls behind the application-owned `ILlmProvider` interface. Financial services depend on named AI capabilities, not provider SDKs, endpoints, models, or transport errors.

## Adapter composition

A provider-specific adapter implements `ILlmProvider` and maps its transport payload into `LlmProviderResponse`. The adapter can be wrapped by `ResilientLlmProvider` before it is registered with `RegisteredLlmProviderResolver`.

The wrapper provides:

- a bounded timeout for each attempt;
- at most three total attempts;
- retries only for safe `LlmProviderException` values marked transient, including wrapper-generated timeouts;
- caller cancellation that is never retried or remapped;
- safe mapping of unknown provider exceptions without exposing raw provider details.

Response JSON is still validated against the versioned prompt schema by `AiOrchestrationService`. Token usage from a successful provider response is captured in privacy-safe call metadata; raw input, prompts, output, and provider errors are excluded.

## Environment configuration

.NET configuration maps environment variables with double underscores. Non-secret provider settings use:

- `AiOrchestration__Provider__Name`
- `AiOrchestration__Provider__Model`
- `AiOrchestration__Provider__Endpoint`
- `AiOrchestration__Provider__RequestTimeoutSeconds`
- `AiOrchestration__Provider__MaximumAttempts`
- `AiOrchestration__Provider__RetryDelayMilliseconds`

Provider name, model, and endpoint must be supplied together. The endpoint must use HTTPS and cannot contain URI user information, preventing credentials from being embedded in non-secret options. Timeout is limited to 1-120 seconds and retry delay to 0-5000 milliseconds.

When identity settings are valid, the API composition root registers the `transaction.parse` model route from them. `AiProviderClientOptions.CreateResilienceOptions` produces the validated runtime settings used to wrap a provider-specific adapter. Its attempt ceiling is derived from the registered transaction prompt policy, currently two total attempts, so environment configuration cannot exceed the approved retry budget.

Credentials are intentionally absent from `AiProviderClientOptions`. A provider adapter must obtain credentials from its approved environment or secret-store integration without logging or adding them to application configuration files.

## Error contract

Provider adapters use stable, privacy-safe error codes in `LlmProviderException` and mark only genuinely transient failures as retryable. After the configured attempts are exhausted, or for any non-transient failure, the application returns a generic provider failure and records only the technical call status.

Unknown adapter exceptions are converted to `provider_failure`; their raw messages and inner exceptions do not cross the application boundary.
