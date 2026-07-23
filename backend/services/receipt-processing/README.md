# Receipt Processing Service

FIN-25 introduces authenticated receipt upload, encrypted object storage, safe metadata, and an OCR-to-draft event pipeline.

- Original filenames and raw receipt bytes are never stored in metadata or returned by the API.
- The in-memory object adapter encrypts image bytes with an ephemeral AES-256-GCM key.
- `receipt.uploaded.v1` starts OCR processing through `IReceiptUploadedConsumer`.
- OCR provider text is transient. Stored OCR state contains only status, confidence, and ambiguity codes.
- `ocr.completed.v1` carries a normalized candidate, not raw OCR text, over an authenticated internal HTTP delivery path; Transaction Intake converts it into a reviewable draft.
- OCR output is probabilistic and cannot confirm or persist an authoritative income or expense.

Configure `ReceiptProcessing__TransactionIntake__BaseAddress` with the internal Transaction Intake service URL. Configure both services with the same environment-provided `ReceiptProcessing__Events__SharedSecret` of 32 to 256 characters. Never put this value in source control or logs.

The receipt-upload transport and storage adapters are development implementations. Production deployment must supply durable object storage, broker-backed receipt-upload delivery, metadata persistence, and an approved OCR provider through the existing interfaces.
