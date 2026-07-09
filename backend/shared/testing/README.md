# Shared Testing Utilities

This folder is reserved for deterministic test helpers that can be reused across backend test projects.

Allowed examples:

* synthetic test-data builders;
* deterministic clocks and identifier generators;
* fake message transports and provider adapters;
* reusable assertion helpers;
* privacy-safe log and contract test utilities;
* local integration-test infrastructure helpers.

Not allowed:

* production runtime dependencies;
* real user, credential, transaction, receipt, OCR, or LLM data;
* shared mutable fixtures that make tests order-dependent;
* service-owned domain logic hidden inside test helpers;
* network calls to production providers;
* secrets or environment-specific credentials.

Test helpers must preserve service ownership. A helper may provide a fake transport or synthetic fixture, but each service test suite remains responsible for verifying its own domain rules and persistence behavior.
