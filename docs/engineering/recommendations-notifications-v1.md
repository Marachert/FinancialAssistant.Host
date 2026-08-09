# Recommendations and Notifications v1

## Deterministic authority

Recommendation codes are generated from backend facts. The v1 rules cover a
configured daily expense limit being reached, monthly expenses approaching or
reaching confirmed income, a low or strong deterministic financial score, and
a non-invasive steady-course fallback.

Recommendation identifiers are stable hashes of pseudonymous owner scope,
currency, source event, and rule code. Event replay is idempotent.

## Recommendation lifecycle

Every generated recommendation starts `active` with `statusChangedAtUtc` equal to its generation time. The authenticated read endpoint performs `active -> read`; dismissal performs `active|read -> dismissed`; and replacement by newer accepted facts performs `active|read -> expired`. Idempotent same-state writes are allowed, terminal recommendations never reactivate, and owner scope is enforced by the store. The trusted recommendation response exposes both lifecycle fields. This lifecycle is backend-owned and cannot be changed by a wording provider.

## MVP recommendation rules

| Code | Deterministic trigger |
| --- | --- |
| `high-spending-category` | largest confirmed expense category is at least 40% of monthly confirmed expenses |
| `monthly-budget-nearing-limit` | confirmed monthly expenses use at least 80% of the Profile-owned monthly budget |
| `missing-income` | confirmed monthly expenses exist and confirmed monthly income is zero |
| `incomplete-profile` | Profile settings are available and explicitly incomplete |
| `uncategorized-expenses` | confirmed uncategorized expense total is greater than zero |
| `positive-budget-progress` | complete Profile settings exist, confirmed income exceeds expenses, and budget use is at most 75% with no risk rule active |

Analytics publishes category amounts from confirmed active projections. Monthly
budget and completeness come from `IRecommendationProfileSettingsProvider`,
whose checked-in POC adapter is in-memory. Unknown Profile state does not
produce an incomplete-profile recommendation. Recommendation codes are
deduplicated before persistence and publication.

`IRecommendationWordingProvider` can later provide bounded wording. It
receives an already-authoritative recommendation and can return only title and
body. Wording is rejected when blank, oversized, or control-character-bearing.
No provider is enabled in v1.

## Event flow

The Analytics projector publishes the current UTC reporting date after each
accepted financial-record change, including when the changed record belongs to
an older period. It resolves the backend-owned daily limit for that reporting
date. Process-local pending publication scopes retain failed currency events so
a financial-event retry republishes only the unconfirmed scopes.

1. Recommendation Service consumes `analytics.updated.v1` or
   `score.calculated.v1`.
2. Accepted events update an owner/currency insight snapshot.
3. Deterministic rules replace the current recommendation set.
4. Each item publishes `recommendation.generated.v1`.
5. Notification Service reads owner-scoped channel preferences.
6. Enabled channels are prepared independently from versioned templates.
7. Each accepted preparation publishes `notification.prepared.v1`.

RabbitMQ mode uses `fa.events`, a quorum application queue, publisher
confirms, delayed retries at 5 seconds, 30 seconds, and 5 minutes, and a
terminal DLQ. Malformed contracts go directly to the DLQ.

## Storage and delivery limitations

The checked-in POC store is process-local memory. It does not claim durable
history, an outbox, replica coordination, or restart recovery. Production work
must add service-owned durable stores and outboxes before external delivery is
enabled.

Push and web are provider-neutral preparations. `INotificationPreferenceProvider`
is consulted before preparation; explicit channel opt-outs suppress both
preparation and publication. The POC adapter is process-local memory and
defaults both channels to enabled until Profile integration supplies settings.

Push and web are provider-neutral preparations. No external provider, token,
device registration, or paid API is used. Provider credentials and raw endpoint
identifiers must never enter logs or event payloads.
