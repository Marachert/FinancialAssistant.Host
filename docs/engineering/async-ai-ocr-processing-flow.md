# Asynchronous AI and OCR processing flow

## Purpose

AI parsing and OCR extraction are provider-dependent jobs. Public financial APIs accept
work, persist an owned request or receipt, and expose status; workers perform provider calls
after the REST request has completed. Provider output remains suggestion data and cannot
confirm a financial record.

This document defines the versioned boundary contracts and lifecycle. Broker transport,
retry counts, backoff, and dead-letter policy are configured separately. FIN-116 owns the
detailed retry and permanent-failure rules.

## Responsibilities

| Boundary | Responsibility |
| --- | --- |
| Public REST | Authenticate, validate bounded input, enforce idempotency, persist owned state, return the resource and current status. |
| Event publisher | Publish from durable owned state and retry publication without duplicating the business resource. |
| Job dispatcher | Convert a source event into one idempotent provider job command. |
| AI or OCR worker | Load source data through an authenticated service boundary, call the provider, validate and normalize output, and publish status plus one terminal event. |
| Transaction Intake | Apply suggestion data to a reviewable draft and expose user-visible status. |
| Confirmation REST flow | Revalidate the reviewed draft deterministically and publish `transaction.confirmed.v1`; it never runs from an AI or OCR event. |

Raw natural-language input, prompts, receipt bytes, OCR text, provider payloads, and
exception details are not copied into commands or events. Contracts carry opaque owned
references, normalized suggestion references, safe failure categories, and correlation
identifiers.

## Job lifecycle

The common lifecycle is:

```text
queued -> processing -> suggestion_ready
                    \-> failed
failed -> queued
```

- `queued` means the source event was accepted and a job command is available.
- `processing` means one worker owns the current attempt.
- `suggestion_ready` is terminal for that job and points to validated suggestion data.
- `failed` is terminal for the attempt. `Retryable` says whether policy may enqueue another
  attempt; it does not schedule the retry itself.
- A retry keeps the same `JobId`, increments `Attempt`, creates a new command identifier,
  and transitions from `failed` to `queued`.

Consumers deduplicate source events by `EventId`, commands by `CommandId`, and the logical
operation by `JobId`. Status consumers reject stale attempts and invalid transitions.

## AI parsing sequence

1. Transaction Intake accepts a natural-language draft request, stores the sensitive payload
   behind an opaque reference, and publishes `transaction.draft-created.v1`.
2. The AI dispatcher consumes the draft event and publishes `ai.parsing.requested.v1`.
3. The AI worker publishes `ai.parsing-status-updated.v1` with `processing`, resolves the
   payload through the authenticated owner boundary, and calls AI Orchestration.
4. Valid schema-checked output is stored as suggestion data. The worker publishes
   `ai.suggestion-ready.v1`, then status `suggestion_ready`.
5. A safe provider or validation failure publishes `ai.parsing-failed.v1`, then status
   `failed`. Transaction Intake surfaces the safe category and retry availability.

## OCR extraction sequence

1. Receipt Processing accepts and securely stores a receipt, then publishes
   `receipt.uploaded.v1`.
2. The OCR dispatcher consumes the upload event and publishes
   `ocr.extraction.requested.v1`.
3. The OCR worker publishes `ocr.extraction-status-updated.v1` with `processing`, loads the
   receipt through owned storage, and calls the configured OCR provider.
4. Valid normalized candidates publish the existing `ocr.completed.v1` suggestion event,
   then status `suggestion_ready`.
5. A safe extraction or validation failure publishes `ocr.extraction-failed.v1`, then status
   `failed`. The receipt status endpoint surfaces the safe category and retry availability.

## REST and event separation

Create and upload endpoints do not wait for provider completion in the production flow.
They return the accepted resource with `queued` status. Status reads are synchronous and
side-effect free. Events and commands perform provider work and update owned processing
state. Development transports may execute consumers in-process, but must preserve the same
contracts, idempotency keys, lifecycle, and privacy boundaries.

Suggestion events cannot call confirmation. A user must review the draft and invoke the
authenticated confirmation REST endpoint. Deterministic validation is repeated there before
any authoritative income or expense event is published.
