# Recommendations and Notifications Service v1

This POC host contains two explicit modules:

- Recommendation Service consumes `analytics.updated.v1` and
  `score.calculated.v1`, then derives deterministic fact-backed tips.
- Notification Service consumes `recommendation.generated.v1`, prepares
  push and web messages from versioned templates, publishes
  `notification.prepared.v1`, and tracks delivery status.

The modules are co-hosted for the POC but keep separate application services,
domain types, event contracts, and publisher boundaries. They can be split into
independent deployables without changing their public contracts.

Recommendations are created with `active` status. The defined terminal lifecycle states are `dismissed` and `expired`; terminal recommendations cannot become active again. Status and `statusChangedAtUtc` are part of the trusted recommendation response.

No external AI or delivery provider is called. `IRecommendationWordingProvider`
is a wording-only boundary and defaults to deterministic text. Backend facts,
recommendation codes, severity, and notification state remain authoritative.

Development storage and publisher adapters are in-memory and are not
crash-durable. RabbitMQ mode uses publisher confirms, a quorum consumer queue,
three delayed retries, and a terminal dead-letter queue.

Trusted APIs:

- `GET /api/v1/recommendations?currency=USD`
- `GET /api/v1/notifications?currency=USD`
- `PUT /api/v1/notifications/{notificationId}/delivery-status`

Every request requires the trusted gateway secret and user context headers.
