# Mobile

React Native mobile workspace for the Financial Assistant Android and iOS client.

Canonical application source boundary:

```text
mobile/app-react-native/
```

## Planned responsibilities

- Universal money input UI.
- Transaction and receipt draft confirmation.
- Home financial console and activity views.
- Financial score, recommendations, progress, and achievements.
- Camera/file receipt intake.
- Push notification handling.

The mobile client calls backend capabilities only through the Public API Gateway. Backend deterministic logic remains authoritative for financial data and calculations.
