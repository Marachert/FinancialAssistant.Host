# Confluence And GitHub Documentation Maintenance

Related Jira: FIN-269. Documentation is part of delivery, not an optional
postscript. GitHub holds versioned implementation contracts, commands and the POC
ledger; Confluence holds discoverable product, architecture and operational
summaries. Neither should contradict merged behavior or silently treat a target
design as deployed functionality.

## Per-Change Gate

1. Read the relevant GitHub source, code/tests, Jira scope and current Confluence
   page before writing. Preserve user edits and historical evidence.
2. Update affected contracts, examples, commands, limitations and indexes in the
   implementation PR. Use a separate focused maintenance PR for a broad audit,
   only after the existing agent PR is finished. Never open two active delivery PRs.
3. Read Confluence again immediately before mutation. Preserve its title, page
   identity, history, diagrams and comments; use version/concurrency protection
   where available. Back up full bodies before broad replacements. A truncated
   tool response is not a full page and must never be saved as its replacement.
4. Re-read the written page and verify content, version, links and preserved
   structure. On an uncertain timeout, inspect the actual version/body before
   retrying. Do not duplicate comments or updates blindly.
5. Before merge, record pre-merge evidence. After GitHub independently reports
   merged=true, record actual merge SHA/time, final head, exact-head CI and review
   results in Jira and Confluence. Then transition Done and evaluate the parent.
6. Recompute `POC_PROGRESS.md` after every leaf closure. Commit its new snapshot
   and history through a guarded PR before starting another product ticket.
   Report numerator/denominator, percentage-point change and first-user readiness
   separately. Maintenance outside the canonical feature hierarchy adds no credit.
7. Refresh affected parent/index/status pages, not only footer comments. Keep the
   Home, Overview, Architecture, Catalog, Diagrams, Delivery and Bootstrap summaries
   aligned when they repeat the current delivery state.

## Source-To-Page Map

| GitHub source area | Confluence owner pages |
| --- | --- |
| `README.md`, `docs/architecture/current-implementation.md`, `docs/agent/POC_PROGRESS.md` | [Home](https://marachert.atlassian.net/wiki/spaces/FA/pages/262339), [Overview](https://marachert.atlassian.net/wiki/spaces/FA/pages/458753), [Epic Map](https://marachert.atlassian.net/wiki/spaces/FA/pages/262618) |
| `docs/architecture/`, service READMEs | [Architecture](https://marachert.atlassian.net/wiki/spaces/FA/pages/262398), [Catalog](https://marachert.atlassian.net/wiki/spaces/FA/pages/589905), [Diagrams](https://marachert.atlassian.net/wiki/spaces/FA/pages/557178) |
| `docs/architecture/storage-policy.md`, Elasticsearch contracts | [Storage policy](https://marachert.atlassian.net/wiki/spaces/FA/pages/491621), [Data section](https://marachert.atlassian.net/wiki/spaces/FA/pages/557057), [Index catalog](https://marachert.atlassian.net/wiki/spaces/FA/pages/557260) |
| `docs/api/`, service contracts and gateway groups | [API index](https://marachert.atlassian.net/wiki/spaces/FA/pages/131075) and each affected contract page |
| Identity implementation/engineering guides | [Identity baseline](https://marachert.atlassian.net/wiki/spaces/FA/pages/1835031), [API](https://marachert.atlassian.net/wiki/spaces/FA/pages/1966104), [Sessions](https://marachert.atlassian.net/wiki/spaces/FA/pages/2129922), [Events](https://marachert.atlassian.net/wiki/spaces/FA/pages/2326539) |
| `docs/events/`, shared and service event contracts | [Event envelope](https://marachert.atlassian.net/wiki/spaces/FA/pages/262498), [Event governance](https://marachert.atlassian.net/wiki/spaces/FA/pages/622674), affected producer/consumer pages |
| `docs/product/`, mobile README and screens | [UX index](https://marachert.atlassian.net/wiki/spaces/FA/pages/524289), [UI specification](https://marachert.atlassian.net/wiki/spaces/FA/pages/131116), affected screen/flow pages |
| AI/OCR engineering and security guides | [AI/OCR index](https://marachert.atlassian.net/wiki/spaces/FA/pages/491541), [AI test plan](https://marachert.atlassian.net/wiki/spaces/FA/pages/622594), [Safety](https://marachert.atlassian.net/wiki/spaces/FA/pages/426153) |
| `docs/security/`, `docs/legal/` | [Security/privacy](https://marachert.atlassian.net/wiki/spaces/FA/pages/557077), [Privacy checklist](https://marachert.atlassian.net/wiki/spaces/FA/pages/590005), affected access/deletion/export pages |
| Monitoring/Audit/MCP/admin source and contracts | Catalog/Diagrams plus [Operational metrics](https://marachert.atlassian.net/wiki/spaces/FA/pages/622614), [Audit](https://marachert.atlassian.net/wiki/spaces/FA/pages/458833), [MCP](https://marachert.atlassian.net/wiki/spaces/FA/pages/262518) |
| `.github/workflows/`, test plans, `docs/delivery/`, `infra/` | [Testing](https://marachert.atlassian.net/wiki/spaces/FA/pages/131095), [Deployment](https://marachert.atlassian.net/wiki/spaces/FA/pages/425993), [Release checklist](https://marachert.atlassian.net/wiki/spaces/FA/pages/131336) |
| `AGENTS.md`, `docs/agent/`, delivery skill/scripts | [Delivery](https://marachert.atlassian.net/wiki/spaces/FA/pages/458773), [DoD](https://marachert.atlassian.net/wiki/spaces/FA/pages/426194), [Bootstrap](https://marachert.atlassian.net/wiki/spaces/FA/pages/4259842) |

## Audit And Safety

Inventory all current pages and repository Markdown before broad audits. Retain
intentional templates; do not fabricate meeting records, people, approvals,
deployments or dates. Check Markdown links, code paths, page IDs/titles and
source commit existence. Explain historical or compatibility exceptions.

Use immutable source links for audit evidence and relative links for repository
navigation. Distinguish code merged, local tests, exact-head CI, configured
environment, deployed system and first-user acceptance. Green synthetic tests
alone do not certify production readiness, privacy compliance or zero spending.

Never publish raw provider payloads, credentials, financial data or private
machine paths. Runtime checkpoints remain ignored and local; sanitize the
tracked report. On pause, persist the active issue/branch/PR/head, unfinished
verification and next priority, then release only the owned lock.
