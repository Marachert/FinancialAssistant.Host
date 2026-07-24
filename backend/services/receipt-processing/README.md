# Receipt Processing Service

FIN-25 introduces authenticated receipt upload, encrypted object storage, safe metadata, and an OCR-to-draft event pipeline.

- Original filenames and raw receipt bytes are never stored in metadata or returned by the API.
- The in-memory object adapter encrypts image bytes with an ephemeral AES-256-GCM key.
- `receipt.uploaded.v1` starts OCR processing through `IReceiptUploadedConsumer`.
- `IOcrProviderClient` isolates the external OCR adapter. `ResilientOcrProvider` enforces bounded timeout and transient retry settings before returning suggestion data to the application.
- OCR provider text is transient. Stored OCR state contains only status, confidence, and ambiguity codes.
- The deterministic normalizer separates raw text from merchant, date, total, currency, optional tax, and bounded line-item placeholder candidates. Multiple totals and other conflicts remain explicit ambiguity codes.
- `ocr.completed.v1` carries a normalized candidate, not raw OCR text, over an authenticated internal HTTP delivery path; Transaction Intake converts it into a reviewable draft.
- OCR output is probabilistic and cannot confirm or persist an authoritative income or expense.

Configure `ReceiptProcessing__TransactionIntake__BaseAddress` with the internal Transaction Intake service URL. Configure both services with the same environment-provided `ReceiptProcessing__Events__SharedSecret` of 32 to 256 characters. Never put this value in source control or logs.

OCR resilience defaults to a 30-second timeout, two total attempts, and a 100-millisecond retry delay. Override these with `ReceiptProcessing__Ocr__RequestTimeoutSeconds` (1-120), `ReceiptProcessing__Ocr__MaximumAttempts` (1-3), and `ReceiptProcessing__Ocr__RetryDelayMilliseconds` (0-5000). Only failures explicitly classified as transient are retried; provider details are mapped to safe application errors.

FIN-114 records privacy-safe OCR audit metadata: the receipt-upload event identifier, bounded provider/model keys, processing duration, confidence, a safe failure category, and a trace identifier. Configure the non-secret identity fields with `ReceiptProcessing__Ocr__ProviderName` and `ReceiptProcessing__Ocr__ModelKey`. The receipt identifier correlates this metadata with the Transaction Intake draft source reference; raw image bytes, extracted text, provider responses, exception messages, and stack traces are excluded.

The receipt-upload transport and storage adapters are development implementations. Production deployment must supply durable object storage, broker-backed receipt-upload delivery, metadata persistence, and an approved OCR provider through the existing interfaces.
