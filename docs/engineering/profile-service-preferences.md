# Profile Service Preferences

FIN-19 introduces the Profile Service baseline. FIN-88 completes the MVP settings model used by localized UX, financial calculations, recommendations, notifications, and onboarding.

## Service boundary

Profile owns:

- locale;
- timezone;
- default currency;
- first day of week;
- monthly budget preference;
- budget and weekly-summary notification placeholders;
- profile and preference onboarding completion flags;
- privacy mode;
- AI personalization opt-in.

Identity owns accounts, credentials, provider links, sessions, and authentication events. Profile consumes the minimal `user.registered.v1` signal needed to create a default profile and intentionally does not copy email, phone, display name, provider tokens, raw provider subjects, session identifiers, receipts, OCR text, or financial transaction payloads.

## User registration flow

```text
Identity authoritative account write
-> user.registered.v1 integration event
-> Profile internal event handler
-> default profile created if missing
```

The handler is idempotent. Replaying the registration signal returns the existing profile instead of creating a duplicate.

## REST contract

```text
GET  /users/me
PUT  /users/me/preferences
POST /internal/profile/v1/events/user-registered
```

`GET /users/me` and `PUT /users/me/preferences` require the API Gateway to forward `X-Gateway-User-Id` after successful authentication. Profile does not trust user IDs supplied directly by public clients.

## Defaults and validation

New profiles use:

```text
locale = en-US
timezone = UTC
currencyCode = USD
firstDayOfWeek = monday
monthlyBudgetAmount = 0
budgetNotificationsEnabled = false
weeklySummaryNotificationsEnabled = false
profileOnboardingCompleted = false
preferencesOnboardingCompleted = false
privacyMode = standard
aiPersonalizationEnabled = false
```

Preference updates are deterministic backend logic:

- locale must resolve through .NET culture metadata;
- timezone must resolve through .NET timezone metadata;
- currency is normalized to a three-letter uppercase code;
- first day of week is normalized to a lowercase weekday name;
- monthly budget amount cannot be negative, and zero means not configured;
- privacy mode is either `standard` or `strict`;
- AI personalization is explicit and defaults to disabled.

Notification fields are preference placeholders only; delivery is owned by a later notification increment. Onboarding flags record completion state without inferring it from financial activity.

## Storage and privacy

The FIN-19 runtime adapter is in-memory for local development and CI. Production persistence is deferred to a later storage increment and must be Profile-owned. Other services and the API Gateway must use Profile APIs/events rather than querying Profile storage directly.

Profile logs and evidence must remain privacy-safe. Do not log raw user identifiers alongside preference payloads, and do not store financial transactions, receipts, OCR text, or LLM prompts/responses in Profile.

## Automated coverage

`FinancialAssistant.Profile.Tests` verifies:

- `user.registered.v1` creates a default profile;
- users can read their own profile via gateway context;
- preference updates affect only the authenticated user's profile;
- invalid currency, locale, timezone, first-day-of-week, and monthly-budget preferences return a validation problem;
- the registration event contract excludes sensitive Identity attributes;
- Domain remains isolated from Infrastructure and provider clients.
