# CI quality gates

This document captures the initial CI baseline for FIN-13.

Implemented/documented scope:

- FIN-251 — CI scope and repository quality gates;
- FIN-252 — .NET restore/build/test workflow;
- FIN-253 — formatting and linting baseline;
- FIN-254 — test reporting and CI diagnostics.

## Workflow file

```text
.github/workflows/backend-ci.yml
```

## Required checks

| Check | Purpose |
| --- | --- |
| `ci-dotnet-build-test` | Restore, build, and test backend/shared .NET code when a .NET target exists |
| `ci-dotnet-format` | Verify formatting with `dotnet format --verify-no-changes` when a .NET target exists |

## CI triggers

The backend CI workflow runs on:

- pull requests targeting `main` or `develop`;
- pushes to `main` or `develop`;
- manual `workflow_dispatch`.

The workflow is scoped to backend/shared/test code and repository-level .NET build files.

## .NET target detection

The repository is still in the platform-foundation stage, so a backend solution may not exist yet.

Before running restore/build/test/format, the workflow looks for a .NET target in this order:

1. root-level `*.sln`;
2. `backend/**/*.sln`;
3. `backend/**/*.csproj`;
4. any repository `*.sln` or `*.csproj`, excluding `bin/`, `obj/`, and `.git/`.

If no `.sln` or `.csproj` file exists yet, the workflow emits a GitHub notice and skips the .NET commands successfully. This keeps infrastructure/documentation PRs from failing before backend code is introduced.

Once a backend solution or project is added, the same jobs automatically become enforcing checks.

## Local reproduction commands

Run these from the repository root after a .NET solution or project exists:

```bash
dotnet --info
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format --verify-no-changes --verbosity diagnostic
```

If the backend solution is moved under a specific path, use the explicit solution path:

```bash
dotnet restore backend/FinancialAssistant.sln
dotnet build backend/FinancialAssistant.sln --no-restore --configuration Release
dotnet test backend/FinancialAssistant.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format backend/FinancialAssistant.sln --verify-no-changes --verbosity diagnostic
```

## Test reporting

The workflow writes TRX test results to:

```text
TestResults/
```

The workflow uploads test results as an artifact named:

```text
dotnet-test-results
```

The artifact upload runs with `if: always()` so results are available even when tests fail. If tests are skipped because no .NET target exists yet, no artifact is expected.

## Failure guide

| Failed step | Likely cause | Developer action |
| --- | --- | --- |
| Detect .NET solution or project | unexpected shell or path issue | inspect workflow output and repository paths |
| Setup .NET SDK | SDK version mismatch or CI provider issue | verify `global.json` and workflow SDK version |
| Restore solution | broken package reference, missing source, invalid solution path | run `dotnet restore` locally against the detected target |
| Build solution | compilation error, analyzer error, project reference issue | run `dotnet build --no-restore --configuration Release` locally |
| Test solution | failing unit/integration test | run `dotnet test --no-build --configuration Release` locally |
| Verify formatting | formatting differs from repository baseline | run `dotnet format` locally and commit formatting-only changes |
| Upload test results | missing result files or artifact config issue | verify `TestResults/**/*.trx` path |

## Pull request quality gate expectation

A pull request should not be treated as merge-ready unless:

- restore succeeds when a .NET target exists;
- build succeeds when a .NET target exists;
- tests pass when a .NET target exists;
- formatting check passes when a .NET target exists;
- CI output is readable enough to diagnose failures;
- the change does not introduce sensitive data into logs, tests, or artifacts.

When branch protection is configured, require these checks on the main development branch:

```text
ci-dotnet-build-test
ci-dotnet-format
```

## Security and privacy rules

- CI logs must not print secrets, tokens, API keys, or production configuration values.
- CI artifacts must not contain real receipts, real transaction data, OCR text, AI prompts/responses, or personal data.
- Test fixtures must be synthetic.
- Failing test messages must avoid embedding sensitive payloads.
- CI must not require production credentials.

## Out of scope for this baseline

- Deployment pipeline.
- Production release automation.
- Mobile app store publishing.
- Infrastructure provisioning.
- Kubernetes or cloud deployment.
- Advanced vulnerability management.
- Paid external quality tools.
- Performance testing.
- End-to-end mobile or web UI automation.
- Frontend/mobile linting.
