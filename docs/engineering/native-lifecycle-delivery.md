# Native lifecycle delivery

## Current status

FIN-276 is in progress under the owner-approved local tracking exception while
Jira is suspended. The implementation is on its guarded delivery branch and has
no completion credit before reviewed exact-head CI and actual merge. Messaging 73 and
Recommendations 53 focused tests passed separately. Actual combined automatic
financial delivery and PostgreSQL crash/restart/replay passed twice, including
the repository-owned runner described below. Installed recovery and first-user
readiness remain unqualified.

Income and Expense have opt-in native registrations and focused adapter tests
(20 tests passed for each service). `Income:Messaging:Enabled` and
`Expense:Messaging:Enabled` require their respective `Messaging:ConnectionString`
settings; an enabled but missing/invalid broker configuration fails closed.
Disabled transport is a persistence-only development stage, not a fallback or a
ready deployment. The qualified synthetic path does not enable transport or
credentials in an unconfigured installed deployment.

The full backend baseline before the Profile reader changes passed 1216/1216
tests across 25 projects, with no failures or skips. Subsequent focused runs
passed Profile 39/39, Analytics 52/52 and Score 33/33; these are separate runs,
not a new full-solution result. The Analytics build retained its pre-existing
nullable test warning. Those historical scoped results do not establish merge or completion.

A subsequent full restore/Release build and 1252/1252 tests across 25 projects
passed, with no test failures/skips and four existing MSBuild cache warnings.
That full result predates the Audit consumer below. Its later focused build had
zero warnings/errors and all 45 Audit tests passed; no new full-suite total is
inferred from that subset.

Native Audit now has opt-in `Audit:Messaging:Enabled` and its own
`Audit:Messaging:ConnectionString`, separate from the legacy consumer settings.
Its owned `fa.audit.events.v1` subscription admits only explicitly configured
canonical producer roles. The adapter checks authenticated broker/body producer
agreement and delegates to the existing Audit policy and owned transaction.
Invalid metadata/conflicting immutable receipts are terminal; storage outages and
non-shutdown cancellation remain retryable. The focused tests alone are not the
combined proof; actual financial-to-Audit delivery is covered below.

## Authorized Profile Input

Native Profile exposes only the minimized internal POST
`/internal/profile/financial-settings`. The JSON request contains an owner hash,
not a raw user identifier. Its scoped `X-Profile-Reader` and
`X-Profile-Authentication` headers authorize only Analytics, Financial Score and
Recommendations/Notifications. Distinct reader secrets cannot reuse the gateway
secret; these credentials never authorize public Profile routes. The endpoint is
disabled unless `Profile:FinancialReaders:Enabled` is true and all three
`Profile:FinancialReaders:<reader>:SharedSecret` values are configured.

The Profile-owned encrypted immutable hash lookup is maintained atomically with
create/update/delete and checked before reading Profile-owned rows. Responses
preserve the durable Profile revision and exact decimal budget without exposing
raw identity, locale or time zone. Existing profiles without that lookup fail
closed until their normal bootstrap/update establishes it; no full backfill or
installed upgrade qualification is claimed.

`Analytics:Profile:SharedSecret` and `FinancialScore:Profile:SharedSecret` must
match only their respective Profile reader credentials. Each owner may override
`Profile:BaseAddress` under its own configuration prefix; the default is the
canonical loopback Profile endpoint. The client permits loopback HTTP(S) only,
disables proxy/redirects, bounds the response to 16 KiB and five seconds, and
rejects unknown, invalid, mismatched or stale settings without a default budget.

Analytics and Score materialize authorized snapshots in their own storage before
financial processing; authenticated dashboard/default-score requests also refresh
their own settings input. Profile defines only a monthly budget in its configured
currency. Analytics never infers daily/weekly limits, and neither owner performs
FX conversion. Known unconfigured/other-currency settings are distinct from an
unknown Profile, which remains unavailable. Settings-only downstream recalculation
and deployment secret wiring remain unfinished. Automatic financial convergence
is covered below; no installed-host or first-user readiness is inferred.

## Private Causal Order

### Native Configuration Handoff

Enable messaging only after provisioning the durable topology with distinct
service users and explicitly supplying each owner connection string. No gateway
credential grants broker administration, peer access or Profile reader access.

