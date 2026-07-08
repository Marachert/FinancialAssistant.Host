# Gateway and Identity Validation Checklist

## Purpose

FIN-83 defines the validation gate for the Financial Assistant Public API Gateway and Identity Service before work moves to the next backend epic.

The checklist covers:

* route configuration and public access boundaries;
* account registration and sign-in behavior;
* session rotation, revocation, expiry, and secret handling;
* Google, Apple, and phone provider preparation;
* Gateway and Identity throttling behavior.

The checklist does not make the Gateway responsible for Identity business rules. The Gateway validates perimeter routing, authentication enforcement, dispatch, correlation, and rate limiting. Identity remains the source of truth for accounts, provider links, challenges, credentials, sessions, and authentication lifecycle events.

The machine-readable source is:

```text
docs/engineering/gateway-identity-validation-checklist.json
```

`GatewayIdentityValidationChecklistTests` validates the JSON structure, mandatory domains, negative cases, Markdown synchronization, and references to existing test methods. This makes the checklist immediately enforceable in the normal backend test run and suitable for future dedicated CI reporting.

## Required execution

Run from the repository root:

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet format FinancialAssistant.Backend.sln --verify-no-changes
```

A release candidate is not ready when any mandatory automated check fails, a referenced test is removed without updating the contract, or an applicable negative case has not been exercised.

## Route checks

### GATEWAY-ROUTE-001 — fail fast on ambiguous or unsafe configuration

Expected:

* route keys are unique;
* every route has an explicit HTTP method allowlist;
* enabled destinations have an address;
* destination addresses use HTTP/HTTPS and contain no embedded credentials or query values;
* invalid configuration fails before traffic is accepted.

Negative cases:

* duplicate route key;
* empty method collection;
* enabled destination with no address;
* FTP destination;
* destination URI containing user information;
* destination URI containing query data.

Evidence:

```text
GatewayRoutingConfigurationTests.RouteCatalog_WhenRouteKeyIsDuplicated_FailsFast
GatewayRoutingConfigurationTests.RouteCatalog_WhenMethodsAreNotExplicit_FailsFast
GatewayRoutingConfigurationTests.DestinationCatalog_WhenEnabledDestinationHasNoAddress_FailsFast
GatewayRoutingConfigurationTests.DestinationCatalog_WhenAddressIsUnsafe_FailsFast
```

### GATEWAY-ROUTE-002 — sanitize route discovery and match public endpoints exactly

Expected:

* public route descriptors contain service ownership but no internal destination keys or hosts;
* public access is granted only for the exact configured method and path;
* method or path variations continue through the authenticated boundary.

Negative cases:

* internal destination metadata exposed by `/gateway/routes`;
* `GET` accepted when only `POST` is public;
* a similar but different path treated as public.

Evidence:

```text
GatewayRoutingConfigurationTests.RouteCatalog_PublicDescriptors_DoNotExposeInternalDestinationKeys
GatewaySecurityBoundaryTests.PublicAllowlist_AllowsExactMethodAndPathWithoutAccessToken
GatewaySecurityBoundaryTests.PublicAllowlist_DoesNotAllowDifferentMethodOrPath
```

## Account checks

### IDENTITY-ACCOUNT-001 — registration and sign-in happy path

Expected:

* registration creates an account and server-side session;
* sign-in creates a distinct session for the same account;
* versioned lifecycle events are published;
* credentials are never returned.

Negative cases:

* missing lifecycle event;
* reused access or refresh values;
* email or password echoed in a response.

Evidence:

```text
IdentityRegistrationLoginTests.RegisterAndSignIn_ReturnSessionsAndPublishVersionedEvents
```

### IDENTITY-ACCOUNT-002 — deterministic and enumeration-safe failures

Expected:

* duplicate registration returns a safe conflict;
* malformed requests return field-level validation errors;
* wrong password and unknown email return the same public authentication failure;
* safe metric events contain no email or account identifier.

Negative cases:

* duplicate account silently created;
* weak password or malformed client accepted;
* unknown account distinguishable from wrong password;
* email or password leaked in errors or events.

Evidence:

```text
IdentityRegistrationLoginTests.DuplicateRegistration_ReturnsSafeConflict
IdentityRegistrationLoginTests.InvalidRegistration_ReturnsFieldErrors
IdentityRegistrationLoginTests.WrongPasswordAndUnknownEmail_ReturnSameFailureAndSafeMetricEvents
```

### IDENTITY-ACCOUNT-003 — protected credential storage

Expected:

* normalized email lookup uses a purpose-protected hash;
* password storage uses the configured password hashing algorithm;
* no raw email or password is stored in the credential record.

Negative cases:

* raw email used as lookup key;
* raw password persisted;
* password text embedded in the stored hash representation.

Evidence:

```text
IdentityRegistrationLoginTests.StoredCredential_ContainsOnlyProtectedLookupAndSecretValues
```

## Session checks

### IDENTITY-SESSION-001 — refresh rotation and replay-family revocation

Expected:

* a refresh value succeeds once;
* successful refresh rotates access, refresh, and session identifiers;
* reuse of the old value revokes the entire family;
* `token.revoked.v1` records the normalized replay reason.

Negative cases:

* old refresh value accepted twice;
* replacement session survives replay detection;
* replay revocation event omitted.

Evidence:

```text
IdentitySessionLifecycleTests.Refresh_RotatesSessionAndReuseRevokesEntireFamily
```

### IDENTITY-SESSION-002 — logout revocation

Expected:

* logout revokes the authoritative server-side session;
* current-user context rejects the old access value;
* refresh rejects the old refresh value;
* a normalized logout revocation event is published.

Negative cases:

* access remains usable after logout;
* refresh remains usable after logout;
* logout event omitted.

Evidence:

```text
IdentitySessionLifecycleTests.Logout_RevokesSessionForCurrentContextAndRefresh
```

### IDENTITY-SESSION-003 — protected refresh storage and expiry

Expected:

* only a keyed refresh hash is stored;
* expired sessions fail atomic rotation;
* expired status is recorded.

Negative cases:

* raw refresh value persisted;
* expired session rotated;
* expiry not reflected in stored state.

Evidence:

```text
IdentitySessionLifecycleTests.PersistedSession_StoresOnlyHashOfRefreshValue
IdentitySessionLifecycleTests.ExpiredSession_IsRejectedByAtomicRotationStore
```

## Provider preparation checks

### IDENTITY-PROVIDER-001 — Google

Expected:

* valid provider identity creates or reuses a protected provider link;
* raw token and subject are not persisted or returned;
* a verified email matching a local credential requires explicit linking;
* invalid input, invalid tokens, and provider outages return normalized failures.

Negative cases:

* raw token returned;
* verified email silently links accounts;
* invalid token accepted;
* provider outage exposed as a generic internal exception.

Evidence:

```text
GoogleSignInTests.ValidGoogleToken_CreatesProviderAccountAndSession
GoogleSignInTests.RepeatedGoogleToken_ReusesLinkedAccount
GoogleSignInTests.VerifiedEmailMatchingLocalCredential_RequiresExplicitLink
GoogleSignInTests.InvalidGoogleToken_ReturnsSafeAuthenticationFailure
GoogleSignInTests.GoogleProviderUnavailable_ReturnsSafeServiceUnavailable
GoogleSignInTests.InvalidGoogleRequest_ReturnsValidationErrors
```

### IDENTITY-PROVIDER-002 — Apple

Expected:

* token and nonce validation succeeds only for a valid pair;
* stable provider identifiers reuse the linked account;
* verified email does not silently link accounts;
* provider outages and malformed requests are normalized safely.

Negative cases:

* token or nonce returned;
* missing or invalid nonce accepted;
* verified email silently linked;
* unavailable provider exposed as an internal failure.

Evidence:

```text
AppleSignInTests.ValidAppleToken_CreatesProviderAccountAndSession
AppleSignInTests.RepeatedAppleToken_ReusesLinkedAccount
AppleSignInTests.VerifiedEmailMatchingLocalCredential_RequiresExplicitLink
AppleSignInTests.InvalidAppleTokenOrNonce_ReturnsSafeAuthenticationFailure
AppleSignInTests.AppleProviderUnavailable_ReturnsSafeServiceUnavailable
AppleSignInTests.InvalidAppleRequest_ReturnsValidationErrors
```

### IDENTITY-PROVIDER-003 — phone verification

Expected:

* challenge start accepts E.164 input and returns only a masked destination;
* confirmation is one-time and bound to the client instance;
* resend cooldown and maximum-attempt lockout are enforced;
* provider failure returns a safe service-unavailable result;
* phone and code values are never returned.

Negative cases:

* raw phone or code returned;
* immediate resend accepted;
* locked challenge completed;
* another client confirms the challenge;
* malformed phone or purpose accepted;
* provider outage leaks destination data.

Evidence:

```text
PhoneVerificationTests.ApprovedVerification_CreatesPhoneAccountAndSession
PhoneVerificationTests.RepeatedApprovedVerification_ReusesLinkedAccount
PhoneVerificationTests.ImmediateRepeatStart_ReturnsCooldownRateLimit
PhoneVerificationTests.MaximumRejectedAttempts_LocksChallengeWithoutLeakingCode
PhoneVerificationTests.DifferentClientCannotConfirmChallenge
PhoneVerificationTests.ProviderUnavailable_ReturnsSafeServiceUnavailable
PhoneVerificationTests.InvalidStartRequest_ReturnsValidationErrors
```

Provider production readiness also requires deployment-time confirmation that provider identifiers, audiences, issuers, discovery endpoints, HMAC keys, and external credentials come from approved configuration and secret storage. The automated tests use provider-neutral stubs and do not prove external-provider availability.

## Throttling checks

### GATEWAY-THROTTLING-001 — perimeter protection

Expected:

* repeated sensitive requests return a normalized 429 with `Retry-After`;
* no credential data is returned;
* spoofing `X-Client-Instance-Id` does not reset the IP-wide partition;
* partition storage remains bounded;
* health endpoints remain available.

Negative cases:

* credentials leaked in a 429 response;
* client header bypasses a limit;
* unlimited partition creation;
* health checks throttled.

Evidence:

```text
GatewayRateLimitingTests.SignInLimit_ReturnsSafe429WithoutCredentialLeakage
GatewayRateLimitingTests.ChangingClientInstanceHeader_DoesNotResetIpWidePartition
GatewayRateLimitingTests.PartitionCache_IsBoundedAndOverflowDoesNotCreateFreshBuckets
GatewayRateLimitingTests.HealthEndpoints_AreExcludedFromRateLimiting
```

### IDENTITY-THROTTLING-001 — service defense in depth

Expected:

* registration and sign-in policies remain operation-specific;
* 429 responses contain normalized metadata and no account data;
* spoofing the client-instance header does not reset the IP partition;
* health endpoints are excluded.

Negative cases:

* account data leaked in a 429 response;
* one operation consumes another operation's policy;
* client header bypasses a limit;
* health checks throttled.

Evidence:

```text
IdentityRateLimitingTests.RegistrationLimit_ReturnsGeneric429WithoutAccountLeakage
IdentityRateLimitingTests.RateLimitPolicies_AreIndependentByOperation
IdentityRateLimitingTests.ChangingClientInstanceHeader_DoesNotResetIdentityIpPartition
IdentityRateLimitingTests.HealthEndpoint_IsNotRateLimited
```

## Review gate

A reviewer must confirm:

* all five required domains are present in the JSON contract;
* each check has at least one explicit negative case;
* referenced source files and test methods exist;
* the Markdown and JSON use the same stable check IDs;
* new Gateway or Identity behavior updates the checklist and its evidence in the same PR;
* no checklist item treats the LLM as identity truth or moves domain logic into the Gateway;
* logs and test fixtures use synthetic data only;
* external provider readiness is separately verified for the target environment.

## Future CI evolution

The current backend test suite already validates checklist integrity. A later CI job may consume the JSON contract to produce domain-specific reports, map checks to test-result names, require environment evidence for external providers, or block releases when mandatory checks are not enforced. The JSON schema must be versioned before introducing breaking fields.
