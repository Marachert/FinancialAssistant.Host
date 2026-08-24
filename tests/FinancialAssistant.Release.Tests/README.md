# Backend Release Tests

This project is the executable synthetic release gate for the first-user POC.
It composes existing service test hosts and public contracts; it does not bypass
service validation or access another service's private store except to observe
the authoritative record produced by the owning test host.

The core flow covers registration, sign-in, free-form intake, draft review,
confirmation, authoritative Expense creation, Analytics dashboard projection,
and deterministic Financial Score output. Cross-host propagation uses the same
versioned financial lifecycle envelope as runtime consumers.

Contract tests verify critical OpenAPI paths, event-envelope fields and version,
and service-owned Elasticsearch index/alias mappings. Privacy tests inspect
operational Monitoring, Audit, and MCP contracts plus structured log templates.

All identities and financial values are synthetic. The suite uses in-memory
adapters and requires no live AI/OCR provider, Docker dependency, paid credit,
or external account.
