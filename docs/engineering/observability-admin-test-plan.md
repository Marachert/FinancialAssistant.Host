# Observability And Admin Test Plan

Related Jira: FIN-199, parent FIN-36. This is a test-plan definition, not a report
that every scenario has passed. The [scenario inventory](observability-admin-test-plan.json)
records planned cases and existing evidence starting points. A source test file
does not prove the entire row or a deployment. Do not convert planned cases into
passed results from this document's contract checks.

## Scope And Entry Criteria

Cover logs, health, audit, admin access, failed jobs and alert rules, including
privacy and MCP diagnostics. Follow the [observability contract](../architecture/backend-observability-strategy.md),
[support workflow](failed-job-support-workflow.md) and
[alert rules](alerting-and-incident-priorities.md). Financial truth stays with
deterministic owner services; no diagnostic test repairs or confirms a transaction.

Use synthetic data, fake clocks, in-process ASP.NET hosts, stub transports and
local browser request interception by default. Deny unexpected outbound calls.
Do not use real accounts, receipts, OCR, prompts or tokens. No provider, paging
destination, paid exporter, administrator provisioning or infrastructure is
activated by this plan. Host-level cases require an explicitly approved isolated
test environment, owners, privacy/retention and maximum-spend approval first.

Record exact commit, build, target environment, toolchain and dependency versions,
coverage inventory, synthetic fixture version and owner before execution. Empty,
disabled or unwired sources must not be replaced with demo records in the app.

## Scenario Matrix

Each row supplies a stimulus and observable oracle. Inventory evidence paths are
existing partial automated coverage or implementation-contract references; gaps
remain planned until executed. `offline` needs no deployed external service;
`approved-host` is separately gated and must not run as an automatic fallback.

| ID | Area / stimulus | Required result and evidence |
| --- | --- | --- |
| OBS-LOG-01 | Capture representative successful, denied and failed requests with synthetic canaries in body, headers, URL values and exception details | Parse emitted JSON, assert stable event ID/name, constant template, UTC time, level, bounded fields and no canaries; inspect captured sink rather than only property names |
| OBS-LOG-02 | Invalid, overlong, newline, identity-shaped correlation; HTTP-to-event propagation and duplicate delivery | Normalize/reject unsafe input without reflecting it, preserve approved technical correlation across owner boundaries; no user IDs in metric labels or raw payload logs |
| OBS-HLT-01 | Healthy/degraded/unavailable/unknown results; dependency timeout with live process | Liveness does no dependency/provider work; required readiness follows200/503 contract, optional loss does not fail unrelated core; no-store and safe error category, no exception/body leak |
| OBS-HLT-02 | Missing config, malformed/partial response, stale producer, failed probe with numeric zeros | Unknown/not-configured visible, not green; failed/default-zero totals cannot prove recovery or empty queue; record current adapter limitations as failures of readiness evidence, not silently passing them |
| OBS-AUD-01 | Accepted event, same-ID duplicate, conflicting replacement, invalid action/domain and legacy envelope | One authoritative append, conflict rejected, catalog and legacy constraints enforced; no update/delete correction API; query expiry enforced with fake clock |
| OBS-AUD-02 | Untrusted ingestion, unauthorized query, malformed actor/correlation, exception canaries | Authentication/role denial before data access; safe responses/logs; authorized pseudonymous audit hashes remain within Audit scope and must not flow into Monitoring aggregates |
| OBS-AUD-03 | Approved-host persistent append, process restart, expiry and restore using synthetic events | Durable append/replay/retention verified by Audit owner; in-memory tests do not certify restart durability or backup erasure; no cross-service storage writes |
| OBS-ADM-01 | No/invalid/expired token, ordinary user, forged admin role, spoofed gateway trust, direct service route | Missing/invalid trust401, authenticated non-admin403 where specified; upstream strips client trust headers and service independently validates trust/role; no privileged data or side effects |
| OBS-ADM-02 | Late200/401/403 from old session after logout/relogin, failed refresh and expired session | Old response cannot contaminate replacement session; logout/authorization failure clears visible data and authority; no tokens in storage, cookies, logs or URLs |
| OBS-ADM-03 | Proxy unknown path/method, foreign Origin, redirect, oversize payload and upstream timeout | Exact route/origin/method allowlists; no gateway-secret forwarding from browser; bounded response/time, no-store and CSP; generic error without reflecting upstream data |
| OBS-ADM-04 | Approved-host browser sign-in, role revocation, hidden-tab refresh, keyboard/mobile viewport and network failure | Real gateway/service authorization, fresh/empty/unavailable UI distinctions, disabled support lookup, no raw fields; opt-in30-second refresh pauses hidden; no screenshots containing real data |
| OBS-JOB-01 | Unknown source/kind/state/category, same-revision conflict, duplicate/out-of-order update,201 entries,24-hour expiry | Reject unsafe fields/relationships, idempotent duplicates do not extend TTL, latest revision wins, capacity200; restart/eviction remains a visibility gap, not success |
| OBS-JOB-02 | Tabletop AI/OCR/notification timeout, permanent failure, suppression, cancellation, exhausted budget and ambiguous acknowledgement | Apply separate provider/job/broker retry contracts; no blind replay, budget reset or send; unknown attempts stay unknown; safe messages and restricted notes, no financial confirmation |
| OBS-JOB-03 | Approved-host synthetic producer-to-Monitoring and owner recovery with source restart | Verify actual ownership, observation freshness and state mapping; random monitoring UUID/revision are not domain authorization/attempt counts; producer wiring is not assumed from signal endpoint tests |
| OBS-ALT-01 | Offline alert inventory, rule IDs, priority, source prerequisites and support links | Required categories present, destination null and enabled false; definitions include recovery/unknown/owner targets; no evaluator or paging success inferred |
| OBS-ALT-02 | Approved evaluator:20/20 failures over5minutes,1/20=5%,19/19 and zero traffic; pending CI before/after30minutes and requested release | Full-window HTTP minimum and inclusive threshold; insufficient/zero traffic not healthy; routine pending checks only block merge during grace, overdue/requested-release cases escalate, merge always requires green exact-head checks |
| OBS-ALT-03 | Approved evaluator: stale/missing/duplicate/out-of-order samples, counter reset, restart, maintenance expiry, severity increase and overdue acknowledgement | Preserve unresolved incidents under unknown data; restore durable state or reconcile; dedupe without suppressing escalation; maintenance cannot clear security/integrity or release gates; owner confirms recovery |
| OBS-MCP-01 | Missing secret, unknown role/tool, developer requesting restricted cost tool, arbitrary architecture/query input, audit sink failure | Deny unauthorized input, enforce six-tool registry/current per-role discovery; no storage/shell/network escape; audit failure fails closed; FIN-196's four additional definitions remain unregistered |
| OBS-PRV-01 | Canary sweep over every allowed/denied/error route, serialized logs, UI DOM, problem responses and alert draft | Closed-field projection and raw-value absence; no exception, credentials, financial/OCR/provider content or customer identifiers; sanitized evidence only; missing sink capture is a test gap |
| OBS-PRV-02 | Approved-host TLS, reverse-proxy cache, restricted evidence permissions, retention/deletion and alert destination | Confirm real access boundary, no cache leakage and approved disposal/delivery receipt; source code or synthetic unit tests alone are insufficient |

