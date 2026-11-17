# Feedback verification, 2026-11-15

Verification used a detached worktree based on `6e36e6b` with the feedback patch.
Build outputs and fixtures were isolated from the active checkout. File hashes
confirmed that all 22 changed source and test files matched the checked snapshot.

All 39 selected scenarios passed. The first run passed 38 scenarios. The remote
icon assertion assumed that the Git header had only one vector icon. After the
arrow became a vector icon, the assertion was updated to find the remote cloud
by its stable feedback ID. The focused retry passed. No production changes were
needed for that failure.

Architecture rules, analyzer self-checks, feedback identity compiler fixtures,
C# formatting, code metrics and duplicate checks passed after the last change.
The [independent usability review](feedback-usability-review-2026-11-15.md) found
no remaining blockers. Offscreen evidence includes 100% and 150% chooser and Git
header layouts. Native checks cover the Windows Recycle Bin and real junctions.

## Selected scenarios

The selection covers directory input and saved paths, shared list styling,
Git input races and headers, permanent deletion, and the shared viewer and file
adapters changed by this batch.

```text
history-keyboard-restores-cursor
history-empty-and-refresh
history-missing-directory-and-retry
history-failed-and-obsolete-reads-excluded
history-cancel-pending-navigation
history-pending-navigation-completes-once
favorites-add-rename-and-remove
favorites-shared-navigation-and-cursor
favorites-missing-directory-and-retry
favorites-cancel-pending-navigation
navigation-shared-window-and-shortcuts
navigation-tab-and-reverse-tab
navigation-two-column-rendering
navigation-pinned-favorites-and-type-search
navigation-search-active-column-and-keyboard-layout
rules-navigation-shortcuts-and-editor-priority
operations-move-and-copy-delete-fallback
operations-delete-confirmation-and-permanent-tree
operations-native-recycle-retains-contents
operations-failure-continues-batch
operations-junction-cycle-and-safe-delete
feedback-navigation-labels-and-disabled-actions
viewer-continuous-scroll-end-and-status
design-system-shared-surfaces
git-panel-status
git-remote-colours
git-commit-menu
git-history-commit-message
git-history-folder
git-usability-return-and-viewer
git-usability-empty-and-refresh
git-usability-cancel-and-deletion
accumulated-cd-home-and-path-display
feedback-navigation-command-focus
feedback-navigation-pages-and-colors
feedback-navigation-missing-parent
feedback-git-viewer-cursor-and-paths
feedback-delete-readonly-failure
feedback-delete-readonly-git-objects
```

Run these IDs as comma-separated filters with `scripts/Test-Xcmd.ps1 -Filter`.
Local results are under
`artifacts/feedback-finalization-20261115/artifacts/probe/feedback-finalization`
and `feedback-finalization-remote`.

## Ready build and limits

Release `20261115-163429-543-13b19c53` passed seven additional smoke scenarios in
40.1 seconds before publication. Its package audit passed. The release uses the
permanent `Xcmd.exe` launcher and an immutable release directory.

This is targeted verification. The full regression suite was not requested.
All application windows used headless handles. These checks do not prove native
Windows keyboard delivery, desktop focus or external application appearance.
