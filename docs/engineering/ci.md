# CI Quality Gates

This document defines the current Backend CI baseline and pull request quality gates for Financial Assistant.

Related documentation:

```text
README.md
docs/delivery/developer-onboarding.md
docs/engineering/contributing.md
.github/workflows/backend-ci.yml
```

## Required jobs

| Job | Purpose |
| --- | --- |
| `ci-dotnet-build-test` | Restore and build the selected .NET target, run test projects, and upload TRX results |
| `ci-dotnet-format` | Verify repository formatting with `dotnet format --verify-no-changes` |

## CI triggers

Backend CI runs on:

- pull requests targeting `main` or `develop`;
- pushes to `main` or `develop`;
- manual `workflow_dispatch`.

Path filters cover:

- backend, shared, and test code;
- mobile and web-admin source boundaries;
- local Docker Compose infrastructure;
- root and documentation indexes;
- architecture, API, event, security, delivery, and engineering documentation;
- solution/project and central build files;
- `.gitignore`, `.gitattributes`, `.editorconfig`, and `LICENSE`;
- the workflow file itself.

Documentation entry-point changes intentionally run repository tests so commands, links, paths, and ownership rules cannot drift silently.

## .NET target detection

The workflow selects a target in this order:

1. root-level `*.sln`;
2. `backend/**/*.sln`;
3. `backend/**/*.csproj`;
4. any repository `.sln` or `.csproj`, excluding generated directories.

The canonical target is:

```text
FinancialAssistant.Backend.sln
```

## Test project detection

The workflow runs `dotnet test` when at least one project contains:

```xml
<IsTestProject>true</IsTestProject>
```

The root solution includes repository tests that enforce source layout, shared ownership, documentation onboarding, CI path filters, and tracked-file hygiene.

## Commands executed by CI

Equivalent local commands:

```bash
dotnet --info
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

## Pull request quality gate

A pull request is merge-ready only when:

- the scope matches the related Jira issue;
- the PR body explains the change and verification;
- `ci-dotnet-build-test` succeeds on the final head;
- `ci-dotnet-format` succeeds on the final head;
- architecture, API, event, security, delivery, and onboarding documentation is updated where behavior changes;
- no secrets, generated binaries, real receipts, raw OCR text, real LLM content, personal data, or real financial data are introduced;
- all actionable review comments are processed;
- unresolved review threads are zero.

## Review processing gate

For every actionable review comment:

1. validate the finding;
2. implement the smallest correct fix;
3. add regression coverage where practical;
4. wait for the updated CI pipeline;
5. reply in the original thread with commit and CI evidence;
6. mark useful feedback positively when requested;
7. resolve the thread only after the final pipeline is green;
8. re-check review threads, submissions, and conversation comments before merge.

## Branch protection expectation

Recommended protected branches:

```text
main
develop
```

Recommended `main` rules:

- require a pull request before merge;
- require `ci-dotnet-build-test` and `ci-dotnet-format`;
- require conversation resolution when supported;
- block force pushes;
- block branch deletion;
- use squash or linear-history behavior according to the repository owner workflow.

Branch protection is a GitHub repository setting and is not created by the workflow YAML itself.

## Test reporting and diagnostics

Test results are written to:

```text
TestResults/
```

The workflow uploads:

```text
dotnet-test-results
```

Build failures upload:

```text
dotnet-build-diagnostics
```

These artifacts must contain only synthetic, privacy-safe information.

## Failure guide

| Failed step | Likely cause | Developer action |
| --- | --- | --- |
| Detect .NET target | Unexpected repository path or workflow logic | Inspect selected target and path filters |
| Detect test project | Missing `<IsTestProject>true</IsTestProject>` or detection issue | Inspect test `.csproj` files |
| Restore solution | Package source/reference or solution issue | Run `dotnet restore FinancialAssistant.Backend.sln` |
| Build solution | Compiler, analyzer, or project-reference error | Run the Release build locally |
| Test solution | Unit, integration, repository, or documentation regression | Reproduce the first failing assertion locally |
| Verify formatting | Source differs from `.editorconfig` | Run `dotnet format FinancialAssistant.Backend.sln` |
| Upload results | Missing TRX files or artifact path issue | Inspect `TestResults/**/*.trx` |

## Security and privacy rules

CI logs, artifacts, fixtures, and failure messages must not expose:

- tokens, passwords, API keys, private keys, or production settings;
- real identities or personal financial data;
- real receipt content or raw OCR text;
- real LLM prompts or responses;
- generated local environment files.

Use synthetic fixtures and sanitized identifiers.

## Current out-of-scope items

Dedicated Jira tasks should introduce these when justified:

- production deployment and release automation;
- mobile app store delivery;
- cloud/Kubernetes provisioning;
- complete dependency and vulnerability management;
- frontend/mobile linting and end-to-end automation;
- performance, resilience, and disaster-recovery testing.
