# AI Orchestration service baseline

FIN-105 turns the existing orchestration core into a template-aligned .NET 8 service boundary.

## Projects

- `FinancialAssistant.AiOrchestration.Api` owns process hosting, health, readiness, correlation, OpenAPI, and service information.
- `FinancialAssistant.AiOrchestration.Contracts` owns provider-neutral capability and suggestion contracts.
- Application owns orchestration and the `ILlmProvider` boundary.
- Infrastructure owns routing, prompt registry, schema validation, and privacy-safe metadata adapters.
- Domain remains independent of provider SDKs and financial record services.

## Authority

AI output is suggestion data. `AiCapabilityResult` contains an `AiSuggestionReview` envelope, and the baseline returns unverified output with `RequiresReview = true`. No API or application path writes confirmed income, expense, balance, limit, score, or other authoritative financial state.

## Configuration

Service identity and suggestion-only authority are non-secret defaults in `appsettings.json`.
Provider credentials and runtime selection must come from environment-backed
configuration and must never be committed or logged. Provider configuration,
disabled/fallback behavior and usage limits are documented in
[provider configuration](ai-ocr-provider-configuration.md) and
[cost controls](ai-ocr-usage-cost-controls.md). Existing abstractions and tests do
not authorize or prove a live paid provider integration.
