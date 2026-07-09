# Security Documentation

This folder contains security boundaries, threat assumptions, privacy rules, and operational security guidance for Financial Assistant.

## Scope

Security documentation should cover:

* authentication, session, token, and provider boundaries;
* gateway perimeter controls and trusted user context;
* authorization and resource ownership;
* secrets and configuration management;
* privacy-safe logging and monitoring;
* rate limiting and abuse protection;
* receipt, OCR, LLM, notification, and file-storage risks;
* data minimization, retention, and deletion expectations;
* incident-oriented verification checklists.

## Rules

Never commit production credentials, private keys, access tokens, refresh values, provider secrets, phone verification codes, or real user/financial data.

Security controls must be enforced by backend and infrastructure components. Client-side checks improve UX but are not authorization boundaries.

LLM and OCR providers receive only the minimum approved data. Their output remains untrusted until backend schema and business validation completes.

Operational documentation must use sanitized examples and must not expose internal credentials, raw financial records, receipt content, OCR text, or LLM prompts/responses.
