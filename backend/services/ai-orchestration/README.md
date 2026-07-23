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

Runtime provider adapters and durable metadata storage are intentionally separate infrastructure additions. The current adapter is explicitly in-memory and suitable only for this delivery increment.
