# Contributor CI workflow

This guide explains how contributors should prepare changes, run local checks, open pull requests, and troubleshoot CI failures for the Financial Assistant repository.

It implements FIN-256 and complements:

```text
docs/engineering/ci.md
.github/workflows/backend-ci.yml
```

## Contributor flow

Recommended flow for every Jira-driven change:

1. Pick one Jira issue or one small logical change.
2. Create a feature branch from `main`.
3. Make the code, infrastructure, or documentation change.
4. Run the relevant local checks.
5. Push the branch.
6. Open a pull request.
7. Wait for CI.
8. Fix failures or document accepted skips.
9. Merge only when the PR is review-ready and CI is green.

Recommended branch naming:

```text
feature/FIN-123-short-description
codex/FIN-123-short-description
fix/FIN-123-short-description
docs/FIN-123-short-description
```

For example:

```text
codex/fin-256-contributor-ci-workflow
```

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

The workflow is scoped to backend/shared/test code, repository-level .NET build files, `.editorconfig`, and the workflow file itself.

## What CI checks

Current required checks:

```text
ci-dotnet-build-test
ci-dotnet-format
```

`ci-dotnet-build-test` runs:

```bash
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --logger trx --results-directory TestResults
```

`ci-dotnet-format` runs:

```bash
dotnet format --verify-no-changes --verbosity diagnostic
```

The workflow automatically detects a `.sln` or `.csproj`. If no .NET target exists yet, the .NET commands are skipped with a GitHub notice. This is acceptable only during repository/platform foundation work before backend code exists.

## Local checks before opening a PR

After a .NET solution or project exists, run these commands from the repository root:

```bash
dotnet --info
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format --verify-no-changes --verbosity diagnostic
```

If the backend solution is located under `backend/`, use the explicit solution path:

```bash
dotnet restore backend/FinancialAssistant.sln
dotnet build backend/FinancialAssistant.sln --no-restore --configuration Release
dotnet test backend/FinancialAssistant.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format backend/FinancialAssistant.sln --verify-no-changes --verbosity diagnostic
```

For infrastructure-only changes, at minimum check Docker Compose configuration locally when possible:

```bash
cd infra/docker-compose
cp .env.example .env
docker compose config
```

Do not commit `.env`.

## How to read CI failures

Use the failed GitHub Actions step name first. It usually tells you where to start.

| Failed step | What it usually means | First local command to run |
| --- | --- | --- |
| Detect .NET solution or project | Workflow path detection issue | Inspect repository paths and workflow output |
| Setup .NET SDK | SDK version mismatch or GitHub runner issue | `dotnet --info` |
| Restore solution | Package reference, NuGet source, or solution path issue | `dotnet restore` |
| Build solution | Compile error or project reference issue | `dotnet build --no-restore --configuration Release` |
| Test solution | Unit or integration test failure | `dotnet test --no-build --configuration Release` |
| Verify formatting | Formatting differs from `.editorconfig` baseline | `dotnet format` |
| Upload test results | Test output path issue | Check `TestResults/**/*.trx` |

## What to do when tests fail

1. Open the failed GitHub Actions job.
2. Find the first failing test or assertion.
3. Reproduce locally with the same command.
4. Fix the code or the test.
5. Do not delete tests only to make CI green.
6. If the test is invalid because requirements changed, update the test and document the reason in the PR.

## What to do when formatting fails

Run:

```bash
dotnet format
```

Then check again:

```bash
dotnet format --verify-no-changes --verbosity diagnostic
```

Commit formatting-only changes separately when possible. Tiny formatting fixes inside the same PR are acceptable if they are directly related to the changed files.

## Pull request expectations

A PR should include:

- concise summary;
- related Jira issue key when applicable;
- what changed;
- how it was validated;
- known limitations or accepted skips.

A PR should not include:

- `.env` files;
- production configuration values;
- real receipts;
- raw OCR text;
- AI prompts/responses from real users;
- tokens, API keys, passwords, or private certificates;
- personal financial data;
- generated Docker volumes, logs, `bin/`, `obj/`, or test output folders.

## Accepted CI skip during foundation stage

During the platform-foundation stage, CI may skip .NET commands when no `.sln` or `.csproj` exists. This is acceptable for documentation and infrastructure baseline PRs.

After backend services are introduced, skipped .NET checks should be treated as a problem. The workflow should detect the backend solution or be updated to point to it explicitly.

## Out of scope for the first CI skeleton

The first CI skeleton intentionally does not include:

- deployment pipeline;
- production release automation;
- Apple App Store or Google Play delivery;
- cloud/Kubernetes provisioning;
- full security scanning suite;
- paid quality tools;
- end-to-end mobile/web UI automation;
- performance testing.

These should be added later through dedicated Jira tasks when the product has enough implementation surface to justify them.
