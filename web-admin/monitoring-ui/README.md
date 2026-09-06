# Monitoring Web UI

This folder is the canonical workspace for the Financial Assistant internal monitoring and administration web client.

## Responsibilities

The monitoring UI may present approved operational information such as:

* service health and deployment status;
* sanitized gateway route diagnostics;
* queue, cache, storage, and provider availability summaries;
* safe processing-state and failure summaries;
* support-oriented correlation identifiers and non-sensitive diagnostics.

## Boundaries

The UI must use admin-protected REST APIs through the Public API Gateway.

It must not:

* read Elasticsearch, RabbitMQ, Redis, MinIO, or service databases directly;
* expose raw financial records, receipt images, OCR text, prompts, LLM responses, tokens, secrets, or credentials;
* infer admin authority from client-controlled headers or local state;
* implement service-owned business rules or financial calculations;
* become a general accounting interface for ordinary users.

The owning backend service decides which operational data is safe to expose. The gateway enforces the configured admin perimeter.

## FIN-194 dashboard baseline

The React client now implements service readiness, recent and failed processing
jobs, AI usage, OCR/parsing quality, and a disabled user-support lookup placeholder.
Only a successful server-authorized snapshot unlocks dashboard content. The UI
does not infer admin authority from decoded tokens or local role flags.

Access and refresh tokens exist only in the client closure in memory. No local
storage, session storage, cookies, analytics SDK, external CDN, or service worker
is used. Sign-out clears visible data immediately and attempts server revocation.
Expired sessions require sign-in again. 401/403 clears authority; failed refreshes
clear the old snapshot rather than presenting stale data as current. Automatic
refresh is opt-in, every 30 seconds, and pauses in hidden tabs.

The jobs endpoint is bounded operational metadata, not financial or user-level
support data. It is process-local (200 entries, 24 hours), not a durable queue,
audit trail, or exhaustive failure history. Producers must submit approved
signals; an empty list means no retained observations, not that no work exists.
AI/OCR counters are process-local observations and cost values are provider
micro-units, not a claimed account bill or a currency conversion.

## Build and run

Use Node 22.13+ and the declared package dependencies. The client reuses Metro,
already used by the repository's mobile stack, to produce a self-contained web
bundle; icons are generated from Lucide into build output. No runtime CDN is used.
See the [Metro bundling API](https://metrobundler.dev/docs/api/).

```powershell
npm install --no-audit --no-fund
npm run verify
$env:MONITORING_GATEWAY_URL = 'http://127.0.0.1:5000'
npm start
```

For an offline supervised run, existing compatible dependencies can be supplied
through `NODE_PATH`; no installation is required. Build from this directory.
`PORT` defaults to 5184. The local server binds only 127.0.0.1; a busy port fails
without replacing another process. The gateway origin is operator-controlled,
HTTPS except for loopback development, and never supplied by browser input.

The development server serves only known build assets and proxies four exact
gateway paths: sign-in, logout, monitoring snapshot, and monitoring jobs. It
forwards bearer authorization, never client-controlled gateway trust headers;
rejects cross-origin browser calls; refuses redirects; uses bounded request and
response sizes, timeouts, no-store, and a self-only CSP. It is not a production
internet-facing reverse proxy.

For an approved deployment, serve `dist` and the same `/gateway` allowlist from
one HTTPS origin behind the existing Gateway. Require real Identity validation,
an explicitly provisioned admin account, active identity routes, and matching
environment secrets between Gateway and Monitoring. Never activate placeholder
authentication, embed gateway secrets in assets, enable direct service access,
or self-register an administrator. No admin provisioning is implemented here.

## Structure

```text
web-admin/monitoring-ui/
  src/
    app/
    features/
    shared/
    api/
```

FIN-47 established the source boundary; FIN-194 implements this baseline.
The synthetic fixtures under `tests` are never imported into the application
bundle. Browser smoke tests intercept only local requests and do not log in to
real accounts or call paid providers.
