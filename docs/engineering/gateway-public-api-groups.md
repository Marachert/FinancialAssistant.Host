# Gateway Public API Groups

## Purpose

FIN-84 documents the public REST capability groups exposed through the Financial Assistant Public API Gateway for Android, iOS, Web, and admin clients.

In this document, **public API** means an API reachable through the public gateway. It does not mean anonymous access. Most groups require an authenticated session, and the monitoring group requires an admin role.

The gateway is the single public HTTP entry point. It routes requests to the backend service that owns the capability, applies technical perimeter controls, and returns the downstream response. It does not own domain data or business rules.

## Sources of truth

The runtime route contract is configured in:

```text
backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/appsettings.json
Gateway:RouteMap:Routes
Gateway:Security:PublicEndpoints
```

This document is the client- and delivery-facing view of that configuration.

Related technical documentation:

```text
docs/engineering/api-gateway-routing-foundation.md
docs/engineering/gateway-route-groups-and-destinations.md
docs/engineering/gateway-access-control.md
```

## Current availability

All repository-default routes currently have status `placeholder`, and all internal destinations are disabled.

Therefore:

* the groups define the intended public contract and ownership boundary;
* repository-default calls return safe placeholder or destination-unavailable responses rather than reaching a business service;
* a route becomes operational only after its owning service contract is ready, its destination is enabled, the route status is changed to `active`, and deployment verification passes.

Clients must not infer production availability from the presence of a route in this document. Runtime availability is visible through the deployed environment and the sanitized `GET /gateway/routes` endpoint.

## Request flow

```text
Android / iOS / Web / admin client
-> Public API Gateway
-> correlation and gateway rate limiting
-> exact public allowlist or access-token validation
-> route-level authenticated/admin policy
-> owning service REST API
-> owning service deterministic validation and business logic
-> response through the gateway
```

The gateway does not publish business events for ordinary proxy requests. After a synchronous request is accepted, the owning service may publish domain or integration events through RabbitMQ.

## Security model

### Exact public identity endpoints

The `/auth` route group is deny-by-default and uses the `authenticated` route policy.

Only these exact method-and-path pairs bypass access-token validation:

| Public operation | Purpose |
| --- | --- |
| `POST /auth/v1/register` | Create an account and initial session |
| `POST /auth/v1/sign-in` | Authenticate with email and password |
| `POST /auth/v1/refresh` | Rotate a session using an Identity-owned refresh value |
| `POST /auth/v1/providers/google/sign-in` | Authenticate with a Google identity token |
| `POST /auth/v1/providers/apple/sign-in` | Authenticate with an Apple identity token and nonce |
| `POST /auth/v1/providers/phone/verifications` | Start phone verification |
| `POST /auth/v1/providers/phone/verifications/confirm` | Confirm phone verification |

A different method or path under `/auth` requires a valid access token. For example, `POST /auth/v1/logout` and `GET /auth/v1/me` are authenticated operations.

### Authenticated groups

Authenticated groups require a valid Identity-issued bearer access token. The gateway validates the technical token contract and forwards a small trusted user/session context.

The owning service must still enforce resource ownership and domain authorization. Gateway authentication is not a substitute for service-level authorization.

### Admin group

The admin monitoring group requires a valid bearer token and the configured admin role. Client-supplied admin headers have no authority.

## Configured route contract

| Route key | Public path contract | Methods | Gateway access | Owning service | Repository status |
| --- | --- | --- | --- | --- | --- |
| `auth` | `/auth`, `/auth/{**gatewayPath}` | GET, POST | `authenticated` with exact public allowlist | Auth Service | `placeholder` |
| `profile-me` | `/users/me`, `/users/me/{**gatewayPath}` | GET, PUT, PATCH | `authenticated` | Profile Service | `placeholder` |
| `categories` | `/categories`, `/categories/{**gatewayPath}` | GET, POST, PUT, PATCH | `authenticated` | Category Service | `placeholder` |
| `transaction-intake` | `/transactions/intake`, `/transactions/intake/{**gatewayPath}` | POST | `authenticated` | Transaction Intake Service | `placeholder` |
| `transaction-draft-confirm` | `/transactions/drafts/{id}/confirm` | POST | `authenticated` | Transaction Intake Service | `placeholder` |
| `receipts` | `/receipts`, `/receipts/{**gatewayPath}` | GET, POST | `authenticated` | Receipt File Intake Service | `placeholder` |
| `analytics` | `/analytics`, `/analytics/{**gatewayPath}` | GET | `authenticated` | Analytics Service | `placeholder` |
| `score` | `/score`, `/score/{**gatewayPath}` | GET | `authenticated` | Financial Score Service | `placeholder` |
| `recommendations` | `/recommendations`, `/recommendations/{**gatewayPath}` | GET | `authenticated` | Recommendation Service | `placeholder` |
| `notifications` | `/notifications`, `/notifications/{**gatewayPath}` | GET, POST, PATCH | `authenticated` | Notification Service | `placeholder` |
| `admin-monitoring` | `/admin/monitoring`, `/admin/monitoring/{**gatewayPath}` | GET | `admin` | Monitoring Admin Service | `placeholder` |

