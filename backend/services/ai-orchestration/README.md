# AI Orchestration Service

The AI Orchestration Service owns all LLM provider integration boundaries. Other services request a named capability through `IAiOrchestrationService`; they do not reference provider SDKs.

The FIN-24 foundation includes:

- `ILlmProvider` and provider resolution by stable provider name;
- capability-to-provider/model routing through `IModelRouter`;
- versioned prompts through `IPromptRegistry`;
- structured output validation before any result is returned;
- call and token-usage metadata storage that cannot contain raw input, prompt text, output, or provider errors.

FIN-105 adds the shared service baseline:

- `FinancialAssistant.AiOrchestration.Api` with liveness, readiness, correlation, OpenAPI, and service-info endpoints;
- `FinancialAssistant.AiOrchestration.Contracts` for named capability requests and suggestion-only results;
- an explicit review envelope whose unverified AI output always requires deterministic review;
- empty development provider placeholders without credentials or production values.

The baseline does not expose a capability execution endpoint and does not register a provider adapter. Those behaviors are delivered by later provider and capability tasks. The API cannot write confirmed financial records.

FIN-106 defines the provider-neutral natural-language transaction parsing contract. It includes typed draft suggestions, overall and per-field confidence, explicit ambiguities and missing fields, and a user-review explanation. The response always serializes `outputAuthority: "suggestion"` and `requiresReview: true`; deterministic Transaction Intake remains responsible for validation and confirmation.

FIN-107 registers version 1 of the `transaction.parse` prompt and fail-closed response schema. The template minimizes personal data, treats input as untrusted, requires explicit confidence and ambiguity output, and rejects authority fields. Its policy permits one bounded retry for transient or schema-invalid results, then requires manual review without retaining provider output.

FIN-108 completes the provider client boundary with reusable per-attempt timeout and transient retry behavior, safe provider error mapping, and environment-bound non-secret endpoint/model/resilience settings. Provider-specific adapters still remain separate and must obtain credentials from an approved secret source.

FIN-114 extends safe call metadata with request/trace correlation, processing duration, nullable confidence, and bounded failure categories. Metadata may include provider/model keys, prompt identity/version, status, token counts, and timestamps, but never raw input, prompt templates, model output, exception messages, or stack traces.

Runtime provider adapters and durable metadata storage are intentionally separate infrastructure additions. The current adapter is explicitly in-memory and suitable only for this delivery increment.
