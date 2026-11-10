# Development and testing requirements

Confirmed cross-cutting requirement, 2026-11-09: the agent must be able to test every application feature independently, without opening visible windows or requiring human assistance.

These requirements apply to [all user workflows](use-cases.md), including [search](search.md). They are a condition for architecture and framework choices, not a later testing improvement.

Use the [development workflow](development-workflow.md) to choose when checks run. Active feedback iterations use a smoke check under 60 seconds and an automatically published local feedback build. Full quality checks, complete regression coverage, and independent usability review are required during finalization before committing. Use only Release / Windows x64 for this user's machine.

## Required capabilities

- Provide a documented headless entry point that starts, drives, inspects, and closes the application under test.
- Make every application command and meaningful state transition accessible to the test driver. Use stable action and state identifiers instead of screen coordinates.
- Test the same command handlers, state transitions, and services that the interactive application uses. A separate imitation of the application is not sufficient.
- Exercise the UI layer without visible windows, including keyboard routing, focus, selection, dialogs, list updates, and viewer switching. Use offscreen rendering where layout or appearance affects behavior.
- Run real filesystem and archive integration tests in isolated temporary directories. Use real encoded files, links, repositories, and archive samples for supported cases.
- Make background work observable: queue state, search scope, results, progress, failures, and cancellation must be available to assertions.
- Support repeatable setup and cleanup, bounded waits, structured results, and a nonzero exit status on failure.
- Keep normal test runs independent of the user's files, TC settings, live Git repositories, credentials, and desktop state.
- Let the agent create fixtures, execute the full suite, inspect failures, and rerun relevant checks without human setup or decisions.

The exact command names and test-driver protocol will be chosen with the implementation. Do not document an executable command as available before it exists.

## Keep behavior separate from infrastructure

Express navigation, selection, search scope/order, operation plans, conflict decisions, and queue state as domain behavior. Keep filesystem access, OS integration, process launch, archive readers, rendering, and input transport in separate adapters.

Use direct command and state tests for fast feedback. Add real adapter integration tests and offscreen UI tests where those layers can change the result. Domain tests alone do not establish all-feature coverage.

Test infrastructure, fixtures, process control, and helper code must stay separate from readable scenario cases. Test cases should use the user-story terms.

## Feature coverage

| Feature group | Required automated evidence |
| --- | --- |
| Panels and navigation | Directory changes, sorting, history, favorites, Ctrl+Left/Right, cursor restoration, and focus/keyboard behavior through the actual application state. |
| Command line | Actual input and focus routing, name/path insertion, quotes, Escape, panel return, bounded command history, local cd, launch errors and draft persistence. Drive the production Windows ShellExecute adapter with a windowless executable and a real shortcut; verify raw arguments and the active panel's working directory. |
| File operations | Real copy, move, create, rename, delete, partial failure, conflict decisions, and selection/focus afterward. Check file contents and resulting paths. |
| Queue | Concurrent UI use, operation ordering, progress, cancellation, and failures, with controlled scheduling where needed. |
| Viewers and preview | Real supported format samples, text encodings, binary views, viewer routing, opposite-panel preview, and offscreen UI behavior. |
| Archives | Real supported archives, entry enumeration, viewing/extraction paths, creation, malformed input, and cancellation. |
| Search | Name, mask, and contents matches; encoding and archive defaults; ignore rules; search scheduling; result-panel operations; incomplete coverage; cancellation. |
| Git and comparison | Temporary repositories and directory pairs with known contents; actual statuses and diffs; configured comparison behavior. |
| Links and junctions | Real supported Windows link types, target inspection, broken links, cycles, and operation behavior. If test-environment permissions prevent a required fixture, report a coverage failure rather than passing a mock-only replacement. |
| Windows shell integration | Native adapter discovery and action routing without showing menus; noninteractive execution checks where supported. Keep native visual coverage limits explicit. |

## Native application boundaries

The application can expose and test its own behavior without visible windows. Another program's visible UI, such as a terminal window or a third-party Windows shell-extension menu, is outside that direct control.

The command line uses the production Windows ShellExecute adapter. Ordinary panel and input tests use a recording launcher and never start user programs. Offscreen images cover the compact command line at normal and 150% scale. The application does not host a terminal process.

Environment-refresh checks use real CMD and Windows PowerShell with a windowless reporting executable. Each fixture creates only uniquely named temporary user variables and deletes them in cleanup, without broadcasting environment changes or changing the user's existing PATH. Controlled environment snapshots verify PATH lookup and read failures. Check that the Xcmd process environment is restored after success and failure. Do not log unrelated environment values.

Native file-opening checks use `tests/Xcmd.LaunchProbe`, a windowless executable that writes arguments and its working directory to an isolated report. The production Windows shell adapter starts the helper directly and through a real `.lnk` file. Third-party editor appearance and native Windows input delivery are outside these checks.

