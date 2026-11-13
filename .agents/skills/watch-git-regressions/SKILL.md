---
name: watch-git-regressions
description: Monitor local Git commits from a separate agent session, run the full test suite in isolated worktrees, and fix and commit verified repairs. Use when the user requests ongoing post-commit regression monitoring, not for ordinary feature verification or a one-time test run.
---

# Watch Git regressions

Run this workflow in a separate agent session. A skill describes the workflow;
monitoring lasts while its executing session remains active. Creating or editing
this skill does not start a monitor.

## Start

Use the requested repository and watch its local `main` branch. This workflow
uses only `main`; do not create feature, repair or temporary named branches.
Use an independent repository copy or a detached worktree for isolation.
Read the repository instructions and find its complete verification command.
For Xcmd, read `docs/development-workflow.md` and use `scripts/Test-Xcmd.ps1`
without a filter. Keep the repository's build profile and headless requirements.

Create a monitoring directory outside the development checkout. Keep a durable
record of the watched repository/ref, observed commit IDs, verified commit IDs,
results, log paths, pending repairs and repair commits. Use a lock so only one
monitor handles this repository/ref at a time. Do not share build outputs with
the development checkout or another worker.

Verify the current branch tip once unless matching full-suite evidence already
exists. Then watch for new commits. Poll with bounded waits of about 30 seconds;
use a waiting tool when available. Continue until the user stops monitoring.
Do not report that monitoring is running after ending the executing session.

## Check committed snapshots

Resolve each observed branch tip to a full commit ID. Process new commits in
ancestry order. If the branch history is replaced or no longer contains the
previous cursor, record the change and verify the new tip instead of assuming
the old result applies. Keep watching the configured local ref; fetch only when
the user requests remote monitoring.

Create a detached worktree at the exact commit being checked, for example with
`git worktree add --detach <new-directory> <commit-id>`.
Use a new directory and logs for each run. Do not reset, clean, stash or switch
the user's checkout. Run the full verification command, including required
native integrations and static checks. On Windows, start console helpers hidden
and redirect their output, as required by the repository.

Record a passing result against that exact commit ID. If the watched branch
advances during verification, finish the current result and queue the new
commits; never label a newer commit as checked by an older run.

## Repair failures

The monitoring request authorizes local regression fixes and repair commits.
Diagnose failures in the isolated worktree. Read the relevant feature contract
and use focused tests while fixing the cause. Preserve intended behavior and
test coverage; do not weaken checks, remove assertions or replace required real
integrations with mocks to obtain a pass.

Distinguish product failures from missing services, permissions and tools.
Record an environment failure with diagnostics. Do not retry the same unchanged
environment indefinitely. Stop a repair that needs a product decision and
report the decision needed. After three unsuccessful repair attempts for one
commit, retain the worktree and report the unresolved failure; continue watching
later commits without claiming the failed commit passed.

After focused checks pass, run the complete gate on the repaired snapshot.
Commit only the verified repair and its relevant tests/documentation at detached
HEAD, following the repository's commit convention. Record the tested base,
repair commit and passing evidence. Keep generated logs out of the commit unless
the repository requires them. Do not create an empty commit for a passing check.

## Publish into main

The user's monitoring request authorizes putting verified repairs into `main`
without another approval. The detached worktree is only an isolated preparation
area; the delivered repair belongs to `main`, not to another branch.

Before integration, check the current `main` tip and the development checkout's
staged, unstaged and untracked changes. When the checkout is clean and `main`
still points to the tested base, fast-forward it to the verified repair commit
using Git's normal checkout and index protections. Do not update a checked-out
branch with a raw ref write. If another Git writer wins, recheck and retry;
never force the update.

If `main` has advanced, inspect whether the latest version still needs the fix.
When needed, apply the repair to a fresh detached snapshot of the new tip and
verify it again before publishing. Do not replay a stale repair blindly. If
uncommitted work prevents integration, retain the pending repair and retry when
the checkout is clean. Do not stash, overwrite or commit the user's draft.

Record success only after the repair is on `main`. Track already verified repair
commits so observing the monitor's own commits does not create a repair loop.
Do not rewrite `main`, create another branch or push unless separately requested.
On stop, release the monitor lock and keep useful diagnostics and worktrees
that still hold pending repairs.
