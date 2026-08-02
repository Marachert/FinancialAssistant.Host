# Documentation

Developer, architecture, security, API, event, and delivery documentation workspace for Financial Assistant.

## Start here

New contributors should begin with:

```text
docs/delivery/developer-onboarding.md
docs/delivery/local-developer-smoke-test.md
```

Repository entry point:

```text
README.md
```

Contributor and CI details:

```text
docs/engineering/contributing.md
docs/engineering/ci.md
```

Platform storage conventions:

```text
docs/engineering/elasticsearch-index-naming.md
```

Platform messaging conventions:

```text
docs/events/rabbitmq-topology.md
docs/events/event-contract-versioning.md
```

Canonical financial calculation policy:

```text
docs/architecture/financial-source-of-truth.md
```

Financial core ownership and interaction boundaries:

```text
docs/architecture/financial-core-service-boundaries.md
```

Financial core end-to-end API and event flow:

```text
docs/architecture/financial-core-e2e-flow.md
```

Financial summary projection baseline:

```text
docs/engineering/financial-summary-read-model.md
```

Analytics dashboard projection and API:

```text
docs/engineering/analytics-dashboard-read-model.md
docs/api/analytics-dashboard-v1.md
```

Deterministic financial score and history API:

```text
docs/engineering/financial-score-v1.md
docs/api/financial-score-v1.md
```

Financial core validation coverage:

```text
docs/engineering/financial-core-validation-test-plan.md
```

## Canonical folders

```text
docs/architecture/  System context, service boundaries, ownership, and architecture decisions
docs/api/           Client-facing REST contracts and service integration APIs
docs/events/        RabbitMQ event contracts and asynchronous delivery rules
docs/security/      Security, privacy, abuse protection, and operational safety
docs/delivery/      Onboarding, sequencing, release readiness, and verification evidence
docs/engineering/   Detailed implementation, CI, and contributor guides
docs/reviews/       Review records and acceptance evidence
```

## Documentation rules

- Link stable documents to the related Jira work when practical.
- Keep service-owned implementation details near the service or under engineering documentation.
- Distinguish synchronous REST flows from asynchronous RabbitMQ flows.
- Keep backend deterministic logic authoritative for financial data and calculations.
- Treat OCR and LLM output as probabilistic input that requires backend validation.
- Update commands and repository paths in the same pull request as the implementation change.
- Use synthetic examples only; do not publish secrets, tokens, real user data, financial records, receipt content, OCR text, or LLM prompts/responses.
