# Monitoring Web UI

This folder is the canonical workspace for the Financial Assistant internal monitoring and administration web client.

## Responsibilities

The monitoring UI may present approved operational information such as:

* service health and deployment status;
* sanitized gateway route diagnostics;
* queue, cache, storage, and provider availability summaries;
* safe processing-state and failure summaries;
* support-oriented correlation identifiers and non-sensitive diagnostics.

## Boundaries

The UI must use admin-protected REST APIs through the Public API Gateway.

It must not:

* read Elasticsearch, RabbitMQ, Redis, MinIO, or service databases directly;
* expose raw financial records, receipt images, OCR text, prompts, LLM responses, tokens, secrets, or credentials;
* infer admin authority from client-controlled headers or local state;
* implement service-owned business rules or financial calculations;
* become a general accounting interface for ordinary users.

The owning backend service decides which operational data is safe to expose. The gateway enforces the configured admin perimeter.

## Planned structure

```text
web-admin/monitoring-ui/
  src/
    app/
    features/
    shared/
    api/
```

Framework scaffolding and UI implementation belong to dedicated frontend tasks. FIN-47 establishes only the canonical source boundary.
