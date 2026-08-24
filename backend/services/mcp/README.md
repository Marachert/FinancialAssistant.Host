# MCP Server

The MCP Server is the internal policy boundary for role-controlled operational
tools. It uses the official C# Model Context Protocol SDK and stateless
Streamable HTTP at `/mcp`.

## Allowlisted tools

- `system_health`: safe aggregate service, RabbitMQ, and Elasticsearch status;
- `ai_cost_summary`: aggregate request, token, and estimated-cost counters;
- `parsing_quality`: aggregate success, review-required, and failure counters;
- `prompt_eval_summary`: aggregate evaluation status without prompts/responses;
- `jira_issue_draft`: local draft generation only; it never submits to Jira;
- `architecture_lookup`: exact-key lookup for four governed Confluence pages.

The registry contains no arbitrary Elasticsearch, SQL, filesystem, shell, HTTP,
or production-write tool. MCP never accesses another service's storage.

## Security and audit

`X-Mcp-Authentication` must match a 32-character environment-provided shared
secret. `X-Mcp-Roles` is reduced to `admin`, `operator`, `developer`, and `qa`.
The SDK authorization filter removes unauthorized tools from protocol listings
and rejects unauthorized calls; the application registry repeats the role check.

Every `/mcp` request produces privacy-safe protocol audit evidence. Every tool
handler records its allowlisted name, outcome, correlation ID, safe failure
category, and timestamp. Audit payloads exclude prompts, provider responses,
financial values, receipt/OCR text, personal identifiers, secrets, and arbitrary
metadata. `InMemory` is the default POC audit adapter; `Http` posts the same safe
contract to Audit Service and fails closed when that adapter is unavailable.

## Configuration

Secrets are never stored in this repository. Configure:

- `Mcp__Authentication__SharedSecret`;
- optional `Mcp__Monitoring__BaseAddress` and
  `Mcp__Monitoring__SharedSecret`;
- for central audit, `Mcp__Audit__Mode=Http`, `Mcp__Audit__BaseAddress`, and
  `Mcp__Audit__SharedSecret`.

Prompt evaluation counters are aggregate configuration/projection values. No
paid provider or external credit is required by this service.
