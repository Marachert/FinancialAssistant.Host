# Mobile

React Native mobile workspace for the Financial Assistant Android and iOS client.

Canonical application source boundary:

```text
mobile/app-react-native/
```

## Implemented baseline

- Authentication, onboarding and profile/settings screens.
- Free-form text and camera/file receipt intake with editable draft review, rejection and confirmation.
- Home, Add, Insights and Settings navigation, activity, score and recommendation views.
- Notification inbox and lifecycle actions, loading/empty/error and offline states.

See the [application guide](app-react-native/README.md) and
[implemented UX contract](../docs/product/mobile-poc-ux.md).
Native audio capture, the complete wallet/debt/reserve concept and real push/web
delivery are not all implemented. Store signing, submission, tester installation
and approved-host acceptance remain release gates, not consequences of a merged UI.

The mobile client calls backend capabilities only through the Public API Gateway. Backend deterministic logic remains authoritative for financial data and calculations.

Release-candidate validation is defined in:

```text
docs/engineering/mobile-smoke-regression-test-plan.md
```

App Store Connect, TestFlight, Google Play internal testing, metadata, privacy,
and cost-controlled operator steps are defined in:

```text
docs/delivery/mobile-store-release-tracks.md
```
