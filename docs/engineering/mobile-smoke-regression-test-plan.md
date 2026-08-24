# Mobile Smoke and Regression Test Plan

Related Jira: FIN-189.

## Purpose

This plan defines the minimum release-candidate validation for the Financial
Assistant React Native application on iOS and Android. It covers authentication,
onboarding, dashboard, free-form input, receipt upload, draft confirmation,
analytics, notifications, settings, loading, empty, offline, and error behavior.

A completed checklist is evidence for the mobile P7 boundary only. It does not
make the POC ready for first-user testing while runtime P6, P8, P9, deployment,
privacy, or end-to-end gates remain blocked.

## Safety and authority rules

- Use synthetic accounts, amounts, categories, phrases, and generated receipt
  images only. Never use production identities, credentials, financial data,
  receipts, OCR text, prompts, provider responses, or endpoints.
- Use a local or explicitly approved test gateway. Do not enable a live paid
  provider to execute this plan.
- Backend-confirmed records, summaries, limits, scores, and recommendations are
  authoritative. Mobile rendering must not replace or recalculate them.
- OCR and LLM output is suggestion input. A user must be able to review or edit
  it before backend confirmation.
- Record only safe scenario IDs, pass/fail state, timestamps, candidate SHAs,
  CI links, and sanitized diagnostics. Do not attach raw input or receipt bytes.

## Candidate record

| Field | Required value |
| --- | --- |
| Release name/version | |
| Candidate commit SHA | |
| GitHub pull request | |
| Backend CI run | |
| Mobile CI run | |
| Gateway/backend build | |
| Android OS, device/emulator, app build | |
| iOS version, device/simulator, app build | |
| Test date in UTC | |
| Tester | |
| Jira evidence | |
| Confluence evidence | |
| Final decision | Pass / Blocked |

All evidence must reference the same candidate SHA. Evidence from an earlier
head is invalid after source, dependency, configuration, or native-build changes.

## Environment and fixtures

Before manual validation:

```powershell
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
Set-Location mobile/app-react-native
npm ci --no-audit --no-fund
npm run verify
```

Start the approved backend and Public API Gateway using
`docs/delivery/local-developer-smoke-test.md`. Set only
`EXPO_PUBLIC_API_URL` in the local mobile environment. Start Expo with a clean
cache and install the same candidate on both platforms:

```powershell
npx expo start --clear
```

Required synthetic fixtures:

| Fixture | Required state |
| --- | --- |
| `mobile-new-user` | registered account with no completed onboarding or confirmed records |
| `mobile-empty-user` | completed onboarding, no confirmed records, notifications empty |
| `mobile-active-user` | confirmed synthetic income and expenses in daily, weekly, and monthly periods |
| `mobile-limited-user` | active user with configured monthly budget and at least one approaching limit |
| Synthetic receipt | generated JPEG or PNG with invented merchant, date, and amount; no real metadata |

Reset fixture state before a rerun. Never repair a failed case by editing a
backend database directly; use supported APIs or recreate the synthetic account.

## Platform matrix

Run every `MOB-SMK` scenario on both required paths:

| Path | Minimum evidence |
| --- | --- |
| Android | Supported Android emulator plus candidate app build; add one physical-device pass before first-user distribution |
| iOS | Supported iOS simulator on macOS plus candidate app build; add one physical-device pass before first-user distribution |

Run every `MOB-REG` scenario on the platform named in its row. Rows marked
`Both` must pass independently on iOS and Android. Permission dialogs, camera
and file pickers, app backgrounding, secure storage, and device settings must
not be inferred from the other platform.

## Smoke checklist

