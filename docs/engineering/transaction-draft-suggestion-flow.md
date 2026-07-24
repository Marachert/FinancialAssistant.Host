# Transaction draft suggestion flow

Transaction Intake treats natural-language parsing and receipt OCR as suggestion
sources. Both paths produce a `TransactionDraft`; neither path creates an income,
expense, transfer, balance, or other confirmed financial record.

## Draft suggestion metadata

Each draft preserves a nested `suggestion` object:

- `source` distinguishes `ai_natural_language` from `receipt_ocr`;
- `sourceReferenceId` correlates receipt suggestions without storing raw OCR text;
- `outputAuthority` is always `suggestion`;
- `confidence` is the normalized overall confidence from the source;
- `ambiguities` combines source ambiguity codes with deterministic validation codes;
- `missingFields` combines source-reported fields with fields rejected or absent after
  deterministic validation;
- `reviewMessage` gives the client a bounded, non-sensitive prompt for user review.

The existing top-level confidence, ambiguity, and review fields remain available for
backward compatibility. They are produced from the same normalized values as the
nested metadata.

## Source mapping

Natural-language intake maps parser candidates to the `ai_natural_language` source. The
current deterministic parser is a development adapter behind the provider-neutral
parser interface; a future AI adapter can supply the same draft contract without
changing Transaction Intake authority.

Receipt Processing publishes normalized OCR candidate fields and ambiguity codes.
Transaction Intake maps those events to the `receipt_ocr` source and retains the
receipt identifier as the source reference. Raw receipt bytes and raw OCR output are
not copied into the draft.

Confidence below `0.75`, any ambiguity, or any missing field marks a draft as requiring
review. Regardless of that flag, creating a confirmed financial record still requires
the explicit authenticated confirmation endpoint and its deterministic validation.
