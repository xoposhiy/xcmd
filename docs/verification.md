# Verification coverage

`git-usability-return-and-viewer`, `git-usability-empty-and-refresh` and
`git-usability-cancel-and-deletion` cover the repaired Git review findings:
cross-panel return state, viewer origin and read-only menus/shortcuts, unknown file
dates, contextual legends/help/footer, explicit empty states, coherent report and
panel refresh, background live refresh and new HEAD, command-line-focus cancellation,
visible expected errors and staged-deletion diff. Existing `git-history-*` cases
also cover actionable deleted-commit paths and mouse resizing of the message area.
These are targeted feedback checks, not a full regression result.

`git-history-commit-message` checks the inline full message below the Git log,
paragraphs and Unicode, scrolling and text selection, keyboard focus, rapid cursor
changes, cached messages, clearing log navigation rows, commit columns without
file icons, file-column restoration in snapshots, and keeping the message visible
across snapshot folder and file selection. Ordinary folders restore the full
viewport. It renders long log and snapshot messages without native windows,
including the log at 100/150%.

`git-history-folder`, `git-history-file-and-diff`, `git-history-working-tree` and `git-history-root-and-empty`
exercise the [Git history prototype](git-history.md) with real temporary repositories.
They cover file and folder filters, adjacent-panel focus and restoration, F3 commit
details, changed files, repository snapshots, Unicode names, renames, deletions,
initial commits, literal path filters, empty repositories and basic diffs against
HEAD or the first parent. The working-tree scenario checks scoped directory changes
above filtered commits, staged and working statuses, untracked files, ignored-file
exclusion, F3, Enter, file diff, deleted-file viewing and removal of the entry when
only other folders have changes. The file-history scenario checks single-file
Working tree filtering. The folder scenario uses the production virtual filesystem
and the same file table for full snapshots, unchanged files, Git cells, parent-row
F3, hover behavior, keyboard sorting and cursor restoration. It renders the history and snapshot
controls offscreen at 100/150%. This is feedback smoke coverage; history paging,
merge-specific integration and full finalization remain outside this smoke set.

`history-parent-selects-exited-folder` opens a folder through History while its
parent remembers a different cursor, then checks Ctrl+PageUp, Backspace and Enter
on `..`. Each parent command selects the exited folder, so Enter returns to the
same location. `rules-parent-cursor-name` covers ordinary folders, archive roots
and nested archive sources. Existing parent and archive scenarios cover the
surrounding navigation paths.

`git-rows-*` uses isolated real repositories and commits to cover staged and
working-tree changes on the same file, additions, untracked paths, rename paths
with spaces and Unicode, deletion-only folders, real merge conflicts, ignored
paths, descendants of collapsed untracked directories, unchanged native folder
icons before and after commit, old column-state migration and narrow renders at 100/150%.

`git-rows-child-repositories` checks a real non-Git parent, clean and modified
child repositories, independent staged and working-tree states, ordinary-folder
empty cells, navigation cleanup and 100/150% renders. `git-rows-child-worktrees`
checks a real linked worktree with a `.git` file and a detached HEAD.
`git-child-background-navigation` delays child results to check that the current
header appears first and navigation discards obsolete child statuses.

`operations-delete-large-tree` deletes 1,200 real small files through the actual
queue with its details visible, records elapsed time and render intervals, and
checks that the tree disappears while neighbouring files remain.
`operations-delete-large-tree-cancel` clicks Cancel current after ten real file
deletions and checks that all 1,190 unstarted files remain. Existing
deletion, junction, cancellation, queue, conflict and native Recycle Bin scenarios
cover shared operation behavior. Full regression checks run only on explicit
user request.

`rename-printable-key-input` checks the actual routed key-down result before
delivering text, then commits a name with letters and a space to the filesystem.
Vertical editor keys keep the file cursor in place. Command-line checks cover
focus returning after successful launch and `cd`, correction after failure, and
preservation of a newer draft or a panel chosen during a pending launch.
`operations-shift-f8-permanent-deletion` checks all four deletion shortcuts,
their confirmation defaults and cancellation, then confirms permanent deletion
of an isolated fixture tree through Shift+F8.

`accumulated-cd-home-and-path-display` drives `cd ~`, both home-subfolder separator
forms, quoted names, lowercase displayed drive letters and unchanged UNC paths.
`accumulated-startup-age` checks the explicit English `Uptime: 9 min.` title text and its
space in a narrow window. Existing command-line navigation, layout and workspace
handoff scenarios cover the surrounding workflow.

