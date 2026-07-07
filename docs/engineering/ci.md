# CI quality gates

This document captures the initial CI baseline for FIN-13.

Implemented/documented scope:

- FIN-251 — CI scope and repository quality gates;
- FIN-252 — .NET restore/build/test workflow;
- FIN-253 — formatting and linting baseline;
- FIN-254 — test reporting and CI diagnostics;
- FIN-255 — PR quality gate and branch protection expectations.

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

## PR quality gate policy

Every code or infrastructure change should go through a pull request. Direct commits to `main` should be avoided except for emergency repository administration.

A pull request is considered merge-ready only when all applicable requirements are true:

- the PR has a clear summary and scope;
- the PR references the related Jira issue when applicable;
- `ci-dotnet-build-test` succeeds;
- `ci-dotnet-format` succeeds;
- changed documentation is updated when behavior, commands, or developer workflow changes;
- no secrets, production configuration values, real receipts, raw OCR text, AI prompts/responses, personal data, or real financial data are added;
- failing or skipped checks are understood and documented before merge.

During the platform-foundation stage, .NET restore/build/test/format commands may be skipped when no `.sln` or `.csproj` exists yet. This is acceptable for infrastructure/documentation PRs. After backend code is added, skipped .NET checks should be treated as a warning and investigated.

## Branch protection expectation

Recommended protected branches:

```text
main
develop
```

Initial `main` branch protection should require:

- pull request before merge;
- status checks to pass before merge;
- required status checks:
  - `ci-dotnet-build-test`;
  - `ci-dotnet-format`;
- conversation resolution before merge, if enabled;
- linear history or squash merge, if the team chooses that workflow;
- block force pushes;
- block branch deletion.

Recommended `develop` branch protection can be slightly lighter during PoC:

- pull request before merge;
- status checks to pass before merge;
- required status checks:
  - `ci-dotnet-build-test`;
  - `ci-dotnet-format`.

Repository setting limitation:

Branch protection is a GitHub repository setting. It is not implemented by the CI workflow file itself. If the available automation tool cannot modify branch protection settings, this document is the source of truth until an admin configures the rules in GitHub repository settings.

## Recommended merge behavior

Preferred default for the PoC stage:

- small PRs;
- squash merge for feature branches;
- merge only after CI is green;
- avoid mixing unrelated Jira issues in one PR;
- avoid committing generated local files such as `.env`, Docker volumes, logs, and test result folders.

Do not block the first PoC with heavy enterprise controls such as mandatory multi-review approvals, paid quality tools, or deployment gates unless the team explicitly decides to add them later.

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
