# File comparison finalization, 2026-11-17

This is targeted verification. The complete regression suite was not requested.

## Source and checks

Checks ran in a detached worktree with fixed inputs and isolated build outputs,
using Release / Windows x64 / .NET SDK 10.0.102. All application windows were
headless. Native launch fixtures used a windowless executable.

The selected run passed 17 of 18 scenarios. One Git history assertion still
expected Ctrl+Shift+C to copy a path. The assertion now checks the comparison
error, unchanged clipboard, and absence of an external launch for a Git snapshot.
The corrected scenario passed on rerun. Production source did not change between
these runs, so all 18 selected scenarios have passing evidence.

- Selected source tree: `da42c4285af5ca75231d8a1c3d46f156830bdcb1`.
- Corrected test source tree: `f8a60fc2b9466e97b2a2644819ff18a84a90aee2`.
- NativeAOT launcher and application builds: passed without warnings or errors.
- Architecture: 11 rules and deliberate-violation checks passed.
- Analyzer self-checks: 14 deliberate violations passed.
- Feedback identity analyzer: 9 compiler fixtures passed.
- C# formatting, code metrics, and duplicate detection: passed.
- The corrected test file was rebuilt and checked for formatting before its rerun.

Results are in `artifacts/comparison-final/results.json` and
`artifacts/comparison-git-final/results.json` in the detached worktree. These
generated artifacts are not required inputs for a clean checkout.

## Scenario selection

```powershell
scripts/Test-Xcmd.ps1 -Filter 'real-directory-and-render,comparison-,clipboard-copy-files-and-folder-to-panel,clipboard-path-and-selected-command-argument,viewer-text-search-edit-return,viewer-native-editor-boundary,command-line-copy-selection-or-all,command-line-insert-name-path-and-selection,command-line-errors-repeat-and-pending-launch,command-line-native-quoted-exe-and-raw-arguments,command-line-compact-scaled-layout,git-history-file-and-diff,git-history-commit-message'
```

The selection covers comparison from the keyboard and menu, cursor order despite
marks, search-result paths, invalid targets, launch failures, held keys, and text
input. Shared clipboard, command-line, viewer, and Git scenarios cover the changed
shortcut and event wiring. Native launch checks verify argument boundaries,
working directories, spaces, Unicode, and shell metacharacters. Offscreen checks
cover startup and the command line and Git message at normal and 150% scale.

## Independent usability review

The reviewer inspected the workflow, errors, menu, shortcut hints, source, and
smoke evidence. Finalization moved existing event subscriptions into methods to
keep the constructor within the code metrics limit. The reviewer found that panel
activation must stay subscribed before shortcut hint updates. The final code keeps
that order, and `git-history-commit-message` checks switching from a file panel to
the opposite Git message and updating the hints. The reviewer confirmed the fix
and reported no open findings.

## Limits

The native fixture verifies Xcmd's Windows launch contract. It does not verify the
VS Code interface, diff algorithm, or startup time. Headless tests do not verify
native keyboard delivery or desktop focus. Binary diff, directory comparison,
archive members, and Git snapshot comparison in VS Code remain outside this feature.
