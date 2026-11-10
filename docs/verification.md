# Verification coverage

Run `./scripts/Test-Xcmd.ps1` for the complete finalization path or `./scripts/Release-Xcmd.ps1` to verify and publish a final build. Both commands run architecture checks, analyzer self-checks, formatting, code metrics and headless behavior checks. The GitHub Actions workflow uses the same verification path on Windows. Active feedback iterations use the smaller checks described in the [development workflow](development-workflow.md).

The recorded environment on 2026-11-10 used the SDK in `global.json`, .NET runtime 10.0.2, Avalonia 12.1.6 and Windows 10.0.26300. That historical run passed all 168 scenarios with no build warnings and zero native application windows. Compiled architecture rules, evaluated project/package policies in Debug and Release, the isolated launcher build policy, 14 deliberate analyzer violations, formatting and Lizard metrics passed. New checks use only Release / win-x64 / x64; historical evidence files remain unchanged.

## Behavioral coverage

| Requirement | Scenario groups | Evidence |
| --- | --- | --- |
| UC01, UC02: quick launch and file opening | `command-line-*`, `command-*` | Actual typing, focus keys, Escape, history and draft recovery, name/path insertion, quotes, local cd, held Enter, failures, pending launches, saved editor state, feedback, selection-or-all clipboard copying, left-trimmed paths, wrapped keyboard hints and compact layout at 150%. The production ShellExecute adapter launches a windowless executable and a real shortcut; reports verify raw arguments, Unicode, relative targets, working directory and concise missing-program errors. |
| UC01, UC02: current Windows environment | `command-line-native-environment-*` | Real CMD and Windows PowerShell receive user-variable changes made after launcher construction. Check additions, deletion before the first launch, repeated refresh, expanded values and targets, inherited process-only variables, working directory and restoration after launch. A controlled snapshot verifies updated PATH lookup and visible read/launch failures without changing the user's PATH. |
| UC03: name jumping | `alt-prefix-*`, `refinement-alt-*`, `refinement-search-*` | Actual input routes, Alt release, unmatched letters, physical keys with and without text events, duplicate suppression and loaded keyboard layouts. |
| UC04, UC06: current file operations | `copy-*`, `new-folder-*` | Real individual file copies, contents and no-overwrite errors; real folder creation, input validation, conflicts, actual dialog focus and cancellation. Recursive copy and conflict choices are not implemented. |
| UC12 and part of UC36: folder sizes | `folder-size-*` | Recursive byte totals, empty folders, rendered cells, cancellation, responsive input, partial reads, a real Windows junction cycle, sorting, refresh, persistence and navigation/close cancellation. |
| UC13: sorting | `sort-*`, `natural-name-*`, `parent-row-*`, `refinement-readable-sort-headings` | Actual header clicks and shortcuts; name, extension, size and date; both directions; directories first; natural number order; cursor and selection preservation; centered, larger, light heading text. |
| UC14, UC15: navigation and history | `opposite-panel-*`, `history-*`, `navigation-*`, `parent-row-*`, `ctrl-page-up-*` | Opposite-panel navigation and file fallback, parent navigation, cursor restoration, unique bounded histories, source-panel focus, cancellation, empty history, unavailable directories, obsolete and pending reads, keyboard and pointer input, long paths and scaled layout. |
| UC16: favorites | `favorites-*`, `navigation-*` | Add, rename and remove through actual controls; shared lists, source-panel focus, cursor restoration, validation and retry, pending-read cancellation, immediate saves, older sessions, real process handoff and modal feedback. |
| UC17: rename | `rename-*` | Actual inline editor, full-name selection and caret behavior, isolated navigation keys, Enter/Escape/Tab, files and directories, Unicode, spaces, case-only changes, invalid names, conflicts, removed sources, retry and repeated Enter. |
| UC19: file colors | `colors-*` | Ordered JSON rules, source/script priority, settings reload, invalid settings, selection contrast and renamed file types. |
| UC20: panels and interface | `real-directory-*`, `arrow-*`, `shift-*`, `ctrl-a-*`, `mouse-*`, `tab-*`, `refresh-*`, `resize-*`, `column-*`, `double-click-*`, `dpi-*`, `layout-*`, `refinement-*` | Actual controls, ranges, multi-selection, focus, automatic directory monitoring, missing-directory recovery, sorting, sizing, native system icons, compact dialogs and offscreen rendering at normal and 150% scale. |
| UC38: persistence and local release updates | `workspace-*` | JSON state, cursors, selection, directories, history, columns, scroll, window state, measurements, favorites and command drafts. Real process checks prove source exit, replacement restore, readiness order and safe startup failure. |
| UC39: element feedback | `workspace-feedback-*`, `refinement-feedback-*`, `refinement-resolution-*` | Pointer selection without activation, editor focus, nonempty and multiline comments, cancellation, removal, immediate JSON/Markdown saves, dialog drafts, resolution records and real process handoff. |
| Background reads and large directories | `background-read-*`, `obsolete-read-*`, `large-directory-*` | Controlled pauses around the real filesystem adapter prove input remains usable and newer reads cancel obsolete work. A fixture with 10,009 real entries checks virtualization, recycled-row rename, sorting and rendering. |
| Rules and input precedence | `rules-*` | Seven scenarios check numeric name runs and ties, Windows device names, validation error order, command targets and raw arguments, saved panel constraints, favorite identity, combined keyboard modifiers and editor confirmation priority. |
| UC40: permanent launch and taskbar identity | `launcher-*` | Eight scenarios run the actual self-contained NativeAOT launcher and real Windows shortcuts, preserve arguments and working directory, reject invalid release trees, defer replacement of a locked launcher, read native taskbar properties and complete a headless application update after the launcher exits. Reinstall an existing shortcut with changed icon pixels and compare the actual Windows shell pixels. Preserve the previous icon and reuse the path when pixels do not change. Record launcher exit latency. Native shell properties use one invisible fixture window; all application windows remain headless. See [release evidence](evidence/stable-launcher-2026-11-10.json). |

