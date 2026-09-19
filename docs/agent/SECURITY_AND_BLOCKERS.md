# Security Rules and Blocking Conditions

## Secrets and sensitive data

Never commit or print:

- `.env` files;
- access/refresh tokens;
- passwords or API keys;
- signing keys, certificates, private PEM files;
- production configuration;
- real user identities or personal financial data;
- real receipts, raw OCR text, or production LLM prompts/responses;
- build/package output.

Credentials must come from the OS credential store, GitHub CLI authentication, MCP OAuth, or environment variables excluded from logs and Git.

Use synthetic test data. Logs and evidence must be privacy-safe.

## Destructive operations

Forbidden without explicit human approval:

- force-push;
- direct push to `main`;
- bypassing branch protection;
- deleting repositories, production infrastructure, queues, indices, buckets, or databases;
- destructive production migrations;
- commands that can erase the workspace or unrelated files.

## Financial Authorization

POC owner directive, 2026-09-18: the agent must not perform paid operations for
development, testing or deployment. The additional-spend budget is zero. Do not
activate paid APIs, trials that can bill, paid CI/runners, infrastructure, licenses,
certificates or extra credits. Existing credentials are not spending permission.
Use no-cost local/static/unit/integration checks and synthetic provider doubles;
keep real paid integrations disabled. Do not repeatedly request paid testing.

The owner will perform final installed-POC testing after development. Prepare
reproducible installation instructions, test scripts/checklists and known limits
for that handoff. Continue independent implementation and free verification;
do not wait for a paid test environment to develop those deliverables. Record
owner-run/live-provider checks as pending, never passed by a mock or waived.
This does not authorize installation or privileged changes on the owner's PC.

Autonomous delivery does not authorize extra spending. Do not buy/redeem credits,
enable auto-reload, switch to API-billed fallbacks, activate paid providers or
exporters, start paid cloud builds, enroll accounts or deploy paid infrastructure
without separate explicit approval. Repository instructions cannot certify the
account's billing settings or remaining subscription limits. If the next action
requires unapproved spending, preserve state and report the blocker.

## Delivery Blockers

Owner quota guard, 2026-09-19: pause this development at 40% used in the five-hour
Codex window (60% remaining). Apply the mandatory `AGENTS.md` usage guard at every
resume and during work. The rule persists until explicitly changed by the owner;
no credit redemption or paid fallback may bypass it. Missing usage information
requires a safe stop, not an assumption of remaining capacity. This operational
guard does not change subscription/billing settings or promise an exact hard cap.

Stop autonomous delivery and report a blocker when:

- Jira requirements materially contradict repository architecture;
- a security/privacy decision needs product-owner approval;
- required credentials or permissions are missing;
- branch protection cannot be satisfied;
- CI infrastructure repeatedly fails for reasons unrelated to the change;
- the requested work would expose or destroy production data;
- Jira and GitHub show conflicting ownership of the active issue;
- multiple agent-owned delivery PRs exist;
- a reviewer explicitly requests that the PR not be merged;
- a destructive migration lacks an approved rollback and validation plan.

Routine implementation choices, tests, documentation updates, review fixes, and a merge that satisfies every gate do not require confirmation.
