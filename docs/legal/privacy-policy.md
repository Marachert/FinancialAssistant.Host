# Financial Assistant Privacy Policy

Effective date: 2026-08-25

This policy describes the controlled proof-of-concept release of Financial
Assistant. It does not authorize production use or replace notices required by
the release owner's jurisdiction.

## Product boundary

Financial Assistant helps a signed-in user capture, review, and understand
personal financial activity. Backend deterministic logic owns confirmed
transactions, summaries, limits, analytics, and scores. OCR and AI output is
optional suggestion input and cannot create authoritative financial records
without user review and confirmation.

## Data processed

The controlled POC can process:

- account email, internal user/session identifiers, and security metadata;
- profile preferences such as locale, time zone, currency, privacy mode, and an
  optional budget target;
- free-form transaction input, editable drafts, confirmed income and expense
  details, categories, dates, notes, summaries, limits, scores, and prepared
  recommendations or notifications;
- receipt images selected by the user and derived OCR suggestions when receipt
  processing is enabled;
- minimal operational metadata needed for security, reliability, audit, and
  support, without raw financial text or receipt content in broad logs.

Camera, photo-library, and notification access is requested only in context and
only after user action. The app does not request contacts or location access.

## Purposes and authority

Data is used to provide account security, transaction capture and confirmation,
authoritative financial views, user-selected notifications, privacy controls,
reliability, abuse prevention, and support. It is not used for advertising,
cross-app tracking, sale, or data-broker sharing.

## Storage and protection

Mobile session credentials are stored in platform secure storage. The client
uses an approved HTTPS Public API Gateway and does not receive internal service
credentials. Backend services restrict records by authenticated owner and keep
raw receipt, OCR, prompt, and financial content out of broad events, monitoring,
admin, and MCP surfaces. Store signing keys and provider credentials are kept
outside the repository.

## Providers and sharing

The repository works without a paid AI or OCR provider. If a release owner later
enables an approved provider, the provider, region, retention, data use, cost
limit, fallback, and disclosure must be reviewed before use. The controlled POC
does not include advertising SDKs and does not share personal data for marketing
or tracking.

## Retention and deletion

The release owner must define and verify environment-specific retention and
account-deletion procedures before public release. Controlled testers should use
the private support contact supplied with their invitation to request access,
correction, export, or deletion. Never post an email address, financial data,
receipt, credential, or deletion request in a public GitHub issue.

## Changes and support

The exact store disclosures are versioned in
`mobile/app-react-native/store/privacy-disclosures.json`. They must be reviewed
again whenever application behavior, SDKs, providers, data handling, or release
scope changes.

Public product support and defect tracking are available through the
[Financial Assistant repository issues](https://github.com/Marachert/FinancialAssistant.Host/issues).
Controlled testers receive a private support path in their invitation for any
request containing personal information.
