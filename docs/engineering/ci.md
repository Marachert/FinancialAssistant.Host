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
| `ci-dotnet-build-test` | Restore, build, and test backend/shared .NET code |
| `ci-dotnet-format` | Verify formatting with `dotnet format --verify-no-changes` |

## CI triggers

The backend CI workflow runs on:

- pull requests targeting `main` or `develop`;
- pushes to `main` or `develop`;
- manual `workflow_dispatch`.

The workflow is scoped to backend/shared/test code and repository-level .NET build files.

## Local reproduction commands

Run these from the repository root:

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

The artifact upload runs with `if: always()` so results are available even when tests fail.

## Failure guide

| Failed step | Likely cause | Developer action |
| --- | --- | --- |
| Setup .NET SDK | SDK version mismatch or CI provider issue | Verify `global.json` and workflow SDK version |
| Restore solution | Broken package reference, missing source, invalid solution path | Run `dotnet restore` locally |
| Build solution | Compilation error, analyzer error, project reference issue | Run `dotnet build --no-restore --configuration Release` locally |
| Test solution | Failing unit/integration test | Run `dotnet test --no-build --configuration Release` locally |
| Verify formatting | Formatting differs from repository baseline | Run `dotnet format` locally and commit formatting-only changes |
| Upload test results | Missing result files or artifact config issue | Verify `TestResults/**/*.trx` path |

## Pull request quality gate expectation

A pull request should not be treated as merge-ready unless:

- restore succeeds;
- build succeeds;
- tests pass;
- formatting check passes;
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
