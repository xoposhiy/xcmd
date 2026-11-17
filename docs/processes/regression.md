# Requested regression work

Run the full suite locally only when the user requests it. An explicit request for
ongoing monitoring authorizes repeated runs until the user stops it. Ordinary
feature commits and releases use the targeted [verification process](verification.md).

Test the requested snapshot in a separate copy or detached worktree, with isolated
outputs and fixtures. Do not create a branch or disturb active development. Record
the tested commit, results and useful diagnostics. A source change requires fresh
evidence for affected checks.

When monitoring and repairs are requested, fix detected failures while preserving
the tests and quality limits. Run the relevant scenarios and static checks before
committing a repair. Repeat the complete suite only within the full-regression
request. Report environment failures or product decisions that prevent progress.

Prepare a repair commit in the detached worktree. Fast-forward `main` to it only
when the development checkout is clean and `main` still points to the tested base.
This verified integration needs no separate approval. If `main` advances, apply
and verify the repair against its new tip. If active edits prevent integration,
retain the repair until integration is safe. Never overwrite drafts, force-update
`main` or rewrite its history. Push only when requested.
