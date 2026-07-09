# Financial Assistant Agent Instructions

## Required reading

Before changing code, read:

- `docs/agent/PROJECT_INSTRUCTIONS.md`
- `docs/agent/DELIVERY_WORKFLOW.md`
- `docs/agent/SECURITY_AND_BLOCKERS.md`
- the active Jira issue and its parent/children
- the nearest nested `AGENTS.md`, when present

These are mandatory project instructions.

## Product and architecture

Financial Assistant is an intelligent financial assistant, not a complex accounting system. Minimize manual input and preserve transparent deterministic financial calculations.

Default stack:

- backend: C# / .NET 8;
- public API: REST;
- mobile: React Native;
- web: React or Angular;
- preferred database: PostgreSQL;
- messaging: RabbitMQ;
- OCR and LLM: external providers;
- architecture: pragmatic modular microservices.

Backend deterministic logic is authoritative for transactions, balances, limits, reports, scores, and confirmed financial entities. OCR/LLM output is probabilistic input and must never become financial source of truth.

## Delivery invariant

There must never be more than one active agent-owned delivery PR.

Never start the next Jira leaf-ticket from an unmerged branch. Always restore state from GitHub, CI, Jira, Confluence, and the repository before deciding what to do. Do not rely on previous conversation memory.

## Required iteration order

1. Find an existing open `codex/` PR.
2. When one exists, process it before selecting new work.
3. Fetch inline review threads, review submissions, conversation comments, CI checks, labels, head SHA, and mergeability.
4. Fix every valid actionable finding and add regression coverage where practical.
5. Push, wait for CI on the new head, reply with commit/test/CI evidence, and resolve only after green CI.
6. Recheck all review channels immediately before merge.
7. Merge only when every merge-gate condition passes.
8. Verify GitHub reports `merged = true`; record actual merge commit, `merged_at`, final head, CI, and review state.
9. Update Jira and Confluence, then transition the leaf issue to Done.
10. Close a parent only after all children are verified Done.
11. Select the next unfinished leaf-ticket by Jira rank and branch from current `main`.

## Merge gate

The agent may merge only when all are true:

- repository is `Marachert/FinancialAssistant.Host`;
- branch starts with `codex/` and targets `main`;
- Jira key matches branch and PR title;
- PR is not draft and is mergeable;
- all required checks succeeded on the current head;
- no check is pending, cancelled, or unexpectedly skipped;
- unresolved actionable review threads equal zero;
- no unresolved `CHANGES_REQUESTED` review exists;
- no `blocked`, `do-not-merge`, or equivalent label exists;
- all valid comments were answered with evidence;
- diff remains within Jira scope;
- no secrets, real financial data, generated binaries, or production configuration are included;
- pre-merge Jira/Confluence evidence is current.

Never bypass branch protection, never force-push, and never push directly to `main`. After merge, re-read the PR; do not use the provisional merge SHA from an open PR.

## Jira hierarchy

When an issue has unfinished children, work the next leaf issue and do not implement the parent directly. Keep the parent In Progress until every child is Done and the parent Definition of Done is satisfied.

Before creating a branch or PR, search for duplicates. Before every mutation, perform a fresh state read.

## Verification baseline

For backend changes run the relevant subset of:

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

Do not weaken or delete a valid test merely to make CI green.

## Security

Never commit or expose tokens, passwords, API keys, certificates, `.env` files, production configuration, real identities, personal financial data, receipts, raw OCR data, or real LLM prompts/responses. Use synthetic test data and environment-provided credentials.

## Completion

A task is complete only after the PR is actually merged, post-merge evidence is recorded, Jira is Done, parent status is evaluated, and the next unfinished leaf-ticket is identified or no work remains.
