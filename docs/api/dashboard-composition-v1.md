# Dashboard Composition Contract v1

Related Jira: FIN-144.

This document defines the stable response shape that mobile and web dashboard
teams can mock while service aggregation is implemented later. It does not
activate a new public route. Runtime data remains owned by the Analytics,
Financial Score, Recommendation, and Notification services, and the public
gateway destinations for unfinished insight services remain disabled.

`DashboardCompositionResponse` uses `schemaVersion = "1"` and contains:

- currency, time zone, local reference date, and generated timestamp;
- daily, Monday-to-Sunday weekly, and calendar-month summary widgets;
- top expense-category previews plus `hasMore`;
- an optional score widget with explicit `isAvailable`;
- daily, weekly, and monthly limit progress plus tracking streak;
- recommendation previews plus `hasMore`;
- notification unread count and `hasUnread`;
- explicit empty-state booleans for every dashboard area;
- per-source availability, staleness, and last-successful-update metadata for
  Analytics, Financial Score, Recommendation, and Notification data.

Each `freshness` source has `isAvailable`, `isStale`, and nullable
`lastSuccessfulUpdateAtUtc`. An unavailable source is therefore distinct from
a healthy source that legitimately returns an empty widget. Summary, category,
limit, and streak widgets share the Analytics source state. Score,
recommendation, and notification widgets each use their corresponding source
state.

Empty collections are `[]`, not null. Unavailable score values and
unconfigured limit values are null only where the contract declares nullable
fields. Zero financial totals are valid values and are distinguished from
`hasFinancialData = false`.

Recommendation previews are privacy-safe display text and do not contain raw
facts or prompts. Notification badges contain only a non-negative count.
The response excludes owner hashes, event IDs, revisions, source payloads,
provider credentials, storage models, and internal service addresses.

Authoritative calculations remain in backend services. A future composition
endpoint must call or consume service-owned contracts, preserve owner and
currency boundaries, cap category/recommendation previews, populate every
source's freshness state, and add gateway activation plus end-to-end tests
before clients treat the contract as runtime available.
