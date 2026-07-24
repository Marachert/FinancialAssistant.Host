# AI and OCR processing audit metadata

AI and OCR processing records retain operational facts that support correlation,
monitoring, and failure analysis without retaining the financial input itself.

## Stored fields

AI call metadata stores:

- call/request ID and the same value as a user-safe trace ID;
- capability, prompt name/version, provider name, and model key;
- status, token counts, nullable confidence, safe failure category, timestamps, and
  computed duration.

OCR processing metadata stores:

- receipt ID and receipt-upload event request/trace ID;
- bounded provider name and model key;
- status, confidence, ambiguity codes, safe failure category, completion time, and
  processing duration.

AI call IDs correlate with `ai_natural_language` draft `sourceReferenceId` values.
Receipt IDs correlate OCR metadata with `receipt_ocr` draft `sourceReferenceId`
values. Correlation identifiers are opaque and must not encode user input.

## Failure categories

Failure categories are stable machine-readable codes such as `provider_timeout`,
`provider_failure`, `invalid_provider_response`,
`structured_output_validation_failed`, and `ocr_output_invalid`. Exception messages,
provider response bodies, and stack traces are never copied into audit metadata.

## Raw input policy

Audit metadata must not store natural-language input, prompt templates, model output,
receipt bytes, extracted OCR text, filenames, provider payloads, credentials, or
exception details. Raw provider material remains transient for the minimum processing
window. Production retention and deletion controls apply separately to encrypted
receipt object storage.

Provider and model keys are non-secret configuration values limited to lowercase
letters, digits, `.`, `_`, and `-`, with a maximum length of 64 characters. Audit
stores are currently in-memory development adapters; durable production storage must
enforce access control, retention, and deletion policies before deployment.
