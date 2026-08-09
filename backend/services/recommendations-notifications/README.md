# Recommendations and Notifications Service v1

This POC host contains two explicit modules:

- Recommendation Service consumes `analytics.updated.v1` and
  `score.calculated.v1`, then derives deterministic fact-backed tips.
- Notification Service consumes `recommendation.generated.v1`, applies
  owner-scoped channel preferences, prepares enabled push and web messages from
  versioned templates, publishes `notification.prepared.v1`, and tracks
  delivery status.

The modules are co-hosted for the POC but keep separate application services,
domain types, event contracts, and publisher boundaries. They can be split into
independent deployables without changing their public contracts.

Recommendations are created with `active` status, can move to nonterminal `read`, and can end as `dismissed` or `expired`; terminal recommendations cannot reactivate. The deterministic MVP rules cover high category share, monthly budget pressure, missing income, incomplete profile settings, uncategorized expenses, and positive budget progress. Status and `statusChangedAtUtc` are part of the trusted recommendation response.

Notification preferences default to push and web enabled. Explicit opt-outs
are applied before template preparation or event publication through
`INotificationPreferenceProvider`; the checked-in adapter is in-memory.

No external AI or delivery provider is called. `IRecommendationWordingProvider`
is a wording-only boundary and defaults to deterministic text. Backend facts,
recommendation codes, severity, and notification state remain authoritative.

Each recommendation also receives a deterministic explanation with a
localization key, evidence confidence, and allowlisted mobile action. The
optional `IRecommendationExplanationWordingProvider` can return display text
only; the checked-in implementation is unavailable, so safe fallback text is
used without any provider call.

Development storage and publisher adapters are in-memory and are not
crash-durable. RabbitMQ mode uses publisher confirms, a quorum consumer queue,
three delayed retries, and a terminal dead-letter queue.

Trusted APIs:

- `GET /api/v1/recommendations?currency=USD`
- `GET /api/v1/notifications?currency=USD`
- `PUT /api/v1/notifications/{notificationId}/delivery-status`
- `GET /api/v1/notification-preferences`
- `PUT /api/v1/notification-preferences`

Every request requires the trusted gateway secret and user context headers.


## MVP notification triggers

`NotificationTriggerEvaluator` deterministically evaluates confirmed backend
facts and emits these trigger codes:

- `daily-input-reminder` when no confirmed input exists for the local date;
- `budget-limit-approaching` from 80% up to, but excluding, 100% usage;
- `budget-limit-exceeded` at 100% usage or above;
- `score-improved` when the authoritative score increases;
- `recommendation-available` when a recommendation occurrence is reported;
- `receipt-processing-completed` when receipt processing finishes.

Daily and budget triggers use owner, currency, trigger code, and local date as
their stable occurrence key. Score improvement also includes the resulting
score. Recommendation and receipt occurrences use their source event ID.
Replays therefore do not publish duplicate notifications.

Channel preferences are evaluated before preparation and publication.
`NotificationQuietHours` carries start, end, and IANA/Windows time-zone text
as a scheduling placeholder; deferred delivery is intentionally left to the
future delivery adapter. Trigger templates use generic lock-screen-safe wording:
they never include amounts, categories, receipt contents, owner identifiers, or
raw source-event data.

## Notification delivery adapters

`NotificationDeliveryService` routes prepared messages through provider-neutral
`INotificationDeliveryAdapter` implementations. The registered mobile push and
web adapters are intentionally non-sending placeholders: disabled channels are
suppressed, incomplete configuration fails permanently, and even complete
placeholder configuration cannot claim delivery. Only explicitly transient
provider failures are eligible for the bounded fixed-delay retry policy.

Provider, endpoint, credential, channel enablement, and retry values bind from
the `RecommendationsNotifications:Delivery` configuration section. Production
credentials must be supplied through environment variables and are never
committed or logged. See
`docs/engineering/notification-delivery-adapters.md` for statuses, environment
variable names, retry behavior, and the requirements for a future provider
implementation.

## Notification preferences API

New owners default to push and web enabled, all six known notification types
enabled, and no quiet hours. Trusted GET/PUT endpoints expose an owner-scoped
mobile-ready contract for channel toggles, enabled notification types, and
optional local quiet hours. Unknown types and invalid quiet-hour windows are
rejected.

Channel and type preferences are checked before template preparation, storage,
or event publication. Quiet hours are preserved as a future delivery-scheduler
contract; the current non-sending adapter baseline neither sends nor discards
messages during that window. See `docs/api/notification-preferences-v1.md`.
