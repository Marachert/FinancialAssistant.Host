# AI orchestration foundation

## Service boundary

All external LLM calls cross the AI Orchestration application boundary. A caller supplies a named capability, registered prompt name/version, and transient input. Model routing resolves a provider and model without exposing provider SDKs to callers.

Prompt templates and JSON schemas are versioned together. Omitting a prompt version selects the latest registered version. Provider output is parsed and validated before it can leave the service.

## Structured output validation

The foundation validator supports the JSON Schema keywords used by current structured capabilities: `type`, `properties`, `required`, boolean `additionalProperties`, `items`, `enum`, `minItems`, `maxItems`, `minLength`, `maxLength`, `minimum`, and `maximum`. Unsupported validation keywords, unsupported schema types, and malformed registered schemas fail closed.

## Privacy and financial authority

Only technical metadata is stored: call ID, capability, prompt name/version, provider/model, status, token counts, and timestamps. Raw user input, rendered prompts, structured output, provider errors, and provider credentials are never part of the metadata contract.

LLM output remains probabilistic input. Downstream deterministic services must validate and confirm financial entities before treating them as source of truth.
