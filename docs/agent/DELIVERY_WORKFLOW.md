# Autonomous Delivery Workflow

## State recovery

Every run starts by reading current state. Never infer state from a previous run.

Read:

- current `main` SHA;
- open PRs with `codex/` branches;
- Jira issues in progress and their hierarchy;
- CI for the current PR head;
- inline threads, review submissions, conversation comments, labels, and mergeability;
- existing Jira and Confluence evidence.

If multiple active agent PRs or conflicting active Jira leaf issues exist, stop and report the conflict.

## State A: open agent PR exists

Do not start another issue.

1. Process all review channels.
2. Fix every valid finding with the smallest correct change.
3. Add regression coverage where practical.
4. Push and wait for CI on the updated head.
5. Reply in the original thread with commit, test, and CI evidence.
6. Resolve only after green CI.
7. Repeat until no actionable comments remain.
8. Run the merge gate from `AGENTS.md`.
9. Merge using the repository-approved method.
10. Re-fetch the PR and verify `merged = true`.
11. Record actual merge commit, merged timestamp, final head, CI, and review state.
12. Update Jira and Confluence.
13. Transition the leaf issue to Done and evaluate its parent.

A `COMMENTED` review is not automatically blocking; inspect its findings. A `CHANGES_REQUESTED` review is blocking until resolved or dismissed by an authorized reviewer.

## State B: no agent PR exists

1. Query the active hierarchy and select the next unfinished leaf by Jira rank.
2. Read description, Definition of Done, dependencies, parent, and children.
3. Search for duplicate branches and PRs.
4. Transition the leaf to In Progress.
5. Create `codex/fin-<number>-<short-description>` from current `main`.
6. Implement only that Jira scope.
7. Add tests, documentation, edge cases, and ownership notes.
8. Run local verification.
9. Review the diff for secrets, generated output, unrelated changes, and architecture violations.
10. Open a non-draft PR and record implementation evidence in Jira/Confluence.
11. Continue using State A.

## Idempotency

Before every mutation, perform a fresh read:

- before branch creation, search branches;
- before PR creation, search PRs;
- before Jira transition, read current status;
- before evidence insertion, inspect existing evidence;
- before merge, re-fetch head, CI, reviews, threads, comments, labels, and mergeability.

Repeated executions must resume safely and must not duplicate branches, PRs, Jira comments, or Confluence evidence.

## Runtime locking

Desktop automation must use a local lock under `.codex-runtime/delivery.lock`.

- create the lock atomically;
- record process/run identifier and timestamp;
- exit without mutation when a fresh lock exists;
- recover only stale locks after confirming no active process owns them;
- release in a `finally` block.

The lock directory is local-only and must be ignored by Git.

## Stable stopping points

A run may stop only at a safe state:

- PR open with external CI/review pending;
- PR updated and waiting for a newly triggered check;
- documented blocker;
- no unfinished Jira work.

Always report active issue, branch, PR, head, CI state, review state, merge state, and next expected transition.
