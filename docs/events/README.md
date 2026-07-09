# Event Documentation

This folder contains asynchronous integration-event contracts and RabbitMQ delivery conventions for Financial Assistant.

## Scope

Event documentation should define:

* event name, version, and owning publisher;
* business meaning and publication trigger;
* payload schema and privacy classification;
* routing and consumer expectations;
* idempotency and duplicate-delivery behavior;
* ordering assumptions;
* retry, dead-letter, and failure-handling rules;
* compatibility and deprecation policy.

## Rules

The service that owns the state change owns the event intent. The Public API Gateway does not publish domain events for proxied requests.

Events must not be used as a hidden shared database. Consumers build their own state or read models and must tolerate duplicate or delayed delivery.

Event payloads should contain only the minimum information needed by consumers. Secrets, credentials, access tokens, raw receipt images, raw OCR text, unrestricted LLM content, and unnecessary financial details are prohibited.

Stable contract source may live in service-owned contract projects or approved shared integration-contract packages and should be linked from this folder.
