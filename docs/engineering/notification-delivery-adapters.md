# Notification Delivery Adapter Baseline

FIN-139 adds a provider-neutral boundary between prepared notification business
messages and future mobile push or web notification SDKs. Recommendation and
notification rules continue to produce `PreparedNotification` values and do
not reference provider types, credentials, endpoints, or SDKs.

## Boundary

`NotificationDeliveryService` selects exactly one
`INotificationDeliveryAdapter` by the prepared message channel. Adapters return
only a deterministic `NotificationDeliveryAdapterResult`; the application
service converts that result into a `NotificationDeliveryAttempt` and applies
the retry policy. The checked-in push and web adapters are placeholders. They
never make network calls and never claim that a message was delivered.

Delivery status values are:

- `prepared`: trusted message is ready for a delivery adapter;
- `retry-scheduled`: a transient failure may be attempted again;
- `delivered`: a provider confirmed delivery;
- `failed`: delivery ended without provider confirmation;
- `suppressed`: the owner/channel configuration intentionally prevented send.

Only `delivered`, `failed`, and `suppressed` are terminal.

## Configuration

Provider settings are supplied through the standard .NET configuration pipeline.
Production secrets must be environment-provided and must never be committed,
logged, returned by APIs, or included in delivery evidence.

Environment variable placeholders use these names:

- `RecommendationsNotifications__Delivery__Push__Enabled`
- `RecommendationsNotifications__Delivery__Push__Provider`
- `RecommendationsNotifications__Delivery__Push__Endpoint`
- `RecommendationsNotifications__Delivery__Push__Credential`
- `RecommendationsNotifications__Delivery__Web__Enabled`
- `RecommendationsNotifications__Delivery__Web__Provider`
- `RecommendationsNotifications__Delivery__Web__Endpoint`
- `RecommendationsNotifications__Delivery__Web__Credential`
- `RecommendationsNotifications__Delivery__Retry__MaxAttempts`
- `RecommendationsNotifications__Delivery__Retry__DelaySeconds`

Both channels are disabled by default. A disabled channel is `suppressed`.
An enabled channel without complete provider configuration is a permanent
`provider-not-configured` failure. Complete placeholder configuration still
returns `provider-adapter-placeholder` until a reviewed provider-specific
implementation replaces it; therefore this baseline cannot spend money or send
a real notification.

## Retry behavior

Only an adapter failure explicitly marked transient is eligible for retry. The
configured policy uses a fixed delay, caps total attempts with `MaxAttempts`,
and never retries suppression, incomplete configuration, placeholder adapters,
or permanent provider rejection. Defaults are three total attempts and a
30-second delay. Invalid attempt limits fail service construction.

A future provider adapter must keep message bodies privacy-safe, avoid logging
credentials or destinations, map provider errors to stable failure codes, and
add contract tests before it can return `delivered`.