The 2026-11-13 folder feedback iteration checks exact totals excluding link
entries and preservation of unaffected sizes during live refresh. Repository
folder recolouring was removed at the user's request; Git column cells retain
file and descendant status indicators.
The full finalization run passes `folder-size-native-junction-cycle`, including
exact totals, refresh retention and zero-byte junction measurements. The
`folder-size-excluded-links` scenario also checks supplied link entries.
Measurement retention accounts for cached Windows directory timestamps while
later changes still invalidate stale totals.

The `archive-other-panel-*` feedback cases drive actual Ctrl+Left/Right in both directions. Real ZIP and nested-ZIP contents open in the destination while source directories, search results and keyboard focus remain intact. Broken archives and cancelled reads retain the previous destination; Escape preserves the command draft and late results cannot replace the panel. Existing result-location scenarios retain coverage for ordinary files.

The `zip-*` feedback scenarios check the actual native menu's ZIP label alongside Windows verbs; marked-group and single-item routing; keyboard invocation; name validation, collision and cancel; real ZIP contents, selected folders, empty directories and Unicode paths; queue progress counts and preserved source marks; atomic cleanup after a locked-source error, Retry, and cancellation during the production writer's progress callback. The Create ZIP dialog is rendered and checked at 100% and 150%. Native menu discovery uses the production Windows shell objects without displaying a menu; interactive third-party native menus retain the limits below.

The `clipboard-*` feedback scenarios cover actual panel Ctrl+C/X/V, Windows file/effect data through Avalonia's headless clipboard, marked files and folder trees, Unicode paths, insertion into the displayed folder with a different folder highlighted, cut dimming across both panels and reload, move conflicts and retries, safe same-folder copy names, captured destinations, pending-cut repeat protection, preservation of newer clipboard data, direct full-path export and immediate copying of selected command arguments. Real fixture contents and resulting paths are asserted. Native Explorer appearance and a live desktop clipboard round-trip are not covered by these headless checks.

Before a feature commit, run `./scripts/Test-Xcmd.ps1 -Filter '<affected-scenario-filters>'`. Select the changed features and affected shared behavior from the mapping below. The command runs architecture checks, analyzer self-checks, formatting, code metrics and only the selected behavior scenarios. Run the unfiltered command locally only when the user explicitly requests full regression checks. The same local restriction applies to `./scripts/Release-Xcmd.ps1`. GitHub Actions runs all scenarios and static checks automatically for pushes and pull requests to `main`, and for manual runs. Active feedback iterations use the smaller checks described in the [development workflow](development-workflow.md).

The recorded environment on 2026-11-10 used the SDK in `global.json`, .NET runtime 10.0.2, Avalonia 12.1.6 and Windows 10.0.26300. That historical run passed all 168 scenarios with no build warnings and zero native application windows. Compiled architecture rules, evaluated project/package policies in Debug and Release, the isolated launcher build policy, 14 deliberate analyzer violations, formatting and Lizard metrics passed. New checks use only Release / win-x64 / x64; historical evidence files remain unchanged.

## Behavioral coverage

Git header feedback checks also assert that the branch, counters, arrow and button share the folder path's vertical centre at 100/150%, that the menu opens below the button, and that both the badge and its button are hidden after a non-repository result. Non-repository screenshots at both scales and the existing table-bound checks cover the empty header without leaving a disabled-button background or shifting file rows.

`git-commit-menu` drives the actual Git status menu at 100/150%, cancels it, starts Commit from a subdirectory and from the opposite panel, and checks the captured repository, minimal prompt, Sonnet/medium arguments, preserved command draft and available panels. `git-commit-launch-errors` checks visible launch failures and a missing Claude executable. `git-commit-native-launch` uses the production Windows Shell launcher with a windowless reporting executable to verify the literal working directory and argument boundaries through real process creation, including spaces, Unicode and shell characters. These feedback smoke checks do not call a model, commit user files, or verify the external Claude console's appearance or interactive prompts. The production launch uses ordinary interactive Claude Code with its existing settings and login; Xcmd observes launch failures rather than claiming commit completion.

`git-remote-colours` creates local diverged history with one commit ahead and one behind the configured upstream. It checks remote detection without tracking, the vector cloud, distinct deletion counts, and renders clean and changed states. `git-panel-status` also checks clean branch colour, removal of the fallback checkmark and the selected regular-file byte total. The three Git smoke cases passed in feedback release `20261112-132526-940-0118b86f`; these checks do not replace full finalization.

`git-panel-status` uses a real isolated repository and actual panel controls to check Unicode branches, clean status, staged rename, unstaged and untracked counts, whole-repository scope from a subfolder, detached HEAD, offscreen rendering and clearing outside Git. `git-background-navigation` holds Git responses pending while driving keyboard navigation, checks coalescing, stale-result rejection and upstream indicators. These are targeted feedback smoke checks; the complete finalization gate remains separate.