| Owner prefix | Required native transport settings | Additional input |
| --- | --- | --- |
| `TransactionIntake` | `Messaging:Enabled`, `Messaging:ConnectionString` | Owned PostgreSQL and explicit source retention |
| `Income`, `Expense` | `Messaging:Enabled`, `Messaging:ConnectionString` for each owner | Independent owned PostgreSQL configuration |
| `Analytics`, `FinancialScore` | `Messaging:Enabled`, `Messaging:ConnectionString` for each owner | Own `Profile:SharedSecret`, optional loopback `Profile:BaseAddress` |
| `RecommendationsNotifications` | `Messaging:Enabled`, `Messaging:ConnectionString` | Own `Profile:SharedSecret`, optional loopback `Profile:BaseAddress` |
| `Audit` | `Messaging:Enabled`, `Messaging:ConnectionString` | Explicit allowed producers and retention classes |
| `Profile` | No broker role is required by this financial reader path | `FinancialReaders:Enabled` and distinct `FinancialReaders:<reader>:SharedSecret` values |

Every storage owner also requires its own `Storage:Postgres` role/password,
index key and encryption key ring. The fixture creates separate runtime and
migration passwords; applications never borrow the administrator or a peer role.
The fixture is not an installer configuration generator. Protected machine secret
deployment, first-user Identity/Profile/Category bootstrap, settings-change-only
fanout/recalculation, complete object/privacy recovery and Windows Service
installation remain integrated/installer gates, not assertions of this financial
event path. A current Score is the latest committed financial-source calculation;
a Profile-only budget change does not yet republish an existing calculation.

Confirmed financial processing refreshes authorized Profile inputs before each
new source transaction. Unknown, expired or corrupt settings prevent admission;
they never become invented zero budgets or paid-provider fallback. Notifications
default to disabled until an explicit user preference is saved. The private
preparation worker creates prepared intents only and has no provider send loop.

Native Analytics and Score allocate an owner/stream publication ordinal inside
the same owned transaction as each new immutable outbox intent. Separate encrypted
forward/reverse records and the durable head prevent reuse; replay retains the
first ordinal. Ordered native reads require that metadata, and acknowledgements
reject a changed ordinal. Legacy unordered intents fail closed rather than having
an order invented from timestamps or hashes. Upgrade/backfill qualification is
not claimed. Minimal order records survive publication-payload retention.

The optional private `fa-source-sequence` AMQP header is a strict positive Int64.
It does not change public event JSON or financial authority. Only the authenticated
publisher for the relevant owner stream may supply it. Messaging passed 73/73
focused tests; a third actual broker proof completed at 2026-10-29T21:34:08Z with
cleanup, preserving the header through full retry delays, hard restart and hosted
reconnect. This remains technical transport proof, not combined business delivery.

Recommendations' owned transaction can bind that ordinal to its immutable source
fingerprint and per-pipeline/authority/currency head. A stale unseen source records
an empty receipt without changing facts or creating notifications. Reused ordinals
and changed-order replay conflict. Higher ordinals, not timestamp/hash ordering,
admit newer native snapshots. Trigger streams keep Analytics and Score separate;
legacy developer behavior remains unchanged. Enabled native messaging rejects
unordered financial/trigger ingestion. The authenticated native financial consumer
and private recommendation-to-preparation handoff are implemented and exercised
by the combined runtime fixture. Preparation requires its own canonical current
recommendation, suppresses terminal stale items, and never dispatches a provider.

Actual PostgreSQL checks passed for owned allocator rollback/idempotence, bounded
31-owner outbox discovery, exact acknowledgements, retained ordinals and real
restart. Analytics also proved alphabetical two-currency publication order and no
ordinal reuse after payload retention. One new fixture assertion initially assumed
USD before EUR; it was corrected to the existing EUR-before-USD behavior, and the
initial/restart rerun passed without changing production behavior. Separate actual
Recommendation checks passed for both financial streams with backdated/equal-time
inputs, stale trigger suppression, order conflicts, missing-publication rollback,
independent trigger heads and durable replay. No provider was invoked.

## Boundaries

The shared messaging project contains technical transport behavior only. Each
service retains its own business rules, PostgreSQL role/schema, outbox, inbox,
and transaction boundary. No consumer may read another service's tables.

Messages use copied, bounded UTF-8 JSON bodies, persistent mandatory publication,
and publisher confirms. A producer acknowledges an owned outbox item only after
all publications for that item are confirmed. Partial publication or an uncertain
acknowledgement can replay; durable consumer idempotency remains required.

The authenticated broker user identifies the publishing service, not an end
user. Application roles must not have the RabbitMQ impersonator tag. Typed
consumer adapters must validate their own schema, financial invariants, subject,
and any body producer against that authenticated service identity. Transport
metadata validation alone is not authorization to change financial state.

