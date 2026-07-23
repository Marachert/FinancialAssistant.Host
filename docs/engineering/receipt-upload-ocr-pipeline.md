# Receipt upload and OCR pipeline

## Flow

1. The authenticated gateway streams one JPEG, PNG, or WebP file to `POST /receipts`.
2. Receipt Processing validates the declared media type and file signature, enforces a 10 MiB limit, encrypts the bytes, and stores privacy-safe metadata.
3. `receipt.uploaded.v1` starts OCR processing.
4. An `IOcrProvider` returns transient text, confidence, and ambiguity hints.
5. The service normalizes a minimal transaction candidate and stores only OCR status, confidence, and ambiguity codes.
6. `ocr.completed.v1` is consumed idempotently by Transaction Intake, which creates a reviewable draft.

## Trust boundaries

Original filenames, raw images, raw OCR text, object-store keys, and provider errors are excluded from API responses, events, and metadata records. The development object-store adapter encrypts bytes with AES-256-GCM; production must provide durable encrypted object storage and service-owned metadata persistence.

`ocr.completed.v1` contains normalized candidate fields required for draft creation. The event does not confirm a transaction. Transaction Intake applies deterministic validation and requires user review whenever fields are missing, ambiguous, or low confidence.

Both event consumers tolerate duplicate delivery. Publication state is marked with a non-request cancellation token only after downstream processing succeeds, allowing safe retry after interrupted requests.