The `shell-*` feedback smoke cases drive actual right-button input and Alt+Enter through the application controls with a recording shell adapter. They cover the clicked item, inactive-panel activation, preserved marks, Unicode paths, extended commands, directory properties, parent-row exclusion and failure recovery. `shell-native-discovery` creates the production Windows shell context-menu objects for a real file and directory, queries their commands without displaying menus, and checks Open and Properties. Actual interactive menu selection, third-party submenu appearance, and the visible Windows Properties sheet remain native UI coverage limits; this smoke check does not claim to verify them. Shell actions apply to physical files and folders, including archive files themselves, but not virtual archive members.

The 2026-11-12 latency diagnostic records shell path parsing, parent binding, context-object creation and menu population separately in `shell-native-discovery`. It compares a first legacy call, a second legacy call and the new `CMF_ITEMMENU | CMF_ASYNCVERBSTATE` call. In `artifacts/probe/feedback-20261112-082425-829-bd94c0fa/results.json`, the text-file population took 685 ms on first use, 540 ms on the second legacy call and 265 ms with asynchronous verb-state evaluation. Directory population took 182, 79 and 82 ms respectively. The recorded canonical command lists were unchanged. These are diagnostic samples from one process, not a guaranteed end-to-end popup latency or a statistical benchmark. Extensions may still perform synchronous work. The flags are documented in [QueryContextMenu](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-icontextmenu-querycontextmenu).

The `search-panel-*` feedback smoke cases cover transfer through Alt+P, duplicate names with relative paths, opposite-panel location and focus, archive member location, sorting, watcher protection, Escape restoration, workspace JSON round-trip, viewing, inline rename, real copy/move and refresh after removal. The 2026-11-11 focused run passed all three cases with zero native application windows. Feedback release `20261111-222131-392-7c363f93` was published and installed with locked dependencies and `packageAudit: skipped-explicitly`, following the user's request to install despite the unavailable NuGet vulnerability service. This does not establish a successful package audit. Full finalization remains pending.

The `search-interaction-*` cases cover the rendered Alt-query color, matching-row arrow cycles and resets, focus after Search and at the result-list boundary, grouped name mode and Search action, typed skip/issue tables, exact ignore-file locations, and real double-click search scopes for a folder, a single file and a nested ZIP. They also verify table-specific feedback IDs and scoped directories, plus compatibility with older two-column search settings.

The `search-feedback-*` cases cover real pointer column resizing and JSON restoration for both search tables, whole-name and mask queries, compact skipped Path/Reason rows, removal of redundant top space and help, consistent Complete/skipped labels, independent single-window search with real panel navigation and folder creation, minimized-window reuse through Alt+F7, and Alt name ranking by match position with stable row-order ties.

The `remarks-*` feedback cases cover query-field arrows and Alt shortcuts, dropdown histories and JSON persistence, results kept while options change, Enter on checkboxes, unchanged running searches, compact path/line/preview columns, long-line literal search, exact Git ignore rules, viewer query reuse, native icon pixels at 100% and 150%, root/home key routing in both layouts, and separate Open/Resolved feedback tabs. Directory shortcut tests supply fixture listings for root and home instead of reading the user's directories.

The `review-*` cases cover Enter on focused Cancel in transfer and deletion dialogs, keyboard access to operation history, cancelling queued work, focus preserved when actions become disabled or labels change, no replacement marks after Move/Delete, and visible explanations for conflicts deferred by command input. These cases address the independent finalization usability review.

The `range-toggle-*` cases cover repeated Shift+Home/End and Shift+PageUp/PageDown ranges, partial ranges, marks outside a range and exclusion of the parent entry. The `feedback-*-labels*` cases cover static text, status and errors in the main, navigation, folder, transfer, delete, conflict and comment windows, disabled actions, labels inside radio buttons, and feedback that preserves the underlying workflow.

The `cursor-*` cases cover cursor appearance with no marks, keyboard marks, mouse marks and a marked cursor; faint inactive cursors; unchanged row, text, icon and cell geometry; no horizontal overflow for fitting columns at 100% and 150%; scrolling for wide columns; marks preserved by boundary, page and name navigation; and JSON restore of one marked file under the cursor.

The `polish-*` cases cover visible file marks separate from the cursor, repeated Shift+arrow toggling, Escape clearing marks, clipped table rows with space for scroll bars at 100% and 150%, underlined deletion access keys, default action color and Enter behavior in folder, transfer, delete, conflict and navigation dialogs, and multiline comment submission with Ctrl+Enter.

