# Mobile PoC UX Flows

Status: implementation baseline  
Jira: FIN-32  
Platforms: iOS and Android  
Client boundary: mobile/app-react-native

## Product outcome

The mobile PoC lets an ordinary user register, capture money activity with
minimal typing, review every probabilistic result before confirmation, and
understand current financial state without accounting terminology.

The backend is authoritative for confirmed transactions, balances, limits,
analytics, and scores. The mobile client presents server results and never
recalculates authoritative financial values.

## Experience principles

1. Put the next useful action on the first screen.
2. Ask only for information the backend cannot infer safely.
3. Show OCR and AI results as editable drafts, never confirmed facts.
4. Explain uncertainty beside the affected value and offer a direct correction.
5. Use plain money language: spent, received, left today, and monthly progress.
6. Keep destructive or privacy-sensitive actions explicit and reversible where
   the backend supports reversal.
7. Preserve the user's draft across navigation, recoverable errors, and app
   backgrounding. Never persist receipt content or access tokens in plain text.

## Information architecture

| Area | Primary purpose | Entry |
| --- | --- | --- |
| Onboarding | Explain the product, automation boundary, and privacy posture | First launch |
| Authentication | Register, sign in, and recover an authenticated session | Onboarding or expired session |
| Home | Show today's limit, monthly progress, score, recommendation, and recent activity | Default signed-in tab |
| Add | Capture a free-form phrase or receipt | Persistent central action |
| Review | Correct and confirm a transaction draft | Add result |
| Insights | Explore score factors, progress, and recommendations | Bottom tab |
| Settings | Profile, currency, locale, notifications, privacy, and sign out | Bottom tab |

Signed-in bottom navigation contains Home, Add, Insights, and Settings. Add is a
command, not a persistent content tab: it opens the input flow and returns to
Home after confirmation.

## Route contract

| Route | Back behavior | Authentication |
| --- | --- | --- |
| Onboarding | Exits only through platform behavior | Anonymous |
| Sign in / Register | Returns to onboarding until authenticated | Anonymous |
| Home | Platform exit behavior | Required |
| Add input | Returns to previous signed-in screen | Required |
| Receipt capture | Returns to Add without losing text draft | Required |
| Draft review | Returns to Add with draft preserved | Required |
| Confirmation result | Returns to Home and refreshes server state | Required |
| Insights / Score detail | Returns to Insights or Home | Required |
| Settings detail | Returns to Settings | Required |

Deep links to authenticated routes first restore the secure session. On failure,
the app opens Sign in and resumes the intended destination after success.

## Flow 1: onboarding and authentication

1. The welcome screen states the literal product name and the two primary
   outcomes: quick money capture and understandable guidance.
2. A short privacy screen explains that receipt text and free-form input may be
   processed by external OCR or AI providers, while confirmed financial records
   and calculations remain controlled by the backend.
3. The user chooses Create account or Sign in.
4. Forms request only backend-required fields. Password managers, autofill,
   platform keyboards, and accessible error announcements are supported.
5. A successful response stores the session only in platform secure storage and
   opens Home.
6. Invalid credentials stay on the same form, preserve the non-secret fields,
   focus the error summary, and never reveal whether an unrelated account exists.
7. Logout clears secure client state, transient drafts, and cached personal
   responses before returning to Sign in.

## Flow 2: home dashboard

The first viewport prioritizes scanability and action:

- today's remaining amount and its currency;
- monthly progress with spent and budget values;
- current financial score with an updated-at label;
- one latest recommendation with its fact source;
- recent confirmed transactions;
- the persistent Add action.

Loading uses stable skeleton dimensions. Partial API failures keep successful
sections visible and give the failed section a Retry action. Stale data is
labeled with its last successful update time; it is never presented as fresh.

Amounts use the user's locale and currency from backend/profile data. Income and
expense meaning is conveyed by sign, label, and icon, not color alone.

## Flow 3: universal money input

1. Add opens with one focused multiline input and a Receipt action.
2. Placeholder examples are synthetic and short, such as "Coffee 4.50" or
   "Salary 1200".
3. Continue is enabled only after non-whitespace input exists.
4. Submission creates a server-side draft. The app shows a cancellable progress
   state and prevents accidental duplicate submission.
5. A successful response opens Draft review.
6. A timeout or connectivity failure keeps the phrase locally in protected
   transient state and offers Retry.
7. A provider-unavailable response explains that automatic parsing is
   temporarily unavailable and offers manual draft entry when the backend
   supports it.

The client does not run financial calculations or silently infer a confirmed
transaction.

## Flow 4: draft review and confirmation

Draft review is a focused form, not a dashboard card. It contains:

| Field | Behavior |
| --- | --- |
| Type | Income or Expense segmented control |
| Amount | Currency-aware numeric input; server validation is authoritative |
| Currency | Profile default with an explicit picker |
| Category | Searchable option list from the backend |
| Merchant / source | Optional text input |
| Date | Platform date picker, default supplied by the draft |
| Confidence | Plain-language explanation only where uncertainty exists |

The primary command is Confirm transaction. Edit remains inline. Cancel returns
to Add and asks before discarding entered data.

Low-confidence fields are marked individually with an icon, label, and short
reason. The screen focuses the first uncertain or invalid field. Confidence
must not be represented only by a percentage or color.

Confirmation rules:

- send the draft identifier and user edits to the backend once;
- disable the primary command while the request is active;
- use an idempotency key when the API contract provides one;
- show success only after the backend confirms the authoritative entity;
- on validation failure, map field errors and preserve every valid edit;
- on ambiguous server outcome, refresh by draft/idempotency state before
  allowing another confirmation attempt.

