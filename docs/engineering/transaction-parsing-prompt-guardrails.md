# Transaction-parsing prompt guardrails

FIN-107 registers the first reviewable prompt definition for the `transaction.parse` capability. The prompt and its response schema share version `1`; callers that omit a version receive the latest registered definition.

## Template rules

The template treats free-form input as transient, untrusted data. It instructs the provider to:

- return JSON that matches the registered schema, without prose or extra fields;
- produce suggestions only and never confirm or persist financial records;
- use only supplied input and locale context;
- avoid inferring identity, account, card, address, or contact data;
- avoid echoing raw input or unnecessary personal data;
- represent unknown fields with `null` and stable `missingFields` values;
- bound confidence values from `0` to `1`;
- expose material uncertainty through stable ambiguity records.

Provider adapters receive the template and transient input separately. Neither value belongs in AI call metadata or application logs.

## Response schema

The versioned schema accepts only:

- suggested type, amount, currency, date, merchant, category, and note;
- overall and per-field confidence;
- bounded ambiguity records and candidate values;
- stable missing-field names;
- a short, privacy-safe review explanation.

Every object rejects additional properties, and suggested dates must be real ISO calendar dates. Authority fields such as `confirmed`, `persisted`, or `approved` are not part of the schema. AI Orchestration assigns technical call identity, while deterministic Transaction Intake remains responsible for validation and confirmation.

## Retry and fallback

The registered policy allows at most two attempts: the initial call and one retry. A provider adapter may retry only a transient provider failure or a response that fails the registered schema. The retry must use the same prompt version and schema.

Cancellation, authentication, authorization, configuration, unsupported-input, and deterministic validation failures are not retryable. The baseline does not fall back to a second provider, which avoids sending the same financial input to another processor without an explicit routing and privacy decision.

After attempts are exhausted, provider output is discarded and the stable fallback code is `manual_review_required`. The caller must keep the transaction unconfirmed and ask the user for manual review or entry.

## Versioning

Released prompt versions are immutable. A behavioral, guardrail, or schema change adds a new `PromptDefinition` with a higher version and corresponding tests. This keeps pull-request diffs auditable and allows callers to pin a known version during rollout.
