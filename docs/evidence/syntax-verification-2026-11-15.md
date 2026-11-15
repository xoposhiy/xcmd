# Syntax highlighting verification

Date: 2026-11-15. Scope: UC09/UC10 syntax reading and direct highlighting
reports. The user requested a commit. This record covers targeted verification;
the full regression suite was not requested or run.

## Fixed source and checks

The checked snapshot used detached HEAD `34199e2` plus the feature changes in
`C:/work/projects/xcmd-checks/syntax-final-20261115`. Build outputs and fixtures
were isolated from the development checkout. SHA-256 comparison confirmed that
all 32 changed source, project, dependency and test files matched the development
checkout. The local manifest is `artifacts/syntax-final-source-manifest.json`.
Later edits only completed this documentation.

The hidden PowerShell helper ran this command with the repository's .NET SDK:

```powershell
scripts/Test-Xcmd.ps1 -Filter 'syntax-,viewer-text-search-edit-return,viewer-encodings-and-page-boundaries,viewer-binary-large-and-empty,viewer-images-and-fallback,viewer-whole-file-incremental-search,viewer-reading-menu-search-layout,viewer-read-failure-and-keyboard-recovery,viewer-continuous-scroll-end-and-status,viewer-native-editor-boundary,design-system-reader-contrast,feedback-double-click-runs-one-click,issues-ui-save-failure-keeps-draft,issues-ui-append-keeps-external-changes,priority-viewer-follow,archive-summary-and-shell-command-sequence,git-history-file-and-diff' -OutputPath artifacts/probe/syntax-final
```

All 24 scenarios passed. The result file is
`C:/work/projects/xcmd-checks/syntax-final-20261115/artifacts/probe/syntax-final/results.json`.
The local run log is `artifacts/syntax-final-check.log`.

Static checks passed: launcher dependency isolation, project/package policy,
11 architecture rules and deliberate violations, analyzer self-checks with
14 violations, nine feedback-ID compiler fixtures, C# formatting, code metrics
and duplicate detection. The Release / Windows x64 build reported no warnings
or errors. No quality limits or checks were disabled.

## Selected scenarios

| Scenario | Reason for selection |
| --- | --- |
| `syntax-language-grammars` | Requested languages, embedded HTML/Markdown code, token roles and selected spans. |
| `syntax-page-context-and-limits` | Multiline state across pages, Unicode encodings and bounded fallback. |
| `syntax-viewer-selection-and-report` | All nine palettes, search, language selection, text/hex switching and saved report context. |
| `syntax-report-current-selection-and-copy` | Current selection after a menu, plain-text Unicode copy, keyboard/pointer reporting, cancellation and visible save confirmation. |
| `syntax-scroll-and-replacement-during-load` | In-page scrolling during a pending read and cancellation by a replacement load. |
| `syntax-follow-edits-and-rendering` | Syntax after append/truncate and offscreen 100/150% rendering. |
| `syntax-archive-and-git-sources` | Real ZIP entry and committed Git source use the same syntax path. |
| `syntax-failure-cancellation-and-detection` | Missing grammar, cancellation, longest extension and case-insensitive aliases. |
| `viewer-text-search-edit-return` | Existing viewer search and editor handoff. |
| `viewer-encodings-and-page-boundaries` | Reader decoding and Unicode page boundaries. |
| `viewer-binary-large-and-empty` | Bounded loading and non-source content. |
| `viewer-images-and-fallback` | Other viewer content paths remain usable. |
| `viewer-whole-file-incremental-search` | Search across pages with the custom text presenter. |
| `viewer-reading-menu-search-layout` | Menus, reading layout and search controls. |
| `viewer-read-failure-and-keyboard-recovery` | Recovery from read failures. |
| `viewer-continuous-scroll-end-and-status` | Viewport and adjacent-page movement. |
| `viewer-native-editor-boundary` | Production Windows editor-launch adapter. |
| `feedback-double-click-runs-one-click` | Shared feedback input routing. |
| `archive-summary-and-shell-command-sequence` | Existing archive viewing and command flow. |
| `design-system-reader-contrast` | Existing reader selection contrast across palettes. |
| `git-history-file-and-diff` | Existing Git history file and diff viewing. |
| `priority-viewer-follow` | Existing follow-mode append, pause and truncate behavior. |
| `issues-ui-append-keeps-external-changes` | Concurrent changes to the shared issue store. |
| `issues-ui-save-failure-keeps-draft` | A failed save preserves the user's report. |

## Ready build

The updated local release is `20261115-185443-007-11b678f6`, reached through the
permanent `C:/work/projects/xcmd/Xcmd.exe` launcher. The previous release remains
available. The source files still matched the checked snapshot before publishing.

```powershell
scripts/Publish-Feedback.ps1 -Filter 'syntax-report-current-selection-and-copy,syntax-scroll-and-replacement-during-load,syntax-viewer-selection-and-report,viewer-reading-menu-search-layout'
```

All four smoke scenarios passed. Package audit and locked restore passed. This
verification took 76.6 seconds, including a 45.3-second rebuild and about
29 seconds in the four scenarios. The log reports the exceeded 60-second target.
The release records `verificationPhase: feedback-smoke` and
`fullVerification: false`; the 24-scenario finalization evidence above remains
separate. Published output includes TextMateSharp, its grammar package, the
native Onigwrap dependency and grammar licenses. Launcher installation and the
latest-release marker update completed without opening a visible window.

## Usability and limits

The [independent usability review](syntax-usability-review-2026-11-15.md) is
complete. It found stale report selection, cancellation during in-page scrolling
and missing save confirmation in the originating viewer. The implementation and
passing regression scenarios address all three findings. Offscreen rendering
and assertions cover 100% and 150% scale; both images were visually inspected.

These checks use real files, archives, Git repositories, SQLite and the native
grammar engine. UI checks use Avalonia.Headless and Skia; the result records zero
native application windows. Headless clipboard checks cover the application's
plain-text copy path, not the Windows clipboard backend. Native input delivery,
monitor transitions and GPU rendering are outside this evidence. Regex time
budgets are cooperative, not hard process limits. Rich clipboard and image copy
remain deferred.
