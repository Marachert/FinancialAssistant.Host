# Desktop Codex Setup

## Purpose

This repository contains the instructions and tooling required for Desktop Codex to deliver Jira work sequentially, process PR feedback, merge through a strict gate, and continue with the next leaf-ticket.

## One-time operator setup

1. Use a dedicated clone for autonomous delivery.
2. Trust the repository in Desktop Codex so project-scoped configuration is loaded.
3. Authenticate GitHub CLI:

```bash
gh auth login
gh auth status
```

4. Configure Atlassian credentials outside the repository:

```powershell
$env:ATLASSIAN_SITE_URL = "https://marachert.atlassian.net"
$env:ATLASSIAN_EMAIL = "<account email>"
$env:ATLASSIAN_API_TOKEN = "<token from secure store>"
```

Prefer an OS credential store or MCP OAuth. Do not place credentials in `.codex/config.toml`, scripts, shell history, Jira comments, or logs.

5. Validate read access:

```powershell
pwsh tools/delivery/jira.ps1 get-issue FIN-268
pwsh tools/delivery/confluence.ps1 get-page 4227073
```

6. Validate repository checks:

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet format FinancialAssistant.Backend.sln --verify-no-changes
```

## Starting prompt

Use:

```text
Use $financial-assistant-delivery.

Execute one restart-safe Financial Assistant delivery iteration.
Resume an existing codex/ pull request before selecting new Jira work.
Process all review channels, fix valid findings, add regression tests, wait for CI on the current head, and resolve threads only after green CI.
Merge only when every gate in AGENTS.md passes, then verify the actual merged state, update Jira and Confluence evidence, close eligible issues, and continue with the next Jira leaf-ticket by rank.
```

## Automation schedule

Start with supervised manual runs. After two or three successful deliveries, create a recurring Desktop Codex automation in the dedicated clone. A 20–30 minute interval is appropriate because GitHub CI and reviews are external state transitions.

Every run must acquire `.codex-runtime/delivery.lock`. A run finding a fresh lock exits without changes.

## Required GitHub protections

- PR required for `main`;
- required Backend CI checks;
- branch protection cannot be bypassed by the agent;
- direct push to `main` disabled;
- force-push disabled;
- repository-approved merge strategy configured;
- optional automatic Codex review enabled for `codex/` PRs.

## Operational expectations

The agent may merge only after current-head CI is green, all actionable feedback is processed, unresolved blocking reviews are absent, labels allow merge, evidence is current, and the Jira/branch/PR ownership matches.

The agent stops and reports a blocker for permission failures, security/privacy decisions, destructive migrations, production-data risk, contradictory ownership, repeated unrelated CI infrastructure failure, or an explicit reviewer request not to merge.
