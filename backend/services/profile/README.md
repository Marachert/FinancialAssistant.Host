# Financial Assistant Profile Service

.NET 8 Profile Service for FIN-19 and FIN-88.

Canonical engineering documentation:

```text
docs/engineering/profile-service-preferences.md
```

## Responsibility

Profile Service owns user profile preferences needed by transaction parsing and localized UX:

- locale;
- timezone;
- default currency;
- first day of week;
- monthly budget preference;
- budget and weekly-summary notification placeholders;
- profile and preference onboarding completion flags;
- privacy mode;
- AI personalization opt-in.

Identity remains the source of truth for accounts, authentication, credentials, provider links, and sessions. Profile does not copy email, phone, display name, provider tokens, raw provider subjects, session IDs, receipts, OCR text, or financial transactions from Identity.

## API

```text
GET  /users/me
PUT  /users/me/preferences
POST /internal/profile/v1/events/user-registered
GET  /profile/info
GET  /health/live
GET  /health/ready
```

The public profile routes expect the API Gateway to forward a trusted `X-Gateway-User-Id` header after authentication. The internal registration event creates the default profile after `user.registered.v1`.

## Defaults

New profiles start with:

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

Users can update preferences through `PUT /users/me/preferences`. Currency codes are normalized to uppercase three-letter values. Locale, timezone, first day of week, monthly budget amount, and privacy mode are validated by deterministic backend logic. A zero monthly budget means that no budget has been configured yet; notification fields are stored preferences only and do not send messages in this increment.

## Storage

FIN-19 uses an in-memory `IProfileStore` adapter so local development and CI can exercise the service contract without production infrastructure. A future persistence increment must replace it with Profile-owned durable storage. Other services must not read Profile storage directly.

## Runtime and verification

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet run --project backend/services/profile/FinancialAssistant.Profile.Api/FinancialAssistant.Profile.Api.csproj
```

OpenAPI is available in Development and Testing:

```text
/openapi/v1.json
```