The route table is intentionally technical. Client applications should use the stable public paths and never call internal destination addresses directly.

## Identity API group

### Public prefix

```text
/auth
```

### Owner

Auth Service, implemented by the Identity Service boundary.

### Capability ownership

Identity owns:

* account registration and sign-in;
* Google, Apple, and phone authentication;
* access-token issuance;
* refresh rotation;
* logout and revocation;
* current authenticated identity context;
* credentials, provider links, verification challenges, and sessions.

### Gateway responsibility

The gateway owns the exact anonymous allowlist, token validation for protected Identity operations, correlation, rate limiting, safe technical failures, and forwarding.

The gateway does not validate passwords, refresh values, provider tokens, Apple nonces, or phone codes. Those decisions belong to Identity.

## Profile API group

### Public prefix

```text
/users/me
```

### Owner

Profile Service.

### Capability ownership

Profile owns user-facing profile data such as display preferences and profile attributes that are not authentication truth.

Identity remains authoritative for credentials, sessions, provider links, and authentication state. Profile must not become a duplicate identity store.

### Gateway responsibility

The gateway authenticates the request and forwards the trusted technical context. Profile performs resource ownership validation and profile-domain rules.

## Category API group

### Public prefix

```text
/categories
```

### Owner

Category Service.

### Capability ownership

Category owns:

* user and system category definitions;
* category lifecycle rules;
* category visibility and ownership;
* category lookup contracts used by transaction flows.

### Gateway responsibility

The gateway routes and authenticates. It does not decide whether a category can be created, renamed, deleted, or assigned to a transaction.

## Transaction API group

### Public paths

```text
POST /transactions/intake
POST /transactions/intake/{**gatewayPath}
POST /transactions/drafts/{id}/confirm
```

### Owner

Transaction Intake Service.

### Capability ownership

Transaction Intake owns deterministic intake and confirmation behavior, including:

* accepting structured or normalized transaction input;
* creating a draft or intake result;
* validating required transaction fields;
* confirming a transaction draft;
* preventing duplicate or invalid confirmation;
* publishing transaction lifecycle events when appropriate.

### Gateway responsibility

The gateway applies perimeter authentication and intake rate limits, then forwards the request unchanged.

The gateway never calculates amounts, selects categories, validates merchant or date rules, creates transaction entities, or treats LLM output as transaction truth.

## Receipt API group

### Public prefix

```text
/receipts
```

### Owner

Receipt File Intake Service.

### Capability ownership

Receipt File Intake owns the initial file boundary:

* receiving receipt files or images;
* validating file metadata and size;
* storing the file through the approved object-storage boundary;
* creating receipt-processing state;
* starting the asynchronous OCR/extraction pipeline.

The wider receipt pipeline remains:

```text
upload image
-> store file
-> OCR text extraction
-> text normalization
-> structured field extraction
-> categorization
-> expense or transaction confirmation
-> confidence and ambiguity logging
```

### Gateway responsibility

The gateway authenticates, rate-limits intake, and streams the request. It does not inspect receipt content, call OCR, store files, parse line items, or create expenses.

## Analytics API group

### Public prefix

```text
/analytics
```

### Owner

Analytics Service.

### Capability ownership

Analytics owns read models and aggregated views such as daily, weekly, and monthly summaries, trends, budget progress, and report-oriented queries.

Analytics is not the source of truth for transactions. It consumes authoritative service data and events to build read-optimized models.

### Gateway responsibility

The gateway authenticates and routes GET requests. It does not aggregate transactions, calculate reports, or query analytics Elasticsearch indices directly.

## Financial score API group

### Public prefix

```text
/score
```

### Owner

Financial Score Service.

### Capability ownership

Financial Score owns deterministic score calculation, score history, rule versions, explainable contributing factors, and progress-oriented results.

An LLM may explain a score in natural language, but it must not calculate or overwrite the authoritative score.

