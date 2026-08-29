# React Native Mobile Application

This folder contains the Financial Assistant iOS and Android client. FIN-33
establishes the Expo Router application, authenticated navigation, typed
Identity API integration, and secure session handling. FIN-34 adds the core
transaction capture, receipt upload, editable draft review, and explicit
backend confirmation journey. FIN-35 adds the signed-in dashboard, explainable
financial score, recommendations, and profile and notification settings.
FIN-170 adds the post-registration profile setup and initial-navigation gate.
FIN-186 adds shared loading skeletons, explicit empty and friendly error states,
screen-level retry actions, and a live offline banner with manual recheck.
FIN-183 adds backend score history, recommendation detail, and owner-scoped
read and dismissal actions without calculating financial state on the client.
FIN-187 adds typed English and Ukrainian localization catalogs, profile-aware
currency and date formatting, explicit shared-control accessibility semantics,
minimum touch targets, and contrast regression coverage.
FIN-188 adds contextual camera, gallery, and notification permission rationale,
receipt privacy copy, denied-permission recovery, and device-settings return
refresh without making any permission mandatory for manual transaction entry.

## Prerequisites

- Node.js 22.13 or later
- npm
- Expo Go or a local iOS/Android development environment
- the Public API Gateway running locally or in an approved test environment

No paid provider is required to install, verify, or run these authentication
and financial insight screens. Receipt OCR and optional LLM wording remain
disabled unless an operator explicitly configures an approved provider in the
backend environment.

## Configure

Create a local `.env` from `.env.example` and set only the public gateway URL:

```text
EXPO_PUBLIC_API_URL=http://localhost:8080
```

Expo public variables are embedded in the client bundle. Never put tokens,
passwords, API keys, provider credentials, or production secrets in them.

Android emulators normally reach a host gateway through `http://10.0.2.2:8080`.
iOS simulators can normally use `http://localhost:8080`. Physical devices need
an approved reachable HTTPS endpoint or a local network address appropriate to
the development environment.

## Install and verify

```powershell
npm install --no-audit --no-fund
npm run verify
```

`verify` runs strict TypeScript checks, Expo ESLint rules, and repository-owned
structural/security checks.

Manual iOS and Android release-candidate smoke, regression, evidence, and
blocker rules are defined in:

```text
docs/engineering/mobile-smoke-regression-test-plan.md
```

Store build profiles, listing metadata, privacy disclosures, permission copy,
and strict account-owner validation are defined in:

```text
eas.json
store/
docs/delivery/mobile-store-release-tracks.md
```

`npm run verify:release` validates the credential-free repository boundary.
`npm run verify:release -- --strict` additionally requires the ignored local
console record and fails until account-owner store and signing gates are real.
Neither command creates a cloud build, submits a binary, or consumes EAS/store
credits.

## Run

```powershell
npm run android
npm run ios
```

The app restores tokens only from platform secure storage, validates a restored
session with `GET /auth/v1/me`, rotates tokens through `POST /auth/v1/refresh`,
and clears local state after logout even when server revocation fails.

Signed-in users can create a draft through `POST /transactions/intake`, upload
one JPEG, PNG, or WebP receipt through `POST /receipts`, poll its safe status,
resolve the owner-scoped OCR draft through
`GET /transactions/drafts/receipts/{receiptId}`, edit all financial fields,
and confirm through `POST /transactions/drafts/{draftId}/confirm`. Retry keys
are retained for the current phrase or receipt, receipt polling is bounded and
cancellable, and no receipt bytes or free-form financial text are logged.

The dashboard reads authoritative summaries through `GET /analytics/dashboard`,
the current score through `GET /financial-score/current`, score history through
`GET /financial-score/history`, and recommendations through
`GET /recommendations`. Recommendation detail records owner-scoped lifecycle
changes through `PUT /recommendations/{recommendationId}/read` and
`PUT /recommendations/{recommendationId}/dismissal`. Settings use `GET /users/me`,
`PUT /users/me/preferences`, and `GET` or `PUT /notification-preferences`.

User-interface copy for the authentication flow, signed-in shell, dashboard,
analytics, and notifications is resolved through `src/localization/catalogs.ts`.
English is the fallback; `uk` locales select the Ukrainian catalog. Financial
amounts and dates are formatted with the validated profile locale through the
shared localization helpers. Backend-provided recommendation wording remains
server-owned and is not translated or recalculated by the client.
The notification inbox reads prepared owner-scoped messages through
`GET /notifications?currency={currency}` and records the first read timestamp
through `PUT /notifications/{notificationId}/read`.
New profiles complete a short setup for currency, locale, time zone, an optional
monthly budget, and notification consent. Device locale and time zone values are
prefilled when available. The operating-system notification prompt is requested
only after explicit opt-in on the final step; skipping the budget or notifications
does not block completion. Both backend onboarding flags must be complete before
the signed-in navigator opens the dashboard.
Disabled backend capabilities are shown as recoverable unavailable states, and
users can pull to refresh after an operator enables the corresponding service.
The app listens to the device network state through `expo-network`. When the
device is offline, every scaffolded screen shows a clear banner without hiding
already loaded financial information; users can recheck connectivity and retry
the affected screen after reconnecting. Critical data screens use stable shared
skeletons during initial loading and avoid rendering backend problem details or
technical exception text as user-facing errors.
Account delivery preferences remain separate from operating-system permission;
when device permission is blocked, Settings provides a direct recovery action
and refreshes status after the app returns from system settings. Camera and
gallery access is requested only after an in-app explanation and explicit user
continuation. Denial keeps Files, the alternate image source, and manual entry
available. A selected receipt stays local until Upload receipt is chosen, and
the resulting OCR suggestion remains a draft until user confirmation.
The client displays score factors and recommendation evidence returned by the
backend; it does not calculate or replace those deterministic results.

## Boundaries

The client calls backend capabilities only through the Public API Gateway. It
must not calculate authoritative balances, limits, scores, or totals; call
internal services directly; persist tokens in plain-text storage; or log user
credentials, receipt data, OCR text, or financial content.

The backend remains the source of truth. OCR or LLM output stays probabilistic
input and must be presented as an editable draft until the backend confirms an
authoritative financial entity.

Product behavior and reusable visual rules are defined in:

```text
docs/product/mobile-poc-ux.md
docs/product/mobile-ui-kit.md
```