The `operations-review-*` cases cover useful shortcut hints, explicit Copy/Move prompts, content and metadata checks before automatic skip, selective keyboard marking, source scroll preservation after later layout passes, counting before changes, counting cancellation, consistent file totals, byte progress, estimated remaining time, and deletion shortcuts.

The file-operations feedback prototype adds the `operations-*` scenario group. Focused smoke checks cover recursive and empty-folder copy, folder merge, Skip/Rename/Replace, apply-to-all, same-volume move, the copy-delete fallback, partial move, captured queue targets, cancellation, safe overwrite, delete confirmations, partial failures, real junctions, and real Windows Recycle Bin contents. The native recycle fixture restores only its own file and folder after checking their contents. The junction and recycle checks need permission to create a real junction and write to the Windows Recycle Bin; a restricted-environment failure does not count as passing native coverage.

These checks do not establish full finalization coverage. In particular, the forced copy-delete scenario exercises real file changes through a controlled move capability; it is not evidence of a move between two physical volumes. The final quality gate and independent usability review remain pending until the user accepts the prototype.

The `operations-feedback-*` cases cover transfer confirmation, edited destination names, Shift+F5 copies in the active panel, rejection of batch copies into their source folder, deletion-mode choices, Enter confirmation, delayed progress visibility, and fixed progress height with long file names. Focus and compact-layout checks also cover Escape returning from the command line to the active panel. Native recycle checks include choosing Recycle Bin from a Shift+Delete confirmation.

Run a focused check against a built driver with `Xcmd.Probe.exe --filter operations- --output <new-output-directory>`. Use the direct driver for feedback checks; `Test-Xcmd.ps1 -Filter` also runs the architecture stage and is reserved for finalization.

