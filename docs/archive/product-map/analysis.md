# Source analysis: value, ownership, and change impact

[Catalogue](README.md) · [Interactive map](map.html) · [Every source file](source-index.md)

The main ownership question is the shared panel and window state. `PanelView.cs` directly serves 11 stories from 4 epics. Across partial files, `CommanderWindow` serves 25 stories from 8 epics and `PanelView` serves 17 from 7. Examine these places before the next workflow change. A count is a reason to inspect ownership; it is not a defect score.

The catalogue contains **11 epics and 88 stories: 58 user, 5 user-developer, and 25 developer stories**. It covers **187 C# files and 24 auxiliary source inputs**. Source anchors and file coverage are checked automatically. Product docs were compared with the code after the code review.

## 1. What the product offers

The product has 8 kinds of user value: move between locations, organize files, search a tree, inspect contents, work with packages, use Windows tools from file context, work with Git changes, and continue a workspace across sessions. Feedback, release handoff, and the stable launcher form a separate value loop for the person who develops Xcmd while using it.

The 2 developer epics describe the other side of the code: reusable implementation boundaries and reliable delivery and maintenance. They explain the archive codecs, clipboard formats, Git processes, native shell integration, saved state, and build inputs as services for the user stories they enable.

The epic names are proposed product-documentation boundaries. Each story records the narrower implemented behavior.

## 2. Coverage and counting rules

| Project | C# files | C# physical lines |
| --- | ---: | ---: |
| Xcmd.Core | 29 | 1,707 |
| Xcmd.Desktop | 91 | 8,824 |
| Xcmd.Infrastructure | 67 | 4,518 |

File roles in this catalogue: **32 mixed product and technical**, **71 product/routing only**, **84 technical only**. These categories describe mapped responsibilities, not architecture compliance.

The 24 auxiliary files are three project files, four dependency lock files, ten icon assets, one file-color configuration, two grammar files, and four license files. They belong to technical story T25. Imported grammars and binary icons stay outside the C# line counts.

Each file/story relation is one of:

- **Direct implementation:** a product rule, interaction, or state projection. Both user and user-developer stories count here.
- **Routing:** a menu item, shortcut, or delegating entry point. This is shown separately.
- **Technical implementation:** an adapter, contract, reusable mechanism, or lifecycle service.
- **Resource:** an auxiliary build, configuration, or asset input that supports a developer story.

A file can contain several roles. For example, `PanelClipboardController` owns both paste/cut interaction policy and platform clipboard encoding. Classification follows the responsibility of the location, in any project. An Infrastructure location counts as direct implementation when it holds a product rule: the attributes a copy keeps, the Recycle Bin drive policy, the search encodings, the syntax limits, the editor, the Claude Code launch parameters, the ZIP entry rules, and the launcher's release choice.

Counts list each story once per file or partial family. Direct epic counts use the owning epic; related-epic cross-links are not counted. The report uses **five direct stories or three direct epics** as a review prompt.

## 3. Files with the most product responsibilities

| File | Lines | Direct stories | Direct epics | Routed stories | Technical stories |
| --- | ---: | ---: | ---: | ---: | ---: |
| [PanelView.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/PanelView.cs) | 691 | 11 | 4 | 0 | 1 |
| [CommanderWindow.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/CommanderWindow.cs) | 365 | 9 | 5 | 0 | 1 |
| [FileRow.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/FileRow.cs) | 91 | 8 | 3 | 0 | 1 |
| [FileOperationRunner.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Core/FileOperationRunner.cs) | 230 | 6 | 2 | 0 | 0 |
| [WorkspaceLifecycle.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/WorkspaceLifecycle.cs) | 195 | 6 | 4 | 0 | 2 |
| [FileViewingController.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/FileViewingController.cs) | 94 | 6 | 3 | 3 | 0 |
| [FileSearchWindow.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/FileSearchWindow.cs) | 311 | 5 | 1 | 2 | 0 |
| [CommanderWindow.Keyboard.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/CommanderWindow.Keyboard.cs) | 280 | 5 | 3 | 24 | 1 |
| [CommanderWindow.FileOperations.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/CommanderWindow.FileOperations.cs) | 87 | 5 | 2 | 0 | 0 |
| [CommanderWindow.Launching.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/CommanderWindow.Launching.cs) | 68 | 5 | 2 | 0 | 0 |
| [PanelView.SearchResults.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/PanelView.SearchResults.cs) | 117 | 3 | 3 | 0 | 0 |
| [CommanderWindow.Menu.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/CommanderWindow.Menu.cs) | 114 | 3 | 3 | 27 | 1 |
| [ArchivePath.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Core/ArchivePath.cs) | 37 | 3 | 3 | 0 | 1 |

