# Which technical stories earn a place in Xcmd?

Review of the working tree on 19 November 2026. The two developer epics contain 31 stories. This review covers the 29 remaining after setting aside the two already discussed, `AsyncCoordination` and `ApplicationLifecycle`. The six stories in the user-developer epic describe people using Xcmd. They can also be consumers of shared modules.

**Recommendation.** Retire four more technical cards, narrow two, keep fifteen for shared product infrastructure and keep eight as workflows for people who develop Xcmd. These are proposed catalogue edits.

## How to decide

Name the module, the capability it provides and two distinct current stories that use that capability. Trace both paths in the source. A shared call through the same viewer or panel can serve two real needs. Several windows calling a helper for the same need still count as one need. An annotation, a future feature and a test of the first consumer do not establish a second consumer.

Map the shared module to its technical story. Map the code that decides what a specific feature does to that feature. A file that mixes both responsibilities still needs its direct feature links. For a developer using a tool, ask a different question: what can this person do, and what result do they get?

## Four cards to retire

| Card | Evidence | Where the responsibility goes |
| --- | --- | --- |
| **DomainBoundaries** | The three-project rule constrains the whole codebase. Its mapped files declare projects, dependencies and forbidden APIs. There is no shared operation called by two features. [Build rules](../../Directory.Build.props) | Keep the dependency rule in the project instructions and its enforcement under `QualityChecks`. Preserve the original decision and source link. |
| **ClipboardInterop** | `ClipboardTransfer` reads and writes file lists and the preferred drop effect. `ReusePaths` copies plain text directly. The file-list methods are private to the controller that also chooses entries, enqueues jobs and reports results. The two features share a controller, but only one uses the claimed capability. [PanelClipboardController](../../src/Xcmd.Desktop/PanelClipboardController.cs) | Keep both direct user-story links on the controller. Copying selected text in the viewer also uses a different clipboard operation. |
| **DirectoryMonitoring** | The ordinary file panel uses `DirectoryMonitor` for `RefreshEntries`. The live Git panel takes a different polling branch. Saved locations call `DirectoryAvailability.MissingAsync`, which does its own check. The third mapped helper just supplies the home directory. [Refresh paths](../../src/Xcmd.Desktop/DirectoryUpdates.cs) · [Saved locations](../../src/Xcmd.Desktop/DirectoryNavigationDialog.Locations.cs) · [Home directory](../../src/Xcmd.Infrastructure/UserDirectories.cs) | Give the watcher to `RefreshEntries` and the availability check to the saved-location workflow. Review the home-directory helper with its panel caller. |
| **WindowsIdentity** | The main, search and viewer windows all set the same Windows identity. Across those windows the user gets the same promise: a pinned shortcut still opens Xcmd after a version change. That promise is already explicit in `StableShortcut`. [TaskbarIdentity](../../src/Xcmd.Desktop/TaskbarIdentity.cs) · [Shortcut agreement](../stories/product-feedback.md#stableshortcut) | Fold the pinning and icon decisions into `StableShortcut`, preserving their sources. Keep the shared Windows implementation. A second independently specified taskbar feature would warrant another look. |

## Two cards with a narrower shared capability

| Card | What passes the rule | What belongs elsewhere |
| --- | --- | --- |
| **WindowsFileActions** | File-lock diagnostics are shared: `RenameEntry` wraps a rename, and `TransferEntries` wraps a copy in the same diagnostic helper. Recommend a narrower card for finding why Windows denied a file operation. [Rename caller](../../src/Xcmd.Infrastructure/LocalFileSystem.cs) · [Copy caller](../../src/Xcmd.Infrastructure/LocalFileOperations.cs) · [Shared helper](../../src/Xcmd.Infrastructure/FileLockDiagnostics.cs) | Review drag and drop, shell menus, recycling and copy metadata separately. Being Windows APIs gives them no common consumer. Put feature-specific adapters under their features and retain a technical link only for a demonstrated shared part. |
| **ReleaseHandoff** | `ReleaseFiles.FindLatestAsync` validates the published build. The permanent launcher uses it for `StableShortcut`, and the running app uses it through `ReleaseCatalog` for `TryLocalRelease`. Keep a technical card about locating a verified release. [Launcher](../../src/Xcmd.Infrastructure/BootstrapProgram.cs) · [Running app](../../src/Xcmd.Infrastructure/ReleaseCatalog.cs) · [Release checks](../../src/Xcmd.Infrastructure/ReleaseFiles.cs) | The idle switch belongs to `TryLocalRelease`. Restoring readers and panel state belongs to `RestoreWorkspace`. Preserve publishing and retention decisions in the delivery process. |

## Fifteen shared modules to keep

Each row gives two current consumers. The suggested boundary limits what the technical story owns.

| Card | Shared capability and evidence | Two uses |
| --- | --- | --- |
| **FileSystemAccess** | Read directory entries through the same filesystem contract. [Panel session](../../src/Xcmd.Core/PanelSession.cs) · [Size calculator](../../src/Xcmd.Core/DirectorySizeCalculator.cs) | `NavigatePanels` lists a location. `MeasureFolders` walks its descendants. |
| **ExternalLaunch** | Start external programs through the Windows launch adapter. [Launch commands](../../src/Xcmd.Desktop/CommanderWindow.Launching.cs) · [Adapter](../../src/Xcmd.Infrastructure/WindowsExternalLauncher.cs) | `OpenExternalTool` opens an entry. `RunCommand` starts a typed command. |
| **ArchiveAccess** | Read archive entries through the archive-content service. [Panel adapter](../../src/Xcmd.Infrastructure/ArchiveFileSystem.cs) · [Search adapter](../../src/Xcmd.Infrastructure/FileSearchSource.cs) | `BrowseArchives` lists members. `FindFiles` enumerates members to match their names. |
| **SearchSources** | Supply search candidates and access to their contents, including archive members. [Source](../../src/Xcmd.Infrastructure/FileSearchSource.cs) · [Search session](../../src/Xcmd.Core/FileSearchSession.cs) | `FindFiles` matches entries by name. `FindFileText` reads candidate text. Keep matching and scheduling decisions with their features. |
| **RepositoryStatus** | Read repository context and parse Git status records. [Panel reader](../../src/Xcmd.Infrastructure/GitStatusReader.cs) · [Viewer](../../src/Xcmd.Infrastructure/ViewerGitDiff.cs) | `SeeRepositoryState` marks panel entries. `ReadWorkingFileDiff` determines the viewed file's state. |
| **RepositoryLocations** | Present Git paths as directories and entries through the filesystem interface. [GitFileSystem](../../src/Xcmd.Infrastructure/GitFileSystem.cs) | `BrowseHistory` lists revisions. `BrowseSnapshot` navigates the tree at one revision. |
| **RepositoryDocuments** | Produce files the viewer can open for historical contents and Git reports. [Producer](../../src/Xcmd.Infrastructure/GitHistoryDocuments.cs) · [Viewer commands](../../src/Xcmd.Desktop/GitHistoryController.Viewing.cs) | `InspectCommitChanges` can inspect a changed file version. `ReadDiff` opens the patch for a change. |
| **StreamingReads** | Provide bounded reads and common text encoding rules. [Viewer files](../../src/Xcmd.Infrastructure/ViewerFiles.cs) · [Text search](../../src/Xcmd.Infrastructure/FileTextSearch.cs) | `ReadLargeFiles` reads the current window. `FindViewerText` searches beyond it using the selected encoding. |
| **SyntaxSpans** | Return text ranges and their syntax kinds. [Highlighter](../../src/Xcmd.Infrastructure/Syntax/SyntaxHighlighter.cs) · [File view](../../src/Xcmd.Desktop/FileViewer.Syntax.cs) · [Diff view](../../src/Xcmd.Desktop/ViewerDiffSyntax.cs) | `HighlightSource` colors a file. `ReadWorkingFileDiff` maps ranges from the working file, index and HEAD onto diff rows. The deferred export feature plays no part in this decision. |
| **SharedControls** | Supply common default-button and focus behavior. [Transfer dialog](../../src/Xcmd.Desktop/FileTransferDialog.cs) · [Archive dialog](../../src/Xcmd.Desktop/ZipNameDialog.cs) | `TransferEntries` confirms a transfer. `CreateArchive` confirms an archive name. The choice of operation stays in each dialog. |
| **FileIcons** | Resolve and cache system icons for entry rows. [Cache](../../src/Xcmd.Desktop/FileIconCache.cs) · [Name cell](../../src/Xcmd.Desktop/FileNameCell.cs) · [Result rows](../../src/Xcmd.Desktop/PanelView.SearchResults.cs) | `NavigatePanels` shows directory entries. `SearchResultPanel` shows matches from several directories through the same row renderer. Inline rename and marking in that renderer still own their feature links. |
| **ImageDecoding** | Decode an image for the shared reader with a size limit. [Decoder](../../src/Xcmd.Desktop/ViewerImage.cs) · [Viewer entry points](../../src/Xcmd.Desktop/FileViewingController.cs) · [Quick view agreement](../stories/file-inspection.md#quickviewfile) | `ChooseViewMode` displays an image in the viewer. `QuickViewFile` previews the selected image in the opposite panel while the user moves through files. |
| **KeyboardLayouts** | Translate input across installed keyboard layouts. [Name search](../../src/Xcmd.Desktop/CommanderWindow.QuickJump.cs) · [Command recovery](../../src/Xcmd.Infrastructure/CommandLayoutRecovery.cs) | `JumpToName` matches a file name. `RunCommand` retries a mistyped command under another layout. |
| **WorkspaceStorage** | Save and load workspace data across sessions. [State model](../../src/Xcmd.Core/WorkspaceState.cs) · [Store](../../src/Xcmd.Infrastructure/SessionStore.cs) · [Session owner](../../src/Xcmd.Desktop/WorkspaceLifecycle.cs) | `FavoriteLocations` retains named destinations. `RestoreWorkspace` restores panel locations and selection after an update. |
| **FeedbackIdentity** | Resolve a selected control to a stable source ID. [Resolver](../../src/Xcmd.Desktop/FeedbackTarget.cs) · [Feedback entry points](../../src/Xcmd.Desktop/FeedbackController.cs) | `ReportControl` records the chosen target. `ReportHighlighting` attaches a source sample and uses the same target resolution when creating the note. |

## Eight stories where the developer uses a tool

These cards describe actions a developer or their agent takes. Judge the card by the result of that action. Keep any shared test drivers mapped to the tool and keep feature-specific assertions mapped to the feature.

| Card | Outcome and evidence |
| --- | --- |
| **PackageInputs** | A developer can reproduce a Windows build from declared inputs. Keep the build workflow as the story and place technology and platform choices in build guidance. [Build environment](../../scripts/BuildEnvironment.ps1) · [Dependency configuration](../../NuGet.Config) |
| **FeedbackQueue** | An agent can claim a feedback item and see who owns it before working on it. The CLI exposes that workflow. The underlying store also serves note creation and inspection. [Issue CLI](../../src/Xcmd.Infrastructure/IssueCli.cs) · [Issue store](../../src/Xcmd.Infrastructure/IssueStore.cs) |
| **HeadlessVerification** | An agent can drive a UI scenario and inspect a failure without taking over the desktop. [Input driver](../../tests/Xcmd.Probe/Infrastructure/InputDriver.cs) · [Runner](../../scripts/Test-Xcmd.ps1) |
| **IntegrationVerification** | A developer can verify a real process or platform interaction in isolation. Keep this separate from headless UI input while it checks a failure that requires the real boundary. [Process fixture](../../tests/Xcmd.Probe/Infrastructure/RunningApplication.cs) · [Launch probe](../../tests/Xcmd.LaunchProbe/Program.cs) |
| **QualityChecks** | A developer runs checks and gets a specific violation before delivery. [Architecture gate](../../scripts/Test-Architecture.ps1) · [Metrics gate](../../scripts/Test-Metrics.ps1) |
| **BrowseStories** | A developer reads needs in catalogue order in Story Explorer. [Catalogue reader](../../tools/story-explorer/catalogue.py) · [Explorer](../../tools/story-explorer/index.html) |
| **TraceStoryFiles** | A developer follows a story to its code or tests and inspects where responsibilities overlap. [Repository reader](../../tools/story-explorer/repository.py) · [Explorer reports](../../tools/story-explorer/reports.js) |
| **CodeHealth** | A developer looks at measurements to decide where to investigate. This remains a useful task even when every automated check passes. [Health report command](../../scripts/Report-CodeHealth.ps1) |