Independent usability review of the startup workflow required measuring the real launcher, restoring a saved session without directory overrides, and retaining every timing sample. The startup probe follows those requirements. The hidden native probe measures readiness to call Show; it does not prove when pixels appear on the desktop.

Independent usability review of the command-line workflow found no open issues after pending-launch feedback was added. The review used the actual source and a compact 840×400 offscreen image. Repeat independent review for changed user workflows, as required by the [architecture](architecture.md).

Independent usability review of the 2026-11-10 feedback changes found no open issues after keeping each shortcut and action in one group and retaining readable reasons for uncommon launch errors. The review covered source, clipboard and native launch results, command-layout images at 840×400 and 1280×400 with 100% and 150% scaling, and the table headings. These checks do not verify native desktop appearance or clipboard access under every Windows configuration.

## Startup measurements

Published desktop assemblies use ReadyToRun for Windows x64. The Windows backend uses Skia software rendering to avoid initializing ANGLE and a GPU device on each launch. The large-directory scenario records paging render times at normal and 150% scale. The permanent launcher uses NativeAOT with runtime/compiler 10.0.14 and needs no installed .NET runtime. The desktop application still uses the installed .NET runtime. Saved sessions and file-type settings use generated JSON metadata; both panels share one bundled settings load.

`Release-Xcmd.ps1` also runs `Test-Startup.ps1` against the published assemblies before updating the latest-release marker. This separate native diagnostic keeps the main window hidden. The regular behavior suite still creates zero native application windows.

To measure a published release again:

```powershell
./scripts/Test-Startup.ps1 -ApplicationPath releases/<release-id> -Native
```

Omit `-Native` to use Avalonia.Headless. The script uses an isolated installation, the real permanent launcher, the actual `App` startup path, and a saved workspace with 250 real files in each panel. It verifies restored directories, cursor, active panel and command draft. Each process exits before the next run. The test driver is also precompiled to avoid measuring its own JIT work. Results retain the first run and every later sample, launcher exit times, phase timings, median and maximum. Headless runs also retain complete offscreen images. No sample is discarded. The first run after copying files is not a controlled cold-cache or post-boot measurement.

Native timings start before launching the permanent executable and end immediately before the actual App calls Show. The native probe does not show or render its hidden window. Headless runs record the same endpoint and a separate time to the first complete rendered frame. Native timings exclude Explorer click handling, the Show call, and desktop presentation. The probe cannot establish a hard one-second taskbar-click-to-visible-frame guarantee, and the measurements are not a portable timing gate for different machines.

## Complexity refactor evidence

The 2026-11-10 refactor keeps the three production projects and their dependency direction. Production functions at or above 80% of a configured Lizard maximum decreased from 11 to 0. No metric limit changed. `HandlePanelCommand` decreased from 22 to 8 in cyclomatic complexity and from 25 to 7 in cognitive complexity.

The full release verification passed all 156 scenarios. An additional comparison of the Core assemblies before and after the refactor passed 26,124 cases with matching results and exception messages. The comparison covers natural ordering, file names, command parsing and saved workspace validation. These checks supplement the behavioral suite; they do not prove equivalence for every input.

The [comparison evidence](evidence/complexity-refactor-2026-11-10.json) records all 11 methods, project metrics, limits, source fingerprints and the verified release ID. Generate a fresh [code health report](automated-checks.md#code-health-report) to inspect current projects, folders, files and members. Extracting methods adds function entry points, so total cyclomatic complexity and source lines can increase while individual methods become simpler.

## Diagnostics and isolation

The runner starts the same `App`, controls, command handlers and services as the desktop build. Avalonia.Headless replaces the window and input backend; Skia renders into memory. Every application window and dialog must have a zero-valued `STUB` handle. Test scenarios use isolated fixtures and recording launchers unless a scenario explicitly tests the real Windows adapter with a controlled windowless endpoint.

Each run writes `results.json`, state and input traces, failure details and selected PNG images under `artifacts/probe`. A published release includes its own `verification.json`. These generated files are excluded from Git. They are not inputs required for verification from a clean source checkout.

## Limits

- Headless checks do not prove native Windows keyboard delivery, AltGr, IME, clipboard integration, Explorer drag-and-drop, shell-extension menus, GPU rendering, native monitor placement or focus during monitor changes.
- Native keyboard-layout translation and Windows system-icon retrieval have direct integration checks. This does not replace native input-backend coverage.
- External program appearance and interactive behavior remain outside application offscreen checks. Native launch tests prove the application's Windows Shell contract with a controlled endpoint.
- A scaled render proves layout at that scale, not native per-monitor transitions.
- Taskbar checks prove shortcut targets, process/window identity, relaunch properties, cleanup and nonfatal shell failures through Windows APIs. They do not automate Explorer's pin menu or inspect its visible taskbar grouping. Pinning remains a user action.
- Directory reads run in the background. Final sorting and list replacement run on the UI thread; much larger directories need separate measurements and a design if latency grows.
- Architecture and metric gates enforce specific rules. They do not prove complete feature coverage, good state ownership, correct cancellation or usability.
- Planned workflows in the [use cases](use-cases.md) require their own checks before they can be described as implemented.