| Requirement | Scenario groups | Evidence |
| --- | --- | --- |
| UC01, UC02: quick launch and file opening | `command-line-*`, `command-*` | Actual typing, focus keys, Escape, history and draft recovery, name/path insertion, quotes, local cd, held Enter, failures, pending launches, saved editor state, feedback, selection-or-all clipboard copying, left-trimmed paths, wrapped keyboard hints and compact layout at 150%. The production ShellExecute adapter launches a windowless executable and a real shortcut; reports verify raw arguments, Unicode, relative targets, working directory and concise missing-program errors. |
| UC01, UC02: current Windows environment | `command-line-native-environment-*` | Real CMD and Windows PowerShell receive user-variable changes made after launcher construction. Check additions, deletion before the first launch, repeated refresh, expanded values and targets, inherited process-only variables, working directory and restoration after launch. A controlled snapshot verifies updated PATH lookup and visible read/launch failures without changing the user's PATH. |
| UC03: name jumping | `alt-prefix-*`, `refinement-alt-*`, `refinement-search-*` | Actual input routes, Alt release, unmatched letters, physical keys with and without text events, duplicate suppression and loaded keyboard layouts. |
| UC23 and part of UC31: file search feedback | `search-name-*`, `search-text-*`, `search-archive-*`, `search-git-*`, `search-query-*`, `search-scheduling-*` | Real keyboard search and panel navigation, ZIP, TAR, TAR.GZ and RAR4/5 entry names and text, UTF-8/16/32 and Windows-1251/Windows-1252/DOS-866 samples, binary skip, F3 match preview, actual Git ignore rules and tracked exceptions, explicit ignored roots, expansion, query replacement, regex and case, incomplete coverage and offscreen layout. Controlled blocked archive and large-file workers verify ordinary results and cancellation. A dedicated search-result panel and representative performance measurements remain open. |
| UC04, UC06: current file operations | `copy-*`, `new-folder-*` | Real individual file copies, contents and no-overwrite errors; real folder creation, input validation, conflicts, actual dialog focus and cancellation. Recursive copy and conflict choices are not implemented. |
| UC12 and part of UC36: folder sizes | `folder-size-*` | Recursive byte totals, empty folders, rendered cells, cancellation, responsive input, partial reads, exact totals excluding a real Windows junction cycle, zero for links, preserving unaffected measurements on live refresh, sorting, persistence and navigation/close cancellation. |
| UC13: sorting | `sort-*`, `natural-name-*`, `parent-row-*`, `refinement-readable-sort-headings` | Actual header clicks and shortcuts; name, extension, size and date; both directions; directories first; natural number order; cursor and selection preservation; centered, larger, light heading text. |
| UC14, UC15: navigation and history | `opposite-panel-*`, `history-*`, `navigation-*`, `parent-row-*`, `ctrl-page-up-*` | Opposite-panel navigation and file fallback, parent navigation, cursor restoration, unique bounded histories, source-panel focus, cancellation, empty history, unavailable directories, obsolete and pending reads, keyboard and pointer input, long paths and scaled layout. |
| UC16: favorites | `favorites-*`, `navigation-*` | Add, rename and remove through actual controls; shared lists, source-panel focus, cursor restoration, validation and retry, pending-read cancellation, immediate saves, older sessions, real process handoff and modal feedback. |
| UC17: rename | `rename-*` | Actual inline editor, full-name selection and caret behavior, isolated navigation keys, Enter/Escape/Tab, files and directories, Unicode, spaces, case-only changes, invalid names, conflicts, removed sources, retry and repeated Enter. |
| UC19: file colors | `colors-*` | Ordered JSON rules, source/script priority, settings reload, invalid settings, selection contrast and renamed file types. |
| UC20: panels and interface | `real-directory-*`, `arrow-*`, `shift-*`, `ctrl-a-*`, `mouse-*`, `tab-*`, `refresh-*`, `resize-*`, `column-*`, `double-click-*`, `dpi-*`, `layout-*`, `refinement-*` | Actual controls, ranges, multi-selection, focus, automatic directory monitoring, missing-directory recovery, sorting, sizing, native system icons, compact dialogs and offscreen rendering at normal and 150% scale. |
| UC38: persistence and local release updates | `workspace-*` | JSON state, cursors, selection, directories, history, columns, scroll, window state, measurements, favorites and command drafts. Real process checks prove source exit, replacement restore, readiness order and safe startup failure. |
| UC39: element feedback | `issues-*`, `workspace-feedback-*`, `refinement-feedback-*`, `refinement-resolution-*` | Pointer selection without activation, editor focus, multiline comments, cancellation, removal, immediate SQLite saves, legacy migration, transactional work claims, real CLI process competition, dialog drafts and process handoff. |
| Accumulated UI feedback | `accumulated-*`, `git-commit-*` | Space folder measurements, physical Home shortcuts from command focus, readable type width/session persistence, title age/fresh-update message, real clean/dirty folder repositories, bounded background work/cancellation and AI commit launch arguments. |
| Background reads and large directories | `background-read-*`, `obsolete-read-*`, `large-directory-*` | Controlled pauses around the real filesystem adapter prove input remains usable and newer reads cancel obsolete work. A fixture with 10,009 real entries checks virtualization, recycled-row rename, sorting and rendering. |
| Rules and input precedence | `rules-*` | Seven scenarios check numeric name runs and ties, Windows device names, validation error order, command targets and raw arguments, saved panel constraints, favorite identity, combined keyboard modifiers and editor confirmation priority. |
| UC40: permanent launch and taskbar identity | `launcher-*` | Eight scenarios run the actual self-contained NativeAOT launcher and real Windows shortcuts, preserve arguments and working directory, reject invalid release trees, defer replacement of a locked launcher, read native taskbar properties and complete a headless application update after the launcher exits. Reinstall an existing shortcut with changed icon pixels and compare the actual Windows shell pixels. Preserve the previous icon and reuse the path when pixels do not change. Record launcher exit latency. Native shell properties use one invisible fixture window; all application windows remain headless. See [release evidence](evidence/stable-launcher-2026-11-10.json). |

Independent usability review of the startup workflow required measuring the real launcher, restoring a saved session without directory overrides, and retaining every timing sample. The startup probe follows those requirements. The hidden native probe measures readiness to call Show; it does not prove when pixels appear on the desktop.

Independent usability review of the command-line workflow found no open issues after pending-launch feedback was added. The review used the actual source and a compact 840×400 offscreen image. Repeat independent review for changed user workflows, as required by the [architecture](architecture.md).

Independent usability review of the 2026-11-10 feedback changes found no open issues after keeping each shortcut and action in one group and retaining readable reasons for uncommon launch errors. The review covered source, clipboard and native launch results, command-layout images at 840×400 and 1280×400 with 100% and 150% scaling, and the table headings. These checks do not verify native desktop appearance or clipboard access under every Windows configuration.

## Archive feedback checks

Finalization adds `archive-pending-*`, `archive-opening-*`, `archive-report-*` and `archive-extraction-*` checks. They cover focus while a read is pending, visible loading and Escape cancellation, report identity and disabled byte modes, actual Open with Windows menu input, Skip/Replace extraction conflicts, cancellation after a real output block, destination preservation, temporary-file cleanup and refreshed archive contents. The independent [usability review](evidence/archive-usability-review-2026-11-11.md) records the findings and fixes.

