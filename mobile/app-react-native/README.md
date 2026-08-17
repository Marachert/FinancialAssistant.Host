# React Native Mobile Application

This folder is the canonical workspace for the Financial Assistant Android and iOS client.

## Responsibilities

The mobile application owns client-side presentation and device integration for:

* universal money input;
* transaction and receipt draft confirmation;
* daily, weekly, and monthly financial views;
* score, recommendations, progress, and achievements;
* mobile push notifications;
* camera and file selection for receipt intake;
* secure storage of short-lived client authentication state.

## Boundaries

The mobile client must call backend capabilities only through the Public API Gateway.

It must not:

* calculate authoritative balances, limits, scores, or financial totals;
* treat OCR or LLM output as confirmed transaction data;
* call internal service addresses, RabbitMQ, Elasticsearch, or object storage directly;
* persist access tokens in plain-text application storage;
* embed production secrets or provider credentials.

The backend remains the source of truth. AI-assisted and OCR-assisted results must be shown as drafts when user confirmation is required.

## Product design baseline

React Native implementation follows:

```text
docs/product/mobile-poc-ux.md
docs/product/mobile-ui-kit.md
```

The UX contract defines routes, states, platform behavior, accessibility,
privacy wording, and backend dependencies. The UI kit defines semantic tokens
and reusable component behavior. Implementation tickets may refine those
contracts only when the change remains consistent across iOS and Android and is
recorded in the same pull request.

## Planned structure

```text
mobile/app-react-native/
  src/
    app/
    features/
    shared/
    navigation/
    api/
  android/
  ios/
```

Application scaffolding and dependency selection belong to a dedicated frontend implementation task. FIN-47 establishes only the canonical source boundary.
