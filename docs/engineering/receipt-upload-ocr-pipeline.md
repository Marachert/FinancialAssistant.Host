# Receipt upload and OCR pipeline

## Flow

1. The authenticated gateway streams one JPEG, PNG, or WebP file to `POST /receipts`.
2. Receipt Processing validates the declared media type and file signature, enforces a 10 MiB limit, encrypts the bytes, and stores privacy-safe metadata.
3. `receipt.uploaded.v1` starts OCR processing.
4. An `IOcrProvider` returns transient text, confidence, and ambiguity hints.
5. The service normalizes a minimal transaction candidate and stores only OCR status, confidence, and ambiguity codes.
6. Receipt Processing sends `ocr.completed.v1` to Transaction Intake's authenticated internal event endpoint.
7. Transaction Intake consumes the event idempotently and creates a reviewable draft.

## Trust boundaries

Original filenames, raw images, raw OCR text, object-store keys, and provider errors are excluded from API responses, events, and metadata records. The development object-store adapter encrypts bytes with AES-256-GCM; production must provide durable encrypted object storage and service-owned metadata persistence.

`IOcrProviderClient` is the replaceable external-provider adapter. The application-facing `IOcrProvider` buffers only the validated, size-limited image for the duration of extraction, enforces an environment-driven timeout for every attempt, and retries only explicitly transient failures within a maximum of three total attempts. Caller cancellation is never converted into a provider failure. Unknown provider exceptions and timeouts cross the boundary only as safe `OcrProviderException` codes; raw messages and response bodies are discarded.

Runtime settings use `ReceiptProcessing__Ocr__RequestTimeoutSeconds`, `ReceiptProcessing__Ocr__MaximumAttempts`, and `ReceiptProcessing__Ocr__RetryDelayMilliseconds`. Defaults are 30 seconds, two total attempts, and 100 milliseconds. Invalid or out-of-range values fail service construction.

## Candidate normalization

The deterministic normalizer first canonicalizes line endings and horizontal whitespace, then extracts only labeled merchant, total, tax, and line-item values plus valid ISO calendar dates. A single unlabeled monetary value remains supported for minimal provider adapters. Conflicting totals, currencies, dates, merchants, or taxes do not win by order; the affected field remains empty and a stable ambiguity code is emitted.

Line items are bounded to 100 placeholders. Each placeholder can carry description, quantity, unit price, total, currency, confidence, and explicit missing/invalid/mismatch codes. These values remain review suggestions. The candidate model has no raw-text property, and neither raw text nor line-item descriptions are persisted by the current OCR metadata store or integration event.

`ocr.completed.v1` contains normalized candidate fields required for draft creation. The event does not confirm a transaction. Transaction Intake applies deterministic validation and requires user review whenever fields are missing, ambiguous, or low confidence.

Both event consumers tolerate duplicate delivery. Publication state is marked with a non-request cancellation token only after downstream processing succeeds, allowing safe retry after interrupted requests.

The internal HTTP delivery requires `ReceiptProcessing__TransactionIntake__BaseAddress` on Receipt Processing and a matching environment-provided `ReceiptProcessing__Events__SharedSecret` on both services. The endpoint is not exposed through the public gateway. A production broker can replace this transport through `IOcrCompletedPublisher` without changing the event contract or deterministic draft consumer.