Build the Release / win-x64 probe and run its DLL with `--filter archive-`. These focused feedback scenarios drive actual panel keys and viewers without visible windows. They cover Enter, Ctrl+PageDown, Ctrl+Enter followed by Enter, F3 archive reports and member views, F5 file/folder extraction, empty folders, nested archives, parent cursors, Office associations, read-only actions, rejected unsafe paths, unsupported-file recovery and workspace restoration.

The format scenario decompresses real TAR, TAR.GZ, GZip, BZip2, XZ, TAR.BZ2, TAR.XZ, RAR4, RAR5, 7z and 7z SFX files. The RAR and 7z fixtures come from the pinned SharpCompress release; tests never execute their programs. Offscreen captures cover the archive panel and report. The shell-bypass scenario verifies the production command route with a recording launcher; it does not open the user's archive application. Password entry, multipart discovery and other SFX installers have no coverage. These are smoke checks, not full final verification.

## Viewer feedback checks

Run the prebuilt Release / win-x64 probe with `--filter viewer-` to check the viewer without native application windows. Build `tests/Xcmd.Probe/Xcmd.Probe.csproj` first if needed. This filter directly selects the smoke scenarios; it does not invoke the full verification script.

The `viewer-*` scenarios cover actual F3/F4 input, UTF-8/16/32 files, Windows-1251 detection and manual override, Unicode page boundaries, a one-GiB binary file, empty files, text/binary/hex/image switching, image signature routing, real PNG decoding and visible decode failures. Search checks cover Alt-initiated input, digits inside the query, next/previous matches, whole-file wrapping, matches across page boundaries, query replacement and not-found feedback. Offscreen images cover text, binary and image views. The source panel keeps its cursor and focus after closing.

The reading-layout check verifies that content fills the space between the compact menu and status, search and extended details start hidden, commands show shortcuts, Escape dismisses a menu before the window, and requested search/details can be hidden again. Encoding changes use real menu input. F12 and pointer feedback work without a permanent button. Alt+letter starts search; plain typing does not. Digits switch modes and W toggles wrapping outside the query. The continuous-reading check uses a real multi-window UTF-8 log: mouse wheel and PageDown cross read boundaries, End shows an explicit file-tail marker with 100 percent, Home restores the beginning, and the scroll bar seeks within the whole file. Status reports the visible bottom, detected mode/encoding and wrapping.

The viewer feedback scenarios inspect the actual content border, combined header and top-right percentage, exercise maximize/restore/close controls, and retain a working mode for unsupported, oversized and damaged image samples. Pointer input returns to the main panel, navigates into a directory, opens another independent viewer and closes each viewer separately. The launch request keeps the first viewer's original working directory. Main-window close clears all remaining viewers, and open viewers defer release handoff. Header checks cover the drag role and resize-border configuration under Headless; native desktop dragging and Windows Snap remain outside this smoke evidence.

The native editor check uses the production editor launch plan and Windows ShellExecute adapter with a windowless reporting executable. It verifies one literal Unicode file argument and the source working directory. It does not launch Notepad++ or prove that editor's appearance. Viewer finalization requires user acceptance, affected scenarios, static checks and independent usability review. Full regression checks require an explicit user request.

`feedback-double-click-*` checks real pointer input through feedback selection. Double-click opens a menu without a comment, single-click selects a command without running it, and a forwarded command runs once while feedback remains active. The comment form shows its action path. Real session JSON and refinement Markdown retain that path. Existing label, disabled-action, dialog-draft and resolution checks cover single-click compatibility.

`appearance-main-*` checks the combined main title row, caption actions and footer Feedback/Notes controls at normal and minimum sizes. Both main and viewer checks call Avalonia's chrome hit-test to verify that menu clicks receive client input while the title remains draggable. They also open each top-level menu with one click and close it with Escape. This checks the input classification used by the Windows backend without sending native pointer events. `appearance-reader-*` checks the nine MD View theme names, real menu selection, exact cream/dark palette colors on the focused content and title, preserved search state, shared preferences across viewers, immediate session saves and restoration into a new application window. A session without the new theme field keeps a valid default. Offscreen images cover the main frame and light/dark reader palettes, including the thin status separator.

`viewer-images-and-fallback` also checks that text actions are disabled for images and enabled for byte views. `viewer-read-failure-and-keyboard-recovery` deletes a file after the viewer captures its file list, verifies the completed error state, and restores access through external actions and Alt+Left/Right. `viewer-reading-menu-search-layout` checks the temporary feedback hint in the active viewer and restoration of its reading status. The independent [usability review](evidence/viewer-usability-review-2026-11-11.md) records the finalization findings and fixes.

## Startup measurements

