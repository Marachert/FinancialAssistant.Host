# AI and OCR Provider Configuration

## Purpose

FIN-118 defines the environment-based configuration boundary for external AI and
OCR providers. Provider selection must not require a code change, but changing a
provider never transfers approval automatically: privacy review, capability tests,
and release-readiness evidence remain provider and model specific.

Both services start with their external provider disabled. Local configuration
contains empty or explicit `unconfigured` placeholders only. No credential value,
production endpoint, customer data, prompt, receipt, or provider response belongs in
an appsettings file, source control, logs, health output, Jira, or Confluence.

## Deployment Modes

Every provider uses one explicit mode:

- `disabled`: the feature flag is off and provider identity, endpoint, and credential
  reference remain empty placeholders;
- `sandbox`: a non-production provider account and non-production endpoint;
- `production`: the approved production account and endpoint.

An enabled provider must use `sandbox` or `production`. A disabled provider must use
`disabled`. Unknown values fail configuration validation.

## AI Orchestration Settings

The configuration section is `AiOrchestration:Provider`. Environment variables use
the .NET double-underscore mapping.

| Setting | Environment variable | Rule |
| --- | --- | --- |
| `Enabled` | `AiOrchestration__Provider__Enabled` | `false` by default |
| `Mode` | `AiOrchestration__Provider__Mode` | `disabled`, `sandbox`, or `production` |
| `Name` | `AiOrchestration__Provider__Name` | 1-64 lowercase ASCII letters, digits, `.`, `_`, or `-` |
| `Model` | `AiOrchestration__Provider__Model` | Same safe identifier rule |
| `Endpoint` | `AiOrchestration__Provider__Endpoint` | Absolute HTTPS URL without user information |
| `CredentialEnvironmentVariable` | `AiOrchestration__Provider__CredentialEnvironmentVariable` | Name of the separately injected credential variable, not its value |
| `RequestTimeoutSeconds` | `AiOrchestration__Provider__RequestTimeoutSeconds` | 1-120 |
| `MaximumAttempts` | `AiOrchestration__Provider__MaximumAttempts` | 1-2 for the current prompt policy |
| `RetryDelayMilliseconds` | `AiOrchestration__Provider__RetryDelayMilliseconds` | 0-5000 |

When enabled settings are incomplete, options validation stops startup. When the
selected `ILlmProvider` adapter is not registered, startup also stops before a
capability can be executed. Disabled configuration creates no model route.

## Receipt OCR Settings

The configuration section is `ReceiptProcessing:Ocr`.

| Setting | Environment variable | Rule |
| --- | --- | --- |
| `Enabled` | `ReceiptProcessing__Ocr__Enabled` | `false` by default |
| `Mode` | `ReceiptProcessing__Ocr__Mode` | `disabled`, `sandbox`, or `production` |
| `ProviderName` | `ReceiptProcessing__Ocr__ProviderName` | 1-64 lowercase ASCII letters, digits, `.`, `_`, or `-` |
| `ModelKey` | `ReceiptProcessing__Ocr__ModelKey` | Same safe identifier rule |
| `Endpoint` | `ReceiptProcessing__Ocr__Endpoint` | Absolute HTTPS URL without user information |
| `CredentialEnvironmentVariable` | `ReceiptProcessing__Ocr__CredentialEnvironmentVariable` | Name of the separately injected credential variable, not its value |
| `RequestTimeoutSeconds` | `ReceiptProcessing__Ocr__RequestTimeoutSeconds` | 1-120 |
| `MaximumAttempts` | `ReceiptProcessing__Ocr__MaximumAttempts` | 1-3 |
| `RetryDelayMilliseconds` | `ReceiptProcessing__Ocr__RetryDelayMilliseconds` | 0-5000 |

The runtime resolves the disabled OCR client while `Enabled` is `false`, even if
another client is registered. If the flag is enabled but no external OCR adapter is
registered, startup fails without attempting a provider call. Invalid numbers,
booleans, identities, modes, endpoints, or credential references also stop startup.

## Credential Injection

`CredentialEnvironmentVariable` stores only a safe environment-variable name such
as `FINANCIAL_ASSISTANT_AI_PROVIDER_CREDENTIAL`. The named variable is supplied by
the deployment secret store. The repository does not define its value.

Provider adapters must:

1. resolve the named variable at startup;
2. fail closed when it is absent or blank;
3. send it only in the provider authentication mechanism;
4. never include it in options snapshots, logs, metrics, traces, health responses,
   exception messages, support evidence, or configuration dumps;
5. support rotation by deployment configuration without a source change.

## Safe Local Placeholders

The checked-in development files use this shape:

```json
{
  "Enabled": false,
  "Mode": "disabled",
  "NameOrProviderName": "",
  "ModelOrModelKey": "",
  "Endpoint": "",
  "CredentialEnvironmentVariable": ""
}
```

Receipt Processing uses the explicit non-secret identity placeholders
`unconfigured` for `ProviderName` and `ModelKey`. No local placeholder enables a
network call.

## Provider Switching

To switch a provider or model:

1. keep the existing production provider enabled until the replacement passes its
   sandbox privacy, contract, resilience, cost, and failure-path reviews;
2. deploy the replacement adapter and its separately injected credential;
3. update the feature flag, mode, provider/model identifiers, endpoint, and bounded
   resilience values in one environment-scoped deployment change;
4. verify readiness, safe audit metadata, fallback behavior, and suggestion-only
   authority before sending user content;
5. disable and revoke the old provider, then follow its approved deletion process.

Do not use endpoint changes, aliases, or credential rotation to silently switch the
provider behind an unchanged identity. Roll back by setting `Enabled=false`; AI
route registration then stops, and OCR resolves the disabled client with the safe
`processing_provider_disabled` behavior.

## Review Dependencies

Configuration approval depends on:

- the [AI and OCR privacy review checklist](../security/ai-ocr-privacy-review-checklist.md);
- the [AI and OCR integration test plan](ai-ocr-integration-test-plan.md);
- [provider budget and usage controls](ai-ocr-usage-cost-controls.md);
- the [FIN-124 AI/OCR release-readiness checklist](ai-ocr-release-readiness-checklist.md).

Provider output remains untrusted suggestion data. Configuration can never grant a
provider authority to create or confirm a financial record.
