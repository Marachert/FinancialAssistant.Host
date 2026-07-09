---
name: financial-assistant-delivery
description: Resume and execute the Financial Assistant Jira-to-GitHub delivery loop, including review processing, guarded merge, evidence, and next leaf selection.
---

# Financial Assistant Delivery Skill

Read root `AGENTS.md` and every required document before acting.

## One restart-safe iteration

1. Acquire `.codex-runtime/delivery.lock`.
2. Restore state from GitHub, Jira, Confluence, CI, and repository contents.
3. If an open `codex/` PR exists, process it before selecting work.
4. Otherwise select the next unfinished Jira leaf-ticket by rank.
5. Continue until a safe stopping state, blocker, or no work remains.
6. Release the lock in `finally`.

## Open PR flow

Fetch inline threads, review submissions, conversation comments, labels, current head, required checks, and mergeability.

For each valid finding:

- mark useful feedback positively;
- implement the smallest correct fix;
- add regression coverage;
- push;
- wait for CI on the new head;
- reply in the original thread with fixing commit, tests, and CI run;
- resolve only after green CI.

Repeat and perform a final recheck. Run every merge-gate condition from `AGENTS.md`. A `CHANGES_REQUESTED` review is blocking until it is resolved or dismissed by an authorized reviewer.

When the gate passes:

- merge using the repository-approved method;
- re-fetch the PR and verify `merged = true`;
- record actual merge commit, `merged_at`, final head, final CI, and review state;
- update Jira and Confluence;
- transition the leaf to Done;
- evaluate and close eligible parents;
- refresh `main` before selecting the next issue.

## New work flow

When no active agent PR exists:

- query Jira hierarchy and rank;
- select a leaf only;
- inspect dependencies and Definition of Done;
- search duplicate branch/PR;
- transition the issue to In Progress;
- branch from current `main`;
- implement only that issue;
- test, document, and inspect the diff;
- open a non-draft PR with complete evidence;
- continue with the open PR flow.

## Required reporting

Always report active issue, branch, PR, head SHA, CI, review state, merge state, and next expected transition.
