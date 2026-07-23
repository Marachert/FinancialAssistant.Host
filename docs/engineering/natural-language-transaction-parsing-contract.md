# Natural-language transaction parsing contract

FIN-106 defines how Transaction Intake can request a parsing suggestion without granting AI authority to create a confirmed financial record.

## Request

`NaturalLanguageTransactionParseRequest` carries transient free-form input plus optional locale, time-zone, and default-currency context. Request input must not be written to AI call metadata or application logs.

## Response

`NaturalLanguageTransactionParseResponse` contains:

- suggested type, amount, currency, date, merchant, category, and note;
- overall and per-field confidence scores;
- stable ambiguity records with a field and candidate values;
- stable missing-field names;
- a short explanation intended for user review;
- computed `outputAuthority: "suggestion"` and `requiresReview: true` values.

Transaction Intake applies deterministic normalization and validation before creating a draft. Only the financial core can confirm and persist an income or expense.

## Synthetic expense example

```json
{
  "callId": "aicall_synthetic_expense",
  "suggestion": {
    "type": "expense",
    "amount": 42.50,
    "currency": "USD",
    "date": "2026-07-24",
    "merchant": "Synthetic Market",
    "categoryId": null,
    "note": "Weekly groceries"
  },
  "confidence": {
    "overall": 0.78,
    "type": 0.99,
    "amount": 0.98,
    "currency": 0.90,
    "date": 0.82,
    "merchant": 0.74,
    "category": null,
    "note": 0.88
  },
  "ambiguities": [
    {
      "code": "category_multiple_candidates",
      "field": "category",
      "candidateValues": [
        "expense.groceries",
        "expense.household"
      ]
    }
  ],
  "missingFields": [
    "category"
  ],
  "explanation": "The category needs your review before this draft can be confirmed.",
  "outputAuthority": "suggestion",
  "requiresReview": true
}
```

## Synthetic income example

```json
{
  "callId": "aicall_synthetic_income",
  "suggestion": {
    "type": "income",
    "amount": 2500.00,
    "currency": "EUR",
    "date": "2026-07-24",
    "merchant": "Synthetic Employer",
    "categoryId": "income.salary",
    "note": "Monthly salary"
  },
  "confidence": {
    "overall": 0.96,
    "type": 0.99,
    "amount": 0.99,
    "currency": 0.98,
    "date": 0.94,
    "merchant": 0.93,
    "category": 0.97,
    "note": 0.91
  },
  "ambiguities": [],
  "missingFields": [],
  "explanation": "Review the salary suggestion before confirmation.",
  "outputAuthority": "suggestion",
  "requiresReview": true
}
```
