# AI Orchestration Service

The AI Orchestration Service owns all LLM provider integration boundaries. Other services request a named capability through `IAiOrchestrationService`; they do not reference provider SDKs.

The FIN-24 foundation includes:

- `ILlmProvider` and provider resolution by stable provider name;
- capability-to-provider/model routing through `IModelRouter`;
- versioned prompts through `IPromptRegistry`;
- structured output validation before any result is returned;
- call and token-usage metadata storage that cannot contain raw input, prompt text, output, or provider errors.

Runtime provider adapters and durable metadata storage are intentionally separate infrastructure additions. The current adapter is explicitly in-memory and suitable only for this delivery increment.
