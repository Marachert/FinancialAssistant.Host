# Recommendations and Notifications API v1

FIN-30 introduces trusted internal REST contracts for the current deterministic
recommendations and prepared notification delivery state.

## Authentication

The service accepts requests only from the Public API Gateway. The gateway
supplies `X-Gateway-Authentication` and `X-Gateway-User-Id`. The
shared secret is environment-provided and never stored in the repository. The
service hashes the user identifier before accessing owner-scoped state.

## Recommendations

`GET /api/v1/recommendations?currency=USD` returns the current
recommendation set for one owner and currency. Each item includes a stable
identifier, code, severity, safe deterministic wording, numeric facts,
generation timestamp, and an `explanation` object.

The explanation contains a localization key, safe display text, evidence
confidence, an allowlisted mobile action code and app-relative route, and
`isWordingEnhanced`. Confidence is `high` when the rule exposes numeric
deterministic facts and `baseline` when the deterministic rule requires no
numeric fact. It does not represent AI confidence.

Deterministic fallback text is always available. The checked-in optional
wording provider is unavailable and makes no external call. A future provider
may replace display text only; it cannot alter facts, codes, severity, action
metadata, lifecycle, or financial values. Invalid or failed provider output
uses the fallback.

Recommendation facts originate from `analytics.updated.v1` and
`score.calculated.v1`. Optional wording providers cannot change codes,
severity, numeric facts, financial values, or delivery policy.

`PUT /api/v1/recommendations/{recommendationId}/read` records that the
authenticated owner has read an active recommendation. The read state remains
eligible for dismissal or automatic expiry.

`PUT /api/v1/recommendations/{recommendationId}/dismissal` records an
authenticated owner's explicit dismissal with an initialized UTC timestamp.
Newer accepted facts automatically mark superseded active or read
recommendations `expired`. Dismissed and expired states are terminal and
cannot be reactivated.

## Notifications

`GET /api/v1/notifications?currency=USD` returns push and web
preparations with template code, delivery status, and nullable `readAtUtc`.
The gateway alias is `GET /notifications?currency=USD`. Items are ordered by
preparation time and remain owner/currency scoped.

`PUT /api/v1/notifications/{notificationId}/read` and the gateway alias
`PUT /notifications/{notificationId}/read` record the authenticated owner's
first read time. The request contains an initialized `changedAtUtc`. Replays are
idempotent and preserve the first read timestamp; another owner receives `404`.

`PUT /api/v1/notifications/{notificationId}/delivery-status` accepts a
terminal status of `delivered`, `failed`, or `suppressed`
plus an initialized UTC timestamp. A terminal status is immutable. Updates are
restricted to the authenticated owner.

The POC prepares delivery only. It does not call Apple, Google, browser push,
email, SMS, or another paid provider.

## Errors

Errors use problem details with stable codes:

- `trusted_gateway_authentication_required`
- `authentication_required`
- `invalid_recommendation_notification_request`
- `recommendation_not_found`
- `recommendation_status_conflict`
- `notification_not_found`
- `notification_status_conflict`

Examples and tests use synthetic identifiers and values only.
