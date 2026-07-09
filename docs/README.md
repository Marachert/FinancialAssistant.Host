# Documentation

Developer, architecture, security, API, event, and delivery documentation workspace for Financial Assistant.

## Canonical folders

```text
docs/architecture/  System context, service boundaries, ownership, and architecture decisions
docs/api/           Client-facing REST contracts and service integration APIs
docs/events/        RabbitMQ event contracts and asynchronous delivery rules
docs/security/      Security, privacy, abuse protection, and operational safety
docs/delivery/      Implementation sequencing, release readiness, and verification evidence
docs/engineering/   Detailed implementation and engineering guides
docs/reviews/       Review records and acceptance evidence
```

## Documentation rules

- Link stable documents to the related Jira work when practical.
- Keep service-owned implementation details near the service or under engineering documentation.
- Distinguish synchronous REST flows from asynchronous RabbitMQ flows.
- Keep backend deterministic logic authoritative for financial data and calculations.
- Treat OCR and LLM output as probabilistic input that requires backend validation.
- Use synthetic examples only; do not publish secrets, tokens, real user data, financial records, receipt content, OCR text, or LLM prompts/responses.