### PanelView is the first ownership boundary to examine

`PanelView.cs` combines navigation recovery, sorting, selection, rename, folder measurements, name-match display, cut display, colors, and state restore with table construction. Several stories read or change the same rows, cursor, marks, scroll, and editor state. See the [direct stories and exact locations](source-index.md#xcmddesktop), especially [U01](epics/e01.md#u01), [U11](epics/e02.md#u11), [U12](epics/e02.md#u12), and [U57](epics/e08.md#u57).

Before extending a panel workflow, identify who owns selection and cursor state and who may replace rows. A useful next step is to isolate one coherent state transition at a time and keep the panel's focus and restoration rules explicit. More partial files alone keep the same ownership.

### FileRow is a small shared state projection

`FileRow.cs` has 91 lines and directly serves 8 stories from 3 epics: current item, marks, cut state, drop target, quick-name matches, file colors, Git status, and measured sizes. A high story count is expected for a row projection. It becomes a risk if operation orchestration or I/O enters this object. Keep its small display-state role. See [FileRow:8](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/FileRow.cs#L8).

### WorkspaceLifecycle connects different kinds of time and saved state

`WorkspaceLifecycle.cs` directly touches recent and frequent navigation, immediate preference saves, ordinary session restore, feedback refresh, and release handoff. It also owns timers and save coordination. Its 6 direct stories span 4 epics. An error or ordering change here can affect both normal file work and the development feedback loop. See [T21](epics/e11.md#t21), [T22](epics/e11.md#t22), and [UD04](epics/e09.md#ud04).

Before adding another periodic feature, decide whether it needs the workspace lifetime, the issue-store lifetime, or the release-handoff lifetime.

### High counts can still describe one cohesive engine

`FileOperationRunner.cs` serves 6 direct stories, and all of them use the same sequential operation protocol: captured request, inventory, conflict or error decision, execution, and outcome. ZIP creation and extraction reuse that protocol. This is one shared domain mechanism. `FileSearchWindow.cs` has 5 direct stories in 1 owning epic. These are lower-priority ownership concerns than the panel and window families.

`FileViewingController.cs`, `CommanderWindow.FileOperations.cs`, and `CommanderWindow.Launching.cs` are small coordinators with several product decisions. Their counts are change-impact warnings. `ArchivePath.cs` reaches 3 epics because archive opening, displayed paths, and read-only messages share it; see finding D.

## 4. Partial classes change the conclusion

| Family | Files | Lines | Direct stories | Direct epics |
| --- | ---: | ---: | ---: | ---: |
| CommanderWindow | 11 | 1,221 | 25 | 8 |
| PanelView | 8 | 1,055 | 17 | 7 |
| FileViewerWindow | 10 | 1,181 | 10 | 3 |
| FileSearchWindow | 2 | 436 | 5 | 1 |
| GitHistoryController | 2 | 226 | 5 | 1 |
| DirectoryNavigationDialog | 3 | 373 | 3 | 1 |

The three large UI families contain **3,457 lines**, about **39% of Desktop C# source**. `CommanderWindow` spans 8 product epics and `PanelView` spans 7. `FileViewerWindow` has 1,181 lines, but most of its behavior belongs to the reader epic. Its other direct responsibilities are read-only Git origins and syntax feedback.

Partial files make individual files easier to navigate; the class still has one state owner. Family totals are the better measure when you ask how many kinds of work can change one live UI object. A family count is a union of story IDs.

## 5. Routing and schema can produce misleading hotspots

| File | Direct stories | Routed stories |
| --- | ---: | ---: |
| [CommanderWindow.Menu.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/CommanderWindow.Menu.cs) | 3 | 27 |
| [CommanderWindow.Keyboard.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/CommanderWindow.Keyboard.cs) | 5 | 24 |
| [FileViewerWindow.Menu.cs](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/FileViewerWindow.Menu.cs) | 2 | 10 |

The main menu exposes many workflows and implements little of them. Its parent-navigation policy and read-only Git action guard are counted directly; its menu registrations are routing. The keyboard file also contains real policy: the other-panel target choice, reload behavior, and committing a rename before Tab switches panels. Those actions are counted directly.

`WorkspaceState.cs` serializes many features. Here the schema and validation belong to T21; only its explicit idle-restart policy is direct evidence for UD04. Feedback-ID declarations throughout the UI are dependencies on T19.

## 6. Concrete findings

### A. Feedback storage is a startup dependency on the repository layout

**Observed deployment constraint.** Normal startup calls `IssueStore.ForApplication`, which searches ancestors for `Xcmd.slnx` and throws when it cannot find the repository. Feedback storage is therefore part of the standard startup path. This fits a personal local-development app and constrains distribution as a standalone file manager. See [Program:45](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/Program.cs#L45) and [IssueStore:17](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/IssueStore.cs#L17). A different injected lifecycle can supply another store.

### B. Several Git process adapters have different policies

**Observed duplication; possible behavior drift.** Status and commit-root lookup use five-second limits, search scope uses ten seconds, and history uses thirty seconds. Different deadlines can be appropriate. More significant: the history, status, and commit-root adapters remove inherited `GIT_*` variables, while search scope only sets the optional-lock and prompt variables. A process started with a custom Git environment can therefore use a different repository context for search and for status. This is a static risk; no failure was reproduced. Compare [SearchGitScope:76](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/SearchGitScope.cs#L76), [GitHistoryProcess:29](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/GitHistoryProcess.cs#L29), [GitStatusReader:102](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/GitStatusReader.cs#L102), and [ClaudeCommitLauncher:45](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/ClaudeCommitLauncher.cs#L45).

If this boundary changes, share the process and environment mechanism and keep the caller-specific timeout and failure policies.

### C. Completion and launch read the environment differently

**Observed policy difference; possible inconsistency.** Completion reads the inherited process environment and PATH. Windows launch refreshes the user and system environment first. After environment changes outside Xcmd, a command can become launchable before completion offers it, or completion can suggest an obsolete target. See [CommandCompletions:13](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/CommandCompletions.cs#L13) and [WindowsLaunchEnvironment](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/WindowsLaunchEnvironment.cs#L1).

### D. ArchivePath also acts as a general virtual-path utility

**Observed naming and representation coupling.** `ArchivePath.IsEntry` accepts any path that contains `|`, including Git locations. Several methods handle Git as a special case, and `ArchivePath.ReadOnlyMessage` chooses between archive and Git messages. The class represents more than archives. See [ArchivePath:5](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Core/ArchivePath.cs#L5) and [ArchivePath:30](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Core/ArchivePath.cs#L30).

A future virtual-location change should name physical, archive, and Git kinds explicitly within the three production projects.

### E. "Verified release" means a passed check set

**Observed terminology.** `ReleaseFiles.IsVerified` requires a positive check total and zero failures. Smoke-checked feedback releases therefore count as verified. UD04 and UD05 use the term in this sense. See [ReleaseFiles:37](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Infrastructure/ReleaseFiles.cs#L37).

### F. Default color rules overlap on purpose

**Observed overlap.** `.cmd`, `.bat`, `.sh`, and `.ps1` appear in both the source and the executable rules. First-match precedence gives them the source color, as the use-case document describes. Automatic deduplication would change an accepted product rule. See [file-types.json](https://github.com/xoposhiy/xcmd/blob/e22adee899ad68a59da71a9e0eb853ea9a11f882/src/Xcmd.Desktop/config/file-types.json#L1) and [U08](epics/e01.md#u08).

## 7. Boundaries that matter when describing the product

- **Search results are snapshots.** Refresh updates the matched entries without running the query again. The result panel supports source operations; file creation and paste need a regular folder. [U30](epics/e03.md#u30)
- **Archive browsing and archive search have different depth.** Browsing opens nested archives; search reports nested archives as skipped. [U26](epics/e03.md#u26), [U39–U42](epics/e05.md)
- **The viewer is a bounded reader.** It reads file windows and streams searches. Copy and Select all apply to the loaded document window. [U31–U38](epics/e04.md)
- **Git work is inspection plus a commit handoff.** Reads keep the checkout unchanged; diffs are text; commit comparison uses the first parent; Claude Code performs the commit. [U49–U56](epics/e07.md)
- **Two-panel comparison opens VS Code.** The two cursor files must exist on disk. Panel order determines diff order, and marks do not select the pair. This is separate from a Git diff. [U58](epics/e06.md#u58)
- **Cancellation keeps completed changes.** Copy, move, and ZIP creation reject links; size calculation skips them; search lists them as failed paths with the reason Link. [U15](epics/e02.md#u15), [U22](epics/e02.md#u22)
- **Workspace restore covers panels, search, and preferences.** Viewers and the operation queue start empty. A new build starts as a replacement process. [U57](epics/e08.md#u57), [UD04](epics/e09.md#ud04)

## 8. Where to start a follow-up review

1. Before adding another panel action, document the owner of cursor, marks, row replacement, inline edit, and restoration state. Inspect the `PanelView` and `CommanderWindow` families together.
2. Before distributing outside the repository, decide where the feedback store lives and whether startup needs it.
3. Before changing Git search scope or command completion, compare the environment and failure policies in findings B and C.

The catalogue explains implemented value from source. Its validation checks completeness and references; runtime correctness, usability, and regression coverage are separate checks.
