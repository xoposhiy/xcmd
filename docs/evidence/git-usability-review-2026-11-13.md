# Git interface usability review

## Repair status — 2026-11-14

All U01–U11 findings and the four proposed usability improvements are implemented
in feedback release `20261114-011636-600-cb8da35f`, following the user's approval
to fix the whole review. The original findings below document the earlier state.

| Finding | Resolution |
| --- | --- |
| U01 | Every Git entry saves the physical return state, including Ctrl+Left/Right. |
| U02 | Typed viewer documents preserve origin and read-only state through menu and keyboard actions. |
| U03 | Snapshot file modification dates are blank when unavailable. |
| U04 | F3 on `..` opens actionable changed files, including deleted-file viewing and basic diff. |
| U05 | Copy feedback states the Git limitation and no longer recommends unavailable archive commands. |
| U06 | Empty path history and repositories with no commits have specific explanations. |
| U07 | Working tree panels and reports share captured changes; background and manual refresh are visible and new HEAD refreshes the log. |
| U08 | Log sort commands cannot change the next file sort; unsupported menu items are disabled. |
| U09 | Snapshot legends explain first-parent changes. |
| U10 | Git footers and shortcut help match the current mode. |
| U11 | Monospace short IDs fit their column and headers share the cell inset. |

Additional improvements: the help explains the full-repository snapshot transition;
Git commands show progress, replace older requests and support Escape cancellation;
the message divider resizes both areas with useful minimum heights; the commit
action explicitly names the repository and Claude Code. Expected errors stay
visible. Cancellation works with command-line and message focus. Staged deletions
also compare correctly with HEAD when the path is absent from the index.

Validation: **10 targeted headless scenarios passed**, zero failures, zero native
application windows, package audit passed, verification time 54.15 seconds. This
is feedback-smoke evidence, not a full regression gate. An independent re-review
of the current source, results and 100/150% renders found no remaining application
blockers for these review findings.

- [Release evidence](../../releases/20261114-011636-600-cb8da35f/verification.json).
- [Repaired walkthrough observations](../../artifacts/git-usability/fixed-2026-11-14/observations.json).
- [Actionable changed-file list](../../artifacts/git-usability/fixed-2026-11-14/changed-files.png).
- [Deleted-file diff](../../artifacts/git-usability/fixed-2026-11-14/deleted-file-diff.png).
- Regression cases: `git-history-*`, `git-usability-*`, `git-commit-menu`, and `git-rows-file-states`.

## Original review

Reviewed release: `20261113-231713-682-058d775b` and the matching application source.

This review covers Git status, scoped history, Working tree, commit snapshots,
commit messages, changed-file reports, basic diff, navigation and external opening.
No production behavior or release was changed during the original review. Repairs
were separately authorized and completed as recorded above.

## Method and evidence

An independent reviewer inspected the workflows, source and existing 100/150%
headless renders without receiving the implementing agent's conclusions. A second
pass drove the real Avalonia controls with temporary repositories. The walkthrough
checked return navigation, viewer actions, copy feedback, dates, empty history,
sorting and Working tree refresh behavior.

The walkthrough used Avalonia.Headless. It recorded editor requests through the
existing recording launcher; it did not open an external editor or edit a cached
file. It did not run the full regression suite. This is an expert review, not a
study with new users or a measured command-latency benchmark.

Local evidence:

- [Recorded observations](../../artifacts/git-usability/evidence-2/observations.json).
- [Cross-panel return state](../../artifacts/git-usability/evidence-2/cross-panel-escape.png).
- [Historical-file viewer](../../artifacts/git-usability/evidence-2/snapshot-viewer.png).
- [Empty history](../../artifacts/git-usability/evidence-2/empty-history.png).
- [Audit driver](../../tests/Xcmd.Probe/Infrastructure/GitUsabilityAudit.cs).
- Existing log and snapshot renders under `artifacts/probe/feedback-20261113-231713-682-058d775b`.

Severity: P1 breaks an expected navigation path. P2 can mislead the user or block
a useful task. P3 adds friction or unclear presentation. Source-only findings and
unmeasured risks are identified below.

## Preserve these decisions

- Keep the log's Commit, Subject, Author and Date columns separate from file columns.
- Keep the full repository tree and normal file interaction inside a commit.
- Keep the full commit message visible during snapshot navigation.
- Keep Working tree filtered to the selected file or directory.
- Keep staged and working changes separate.
- Keep first-parent comparisons, read-only snapshots and no checkout.
- Keep keyboard navigation and restore the original panel when leaving history.
- Do not add back the removed Back, Diff and Close log button row.

## Findings