## Negative Access And Privacy Oracles

Do not equate a decoded client role with authorization. Test both ingress through
the gateway and independently protected owner endpoints. A forged header must
never grant access. Distinguish service authentication from user bearer tokens;
never embed internal shared secrets in client assets. Verify query/jobs endpoints,
signal ingestion and Audit ingestion independently, including wrong service trust.
MCP's unsupported role may return401 by its current contract, not a forced403.

Privacy fixtures contain unmistakable synthetic canaries for identity, amount,
receipt text, prompt, provider result, credential and exception content. The
fixture values are never real data or valid credentials. Place a canary in one
input location at a time and capture each relevant output/sink. Match decoded
content, including JSON escaping and error paths, and verify allowed structure
so an empty response cannot falsely pass. Avoid assertions that prohibit every
number: approved aggregate counts and technical cost units are allowed.

Audit can expose approved actor/subject pseudonymous hashes and validated
correlation to authorized users under its contract. They are not anonymous and
must not appear in generic Monitoring projections, alert messages or public test
reports. Monitoring's bounded operational UUID is not customer identity or access
authority. Test these separate classifications instead of imposing one schema
on all services. Never attach captured raw sensitive-shaped bodies to public CI;
reports include case ID, safe outcome, counts and artifact hash only.

## Execution And Evidence

Run repository contract tests plus the owning suites needed by the change. From
repository root with approved cached dependencies, examples are:

```powershell
dotnet test tests/FinancialAssistant.Repository.Tests/FinancialAssistant.Repository.Tests.csproj --configuration Release
dotnet test backend/shared/observability/FinancialAssistant.Shared.Observability.Tests/FinancialAssistant.Shared.Observability.Tests.csproj --configuration Release
dotnet test backend/services/monitoring/FinancialAssistant.Monitoring.Tests/FinancialAssistant.Monitoring.Tests.csproj --configuration Release
dotnet test backend/services/audit/FinancialAssistant.Audit.Tests/FinancialAssistant.Audit.Tests.csproj --configuration Release
dotnet test backend/services/mcp/FinancialAssistant.Mcp.Tests/FinancialAssistant.Mcp.Tests.csproj --configuration Release
node --test web-admin/monitoring-ui/tests/client.test.mjs web-admin/monitoring-ui/tests/proxy.test.mjs
```

For full release verification use the normal solution restore/build/test/format
baseline and [backend release suite](backend-release-test-suite.md). Browser build
and manual/device runs follow the [admin README](../../web-admin/monitoring-ui/README.md).
Use existing repository runners when available; never install dependencies or
start paid infrastructure just to clear a blocked case. No new runner is added.

Per-case execution evidence must record case ID, exact head/build/environment,
UTC execution time, owner, fixture version, command/test name, expected/actual
safe outcome, pass/fail/blocked/not-run, and restricted artifact reference/hash.
For host tests add approval, endpoint inventory, source freshness, destination
receipt and cleanup result. A skipped, blocked, pending or not-run required case
is not passed. Do not backfill outcomes from ticket percentages or old-head CI.

## Exit And Remaining Gates

Definition DoD: all six scoped areas mapped, privacy cases and negative admin
cases present, evidence and gaps explicit. This can close FIN-199 after review;
it cannot close runtime P8/P9 acceptance. Existing automated suites cover only
their actual assertions. Signal exporters, owner-window adapters, persistent
incident state, actual paging, browser deployment, roster/response drills and
Audit restart/restore need separately approved evidence.

Any access/privacy leak, authoritative-state mutation, unresolved P1/P2 defect,
missing required source or unverified release gate blocks tester rollout. Do not
weaken tests or auto-waive missing evidence. FIN-200 owns operational go/no-go;
FIN-205 storage validation, FIN-215 security review and FIN-216 actual alerts
remain separate. Preserve a reproducible checkpoint and owner handoff for blocked
cases; no automatic retries of real provider calls or production recovery.
