# Current Implementation And Product Target

Reviewed for FIN-269 against merged FIN-194, PR #247, commit
`783f22746d3902181153076b0871121311e8f4f7` (2026-09-10).
Use the [live closure ledger](../agent/POC_PROGRESS.md) for subsequent progress;
this page describes capability boundaries, not deployed-environment acceptance.

## Capability Map

| Area | Implemented repository baseline | Separate target or runtime gate |
| --- | --- | --- |
| Gateway | REST routing, JWT/role checks, exact public allowlist, trusted context, rate limits and safe errors | Route activation and destinations are environment-controlled; not every handler is enabled by repository defaults |
| Identity/Profile/Category | Deterministic authentication/session/provider-validation handlers, profile preferences and taxonomy | Stable secrets, durable stores and real provider configuration; phone delivery adapter is disabled by default |
| Intake/Receipt/AI | Editable drafts, review/reject/confirm, receipt/OCR processing and controlled provider abstractions | Approved provider budgets, privacy configuration and end-to-end hosted operation |
| Income/Expense | Owner-scoped authoritative validated records and lifecycle contracts | Durable deployment and recovery verification |
| Summary/Analytics/Score | Deterministic projections, reports, history and dashboard composition | Live event wiring, projection rebuild and deployment evidence |
| Recommendations/Notifications | Recommendation generation, prepared notification/inbox lifecycle and preferences | Push/web adapters are non-sending placeholders; no real delivery or deferred quiet-hours scheduler claimed |
| Mobile | Home/Add/Insights/Settings, authentication/onboarding, text/receipt draft review, dashboard, inbox and resilience states | Native audio, complete wallet/debt/reserve flows, signed stores and tester acceptance are not all delivered |
| Monitoring/Audit/MCP | Safe operational aggregates, append-only audit contracts and six allowlisted read-only MCP tools | Durable history, complete producer wiring and approved operational acceptance |
| Admin web | Protected React dashboard, memory-only sessions, service status, bounded recent/failed jobs and AI/OCR summaries | Support lookup is disabled; job history is process-local (200 observations, 24 hours), not a durable job scheduler |
| Infrastructure | Local Compose and Windows POC topology/runbooks | Approved host, TLS, stable secrets, backup/restore drill and real client installation |

The internal admin app is not a consumer web application. The product still targets
Android, iOS and Web, but repository directories alone do not establish delivery.

## End-To-End User Scenario

The implemented contracts support this flow when the required routes, adapters
and environment are enabled and verified:

1. Authenticate through the public gateway and complete profile/onboarding.
2. Submit synthetic text or a receipt; Intake/Receipt/AI produce a draft with
   candidate fields, confidence and explicit ambiguities.
3. Review, correct, reject or confirm. Confirmation invokes deterministic
   validation and the owning Income/Expense write; AI never writes the ledger.
4. Owned events feed projections, dashboard/score and recommendation preparation.
5. Read the resulting financial overview and inbox through protected APIs.
6. Operators inspect safe monitoring/audit signals, not raw user input or receipts.

Failures retain explicit loading/error/retry/review states. A failed provider,
unconfirmed draft, disabled route or non-sending notification adapter must not be
reported as a successful financial write or notification delivery.

See [financial core flow](financial-core-e2e-flow.md),
[mobile UX](../product/mobile-poc-ux.md), and
[Confluence diagrams](https://marachert.atlassian.net/wiki/spaces/FA/pages/557178).

## Known Concept Differences

- Current mobile navigation uses four tabs. Earlier no-bottom-navigation and
  all-circular-metric concepts are targets, not descriptions of every screen.
- Text and receipt capture are implemented. A voice-transcript source enum does
  not prove native audio recording or transcription exists.
- Current profile bootstrap defaults are en-US/UTC/USD. Ukrainian/UAH is product
  intent, not an already-applied default change.
- A period's income-minus-expense flow is not a bank/cash/available balance.
  Full wallet, transfer, debt and reserve APIs in earlier concepts are not all
  implemented by Summary or Analytics.
- Current amount contracts use decimal amounts. Older `amountMinor` design
  examples must not cause silent scaling by 100 or incompatible renaming.
- [Storage policy](storage-policy.md) distinguishes preferred PostgreSQL
  durability from existing in-memory and Elasticsearch contracts.
- Identity still emits legacy `user.signed_in.v1`; the newer shared naming rule
  rejects underscores. Preserve compatibility until a versioned migration is
  explicitly implemented, tested and documented.

## Acceptance Boundary

POC percentage counts canonical Jira leaves, not elapsed time or production
readiness. First-user testing remains Not Ready until the ledger's runtime,
privacy, provider-cost, integration, deployment and client-installation gates have
evidence. Do not invent a calendar ETA, completed restore drill or live delivery.

Implementation references: [Identity sessions](../engineering/identity-session-lifecycle.md),
[gateway groups](../engineering/gateway-public-api-groups.md),
[notification adapters](../engineering/notification-delivery-adapters.md),
[admin API](../api/monitoring-admin-v1.md), [MCP contract](../api/mcp-server-v1.md),
[release tests](../engineering/backend-release-test-suite.md),
[Windows POC](../../infra/windows-poc/README.md).
