# Mobile UI Kit

Status: implementation baseline  
Jira: FIN-32  
Consumer: mobile/app-react-native

## Purpose

This kit gives the React Native implementation a compact, testable visual and
interaction vocabulary. It favors calm financial clarity over decorative
marketing patterns. Individual screens follow mobile-poc-ux.md.

## Token rules

Expose semantic tokens from a single theme module. Feature code consumes
semantic names and does not embed raw colors, spacing, radii, or typography
values.

### Color roles

| Token | Light value | Use |
| --- | --- | --- |
| canvas | #F6F7F9 | App background |
| surface | #FFFFFF | Sheets, rows, and inputs |
| surface-subtle | #EEF1F4 | Secondary grouping |
| text-primary | #18202A | Primary text |
| text-secondary | #5C6773 | Supporting text |
| border | #C9D0D8 | Dividers and input boundaries |
| action | #0B6E69 | Primary commands and focus |
| action-pressed | #075854 | Pressed primary command |
| info | #2156A5 | Informational status |
| positive | #197A45 | Confirmed success |
| warning | #8A5200 | Attention and low confidence |
| critical | #B42318 | Errors and destructive actions |
| on-action | #FFFFFF | Text/icons on action surfaces |

Color never carries financial type, confidence, validation, or status alone.
Pair it with text and an icon. Dark-mode values may be added only as a complete
semantic set with contrast verification; do not invert colors ad hoc in feature
components.

### Typography

Use the platform system font. Sizes are fixed tokens and respect user text
scaling.

| Token | Size / line height | Weight | Use |
| --- | --- | --- | --- |
| display | 32 / 40 | 700 | Primary amount on Home |
| title | 24 / 32 | 700 | Screen title |
| heading | 20 / 28 | 600 | Section heading |
| body | 16 / 24 | 400 | Primary content |
| body-strong | 16 / 24 | 600 | Emphasis and row labels |
| small | 14 / 20 | 400 | Supporting content |
| caption | 12 / 16 | 400 | Metadata and timestamps |

Use tabular numerals for comparable amounts when platform support is reliable.
Do not reduce text below caption to fit a container.

### Spacing and shape

Spacing scale: 4, 8, 12, 16, 24, and 32.

- Screen horizontal inset: 16.
- Related control gap: 8.
- Section gap: 24.
- Input vertical padding: at least 12.
- Repeated item radius: 8.
- Input and button radius: 8.
- Dividers: one physical pixel where supported.
- Shadows: reserved for transient sheets; use borders for persistent structure.

Stable component dimensions prevent loading, validation, or translated text
from shifting adjacent controls.

## Icons

Use one React Native-compatible icon library selected by the implementation
ticket. Choose familiar symbols for back, close, add, camera, upload, edit,
delete, retry, visibility, and settings.

Icon-only controls require an accessible label and at least the platform minimum
touch target. Use icon plus text for consequential or unfamiliar commands such
as Confirm transaction and Use receipt.

## Core components

### ScreenScaffold

Owns safe-area handling, background, keyboard avoidance, title placement, and
optional bottom action area. It does not fetch data.

### AppHeader

Contains a title and at most one leading and two trailing controls. Back uses the
platform arrow and an accessible label. Long titles wrap to two lines without
overlapping controls.

### PrimaryButton

Use for the single preferred command in a view. States: default, pressed,
focused, disabled, and loading. Loading preserves width and includes an
accessible busy state.

### SecondaryButton

Use for alternate commands such as Retake or Add manually. It uses a visible
border and never competes visually with the primary command.

### DestructiveButton

Use only for explicit deletion or discard. It uses the critical role and
requires confirmation when loss cannot be undone.

### IconButton

A square stable target for familiar navigation or tool actions. It has no
rounded text container and always has an accessible label.

### TextField and MoneyField

States: idle, focused, filled, disabled, read-only, invalid, and uncertain.

