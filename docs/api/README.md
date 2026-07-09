# API Documentation

This folder contains client-facing and service integration API contracts for Financial Assistant.

## Scope

API documentation should include:

* public REST paths exposed through the Public API Gateway;
* request and response schemas;
* authentication and authorization requirements;
* stable error codes and correlation behavior;
* pagination, idempotency, retry, and rate-limit expectations;
* service ownership for each operation;
* versioning and compatibility rules.

## Rules

The Public API Gateway is the only client-facing HTTP entry point. Clients must not depend on internal service addresses.

API contracts must not move business logic into the gateway. The owning service remains responsible for domain validation, resource ownership, deterministic calculations, persistence, and event publication.

Examples must use synthetic data and must not contain secrets, real tokens, real financial records, receipt content, OCR text, or LLM prompts and responses.

OpenAPI artifacts may be added here or generated from service-owned source contracts when dedicated implementation tasks establish the generation workflow.
