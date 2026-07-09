# Contributor Workflow

This guide explains how contributors should prepare changes, run local checks, open pull requests, process review comments, and troubleshoot CI failures for the Financial Assistant repository.

Start with the onboarding guide when setting up the repository for the first time:

```text
docs/delivery/developer-onboarding.md
```

Related files:

```text
docs/engineering/ci.md
.github/workflows/backend-ci.yml
FinancialAssistant.Backend.sln
```

## Contributor flow

Recommended flow for every Jira-driven change:

1. Select one Jira issue or one small logical change.
2. Update local `main` with a fast-forward pull.
3. Create a focused branch from current `main`.
4. Make the code, infrastructure, or documentation change.
5. Run the relevant local checks.
6. Push the branch and open a pull request.
7. Wait for CI.
8. Process every actionable review comment.
9. Re-run or wait for the updated CI pipeline after fixes.
10. Reply with commit and verification evidence.
11. Resolve review threads only after the final pipeline is green.
12. Merge through the repository owner workflow.

## Branch naming

Accepted patterns:

```text
feature/FIN-123-short-description
fix/FIN-123-short-description
docs/FIN-123-short-description
codex/fin-123-short-description
```

Keep one Jira issue or one small logical change per pull request.

## Architecture checks before coding

Before implementing a backend feature, identify:

- the business capability and owning service;
- authoritative data and storage ownership;
- synchronous REST operations;
- asynchronous RabbitMQ events;
- Elasticsearch indices and aliases owned by the service;
- deterministic business logic;
- OCR/LLM-assisted behavior that must remain probabilistic and validated.

Do not move financial business rules into the Public API Gateway, shared utility projects, frontend clients, OCR pipelines, or LLM prompts.

## CI workflow location

Backend CI is defined in:

```text
.github/workflows/backend-ci.yml
```

The CI policy and quality gate details are documented in:

```text
docs/engineering/ci.md
```

## When CI runs

Backend CI runs on:

- pull requests targeting `main` or `develop`;
- pushes to `main` or `develop`;
- manual workflow dispatch.

The workflow path filters cover backend/shared/test code, mobile and web-admin boundaries, local Docker Compose files, architecture/API/event/security/delivery/engineering documentation, repository hygiene files, solution/project files, and the workflow itself.

## What CI checks

Current required jobs:

```text
ci-dotnet-build-test
ci-dotnet-format
```

`ci-dotnet-build-test` runs:

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
```

`ci-dotnet-format` runs:

```bash
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

The workflow detects the first available `.sln` or `.csproj`, but the root `FinancialAssistant.Backend.sln` is the canonical backend verification target.

## Local checks before opening a PR

Run from the repository root:

```bash
dotnet --info
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
git diff --check
git status
```

For infrastructure changes, validate Docker Compose:

### Bash, Git Bash, or WSL

```bash
cd infra/docker-compose
cp .env.example .env
docker compose config
```

### PowerShell

```powershell
Set-Location infra/docker-compose
Copy-Item .env.example .env
docker compose config
```

Do not commit `.env`.

## Documentation changes

Update documentation in the same pull request when changing:

- developer commands or prerequisites;
- repository paths;
- API endpoints or contracts;
- service ownership or data flows;
- RabbitMQ events;
- Elasticsearch index/alias rules;
- security/privacy behavior;
- CI or deployment behavior.

The root `README.md` is the repository entry point. Detailed onboarding belongs in `docs/delivery/developer-onboarding.md`; specialized details remain in the owning architecture, API, event, security, delivery, or engineering guide.

## Processing review comments

Review processing is mandatory before a pull request is declared merge-ready.

For each finding:

1. Read the complete thread and affected code/documentation.
2. Decide whether the finding is valid.
3. Implement the smallest correct fix.
4. Add regression coverage when practical.
5. Wait for the updated CI run.
6. Reply in the original thread with:
   - the fixing commit;
   - what changed;
   - relevant test evidence;
   - the successful CI run.
7. Mark useful findings with a positive reaction when requested.
8. Resolve the thread only after the final pipeline is green.
9. Re-check all threads, review submissions, and conversation comments before merge.

Do not resolve a finding merely because code was pushed; verification must complete first.

## How to read CI failures

Use the failed GitHub Actions step name first.

| Failed step | Likely cause | First action |
| --- | --- | --- |
| Detect .NET solution or project | Unexpected repository path or workflow logic | Inspect the selected target in workflow output |
| Detect .NET test project | Test marker missing or detection issue | Inspect test `.csproj` files |
| Setup .NET SDK | SDK version mismatch or runner issue | Run `dotnet --info` |
| Restore solution | Package reference, source, or target issue | Run `dotnet restore FinancialAssistant.Backend.sln` |
| Build solution | Compile, analyzer, or project-reference error | Run the same Release build locally |
| Test solution | Unit/integration/repository test failure | Reproduce the first failing assertion locally |
| Verify formatting | Source differs from `.editorconfig` rules | Run `dotnet format FinancialAssistant.Backend.sln` |
| Upload test results | Result path or logger issue | Inspect `TestResults/**/*.trx` |

## When tests fail

1. Open the failed job.
2. Find the first failing assertion or compiler error.
3. Reproduce locally with the same command.
4. Fix the implementation or invalid assertion.
5. Do not delete or weaken a valid test merely to make CI green.
6. When requirements legitimately changed, update the test and explain why in the PR.
7. Push the fix and wait for the complete updated pipeline.

## When formatting fails

Apply formatting:

```bash
dotnet format FinancialAssistant.Backend.sln
```

Verify again:

```bash
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

## Pull request expectations

A pull request should include:

- related Jira issue key;
- concise summary and scope;
- architecture and ownership implications;
- changed components and paths;
- tests and verification evidence;
- documentation updates;
- known limitations and follow-up work.

A pull request must not include:

- `.env` files;
- production configuration values;
- tokens, API keys, passwords, certificates, signing keys, or private PEM files;
- real receipts or personal financial data;
- raw OCR text from real users;
- real LLM prompts or responses;
- generated Docker volumes, logs, `bin`, `obj`, package, build, or test output.

## Merge-ready checklist

A pull request is merge-ready only when:

- scope matches the Jira issue;
- applicable local checks pass;
- CI is green on the final head;
- review comments were processed;
- unresolved review threads are zero;
- documentation matches current behavior;
- no secrets, generated binaries, or sensitive data are present;
- the PR body contains final head and verification evidence.

## Out of scope for the current contributor baseline

The current workflow does not imply that every future delivery capability already exists. Dedicated Jira tasks should introduce:

- production deployment automation;
- mobile app store delivery;
- cloud/Kubernetes provisioning;
- complete vulnerability-management tooling;
- frontend/mobile CI and end-to-end automation;
- performance and resilience testing.