| ID | Flow | Procedure and expected result | Platform |
| --- | --- | --- | --- |
| MOB-SMK-001 | Register | Create `mobile-new-user`; password visibility and validation work; successful registration establishes a secure session | Both |
| MOB-SMK-002 | Onboarding | Complete locale, time zone, currency, privacy, optional budget, and explicit notification choice; skip paths do not block completion | Both |
| MOB-SMK-003 | Sign in and restore | Sign in, background/terminate, reopen, and verify the secure session restores without showing tokens or internal errors | Both |
| MOB-SMK-004 | Empty dashboard | Open `mobile-empty-user`; zero-safe overview and clear next action appear without invented totals | Both |
| MOB-SMK-005 | Active dashboard | Open `mobile-active-user`; daily, weekly, and monthly summaries match backend responses and pull-to-refresh completes | Both |
| MOB-SMK-006 | Free-form input | Submit an invented income or expense phrase; a reviewable draft opens and no authoritative record exists yet | Both |
| MOB-SMK-007 | Edit and confirm draft | Edit amount, currency, date, category, and note; confirm once; authoritative result appears and replay creates no duplicate | Both |
| MOB-SMK-008 | Reject draft | Choose discard, cancel once, then confirm discard; failed rejection preserves the draft and successful rejection removes it | Both |
| MOB-SMK-009 | Receipt upload | Choose camera and file sources, preview a synthetic image, upload, observe bounded processing, and open the owner-scoped draft | Both |
| MOB-SMK-010 | Receipt fallback | Simulate unavailable/failed OCR; friendly guidance and manual review remain available without exposing provider details | Both |
| MOB-SMK-011 | Analytics | Verify daily, weekly, monthly, category breakdown, empty period, loading skeleton, stale warning, and retry | Both |
| MOB-SMK-012 | Score and recommendations | Verify backend score factors and recommendation reasons are readable; empty and retry states remain usable | Both |
| MOB-SMK-013 | Notification inbox | Verify empty inbox, unread/read labels, mark-read idempotency, refresh, loading skeleton, and retry | Both |
| MOB-SMK-014 | Notification permission | Exercise allow, deny, and open-device-settings recovery; account preference remains distinct from OS permission | Both |
| MOB-SMK-015 | Settings | Update synthetic profile/preferences, verify persistence after refresh, and confirm sign-out clears local session state | Both |
| MOB-SMK-016 | Offline recovery | Disable connectivity with previously loaded data; offline banner appears, cached values remain visible, and recheck/retry recovers | Both |
| MOB-SMK-017 | Friendly failures | Return safe 401, 404, 409, 429, and 5xx test responses; no stack trace, hostname, payload, or technical exception is displayed | Both |
| MOB-SMK-018 | Accessibility sanity | Screen reader labels, focus order, 44/48-point targets, dynamic text, contrast, and orientation-safe layout are usable | Both |

## Regression scenarios

| ID | Risk | Expected result | Platform |
| --- | --- | --- | --- |
| MOB-REG-001 | Invalid credentials | Generic sign-in failure reveals neither account existence nor backend detail | Both |
| MOB-REG-002 | Expired access token | One serialized refresh occurs; request retries once; invalid refresh returns to anonymous navigation | Both |
| MOB-REG-003 | Logout revocation failure | Local secure session is cleared even when server revocation is unavailable | Both |
| MOB-REG-004 | Interrupted onboarding | Relaunch returns to the incomplete step; completed users cannot be routed back accidentally | Both |
| MOB-REG-005 | Duplicate submit tap | Disabled/busy controls and idempotency keys prevent duplicate draft or confirmation results | Both |
| MOB-REG-006 | Draft revision conflict | Stale confirmation shows refresh guidance and never overwrites the newer backend revision | Both |
| MOB-REG-007 | Lost confirmation response | Client reads latest draft state and recovers an already confirmed authoritative result | Both |
| MOB-REG-008 | Receipt picker cancellation | Cancel returns safely with no upload, draft, retained file, or error banner | Both |
| MOB-REG-009 | Unsupported/oversized receipt | Friendly validation appears before or from safe server rejection; no retry loop starts | Both |
| MOB-REG-010 | Background receipt polling | Background/foreground and navigation cancel stale polling and do not update an unmounted screen | Both |
| MOB-REG-011 | Slow period switching | Late analytics responses cannot replace the currently selected period | Both |
| MOB-REG-012 | Partial insight failure | Available dashboard, score, or recommendation data remains visible with honest partial-error guidance | Both |
| MOB-REG-013 | Empty-state transitions | Adding the first confirmed record replaces empty dashboard/analytics state after refresh | Both |
| MOB-REG-014 | Network loss during write | Draft/settings/read actions show retryable state and never claim success without backend confirmation | Both |
| MOB-REG-015 | Network recovery | Offline banner clears after native connectivity changes or manual recheck; retry uses the current screen state | Both |
| MOB-REG-016 | Notification replay | Mark-read replay retains the first read timestamp and does not duplicate inbox entries | Both |
| MOB-REG-017 | Permission permanently denied | App does not reprompt; direct device-settings recovery is offered and returning refreshes status | iOS and Android separately |
| MOB-REG-018 | Locale/time zone | Currency fractions, dates, daily boundary, Monday week, and month render consistently with backend scope | Both |
| MOB-REG-019 | Dynamic text and small screen | Long labels wrap without overlap; buttons and metric grids retain stable dimensions | Both |
| MOB-REG-020 | Privacy observation | Screens, logs, deep links, notifications, and errors expose no token, owner hash, raw phrase, receipt/OCR content, or internal route | Both |

