# Analytics Rebuild and Backfill Process

Related Jira: FIN-147.

## Status

This document and the checked-in contracts define the rebuild control plane.
They do not activate a public or trusted-admin endpoint. The existing
`AnalyticsProjector.RebuildAsync` all-store reset is a process-local
development/test helper and is not a production backfill operation.

Production execution requires a trusted admin tool, durable job/checkpoint
storage, authoritative source readers, staging projections, and an atomic
owner/period swap.

## Authority and scope

A rebuild is scoped by:

- a SHA-256 owner scope hash;
- an inclusive local `periodStart` and `periodEnd`;
- an authoritative `sourceSnapshotVersion` or high-water mark.

The source snapshot contains confirmed Income and Expense records plus their
versioned lifecycle events. Drafts, OCR text, prompts, AI output, raw receipts,
and client-computed totals are forbidden. Analytics, Financial Score, limits
progress, and Recommendation inputs are disposable derived state.

The request period is bounded to 3,650 days. The trusted admin layer resolves a
real authenticated owner to the pseudonymous scope before creating a request;
the hash is never returned in progress responses or logged.

## Idempotency

`AnalyticsRebuildPlanner` calculates a stable job key from contract version,
owner scope hash, inclusive period, and source snapshot version. Retries with
the same source and scope produce the same key. A changed period or source
snapshot produces a different key.

A durable executor must enforce a unique job-key constraint and persist a
checkpoint after each stage and bounded record batch. Restarting a pending,
running, or failed job resumes from its last durable checkpoint. A succeeded
job returns its existing evidence instead of rebuilding again.

Record application remains deterministic:

1. group by owner hash, record type, and record ID;
2. retain the highest revision, breaking an impossible equal-revision conflict
   by rejecting the source snapshot;
3. include only the resulting active confirmed record in totals;
4. keep currencies isolated;
5. calculate daily, Monday-based weekly, and calendar-month aggregates with the
   existing backend formulas.

## Ordered stages

The v1 plan has six ordered stages:

1. `validate-source`: authorize scope, freeze the source high-water mark,
   validate event versions, and reject revision conflicts.
2. `rebuild-analytics`: build owner/period projections in isolated staging
   storage from confirmed records and lifecycle state.
3. `rebuild-score-history`: recalculate deterministic score snapshots from
   the staged analytics timeline and the versioned score formula.
4. `refresh-limit-progress`: recalculate daily, weekly, monthly, and streak
   values using authoritative settings and local-calendar rules.
5. `refresh-recommendation-inputs`: publish or stage deterministic analytics
   and score facts without generating facts through AI.
6. `verify-and-swap`: compare counts/totals, apply events after the frozen
   high-water mark in revision order, then atomically replace only the requested
   owner/period scope.

Live financial events continue to enter the normal consumer. The executor
captures the frozen source high-water mark, builds staging data, and replays
newer events before the atomic swap. It must never call the global
`ResetAsync` operation for a scoped production job.

## Progress and failure evidence

`AnalyticsRebuildProgressResponse` exposes job key, status, current stage,
processed and optional total record counts, timestamps, and an optional safe
failure object. Status is one of `pending`, `running`, `succeeded`,
`failed`, or `cancelled`.

Failure evidence contains a stable code, safe detail, failed stage, and UTC
timestamp. Logs may include job key, stage, source version, counts, duration,
and correlation ID. They must not include owner hashes, identities, amounts,
categories, event payloads, receipts, or prompts.

A failed staging job leaves the active projection untouched. Temporary staging
data is retained only for the documented diagnostic window and then deleted.
Retries use the same job key and checkpoint. Verification or swap failure
requires operator review; partial active-scope replacement is forbidden.

## Admin dependency

A future FIN-36 trusted admin operation must provide authorization, reason and
audit metadata, dry-run preview, explicit confirmation, job status lookup,
cancel/retry controls, and links to safe diagnostics. It must call an internal
executor, never expose rebuild controls through the public gateway, and never
allow a caller to submit totals or derived financial values.
