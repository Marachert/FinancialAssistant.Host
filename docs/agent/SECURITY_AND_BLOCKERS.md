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

Autonomous delivery does not authorize extra spending. Do not buy/redeem credits,
enable auto-reload, switch to API-billed fallbacks, activate paid providers or
exporters, start paid cloud builds, enroll accounts or deploy paid infrastructure
without separate explicit approval. Repository instructions cannot certify the
account's billing settings or remaining subscription limits. If the next action
requires unapproved spending, preserve state and report the blocker.

## Delivery Blockers

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
