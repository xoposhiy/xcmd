# Source analysis: value, ownership, and change impact

[Catalogue](README.md) · [Interactive map](map.html) · [Every source file](source-index.md)

The main ownership question is the shared panel and window state. `PanelView.cs` directly serves {{file src/Xcmd.Desktop/PanelView.cs direct}} stories from {{file src/Xcmd.Desktop/PanelView.cs direct_epics}} epics. Across partial files, `CommanderWindow` serves {{family Xcmd.Desktop/CommanderWindow direct}} stories from {{family Xcmd.Desktop/CommanderWindow direct_epics}} epics and `PanelView` serves {{family Xcmd.Desktop/PanelView direct}} from {{family Xcmd.Desktop/PanelView direct_epics}}. Examine these places before the next workflow change. A count is a reason to inspect ownership; it is not a defect score.

The catalogue contains **{{count epics}} epics and {{count stories}} stories: {{count user_stories}} user, {{count user_developer_stories}} user-developer, and {{count developer_stories}} developer stories**. It covers **{{count code_files}} C# files and {{count aux_files}} auxiliary source inputs**. Source anchors and file coverage are checked automatically. Product docs were compared with the code after the code review.

## 1. What the product offers

The product has {{count user_epics}} kinds of user value: move between locations, organize files, search a tree, inspect contents, work with packages, use Windows tools from file context, work with Git changes, and continue a workspace across sessions. Feedback, release handoff, and the stable launcher form a separate value loop for the person who develops Xcmd while using it.

The {{count developer_epics}} developer epics describe the other side of the code: reusable implementation boundaries and reliable delivery and maintenance. They explain the archive codecs, clipboard formats, Git processes, native shell integration, saved state, and build inputs as services for the user stories they enable.

The epic names are proposed product-documentation boundaries. Each story records the narrower implemented behavior.

## 2. Coverage and counting rules

__PROJECT_TABLE__

__CLASSIFICATION__

The {{count aux_files}} auxiliary files are three project files, four dependency lock files, ten icon assets, one file-color configuration, two grammar files, and four license files. They belong to technical story T25. Imported grammars and binary icons stay outside the C# line counts.

Each file/story relation is one of:

- **Direct implementation:** a product rule, interaction, or state projection. Both user and user-developer stories count here.
- **Routing:** a menu item, shortcut, or delegating entry point. This is shown separately.
- **Technical implementation:** an adapter, contract, reusable mechanism, or lifecycle service.
- **Resource:** an auxiliary build, configuration, or asset input that supports a developer story.

A file can contain several roles. For example, `PanelClipboardController` owns both paste/cut interaction policy and platform clipboard encoding. Classification follows the responsibility of the location, in any project. An Infrastructure location counts as direct implementation when it holds a product rule: the attributes a copy keeps, the Recycle Bin drive policy, the search encodings, the syntax limits, the editor, the Claude Code launch parameters, the ZIP entry rules, and the launcher's release choice.

Counts list each story once per file or partial family. Direct epic counts use the owning epic; related-epic cross-links are not counted. The report uses **five direct stories or three direct epics** as a review prompt.

## 3. Files with the most product responsibilities

__HOTSPOT_TABLE__

### PanelView is the first ownership boundary to examine