Cross-process workspace reports are published atomically and read with bounded retries for missing files or Windows sharing locks. Invalid JSON still fails immediately. `launcher-report-publication-waits-for-unlock` holds a real exclusive file lock, starts the production test report reader, releases the lock and checks the complete result. It also verifies that malformed reports are rejected.

Published desktop assemblies use ReadyToRun for Windows x64. The Windows backend uses Skia software rendering to avoid initializing ANGLE and a GPU device on each launch. The large-directory scenario records paging render times at normal and 150% scale. The permanent launcher uses NativeAOT with runtime/compiler 10.0.14 and needs no installed .NET runtime. The desktop application still uses the installed .NET runtime. Saved sessions and file-type settings use generated JSON metadata; both panels share one bundled settings load.

`Release-Xcmd.ps1` also runs `Test-Startup.ps1` against the published assemblies before updating the latest-release marker. This separate native diagnostic keeps the main window hidden. The regular behavior suite still creates zero native application windows.

To measure a published release again:

```powershell
./scripts/Test-Startup.ps1 -ApplicationPath releases/<release-id> -Native
```

Omit `-Native` to use Avalonia.Headless. The script uses an isolated installation, the real permanent launcher, the actual `App` startup path, and a saved workspace with 250 real files in each panel. It verifies restored directories, cursor, active panel and command draft. Each process exits before the next run. The test driver is also precompiled to avoid measuring its own JIT work. Results retain the first run and every later sample, launcher exit times, phase timings, median and maximum. Headless runs also retain complete offscreen images. No sample is discarded. The first run after copying files is not a controlled cold-cache or post-boot measurement.

Native timings start before launching the permanent executable and end immediately before the actual App calls Show. The native probe does not show or render its hidden window. Headless runs record the same endpoint and a separate time to the first complete rendered frame. Native timings exclude Explorer click handling, the Show call, and desktop presentation. The probe cannot establish a hard one-second taskbar-click-to-visible-frame guarantee, and the measurements are not a portable timing gate for different machines.

The permanent launcher suppresses console creation for its child process. Startup and lifecycle hosts inspect `GetConsoleWindow` at entry and report whether a console is attached. Launcher startup, process handoff and every release startup sample require no attached console. This prevents console test hosts from flashing windows during tests and release verification; hiding application windows alone does not establish this result.

## Complexity refactor evidence

The 2026-11-10 refactor keeps the three production projects and their dependency direction. Production functions at or above 80% of a configured Lizard maximum decreased from 11 to 0. No metric limit changed. `HandlePanelCommand` decreased from 22 to 8 in cyclomatic complexity and from 25 to 7 in cognitive complexity.

The full release verification passed all 156 scenarios. An additional comparison of the Core assemblies before and after the refactor passed 26,124 cases with matching results and exception messages. The comparison covers natural ordering, file names, command parsing and saved workspace validation. These checks supplement the behavioral suite; they do not prove equivalence for every input.

