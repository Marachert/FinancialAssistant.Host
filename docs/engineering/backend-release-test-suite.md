# Backend Release Test Suite

## Release gate

`tests/FinancialAssistant.Release.Tests` is the executable pre-deployment
backend release gate. A passing result proves the bounded synthetic scenarios
below; it does not replace the Windows deployment, mobile-device, backup,
security-review, or controlled-tester tasks that follow FIN-41.

| Gate | Executable evidence |
| --- | --- |
| Core E2E | Register, sign in, submit free-form input, review a draft, confirm it, observe one authoritative Expense, project Analytics, read dashboard, calculate/read score |
| HTTP contracts | Identity, Transaction Intake, Analytics, and Financial Score publish required v1 OpenAPI paths |
| Event contracts | Financial lifecycle envelope round-trips every required identity, version, correlation, owner-hash, and payload field |
| Elasticsearch | Identity-owned physical indices are schema-versioned and read/write aliases are distinct |
| Privacy | Operational Monitoring, Audit, and MCP contracts expose no raw identity/secret/prompt/receipt fields; structured log templates contain no raw sensitive placeholders |

## Execution

```powershell
dotnet test tests/FinancialAssistant.Release.Tests/FinancialAssistant.Release.Tests.csproj --configuration Release
```

The suite uses real ASP.NET test hosts and service application components with
in-memory POC adapters. Event forwarding is explicit and uses the production
contract; no test writes an Analytics or Score projection directly. Inputs use
`example.invalid` identities and synthetic values. No live AI/OCR provider,
Docker dependency, paid credit, or external financial account is required.

## Failure policy

Any failed core flow, missing OpenAPI path, incompatible event envelope,
unversioned/aliased mapping, or privacy assertion blocks the backend release
candidate. Tests must be repaired by correcting product behavior or an
intentionally versioned contract; they must not be weakened to accept a
regression.
