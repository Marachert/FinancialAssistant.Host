# Windows Server PoC deployment contract

## Superseded POC Target

As of 2026-09-15 the approved target is the
[Windows-native WPF installer](../architecture/windows-native-poc.md), supporting
Windows 11 and Windows Server 2022/2025 without Docker or WSL. FIN-270 owns the
new implementation. The FIN-42 contract below is historical Compose evidence,
not instructions or acceptance for the new installer. Native deployment,
durability, security, lifecycle and E2E gates remain open.

## Historical FIN-42 Contract

FIN-42 delivers the repository-owned single-host deployment baseline at
`infra/windows-poc`.

The operator contract is:

- Linux containers run through a supported Docker Engine and Compose v2 runtime
  on the Windows Server host;
- Nginx is the only network-published application entry point;
- backend APIs and platform dependencies communicate on an internal Docker
  network;
- administrative consoles bind to host loopback only;
- all usable credentials and HMAC/signing keys come from ignored environment or
  approved secret-store injection;
- validation, startup, shutdown, verification, backup, and restore are
  non-interactive PowerShell 7 operations;
- Elasticsearch snapshots precede offline archives of the fixed durable-volume
  allowlist;
- AI/OCR provider adapters remain disabled, so deployment validation incurs no
  paid provider cost;
- synthetic data is mandatory for smoke and restore verification.

Use the complete [operator runbook](../../infra/windows-poc/README.md) for host
prerequisites, commands, TLS, secret rotation, backup/restore, and current PoC
limitations.