### U01 — P1: Ctrl+Left/Right can break the promised Escape return

**Evidence: reproduced in the headless walkthrough.** Open history in the right
panel, enter a commit, select a folder, press Ctrl+Left, switch to the left panel
and press Escape. The left panel stays in Git instead of returning to its original
physical folder. Both panels can now be Git panels.

`OpenInPanel` calls `LoadAsync` directly. This does not save `gitReturnState`.
`ExitHistoryAsync` then has no return state and does nothing. This does not delete
files, but it breaks a familiar navigation command and the displayed exit hint.

**Minimum correction:** route every entry into a Git location through the same
return-state workflow. Until that exists, reject this shortcut with a clear reason.

Sources: `CommanderWindow.Keyboard.cs:183`, `PanelView.History.cs:10`,
`PanelView.History.cs:19`. Observation: `crossPanelEscape`.

### U02 — P2: a historical-file viewer loses origin and read-only meaning

**Evidence: reproduced; independently confirmed.** F3 on a snapshot file opens a
temporary copy. The viewer title and path identify that cache file, without the
original repository path or commit. F4 in this viewer sends an editor request for
the temporary copy, with no warning or error. F4 on the same file in the panel is
rejected as read-only.

The audit confirms the launch request, not an actual external edit. Editing this
copy would not change the repository's working file. The user could spend time
editing a file whose relation to the repository is unclear.

**Minimum correction:** carry source path, commit ID and read-only state into the
viewer. Show a title such as `src/file.cs · abc12345 · read-only`. Disable Edit for
historical content, or make a separate temporary-copy action explicit. Apply the
same meaning to Open externally. Text selection and copying should remain available.

Sources: `FileViewingController.cs:37`, `FileViewingController.cs:47`,
`FileViewingController.cs:73`, `FileViewerWindow.Loading.cs:207`.
Observations: `snapshotViewerTitle`, `snapshotViewerEdit`.

### U03 — P2: Modified shows a commit timestamp as every file's modification time

**Evidence: source and walkthrough.** A snapshot assigns the selected commit's date
to all tree entries, including unchanged files. The normal column still says
Modified. The audit captured the same date for changed and unchanged files.

This makes the table look more precise than the available data. A Git tree does
not provide the original filesystem modification time for each file.

**Minimum correction:** show an unknown value for unavailable file times, or label
the value explicitly as the snapshot's commit date. Do not add a Git process for
every file merely to fill this column.

Source: `GitFileSystem.cs:86`. Observation: `snapshotDates`.

### U04 — P2: deleted commit files have no usable path to the basic diff command

**Evidence: source and existing changed-file scenarios; no separate new runtime
case for this finding.** Deleted files correctly do not exist in the full tree.
F3 on `..` lists them in a plain text report. Diff requires a selected file row,
so a deleted path in that report cannot be selected and compared through the same
workflow as an added or modified file.

**Suggested correction requiring a product decision:** keep the full tree as the
default and make changed-file report entries actionable, or provide a navigable
changed-file view using the existing F3/menu route. Deleted rows can then offer
the existing basic diff. This does not require the deferred full diff viewer.

Sources: `GitHistoryController.cs:74`, `GitHistoryController.cs:98`,
`GitHistoryDocuments.cs:30`, `GitFileSystem.cs:85`.

### U05 — P2: Copy feedback sends the user to an unavailable command

**Evidence: reproduced; independently confirmed.** Ctrl+C on a snapshot file says
to use F5 to copy files out of an archive. F5 then says Git history is read-only
and refuses the action. The file is not an archive, and following the instruction
cannot complete the task.

**Minimum correction:** detect Git before archive handling and explain the actual
limitation. Do not recommend F5 until snapshot export is supported. Align Copy
menu availability and keyboard feedback. Export support is a separate decision.

Sources: `PanelClipboardController.cs:41`, `ArchivePath.cs:5`,
`GitHistoryController.cs:146`. Observations: `snapshotCopyMessage`,
`suggestedCopyCommandMessage`.

### U06 — P2: an empty history asks the user to select a missing commit

**Evidence: reproduced for an ignored directory with no matching history.** The
panel contains only `..` and says `Select a commit to read its full message.`
There is no distinction between no matching commits, a repository with no commits
yet and loading failure.

**Minimum correction:** give a result-specific empty state: `No commits changed
this path.` or `This repository has no commits yet.` Keep failures separate and
keep the ordinary parent/exit navigation available.

Sources: `GitFileSystem.cs:92`, `GitCommitMessageView.cs:69`.
Observation: `emptyHistory`.

### U07 — P2: Working tree can show two different current file lists