- Label remains visible after entry.
- Help or error text occupies predictable space where forms would otherwise
  jump.
- MoneyField exposes amount and currency as separate accessible concepts.
- Numeric keyboards are hints only; pasted and localized input still receives
  backend validation.
- Secret fields support password-manager semantics and a labeled visibility
  control.

### SelectField

Opens a searchable sheet for categories, currencies, and other option sets.
Selected values are text, not color swatches. Empty, loading, and failed option
states are explicit.

### SegmentedControl

Used for small mutually exclusive sets such as Income and Expense. Each segment
has selected state semantics. Do not use it for more than three options.

### SwitchRow

Used only for binary preferences. The whole row is tappable and exposes label,
optional explanation, and current state. Server save failure rolls the switch
back and announces the error.

### AmountDisplay

Renders localized amount, currency, and semantic label. It supports privacy
masking and large text without truncating significant digits. Positive and
negative meaning is not color-only.

### ProgressIndicator

Use a linear indicator for daily/monthly progress and a determinate progress
bar for upload. Always pair it with text values. Never imply precision that the
backend did not return.

### StatusBanner

Inline, full-width status for info, warning, error, and success. It includes an
icon, concise message, and optional command. It is not a toast replacement for
errors requiring user action.

### DraftField

Wraps an editable field with provenance and confidence presentation. Uncertain
state uses the warning role, an icon, and plain text such as "Please check this
amount." It never blocks correction.

### TransactionRow

Contains merchant/source or category, date, type, and localized amount.
Accessibility reads the row in that order. The row height may grow for text
scaling and translated content.

### BottomSheet

Used for short option sets, permission explanations, and receipt source
selection. It supports swipe dismissal only when dismissal cannot lose work.

### EmptyState and Skeleton

EmptyState contains a concise factual message and at most one primary action.
Skeletons match final geometry, are hidden from screen readers, and stop
animating when reduced motion is enabled.

## Financial presentation

- Receive/spend labels accompany signs and icons.
- Currency comes from backend/profile data and is never guessed from device
  locale after a financial entity exists.
- Rounding and decimal precision follow backend contract values.
- Score displays include version or updated-at metadata when supplied.
- Cached values display a stale label after freshness expires.
- No client component computes balance, limit, score, or category totals.

## Feedback and motion

- Touch feedback begins immediately.
- Network completion is represented by state change, not celebratory animation.
- Standard transitions last 150 to 250 ms and honor reduced motion.
- Success feedback never hides the authoritative amount or transaction type.
- Toasts are reserved for non-blocking confirmations; actionable failures stay
  in context.

## Accessibility acceptance

Every component must pass:

- screen-reader name, role, state, and error association;
- logical focus order and focus transfer after route or validation changes;
- platform minimum touch target;
- 200 percent text scaling without clipping or overlap;
- contrast verification for text, icons, borders, and focus indicators;
- non-color status communication;
- reduced-motion behavior;
- automated accessibility checks where React Native tooling supports them.

## React Native ownership

Recommended source boundaries:

| Path | Ownership |
| --- | --- |
| src/app/theme | Semantic tokens and theme provider |
| src/shared/ui | Stateless kit components |
| src/shared/accessibility | Shared labels and focus helpers |
| src/features/* | Feature composition and server state |
| src/navigation | Route definitions and authenticated routing |
| src/api | Gateway-only transport and generated/typed contracts |

Shared UI components do not import feature stores, API clients, or financial
business rules. Feature screens provide content and server state through typed
props.

## Component completion checklist

A component is ready when:

- all documented states render with stable dimensions;
- accessible labels and focus behavior are tested;
- translated and scaled text does not overlap;
- loading cannot trigger duplicate submission;
- sensitive content is excluded from logs and analytics;
- snapshots or visual tests use synthetic data;
- iOS and Android behavior has been reviewed;
- no authoritative financial calculation exists in client code.