`PanelView.cs` combines navigation recovery, sorting, selection, rename, folder measurements, name-match display, cut display, colors, and state restore with table construction. Several stories read or change the same rows, cursor, marks, scroll, and editor state. See the [direct stories and exact locations](source-index.md#xcmddesktop), especially [U01](epics/e01.md#u01), [U11](epics/e02.md#u11), [U12](epics/e02.md#u12), and [U57](epics/e08.md#u57).

Before extending a panel workflow, identify who owns selection and cursor state and who may replace rows. A useful next step is to isolate one coherent state transition at a time and keep the panel's focus and restoration rules explicit. More partial files alone keep the same ownership.

### FileRow is a small shared state projection

`FileRow.cs` has {{file src/Xcmd.Desktop/FileRow.cs lines}} lines and directly serves {{file src/Xcmd.Desktop/FileRow.cs direct}} stories from {{file src/Xcmd.Desktop/FileRow.cs direct_epics}} epics: current item, marks, cut state, drop target, quick-name matches, file colors, Git status, and measured sizes. A high story count is expected for a row projection. It becomes a risk if operation orchestration or I/O enters this object. Keep its small display-state role. See [[src/Xcmd.Desktop/FileRow.cs#public sealed class FileRow(]].

### WorkspaceLifecycle connects different kinds of time and saved state

`WorkspaceLifecycle.cs` directly touches recent and frequent navigation, immediate preference saves, ordinary session restore, feedback refresh, and release handoff. It also owns timers and save coordination. Its {{file src/Xcmd.Desktop/WorkspaceLifecycle.cs direct}} direct stories span {{file src/Xcmd.Desktop/WorkspaceLifecycle.cs direct_epics}} epics. An error or ordering change here can affect both normal file work and the development feedback loop. See [T21](epics/e11.md#t21), [T22](epics/e11.md#t22), and [UD04](epics/e09.md#ud04).

Before adding another periodic feature, decide whether it needs the workspace lifetime, the issue-store lifetime, or the release-handoff lifetime.

### High counts can still describe one cohesive engine

`FileOperationRunner.cs` serves {{file src/Xcmd.Core/FileOperationRunner.cs direct}} direct stories, and all of them use the same sequential operation protocol: captured request, inventory, conflict or error decision, execution, and outcome. ZIP creation and extraction reuse that protocol. This is one shared domain mechanism. `FileSearchWindow.cs` has {{file src/Xcmd.Desktop/FileSearchWindow.cs direct}} direct stories in {{file src/Xcmd.Desktop/FileSearchWindow.cs direct_epics}} owning epic. These are lower-priority ownership concerns than the panel and window families.

`FileViewingController.cs`, `CommanderWindow.FileOperations.cs`, and `CommanderWindow.Launching.cs` are small coordinators with several product decisions. Their counts are change-impact warnings. `ArchivePath.cs` reaches {{file src/Xcmd.Core/ArchivePath.cs direct_epics}} epics because archive opening, displayed paths, and read-only messages share it; see finding D.

## 4. Partial classes change the conclusion

__FAMILY_TABLE__

The three large UI families contain **{{count ui_family_lines}} lines**, about **{{count ui_family_percent}}% of Desktop C# source**. `CommanderWindow` spans {{family Xcmd.Desktop/CommanderWindow direct_epics}} product epics and `PanelView` spans {{family Xcmd.Desktop/PanelView direct_epics}}. `FileViewerWindow` has {{family Xcmd.Desktop/FileViewerWindow lines}} lines, but most of its behavior belongs to the reader epic. Its other direct responsibilities are read-only Git origins and syntax feedback.

Partial files make individual files easier to navigate; the class still has one state owner. Family totals are the better measure when you ask how many kinds of work can change one live UI object. A family count is a union of story IDs.

## 5. Routing and schema can produce misleading hotspots

__ROUTING_TABLE__

The main menu exposes many workflows and implements little of them. Its parent-navigation policy and read-only Git action guard are counted directly; its menu registrations are routing. The keyboard file also contains real policy: the other-panel target choice, reload behavior, and committing a rename before Tab switches panels. Those actions are counted directly.

`WorkspaceState.cs` serializes many features. Here the schema and validation belong to T21; only its explicit idle-restart policy is direct evidence for UD04. Feedback-ID declarations throughout the UI are dependencies on T19.

## 6. Concrete findings

### A. Feedback storage is a startup dependency on the repository layout

**Observed deployment constraint.** Normal startup calls `IssueStore.ForApplication`, which searches ancestors for `Xcmd.slnx` and throws when it cannot find the repository. Feedback storage is therefore part of the standard startup path. This fits a personal local-development app and constrains distribution as a standalone file manager. See [[src/Xcmd.Desktop/Program.cs#issues: IssueStore.ForApplication()]] and [[src/Xcmd.Infrastructure/IssueStore.cs#public static IssueStore ForApplication()]]. A different injected lifecycle can supply another store.

### B. Several Git process adapters have different policies

**Observed duplication; possible behavior drift.** Status and commit-root lookup use five-second limits, search scope uses ten seconds, and history uses thirty seconds. Different deadlines can be appropriate. More significant: the history, status, and commit-root adapters remove inherited `GIT_*` variables, while search scope only sets the optional-lock and prompt variables. A process started with a custom Git environment can therefore use a different repository context for search and for status. This is a static risk; no failure was reproduced. Compare [[src/Xcmd.Infrastructure/SearchGitScope.cs#GIT_OPTIONAL_LOCKS]], [[src/Xcmd.Infrastructure/GitHistoryProcess.cs#foreach (var key in start.Environment.Keys]], [[src/Xcmd.Infrastructure/GitStatusReader.cs#foreach (var key in start.Environment.Keys]], and [[src/Xcmd.Infrastructure/ClaudeCommitLauncher.cs#foreach (var key in start.Environment.Keys]].

If this boundary changes, share the process and environment mechanism and keep the caller-specific timeout and failure policies.

### C. Completion and launch read the environment differently

**Observed policy difference; possible inconsistency.** Completion reads the inherited process environment and PATH. Windows launch refreshes the user and system environment first. After environment changes outside Xcmd, a command can become launchable before completion offers it, or completion can suggest an obsolete target. See [[src/Xcmd.Infrastructure/CommandCompletions.cs#foreach (var name in Environment.GetEnvironmentVariables()]] and [WindowsLaunchEnvironment]({{count source_url}}src/Xcmd.Infrastructure/WindowsLaunchEnvironment.cs#L1).

### D. ArchivePath also acts as a general virtual-path utility

**Observed naming and representation coupling.** `ArchivePath.IsEntry` accepts any path that contains `|`, including Git locations. Several methods handle Git as a special case, and `ArchivePath.ReadOnlyMessage` chooses between archive and Git messages. The class represents more than archives. See [[src/Xcmd.Core/ArchivePath.cs#public static bool IsEntry(]] and [[src/Xcmd.Core/ArchivePath.cs#public static string ReadOnlyMessage(]].

A future virtual-location change should name physical, archive, and Git kinds explicitly within the three production projects.

### E. "Verified release" means a passed check set

**Observed terminology.** `ReleaseFiles.IsVerified` requires a positive check total and zero failures. Smoke-checked feedback releases therefore count as verified. UD04 and UD05 use the term in this sense. See [[src/Xcmd.Infrastructure/ReleaseFiles.cs#private static bool IsVerified(]].

### F. Default color rules overlap on purpose

**Observed overlap.** `.cmd`, `.bat`, `.sh`, and `.ps1` appear in both the source and the executable rules. First-match precedence gives them the source color, as the use-case document describes. Automatic deduplication would change an accepted product rule. See [file-types.json]({{count source_url}}src/Xcmd.Desktop/config/file-types.json#L1) and [U08](epics/e01.md#u08).

## 7. Boundaries that matter when describing the product

- **Search results are snapshots.** Refresh updates the matched entries without running the query again. The result panel supports source operations; file creation and paste need a regular folder. [U30](epics/e03.md#u30)
- **Archive browsing and archive search have different depth.** Browsing opens nested archives; search reports nested archives as skipped. [U26](epics/e03.md#u26), [U39–U42](epics/e05.md)
- **The viewer is a bounded reader.** It reads file windows and streams searches. Copy and Select all apply to the loaded document window. [U31–U38](epics/e04.md)
- **Git work is inspection plus a commit handoff.** Reads keep the checkout unchanged; diffs are text; commit comparison uses the first parent; Claude Code performs the commit. [U49–U56](epics/e07.md)
- **Cancellation keeps completed changes.** Copy, move, and ZIP creation reject links; size calculation skips them; search lists them as failed paths with the reason Link. [U15](epics/e02.md#u15), [U22](epics/e02.md#u22)
- **Workspace restore covers panels, search, and preferences.** Viewers and the operation queue start empty. A new build starts as a replacement process. [U57](epics/e08.md#u57), [UD04](epics/e09.md#ud04)

## 8. Where to start a follow-up review

1. Before adding another panel action, document the owner of cursor, marks, row replacement, inline edit, and restoration state. Inspect the `PanelView` and `CommanderWindow` families together.
2. Before distributing outside the repository, decide where the feedback store lives and whether startup needs it.
3. Before changing Git search scope or command completion, compare the environment and failure policies in findings B and C.

The catalogue explains implemented value from source. Its validation checks completeness and references; runtime correctness, usability, and regression coverage are separate checks.