Test the real integration boundary where noninteractive operation is supported, and use a controlled endpoint for the application's own launch contract. Clearly distinguish this from testing the real external program's appearance or interactive behavior. Do not call a mocked launch a verified external integration.

For shell context menus, test native action discovery and invocation paths without displaying the menu when the platform permits it. Exact appearance and interactive behavior of third-party native menus cannot be proven by domain tests or an offscreen copy. Record this as a coverage limit and resolve the verification approach before claiming full feature coverage. Do not silently require a person to click through it.

## Architecture acceptance gate

Before selecting a UI stack, demonstrate a small vertical slice: load a real temporary directory, drive selection with keyboard input, open and dismiss an application dialog, execute a file operation, observe the resulting state and files, render the relevant UI offscreen, and exit. Run the complete slice without a visible application window or human action.

Do not choose a stack solely because its backend has a test API. The application's real input and UI behavior must also be testable through the chosen approach.

### Accepted stack and gate outcome

On 2026-11-09, the user accepted C# and Avalonia after reviewing the panels. The accepted suite demonstrated the required slice without native application windows. The [technology decision](technology-decision.md) records the choice; [verification](verification.md) maps the current regression coverage.

The gate is satisfied for the panels. Keep these requirements for every additional workflow. Native Windows integration and planned features still need their own coverage; stack acceptance does not waive those checks.

## Completion rule

Every implemented user story needs suitable automated checks and a documented way for the agent to run them headlessly. Keep a coverage mapping from implemented stories to those checks. A missing test path, skipped required integration check, or manual-only step is an unresolved development requirement, not a passing result.

On failure, retain useful diagnostics such as command/state traces, fixture paths, worker errors, and offscreen images. Keep success output concise. No human interaction is part of the normal verification workflow.

## Deliver a ready build for review

Build the current Release executable and finish the checks required by the current development phase before asking the user for feedback. Label a feedback build as smoke-checked and a final build as fully verified only after the full gate passes. Give the user the path to the ready executable. Do not require the user to restore dependencies, build the application, run tests or use a launch script.

Use `scripts/Release-Xcmd.ps1` for fully verified final releases. Feedback builds follow the shorter path in the development workflow and have separate smoke-check evidence. Publish each build into a new `releases/<release-id>` directory and include all published dependencies. Never give the user a development `bin` executable or overwrite an existing release. The user must be able to keep the previous release open while development builds and new releases continue. Update `releases/latest.txt` only after the phase-specific checks and installation succeed; keep the previous release available if either fails.

Publish the desktop application with ReadyToRun for Windows x64. Use software rendering for the Windows list-based interface and retain paging measurements at normal and 150% scale. Generate JSON metadata for saved sessions and file-type settings at build time. Load bundled file-type settings once for both panels.

Deliver the permanent `Xcmd.exe` and `Xcmd.lnk` next to `releases`. The launcher must validate the marked release, preserve argument boundaries and the caller's working directory, start the application and exit without waiting for it. Use one stable Windows taskbar identity for the shortcut and all released windows. Pinning a running window must use the permanent launcher. Keep taskbar setup and cleanup failures separate from application startup, process handoff and normal close.

Build the self-contained Windows x64 NativeAOT launcher through `scripts/Build-Launcher.ps1`. It compiles a small source subset of Infrastructure with a separate lock file and isolated outputs. Pin the launcher runtime and compiler to 10.0.14; it does not add a production project. Install the launcher and shortcut before committing the latest-release marker. Preserve an in-use launcher and defer its replacement so a brief launch cannot prevent a new application release. Do not modify the user's taskbar pins automatically.

Publish `release.json` with restart protocol version 1 and replace the latest-release marker atomically. A running release checks for newer verified releases and updates automatically after two seconds without input, with no pending file work, pointer drag, inline rename, dialog or feedback activity. Save the workspace before handing control to a replacement. The replacement must restore state and announce readiness before the old process closes, and must wait for the old process to exit before showing its own window. Failed preparation keeps the current window usable. Keep normal user startup separate from a strict restart: an invalid restart session must not silently become a new empty session.

Run process integration checks with the real `App`, lifecycle controller, session store and process adapter under Avalonia.Headless. Use isolated release trees and session files. Verify source-process exit, replacement state, startup failure, and zero native application windows. These checks do not verify native monitor placement or desktop focus behavior.

Automatic release changes must keep the command draft, history, caret and selection without executing the draft. Command-line focus delays automatic updates. Programs opened through Windows ShellExecute have their own lifetime.

UC39 feedback comments must identify the element, directory and release. Save comments immediately with the session and compile `refinements.md` next to it. Comment mode must not activate the selected control. Verify comment entry, cancellation, removal, disk persistence and preservation during a real process handoff.

Run the application against the user's chosen directories. Do not generate demonstration directories. Automated tests still use isolated fixtures.

Open a visible application window only when the user asks. The user authorized automatic replacement of an already running release when idle; this is application behavior and does not authorize the agent to control the desktop. While the user reviews an iteration, wait for feedback before continuing development or controlling the application.