**Evidence: reproduced.** Open Working tree, then create another untracked file
inside its scope. F3 on `..` reads fresh changes and lists the new file, while the
panel still contains the earlier rows. Neither view tells the user that they have
different capture times. Git locations are excluded from normal directory monitoring.

**Minimum correction:** use one visible refresh rule for the Working tree panel
and its report. At minimum, show that the list is a captured state and make
`Ctrl+R refreshes changes` discoverable. Decide separately whether it should refresh
automatically; committed snapshots should remain immutable.

Sources: `DirectoryUpdates.cs:35`, `GitHistoryController.cs:76`,
`GitFileSystem.cs:112`. Observation: `workingTreeFreshness`.

### U08 — P3: log sorting commands change an invisible future file sort

**Evidence: reproduced.** Ctrl+F6 leaves the commit order unchanged but sets the
panel's file sort to Size. Entering a commit then applies that sort to its files.
The user sees no result when requesting the sort and gets an unrelated result later.

**Minimum correction:** keep log order chronological and make unsupported sorting
commands unavailable there. Preserve file-sort preferences independently. New commit
sorting options are not required to correct this inconsistency.

Sources: `PanelSession.cs:71`, `CommanderWindow.Keyboard.cs:220`.
Observation: `logSort`.

### U09 — P3: the snapshot Git-column legend describes the working tree

**Evidence: captured from the actual header tooltip.** The snapshot's Git column
says `Left: staged · Right: working tree`. Its right-hand symbols describe the
selected commit against its first parent. Individual row tooltips already say Commit.

**Minimum correction:** use a snapshot legend such as `Changes in this commit
compared with its first parent`. Retain the staged/working legend in live folders
and Working tree lists.

Sources: `PanelView.Git.cs:32`, `GitRowStatus.cs:29`.
Observation: `gitColumnLegend`.

### U10 — P3: log status and shared help still describe files

**Evidence: walkthrough and renders.** Selecting a commit produces
`1 selected · ≥ 0 bytes (incomplete)`. A commit is represented internally as a
directory, but this is not useful information for the person reading the log.
The shared shortcut strip also continues to advertise F4 Edit and F5 Copy where
those actions are unavailable.

**Minimum correction:** show commit count and current context in the log footer,
without sizes or directory-measurement warnings. Make the shortcut help reflect
the active Git mode and keep F3, Enter, Backspace, Escape and diff discoverable.

Sources: `PanelView.cs:646`, `CommanderFrame.cs:86`.
Observation: `logFooter`.

### U11 — P3: log hash width and header spacing reduce scan clarity

**Evidence: existing 100/150% renders.** Some ten-character IDs are shortened with
an ellipsis in the Commit column. Header labels and cell values also have different
left insets. The full ID remains available in the message context and tooltip, so
this is friction rather than blocked access.

**Minimum correction:** use a consistent monospace short-ID width or a shorter
fixed displayed ID, with the full ID in the tooltip. Match header and value insets.
Retain the current simple four-column log.

Source: `GitLogPresentation.cs:29`, `GitLogPresentation.cs:51`.

## Questions and improvements that need a decision

These are review hypotheses, not reproduced defects or approved feature work.

1. **Make scope changes explicit.** A file/folder-filtered history intentionally
   opens the full repository snapshot. A short hint such as `Enter: full repository
   snapshot` can explain that transition. Reports should state whether their changed
   files cover the whole commit or only the current snapshot folder.
2. **Show long-operation state.** Log, report and diff opening use a lifetime token
   without per-command replacement or visible progress. On a slow repository, repeat
   requests may finish late and change focus. Command latency and this race were not
   measured. Add working feedback and a clear repeated-command rule before adding
   new navigation controls.
3. **Let the message/list divider move.** The fixed area works, but a long message
   below a short log leaves unused list space. A draggable divider can improve this
   without adding buttons. Keep a minimum useful height for both areas.
4. **Explain the AI commit action where it is chosen.** Its tooltip already says
   that Claude Code handles the whole repository. A clearer menu caption or short
   description can make that scope visible from a subfolder. No extra confirmation
   dialog is recommended.

## Suggested order

1. Correct the cross-panel return state (U01).
2. Preserve source identity and read-only meaning across viewer actions (U02).
3. Correct false dates, copy instructions and empty states (U03, U05, U06).
4. Agree on actionable changed files and one Working tree refresh rule (U04, U07).
5. Remove inherited file semantics from log sorting, legends and status (U08–U10).
6. Finish spacing and ID readability (U11), then evaluate the optional divider.

Do not treat missing branch selection, rename-following history, staging, pull,
push, submodule browsing or a full diff viewer as defects in this agreed prototype.
Those features remain explicitly deferred.
