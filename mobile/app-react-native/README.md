# React Native Mobile Application

This folder contains the Financial Assistant iOS and Android client. FIN-33
establishes the Expo Router application, authenticated navigation, typed
Identity API integration, and secure session handling.

## Prerequisites

- Node.js 22.13 or later
- npm
- Expo Go or a local iOS/Android development environment
- the Public API Gateway running locally or in an approved test environment

No paid provider is required to install, verify, or run these authentication
screens.

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

## Run

```powershell
npm run android
npm run ios
```

The app restores tokens only from platform secure storage, validates a restored
session with `GET /auth/v1/me`, rotates tokens through `POST /auth/v1/refresh`,
and clears local state after logout even when server revocation fails.

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
