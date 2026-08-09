# Notification Preferences API v1

FIN-140 defines the trusted, owner-scoped settings contract used by mobile and
web clients.

## Endpoints

- `GET /api/v1/notification-preferences`
- `PUT /api/v1/notification-preferences`

Gateway aliases are available at `/notification-preferences`. Every request
requires the trusted gateway authentication and user context headers. The owner
identifier is hashed inside the service boundary and is never accepted in the
request body.

The response and PUT request contain:

- `pushEnabled`: enables the mobile push channel;
- `webEnabled`: enables the web notification channel;
- `enabledNotificationTypes`: zero or more values from the known type list;
- `quietHours`: optional local start/end times plus a time-zone identifier.

Known notification types are:

- `daily-input-reminder`
- `budget-limit-approaching`
- `budget-limit-exceeded`
- `score-improved`
- `recommendation-available`
- `receipt-processing-completed`

New users default to both channels and all known types enabled, with no quiet
hours. An empty enabled-type list suppresses every notification type while
preserving channel choices for later use.

## Quiet Hours

Quiet hours are a scheduling contract. Preparation preserves the setting, but
the current non-sending delivery adapter baseline does not yet own a durable
scheduler. A future delivery worker must defer eligible messages until the local
quiet period ends; it must not silently discard them. Start and end must differ,
and the time-zone identifier must be non-empty. Cross-midnight windows are
represented directly, for example 22:00 to 07:00.

## Enforcement and Safety

Channel and notification-type preferences are evaluated before template
preparation, storage, or event publication. Recommendation-generated messages
map to `recommendation-available`. Unknown notification types are rejected
with HTTP 400, updates are owner-scoped, and one user's values cannot affect
another user.

No provider credential, device token, browser subscription, financial amount,
or personal financial data is part of this API.