## Flow 5: receipt capture and upload

1. The user chooses Camera or Files through a platform action sheet.
2. Before first camera access, explain why the permission is needed.
3. Permission denial offers Open settings and Files; it does not dead-end.
4. The preview provides Retake, Remove, and Use receipt actions.
5. Upload progress is visible and cancellable before server processing begins.
6. OCR processing uses a separate progress state so upload and analysis are not
   conflated.
7. The resulting draft always opens Draft review before confirmation.
8. Unsupported type, excessive size, unreadable image, low confidence, provider
   outage, and network failure each have specific recovery copy.
9. The client removes temporary image files according to the platform lifecycle
   after upload/cancel and never writes raw OCR text to logs or analytics.

FIN-188 implements the permission boundary with separate Camera, Gallery, and
Files actions. Camera and Gallery first show localized in-app rationale and
invoke the native permission prompt only after explicit continuation. A denial
offers the alternate image source, Files, and manual transaction entry; a
non-requestable denial also offers the platform app-settings redirect. Receipt
copy states that selection remains local until Upload receipt and that OCR
produces only a reviewable draft. Notification permission follows the same
pre-prompt pattern in Settings, remains optional during onboarding, and is
re-read when the app returns from device settings.

## Flow 6: insights, score, and recommendations

Insights starts with server-computed summaries. It includes:

- current score and version/update metadata;
- simple factor explanations;
- daily and monthly progress;
- category breakdown;
- recommendations linked to the deterministic facts that triggered them.

Recommendations are guidance, not commands. AI-assisted wording is labeled in a
quiet secondary line and must not imply that the model calculated balances or
scores. Missing data produces an honest empty state instead of invented advice.

Selecting a score factor opens a concise explanation and the relevant server
facts. The app never exposes internal prompts, provider payloads, or sensitive
diagnostics.

## Flow 7: settings and privacy

Settings contains:

- profile;
- default currency and locale;
- notification preferences;
- privacy and AI usage;
- sign out.

Notification switches mirror backend preference state. Optimistic UI is allowed
only when a failed update rolls back visibly and accessibly. If operating-system
permission is disabled, the app distinguishes device permission from the
backend preference and offers Open settings.

Privacy and AI usage uses plain statements:

- what input may be sent for OCR or AI assistance;
- that suggested fields require confirmation;
- that backend calculations remain authoritative;
- how to remove a pending draft or receipt before confirmation;
- where to find the product privacy policy when available.

## Shared state model

Every data-bearing screen implements these states:

| State | Required behavior |
| --- | --- |
| Initial loading | Stable skeleton; no fake amounts |
| Refreshing | Existing data remains readable |
| Empty | Explain what is absent and offer the relevant action |
| Field validation | Inline error plus screen-level accessible summary |
| Recoverable error | Preserve input and offer Retry |
| Offline | Preserve safe transient work and identify unavailable actions |
| Unauthorized | Clear session state and route through Sign in |
| Forbidden | Explain the unavailable action without retry loops |
| Rate limited | Respect server retry guidance and prevent request storms |
| Server unavailable | Keep confirmed cached data labeled as stale |
| Success | Confirm the server result and provide the next useful action |

## Platform behavior

Use React Native primitives with platform-specific behavior where users expect
it:

- native safe areas, back gestures, keyboards, date pickers, permission prompts,
  action sheets, and secure storage;
- minimum touch targets of 44 by 44 points on iOS and 48 by 48 dp on Android;
- platform text scaling without clipped amounts or buttons;
- reduced-motion support;
- screen-reader labels that include amount, currency, type, and status;
- no horizontal layout dependency at supported phone widths.

Do not fork product terminology or flow order between platforms. Platform
differences belong to interaction mechanics, not business meaning.

## Backend dependency map

| Mobile capability | Backend contract |
| --- | --- |
| Authentication and session restore | Public API Gateway identity routes |
| Profile, currency, and locale | Profile routes through the gateway |
| Free-form input and editable drafts | Transaction intake routes |
| Receipt upload and OCR draft | Receipt-processing routes |
| Draft confirmation | Authoritative income/expense confirmation routes |
| Home summary | docs/api/dashboard-composition-v1.md |
| Category breakdown | docs/api/analytics-category-breakdown-v1.md |
| Score and history | docs/api/financial-score-v1.md |
| Recommendations | docs/api/recommendations-notifications-v1.md |
| Notification inbox and read state | docs/api/recommendations-notifications-v1.md |
| Notification settings | docs/api/notification-preferences-v1.md |

If a required API shape is absent, the implementation ticket records the gap
instead of embedding service-specific calls or duplicating backend logic.

## Privacy-safe product analytics

Allowed events describe interaction outcomes without user content:

- onboarding_completed;
- authentication_succeeded or authentication_failed with coarse reason;
- input_started and draft_received;
- draft_corrected with field name only;
- transaction_confirmed;
- receipt_upload_started, completed, or failed with coarse reason;
- dashboard_section_failed;
- notification_preference_changed.

Never record amounts, merchant names, free-form phrases, receipt data, OCR text,
tokens, identifiers that expose a person, or AI prompts/responses.

## Implementation handoff checklist

FIN-32 is ready for React Native implementation when all are true:

- every required PoC area has a route, entry, exit, and back behavior;
- loading, empty, error, low-confidence, offline, and success states are defined;
- the draft is always editable and confirmation remains explicit;
- privacy and AI wording preserves the probabilistic/authoritative boundary;
- iOS and Android platform differences are identified;
- accessibility and text-scaling requirements are testable;
- backend dependencies are visible and internal service calls are forbidden;
- component and token rules are defined in mobile-ui-kit.md.
