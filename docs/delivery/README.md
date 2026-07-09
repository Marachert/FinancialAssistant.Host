# Delivery Documentation

This folder contains implementation sequencing, release readiness, deployment, verification, and operational handoff documentation for Financial Assistant.

## Scope

Delivery documentation should include:

* phased implementation plans and increments;
* dependency and rollout order;
* environment and deployment requirements;
* migration and rollback considerations;
* release acceptance criteria;
* CI/CD and manual verification evidence;
* monitoring and support handoff;
* known limitations and follow-up work.

## Rules

Documents must separate MVP requirements from later enterprise hardening.

Every delivery record should identify the owning Jira item, affected services or clients, dependencies, test evidence, and merge/deployment status.

Delivery plans must preserve service ownership and must not activate a gateway route before the owning service contract, security integration, destination configuration, and verification are ready.

Examples and evidence must remain privacy-safe and must not include production secrets, tokens, real user data, financial records, receipt content, OCR text, or LLM prompts/responses.
