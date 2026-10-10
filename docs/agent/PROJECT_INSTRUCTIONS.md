# Financial Assistant Project Instructions

## Product intent

Financial Assistant is a cross-platform intelligent financial assistant for Android, iOS, and Web. It helps users capture income and expenses, submit free-form text and receipt images, automatically extract and classify data, receive spending guidance, view daily/weekly/monthly limits, improve financial literacy, and track a personal financial score.

The UX must remain simple, automation-first, and understandable to ordinary users. Do not turn the product into a complex accounting system.

## Technical baseline

- Backend: C# / .NET 8
- Mobile: React Native
- Web: React or Angular
- Public API: REST
- Preferred transactional database: PostgreSQL
- Message broker: RabbitMQ
- OCR: external provider
- AI: external LLM provider API
- Notifications: mobile push and web notifications
- Architecture: modular/pragmatic microservices without premature fragmentation

## POC Deployment Target

User-approved 2026-09-15: Windows 11 and Windows Server 2022/2025 x64, Server
Desktop Experience, native Windows services installed through a WPF wizard.
No Docker, WSL or Linux VM is required by the POC. Follow
[the canonical installer decision](../architecture/windows-native-poc.md) and
FIN-270's children. Existing Compose material is historical/developer baseline;
keep valid tests and evidence, but do not treat it as native runtime acceptance.
This change does not authorize machine installation or additional spending.

Owner clarification, 2026-09-18: no paid operations during POC development or
testing. The agent delivers implementation, free automated verification and a
reproducible test handoff; the owner performs final installed-POC testing after
development. Live paid providers stay disabled. Pending owner acceptance is not
passed evidence and must not stop unrelated no-cost implementation. Follow the
zero-additional-spend policy in `SECURITY_AND_BLOCKERS.md`.

## Service Architecture

Each business service owns its data and business capability. Separate synchronous REST flows from asynchronous RabbitMQ event flows. REST is used for immediate request/response operations; events are published after owned state changes and for decoupled processing.

Do not confuse business capabilities with technical utility services. Do not share authoritative business tables or Elasticsearch document models across services.

Transactional domain models and analytics/read models must remain separate unless a documented reason justifies combining them.

Core domain concepts include Users, Incomes, Expenses, Categories, Reports, Recommendations, FinancialScoreHistory, Notifications, and user/settings/localization data where required.

## AI and OCR boundary

LLM and OCR are probabilistic helpers, not sources of truth.

Use LLM for:

- natural-language input transformation;
- explanations and recommendations;
- localization and natural-language output;
- UX improvement.

All financial calculations and final state transitions are implemented by deterministic backend logic.

Receipt pipeline:

1. upload image;
2. store file securely;
3. OCR text extraction;
4. normalize text;
5. structure candidate fields;
6. categorize;
7. create or confirm the expense entity;
8. log confidence and ambiguity without exposing sensitive content.

## Delivery decomposition

When decomposition is needed, use Epic -> Story -> Task -> Subtask. For each level capture goal, expected result, dependencies, and Definition of Done.

When a parent has subtasks, implement each leaf separately. Do not perform parent development directly while unfinished children exist.

## Recommended project phase order

1. Product clarification
2. Architecture definition
3. Domain modeling
4. API contracts
5. Backend core services
6. AI/OCR integration
7. Frontend applications
8. Analytics and gamification
9. Notifications
10. Testing
11. Deployment
12. Monitoring
13. Business/investor materials

## Quality bar

A good solution reduces manual input, is extensible across mobile and web, supports future AI capabilities, protects financial data, keeps calculations transparent, and can be delivered incrementally by the team.
