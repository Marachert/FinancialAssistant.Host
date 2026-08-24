# Windows Server PoC deployment contract

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