## Native routing

`fa.events` remains a topic exchange whose routing keys match versioned event
types. `fa.commands` is a private direct exchange for selected-owner handoffs.
For example, `income.transaction-confirmed.v1` selects Income while preserving
the existing `transaction.confirmed.v1` body/type contract. Raw confirmation
bodies must not be broadcast to unrelated financial or Audit consumers.

Native retries use the direct `fa.retry` and `fa.retry-return` exchanges and
owned queues. Retry return targets the original consumer queue, not the shared
event exchange, so retrying one consumer does not redeliver to its peers. The
configured retry delays are 5 seconds, 30 seconds, and 5 minutes; exhausted or
invalid deliveries belong in the owned dead-letter route.

A retried message is published using the retrying consumer's authenticated
broker user. Bounded retry metadata preserves the original service identity.
The receiver accepts that metadata only from its own broker user, on its own
retry-return route, for an explicitly allowed original producer. It must never
forge the original publisher's AMQP user-id.

Application users have no topology administration privileges. Provisioning and
topology qualification are separate from normal publication/consumption. The
native changes supplement, rather than silently redefine, the legacy Compose
topology in [RabbitMQ topology](../events/rabbitmq-topology.md).

## Isolated transport proof

An actual synthetic RabbitMQ 4.3.5 / Erlang 27.3.4.17 run completed successfully at
2026-10-29T19:21:15Z, with all owned processes stopped and temporary credential
files removed. It verified private command routing, broker-enforced role/topic
permissions and publisher identity, mandatory-return failure, retries 0/1/2/3
using the full configured delays, owned dead-lettering, no peer retry fanout,
cancellation redelivery, and durable message recovery after a hard broker crash.

This proof did not invoke financial service adapters or PostgreSQL projectors.
The strengthened restart acknowledgement assertion and actual host-worker
reconnect scenario passed in a second isolated run completed at
2026-10-29T19:50:26Z, again with successful cleanup. A long-lived hosted consumer
reconnected after a second hard broker crash and acknowledged a new synthetic
delivery. These tests still do not prove PostgreSQL business convergence. The
worker's non-shutdown cancellation, retry backoff, shutdown, and payload-free
logging also have focused regression coverage. An acknowledgement counter reports written AMQP
acknowledgements, not end-user acceptance or proof of financial correctness.

The existing financial Audit catalog already permits a system actor. The local
financial-commit mapper uses that actor to describe service persistence only;
it never infers the initiating human and has no actor identifier. The Audit payload omits financial amounts,
currency, category, merchant, and raw record identifiers. Its Audit ID is derived
deterministically from the immutable financial event. Source adapters must still
validate broker/body producer agreement and publish both intents before marking
the owned financial outbox item delivered. All 45 Audit tests passed, including
the mapper's catalog compatibility and minimization cases; this is a separate
focused run, not a full-solution result.

## Automatic Financial Flow Proof

The combined fixture completed successfully at `2026-10-29T22:28:07Z` in owned
run `201b6d1fcec144048034d392687a467c`. It used actual RabbitMQ and PostgreSQL,
separate service credentials/schemas, scoped Profile HTTP authentication, and
production hosted workers for Intake, Income, Expense, Analytics, Score,
Recommendations/Notifications, and Audit. No projector was called manually.

Synthetic confirmed income/expense converged automatically to authoritative
records, deterministic read models, recommendation facts, prepared web
notifications and minimized Audit records. With downstream workers stopped,
another expense reached the broker; with Intake dispatch stopped, another income
remained in its durable outbox. Application processes then exited abruptly and
PostgreSQL was stopped immediately and restarted. Fresh application workers
automatically converged to income `1250.00 USD`, expense `113.37 USD`, four
financial records, current Analytics/Score/Recommendation facts and four Audit
records. An original confirmation was republished through RabbitMQ; the real
consumer acknowledged the replay without adding duplicate financial records.
Notifications remained prepared, never delivered. Provider invocations were zero.

The fixture asserts the actual pending Intake outbox entry before/after restart.
The legacy confirmation-store `Published` flag means durable outbox admission,
not broker acknowledgement. One initial wrapper failure and one incorrect new
fixture expectation were retained; both runs cleaned up owned processes. The
corrected complete run passed, and all owned PostgreSQL/broker processes stopped.
This does not qualify installed services, first-user registration or provider use.