## Release blocker rules

Record every failure in Jira with scenario ID, platform/build, candidate SHA,
safe reproduction steps, expected/actual behavior, severity, and owner.

| Severity | Blocking rule | Examples |
| --- | --- | --- |
| P0 | Blocks every distribution and merge claim until fixed and rerun | cross-owner data, token/secret exposure, authoritative amount corruption, duplicate confirmation, unusable auth, crash/data loss on a critical flow |
| P1 | Blocks first-user POC distribution until fixed and rerun | onboarding/input/receipt/confirmation/dashboard unavailable, misleading success, inaccessible critical action, offline recovery failure, technical or privacy-unsafe user message |
| P2 | May defer only with release-owner approval, linked Jira, workaround, and explicit scope statement | non-critical layout or wording defect with no privacy, accessibility, correctness, or workflow impact |

Any P0 or P1, failed `MOB-SMK` row, missing platform path, pending/cancelled/
skipped required CI check, unresolved actionable review, or missing exact-head
evidence makes the final decision `Blocked`. A known blocker cannot be removed
from the record merely to obtain a passing decision.

## Evidence checklist

- [ ] Candidate SHA, PR, app builds, backend build, and both CI runs are recorded.
- [ ] `npm run verify` passes on the exact candidate.
- [ ] Every `MOB-SMK` row passes on Android and iOS.
- [ ] Every applicable `MOB-REG` row passes on its named platform path.
- [ ] Screenshots or recordings contain synthetic data only and are stored in an approved evidence location.
- [ ] Failed scenarios have linked Jira defects, severity, owner, and safe diagnostics.
- [ ] All P0/P1 defects are fixed and the affected flow plus adjacent regression rows are rerun.
- [ ] Review threads, reviews, comments, labels, mergeability, and required checks are re-read on the final head.
- [ ] Jira and Confluence evidence match the final candidate and decision.

## Exit criteria

Mobile P7 validation passes only when the entire smoke checklist passes on both
platforms, applicable regression scenarios pass, exact-head CI is green, the
blocker register contains no P0/P1 item, and final evidence is complete. Record
the decision as `Blocked` whenever a requirement is not satisfied. Do not infer
first-user readiness from P7 completion alone; evaluate the repository
`docs/agent/POC_PROGRESS.md` gates and the P6, P8, and P9 release evidence.

## Related documents

- `docs/product/mobile-poc-ux.md`
- `docs/product/mobile-ui-kit.md`
- `mobile/app-react-native/README.md`
- `docs/delivery/local-developer-smoke-test.md`
- `docs/engineering/insights-validation-test-plan.md`
- `docs/engineering/insights-release-readiness-checklist.md`
- `docs/agent/POC_PROGRESS.md`