### Gateway responsibility

The gateway authenticates and routes read requests. It does not calculate score values or access score storage.

## Recommendation API group

### Public prefix

```text
/recommendations
```

### Owner

Recommendation Service.

### Capability ownership

Recommendation owns personalized financial guidance, recommendation lifecycle, eligibility, prioritization, and safe explanation output.

Deterministic financial facts and calculations must be supplied by backend services. LLM behavior is probabilistic and may improve explanations or UX, but it is not a source of transaction truth.

### Gateway responsibility

The gateway authenticates and routes requests. It does not build prompts, call the LLM, rank recommendations, or persist recommendation state.

## Notification API group

### Public prefix

```text
/notifications
```

### Owner

Notification Service.

### Capability ownership

Notification owns:

* user notification preferences;
* notification inbox/read state where applicable;
* mobile push and web notification dispatch;
* delivery attempts and provider integration;
* scheduling or consumption of notification-triggering events.

### Gateway responsibility

The gateway authenticates and routes client-facing notification operations. It does not send push messages or consume domain events on behalf of Notification Service.

## Admin monitoring API group

### Public prefix

```text
/admin/monitoring
```

### Owner

Monitoring Admin Service.

### Capability ownership

Monitoring Admin owns approved operational views, service-health summaries, deployment/runtime diagnostics, and administrative monitoring contracts.

It must expose only safe operational data. User data, financial records, receipt content, OCR text, LLM prompts/responses, secrets, tokens, internal credentials, and raw Elasticsearch documents are prohibited.

### Gateway responsibility

The gateway requires the configured admin role, removes spoofed privileged headers, and forwards the request. It does not generate service-owned monitoring data.

## Gateway responsibility boundary

The gateway owns:

* the single public REST entry point;
* public route matching and explicit method constraints;
* exact anonymous endpoint allowlisting;
* access-token and admin-role perimeter enforcement;
* correlation and trace propagation;
* gateway rate limiting;
* safe request forwarding;
* safe gateway-level 401, 403, 429, 501, 503, and 504 responses;
* sanitized route diagnostics.

The gateway does not own:

* account, profile, category, transaction, receipt, analytics, score, recommendation, notification, or monitoring domain state;
* service-owned Elasticsearch indices;
* financial calculations;
* resource ownership decisions;
* OCR processing;
* LLM prompts, completions, recommendation generation, or natural-language parsing;
* business event publication for proxied requests.

## Synchronous and asynchronous boundaries

### Synchronous REST

Use REST through the gateway when a client needs an immediate result:

```text
client
-> gateway
-> owning service
-> immediate response
```

Examples include sign-in, profile reads, category changes, transaction intake, receipt upload acceptance, analytics reads, score reads, recommendation reads, notification preference changes, and admin monitoring reads.

### Asynchronous RabbitMQ

Use RabbitMQ after the owning service has accepted a state change or when work continues independently:

```text
owning service state change
-> transactional event intent / outbox
-> RabbitMQ
-> consuming services
-> read models, OCR stages, analytics, score, recommendations, notifications
```

The gateway does not publish these business events. The service that owns the state change owns the event intent.

## Client integration rules

Client teams should:

* call only the public gateway base URL;
* use the documented public paths rather than internal service addresses;
* send a bearer access token for every operation except the exact public identity allowlist;
* treat `401` as missing/invalid/expired authentication according to the returned stable code;
* treat `403` as insufficient role or authorization;
* respect `429` and `Retry-After`;
* preserve or record the returned correlation identifier for support;
* not depend on internal destination names, ports, Elasticsearch indices, RabbitMQ routing keys, or implementation classes;
* not assume a placeholder group is operational in a deployed environment.

## Change rules

When a public group changes, the same pull request must update:

1. `Gateway:RouteMap:Routes`;
2. `Gateway:Security:PublicEndpoints` when anonymous access changes;
3. gateway tests;
4. this public API group catalog;
5. detailed route/destination documentation;
6. client-facing OpenAPI or API contracts owned by the destination service;
7. deployment activation configuration when the route becomes active.

A gateway route change must not silently move business capability ownership into the gateway.

## Verification

Automated documentation coverage verifies that:

* every configured route key has a row in this document;
* public patterns, methods, access policies, service owners, and repository statuses match `appsettings.json`;
* every exact public identity endpoint is listed;
* the `/auth` route remains deny-by-default and `authenticated` at group level;
* all ten FIN-84 capability groups are documented.

Run:

```bash
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```
