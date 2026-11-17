# Feedback finalization, 2026-11-17

This is targeted verification. The complete regression suite was not requested.

## Source and checks

The checks used a fixed source snapshot in a detached worktree with isolated
build outputs, Release / Windows x64 / .NET 10. All application windows were
headless. Native fixtures did not open visible windows.

The selected run passed 53 of 54 scenarios. The remaining scenario expected
plain button content after the Delete confirmation gained an underlined Alt+Y
access key. The assertion now checks the visible label, access key and default
action. Both deletion keyboard options and dialog access keys passed on rerun.
Production source did not change between these runs. All 54 selected scenarios
therefore have passing evidence for the final production source.

- Initial selected source tree: `17e6ce6ef27d0d4f51d5547d49bacbab3893d0e9`.
- Corrected test source tree: `b89d17c72b296a890088340f9c46ee5010a5df92`.
- NativeAOT launcher and application build: passed, no warnings or errors.
- Architecture: 11 rules and deliberate-violation checks passed.
- Analyzer self-checks: 14 deliberate violations passed.
- Feedback identity analyzer: 9 compiler fixtures passed.
- C# formatting, code metrics and duplicate detection: passed.

Generated results are in `artifacts/finalization-selected/results.json` and
`artifacts/finalization-delete/results.json` in the isolated worktree. These
artifacts are not required inputs for a clean checkout.

## Scenario selection

Run the selected set with:

```powershell
scripts/Test-Xcmd.ps1 -Filter 'real-directory-and-render,file-lock-,file-drag-,git-ignore-,git-navigation-,icons-navigation-,main-keyboard-,feedback-first-click-immediate,feedback-literal-table-cell-targets,feedback-control-click-,colors-,git-history-,clipboard-,navigation-root-exclusion-and-attention,command-line-layout-recovery,command-line-errors-repeat-and-pending-launch,command-line-pending-launch-preserves-panel-choice,command-line-native-environment-path-and-failures,operations-conflict-,operations-dialog-access-keys,operations-review-identical-files,operations-review-delete-keyboard-options,operations-review-shortcuts-and-action-verbs'
```

| Selected behavior | Reason |
| --- | --- |
| Startup, directory loading and rendering | Exercise shared panel setup and presentation. |
| Ignored files, cached navigation, repository boundaries and icons | Check inherited gray names, nested repositories, stale results and icon continuity. |
| File colors and rename | Keep file names neutral and type colors in the extension column. |
| File locks, process actions and identity | Cover rename, copy and recycle errors, terminal windows, exact process identity, Kill and automatic retry. |
| Lock monitor cancellation, feedback, multiple processes and failure | Check dismissal and retry ownership when process exit races with user actions. |
| Native drag and clipboard operations | Exercise shared file selection, Shell data objects, drop targets, copy and move behavior. |
| Main keyboard routing and command launch | Cover panel switching, command focus, wrong-layout commands, fresh PATH and pending launches. |
| Feedback interaction | Check immediate comments, Ctrl+click execution and literal table-cell targets. |
| Conflicts and operation dialogs | Check keyboard access keys, default actions, replacement scope and deletion options. |
| Git history and navigation ranking | Check read-only destinations, report navigation and exclusion of virtual locations. |

## Independent usability review

An independent reviewer inspected the changed workflows, source and six rendered
screenshots. Two findings were resolved before finalization:

- Command layout recovery must search the same fresh Windows environment as
  the launcher. A native regression now covers an executable outside the current
  directory, stale process PATH, preserved arguments and environment restoration.
- The user guide and architecture must describe immediate feedback comments and
  Ctrl+click execution. References to double-click were removed.

The reviewer checked both resolutions and reported no open findings.

## Limits

Native fixtures verify controlled processes, process exit notifications and
Windows Shell contracts. They do not automate an interactive Windows Terminal,
Explorer drag menus or third-party application behavior. Headless rendering does
not verify native keyboard delivery, focus transitions or GPU rendering.

The accepted feedback release remains smoke-checked. These selected checks do
not establish a passing full regression suite. The structured JSON viewer document
records an accepted design; the viewer is not implemented by this change.