Self-review tightened the replay assertion to observe the specific original event
completing in the real registered transactional Income consumer, then require a
subsequent broker acknowledgement. The bounded fixture-only observer delegates
to production behavior; it never substitutes a projector or accepts an unrelated
counter increment as proof. The strengthened complete flow passed at
`2026-10-30T04:38:46Z`, proof `fd4a03c923c746c09ab4f886409f3b45`, with successful
PostgreSQL/broker cleanup and zero provider/projector calls.

After this refinement, the complete `1282/1282` suite across 25 projects passed
again without failures/skips at `2026-10-30T04:47:50Z`. The rebuild had zero errors
and five existing cache warnings; changed-source formatting and candidate privacy/
documentation checks remain required before the updated head is published.

The repository-owned runner independently repeated the complete flow at
`2026-10-30T03:48:01Z`, proof `facb5713df134d088c03bbd2b8626537`, with successful
PostgreSQL/broker cleanup. Its first local listener attempt timed out and cleaned
up; an approved outside-sandbox retry passed. Runtime results are not inferred
from structural tests or from the earlier prototype alone.

Native Recommendation focused tests separately passed `53/53`; owned preparation,
stale suppression, deny-by-default preferences and bounded 31-owner outbox scan
also passed actual PostgreSQL restart fixtures. The full `1252/1252` baseline
predates these changes and must not be represented as their full-solution result.

## Remaining qualification

Current full verification passed `1282/1282` tests across 25 projects, with no
failures or skips, completing `2026-10-30T04:05:55Z`. The first full run found
only an outdated exact scenario-list guard; the expected list was extended with
the new fixture and additional safety assertions, then the complete suite passed.
The latest Release build had zero errors and four existing MSBuild cache warnings.
Full format verified zero changes across 1250 files. Privacy checked all 1290
candidate files; Markdown checked 156 files and 530 links without problems.

The promoted Erlang probe passed at `2026-10-30T04:14:28Z`. The promoted transport
runner also passed the full retry/restart/reconnect scenario at
`2026-10-30T04:21:18Z`, proof `80359875e47f479bad304645fd2bbe9d`; cleanup succeeded
and temporary credential files were removed. Its result remains transport-only,
distinct from the two combined financial proofs above.

### Repeatable Local Commands

These tools consume already available, pinned archives and an existing free
7-Zip installation. They do not download packages, run vendor installers,
register services or invoke paid providers. Use PowerShell 7 on Windows.

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/prepare-native-broker-fixture.ps1 -ArchiveCacheDirectory <local-archive-cache>
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-erlang.ps1 -PreparedPackageDirectory <owned-extraction-directory>
dotnet build backend/shared/messaging/FinancialAssistant.Shared.Messaging.Integration/FinancialAssistant.Shared.Messaging.Integration.csproj --configuration Release
dotnet build backend/shared/messaging/FinancialAssistant.NativeLifecycle.Integration/FinancialAssistant.NativeLifecycle.Integration.csproj --configuration Release
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-broker-tooling.ps1
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-broker.ps1 -PreparedPackageDirectory <owned-extraction-directory> -Scenario Transport
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-broker.ps1 -PreparedPackageDirectory <owned-extraction-directory> -Scenario Financial -PostgresArchivePath <pinned-postgresql-archive>
```

Preparation prints its nonce directory. The Erlang probe creates only owned
launcher/boot configuration, not a machine installation. Each broker run creates
a private nonce directory below `.codex-runtime/broker-proof`; the financial
scenario also creates an owned PostgreSQL fixture. Result files distinguish pass,
cleanup, service-flow verification, and still-unqualified installed acceptance.
The structural tooling test is not a substitute for either runtime scenario.
Do not commit extracted packages, credentials, database directories or logs.

FIN-276 delivery still requires exact-head CI/review, protected merge and post-merge
evidence. FIN-286 and the security/installer tickets retain protected machine
credential deployment, settings-only refresh, first-user bootstrap and complete
installed recovery gates. No unfinished integration/installer capability is
credited to this scoped financial event-delivery proof.

Temporary extraction or isolated execution of pinned vendor packages does not
qualify installed Windows support. No service registration, vendor installer,
paid provider, or financial transaction is part of this development proof.

## Primary references

- [RabbitMQ .NET client guide](https://www.rabbitmq.com/client-libraries/dotnet-api-guide)
- [RabbitMQ validated user-id](https://www.rabbitmq.com/docs/validated-user-id)
- [RabbitMQ access control](https://www.rabbitmq.com/docs/access-control)
- [RabbitMQ publisher confirms](https://www.rabbitmq.com/docs/confirms)