The [comparison evidence](evidence/complexity-refactor-2026-11-10.json) records all 11 methods, project metrics, limits, source fingerprints and the verified release ID. Generate a fresh [code health report](automated-checks.md#code-health-report) to inspect current projects, folders, files and members. Extracting methods adds function entry points, so total cyclomatic complexity and source lines can increase while individual methods become simpler.

## Diagnostics and isolation

The `open-feedback-search-actions` and `open-feedback-responsive-columns` scenarios check underlined access keys, primary action changes, disabled Go, Alt+P, responsive column ordering at normal and 150% scale, overflow, saved width preferences and cursor/mark preservation. They run through the actual controls without native windows.

The [independent result-panel usability review](evidence/search-panel-usability-review-2026-11-11.md) covers result transfer, location, return, renaming, command execution and responsive presentation. `search-panel-rename-preserves-identity` and `search-panel-command-working-directory` cover the two findings fixed during finalization.

`open-feedback-resize-boundaries` continuously narrows and widens the actual window at 100% and 150% scale with a fractional, manually sized Name column in the right panel. A layout-pass guard detects non-converging resizing. Before the fix this reproduced an endless layout cycle near a window width of 1231 DIP. Column adaptation now runs after size or preferred-width changes, outside layout completion; its scrollbar allowance does not depend on scrollbar visibility.

The runner starts the same `App`, controls, command handlers and services as the desktop build. Avalonia.Headless replaces the window and input backend; Skia renders into memory. Every application window and dialog must have a zero-valued `STUB` handle. Test scenarios use isolated fixtures and recording launchers unless a scenario explicitly tests the real Windows adapter with a controlled windowless endpoint.

Each run writes `results.json`, state and input traces, failure details and selected PNG images under `artifacts/probe`. A published release includes its own `verification.json`. These generated files are excluded from Git. They are not inputs required for verification from a clean source checkout.

## Design-system feedback implementation (2026-11-12)

`design-system-shared-surfaces` checks consistent table/header geometry, square buttons, inherited readable typography and action bounds; renders panels, new-folder/transfer dialogs, navigation and narrow search at 100/150% scale. `design-system-operation-details` verifies that the operation table retains source/destination values in separate columns. `design-system-reader-contrast` switches all nine palettes through the actual menus, checks selected-text contrast and preserves the found text. The existing reader persistence scenario also remains required.

These checks use the actual production styles and controls. The implementation is in the feedback phase: the focused smoke set does not replace the complete finalisation gate or independent usability review. A failed NuGet vulnerability-service lookup is a publication blocker unless the user explicitly authorises the documented feedback-only audit exception.

## Feedback finalization (2026-11-13)

The [finalization evidence](evidence/feedback-finalization-2026-11-13.md) records
337 passing scenarios out of 337 and a passing full gate, including the NativeAOT
launcher, architecture, analyzers, formatting, metrics and native integration
checks. The independent usability review is complete. Folder icons use their
ordinary Windows appearance; Git changes remain in the dedicated column.

## Limits

The 2026-11-12 priority batch has focused headless checks in `PriorityBatchScenarios`: `priority-new-file` and `priority-new-file-launch-failure` cover creation, collision, cancel and editor failure; `priority-copy-metadata` checks real Windows timestamps, basic attributes, NTFS streams and folder creation time; `priority-retry-file` and `priority-retry-skip-and-cancel` cover failed-item decisions; `priority-selected-sizes` checks cached directory totals and incomplete bounds without scanning. `priority-command-completion`, `priority-command-directory-quotes` and `priority-command-prefix-history` cover suggestions, a literal trailing separator through the real windowless Windows launcher, draft preservation and prefix history. `priority-viewer-follow` checks appended data, pause/resume and truncation. `priority-shell-group` and `priority-shell-mixed-folders` check group targeting, unchanged marks, keyboard invocation, the mixed-folder restriction and native Windows command discovery without displaying a menu. These are feedback smoke checks, not the full finalization gate. External editor appearance and third-party native menu interaction retain the limits below.

`remarks-native-icons-and-directory-shortcuts` changes the same headless window through 100%, 125%, 150%, 200%, 150% and 100% scaling. Both file panels use real Windows shell icons. Every image must keep a 16-DIP width and height while its bitmap has exactly the physical pixel size required by the current scale. The check captures 100% and 150% offscreen images. It covers scale-change handling without requiring visible windows; native monitor movement and Windows message delivery retain the limits below.

`navigation-pinned-favorites-and-type-search` and `navigation-search-active-column-and-keyboard-layout` cover the two-column directory chooser, pinned favorites, duplicate suppression, typing in the active list, uppermost substring matching, match cycling, ignored unmatched input and keyboard-layout translation. `navigation-two-column-rendering` captures normal and 150% layouts; existing favorite editing, persistence, cancellation and focus scenarios cover the combined list. `selection-size-readable-units` checks readable selected-file totals, unit boundaries and the exact byte tooltip; `priority-selected-sizes` retains incomplete measured-folder coverage. Feedback build `20261112-201525-908-dd97d632` passed 20 focused scenarios in 32.9 seconds. The unavailable package vulnerability audit was skipped under the user's earlier authorization; this is smoke evidence, not full finalization verification. The [independent directory chooser usability review](evidence/directory-chooser-usability-review-2026-11-12.md) records the findings and resolutions for this workflow.

- Headless checks do not prove native Windows keyboard delivery, AltGr, IME, clipboard integration, Explorer drag-and-drop, shell-extension menus, GPU rendering, native monitor placement or focus during monitor changes.
- Native keyboard-layout translation and Windows system-icon retrieval have direct integration checks. This does not replace native input-backend coverage.
- External program appearance and interactive behavior remain outside application offscreen checks. Native launch tests prove the application's Windows Shell contract with a controlled endpoint.
- A scaled render proves layout at that scale, not native per-monitor transitions.
- Taskbar checks prove shortcut targets, process/window identity, relaunch properties, cleanup and nonfatal shell failures through Windows APIs. They do not automate Explorer's pin menu or inspect its visible taskbar grouping. Pinning remains a user action.
- Directory reads run in the background. Final sorting and list replacement run on the UI thread; much larger directories need separate measurements and a design if latency grows.
- Architecture and metric gates enforce specific rules. They do not prove complete feature coverage, good state ownership, correct cancellation or usability.
- Planned workflows in the [use cases](use-cases.md) require their own checks before they can be described as implemented.
