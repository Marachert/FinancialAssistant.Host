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

Service identity and suggestion-only authority are non-secret defaults in `appsettings.json`. Development provider name, model, and endpoint are empty placeholders in `appsettings.Development.json`. Credentials must be supplied through environment-backed provider adapters in later delivery work and must never be committed or logged.
