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
identifier, code, severity, safe deterministic wording, numeric facts, and
generation timestamp.

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
preparations with template code and delivery status.

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
