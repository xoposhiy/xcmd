"""Reviewed story definitions and source anchors. Run build.py to resolve locations."""

EPICS = [
    ("E01", "Move between working locations", "user", "As a user, I want to move between working locations with little navigation effort so that I can keep my attention on the files I need.", "Adds orientation, location recall, and readable panels. File changes belong to E02."),
    ("E02", "Organize files across locations", "user", "As a user, I want to organize files across locations so that I can keep useful files in the right place with control over changes.", "Adds selection, file changes, and operation decisions to navigation. Archive packaging belongs to E05."),
    ("E03", "Find files and text in a directory tree", "user", "As a user, I want to find files and text in a directory tree so that I can act on relevant results without knowing their exact locations.", "Adds discovery across directories. Jumping within the current list belongs to E01; reading a known file belongs to E04."),
    ("E04", "Understand file contents", "user", "As a user, I want to inspect file contents so that I can understand a file before choosing another tool or changing it.", "Adds read-only inspection of known files, including text, binary data, and images."),
    ("E05", "Work with packaged files", "user", "As a user, I want to inspect, unpack, and package files so that I can work with content delivered as archives.", "Adds archive navigation, extraction, and ZIP creation. General transfer decisions belong to E02."),
    ("E06", "Use other tools from the current location", "user", "As a user, I want to use Windows programs and commands from my current file context so that I can continue work in the right tool.", "Adds external launch, command entry, path export, and Windows shell actions."),
    ("E07", "Work with repository changes", "user", "As a user, I want to inspect repository changes and history and start a commit from Xcmd so that I can understand and finish repository work.", "Adds Git status, history, snapshots, reports, diffs, and a commit handoff to Claude Code."),
    ("E08", "Continue work across application sessions", "user", "As a user, I want to continue file work across application sessions so that closing the application does not force me to rebuild my workspace.", "Adds saved workspace state. Moving to a new local build belongs to E09."),
    ("E09", "Improve Xcmd while using it", "user-developer", "As a user-developer, I want to report problems and try new local builds while using Xcmd so that development stays connected to real work.", "Adds feedback on interface elements, syntax reports, release handoff, and a stable launcher for changing releases."),
    ("E10", "Implement workflows over reusable boundaries", "developer", "As a developer, I want to implement workflows over explicit domain and platform boundaries so that new stories can reuse file, search, repository, and presentation services.", "Adds technical capabilities and contracts. An adapter belongs here even when one user story is its only current consumer."),
    ("E11", "Deliver and maintain a reliable application", "developer", "As a developer, I want to package, start, diagnose, and maintain Xcmd consistently so that new workflows can reach the running application safely.", "Adds build inputs, startup, saved state, releases, feedback identities, and the feedback work queue."),
]

STORIES = []


def loc(file, anchor=None, note="", role=None):
    return {"file": file, "anchor": anchor, "note": note, "role": role}


def route(file, anchor, note="Command dispatch only"):
    return loc(file, anchor, note, "routing")


def story(id, epic, title, want, why, behavior, refs, enabled=(), related=()):
    STORIES.append(dict(id=id, epic=epic, title=title, want=want, why=why,
                        behavior=behavior, refs=refs, enabled=list(enabled), related=list(related)))


# E01: Move between working locations

story("U01", "E01", "Navigate two panels without losing the current item",
      "move through two directory panels with the keyboard or mouse", "I can keep source and destination locations in view",
      "Enter or a double-click opens the current item. Backspace and Ctrl+PageUp open the parent and select the folder just left. Ctrl+` opens the home folder and Ctrl+\\ opens the drive root. Tab switches the active panel; a top strip marks it. A missing directory recovers to the nearest existing parent.", [
          loc("C:PanelSession", "public sealed class", "Directory, cursor, history, and latest-load state"),
          loc("C:PanelRules", "ParentCursorName", "Cursor choice when returning to a parent"),
          loc("D:CommanderWindow", "public void Activate(", "Active and opposite panel"),
          loc("D:CommanderWindow", "private async Task OpenCurrentAsync(", "Open the selected location"),
          loc("D:CommanderWindow.Menu", "private static async Task OpenParentAsync(", "Parent navigation across location kinds"),
          loc("D:PanelView", "public async Task<bool> LoadAccessibleAsync(", "Missing-directory recovery"),
          loc("D:PanelView", "Table.DoubleTapped +=", "Open the item under the pointer"),
          loc("D:PanelView", "public void FocusCurrent(", "Cursor visibility and focus"),
          loc("D:PanelView", "public void SetActive(", "Active/inactive panel indication"),
          loc("D:FileRow", "public void SetCurrent(", "Current item and active cursor presentation"),
          loc("D:CommanderWindow.Keyboard", "private bool OpenDirectoryDestination(", "Home/root navigation outcome"),
          route("D:CommanderWindow.Keyboard", "private bool HandlePanelNavigation(", "Enter, parent, and cursor keys"),
          route("D:CommanderWindow.Keyboard", "private bool HandlePanelSwitch(", "Tab switches the panel")], ["T01", "T02", "T04", "T13", "T14"])

story("U02", "E01", "Open a location in the other panel", "show the current folder or the selected folder in the other panel", "I can prepare a destination while my source stays active",
      "Ctrl+Left and Ctrl+Right load the selected folder, or the current folder when a file is selected, into that panel. A selected archive opens its contents there. In a search-result panel the shortcut shows the file's folder with the file selected. Focus stays in the source panel.", [
          loc("D:CommanderWindow.Keyboard", "private void OpenInPanel(", "Target choice, archive opening, result location, and source focus"),
          route("D:CommanderWindow.Keyboard", "private bool HandleControlPanelCommand(", "Ctrl+Left/Right dispatch")], ["T01", "T06"], ["E05"])

story("U03", "E01", "Sort files in a predictable order", "sort the current list by name, extension, date, or size", "I can find and compare entries more easily",
      "A column header click or Ctrl+F3/F4/F5/F6 selects the sort. Selecting the same sort again reverses the file order. Directories stay first in ascending natural name order. Cursor and marks stay on the same entries. Git logs keep their newest-first order.", [
          loc("C:PanelRules", "public static IReadOnlyList<FileEntry> Sort(", "Natural ordering and directory-first policy"),
          loc("C:PanelSession", "public void Sort", "Session sort state"),
          loc("D:PanelView", "public void SortBy(", "Sort with selection and cursor preservation"),
          loc("D:PanelView", "SortBy(field); FocusCurrent();", "Header click sorting"),
          loc("D:PanelView", "private void UpdateHeaders(", "Sort direction indicators"),
          loc("D:PanelView.SearchResults", "private List<FileRow> ResultRows(", "Sort the result snapshot"),
          route("D:CommanderWindow.Keyboard", "private bool HandlePanelNavigation(", "Sort shortcuts")])

story("U04", "E01", "Jump to names with the keyboard layout I use", "type part of a visible name and cycle matches", "I can reach an entry without scrolling through the list",
      "Alt and a letter start name jumping. Matching accepts the letters of every installed keyboard layout, ignores typed letters that match nothing, and highlights the matched text. Up/Down cycle the matches. Cancelling the jump returns plain typing to the command line.", [
          loc("C:NameJump", None, "Match and cycle rules"),
          loc("D:CommanderWindow.QuickJump", None, "Physical-key and text-input coordination"),
          loc("D:PanelView", "public void ShowNameMatches(", "Apply match ranges to rows"),
          loc("D:FileRow", "public void SetNameMatch(", "Visible match state")], ["T18", "T13"])

story("U05", "E01", "Return to recent and frequent locations", "choose a recent or frequently used directory", "I can return to work without typing its path again",
      "Alt+Down opens the chooser with per-panel recent history and shared frequent directories. A directory earns a visit after ten active seconds in it. The score halves every three days. The frequent list shows up to 30 directories: scored directories first, then directories that were only opened briefly or come from panel history, without a score. Roots, archive locations, Git locations, and the current directory are left out.", [
          loc("C:DirectoryHistory", None, "Bounded unique recent history"),
          loc("C:DirectoryAttention", None, "Ten active seconds earn a visit"),
          loc("C:DirectoryUsageBook", None, "Scores, decay, qualifying locations, and suggestions"),
          loc("D:DirectoryNavigationDialog", "public DirectoryNavigationDialog(", "History and frequent-location presentation"),
          loc("D:CommanderWindow", "private void OpenNavigation(", "Chooser opening and navigation result"),
          loc("D:WorkspaceLifecycle", "private void ObserveDirectoryAttention(", "Record qualified visits"),
          loc("D:CommanderWindow", "DirectoryUsage.Seed([path]);", "Add opened directories as candidates without a score"),
          loc("D:CommanderWindow.State", "DirectoryUsage.Seed(", "Add panel history as candidates without a score")], ["T21"])

story("U06", "E01", "Keep named favorite directories", "add, rename, remove, and open favorite locations", "I can maintain a short list of useful places",
      "Ctrl+D opens the Favorites list. Favorites are shared between panels, have unique paths, and are saved immediately. Pins come before frequent directories, and a pinned path does not repeat among them.", [
          loc("C:FavoriteDirectories", None, "Validation, unique paths, and pin operations"),
          loc("D:DirectoryNavigationDialog", "private void AddCurrent(", "Add current directory"),
          loc("D:DirectoryNavigationDialog", "private void RenameSelection(", "Rename a pin"),
          loc("D:DirectoryNavigationDialog", "private void RemoveSelection(", "Remove a pin"),
          loc("D:DirectoryNavigationDialog", "private object[] FavoriteLocations(", "Pins before frequent directories"),
          loc("D:WorkspaceLifecycle", "private Task SaveFavoritesAsync(", "Immediate save and visible save errors")], ["T21"])

story("U07", "E01", "Find a saved location and understand missing paths", "search location names and paths and see when a location is missing", "I can choose a usable destination without guessing",
      "Typing in the active chooser column selects the first name or path that contains the text, with keyboard-layout alternatives. Up/Down cycle matches. Escape clears the query before it closes the chooser. Missing directories are marked with their nearest existing parent. An opening error stays visible in the chooser.", [
          loc("D:DirectoryNavigationDialog.Search", None, "Chooser name/path search and cycling"),
          loc("D:DirectoryNavigationDialog.Locations", None, "Missing-location messages and list navigation"),
          loc("D:DirectoryNavigationDialog", "public void ShowError(", "Keep an opening error visible")], ["T04", "T18"])

story("U08", "E01", "Keep a readable panel layout", "adjust columns and recognize file types by color", "I can scan the information that matters in the space available",
      "Columns keep their chosen widths and hide lower-priority fields when space is limited. Reset column widths restores the defaults. Ordered JSON rules color the type column by file name or extension; the first matching rule wins. Ordinary names use the text color, and Git-ignored names use gray. Cached ignore state carries into child folders until a new repository boundary is found. Reload file colors applies edited rules to existing rows. A reload that fails to read, parse, or validate the rules keeps the current colors and shows a message. Windows file icons come from a technical adapter.", [
          loc("D:PanelView.Columns", None, "Width preferences, migration, and adaptive visibility"),
          loc("D:PanelView", "public void ResetColumns(", "Restore column defaults"),
          loc("D:PanelView", "public void SetFileTypes(", "Apply color rules without replacing the list"),
          loc("D:CommanderWindow.Settings", None, "Reload rules and keep the current colors on error"),
          loc("C:FileTypes", None, "Rule validation and first matching rule"),
          loc("D:FileRow", "public void SetFileColor(", "Color combined with cursor and mark state")], ["T13", "T14", "T25"])

story("U09", "E01", "See external directory changes without losing my place", "refresh directory contents while keeping cursor, marks, and edits stable", "I can work with files also changed by other programs",
      "Filesystem changes refresh the panel through a guarded UI update that waits during sensitive interactions. Live Git panels refresh at most every three seconds. Ctrl+R reloads the active panel and keeps its selection. A refresh keeps cursor, marks, and valid folder measurements. Search-result panels keep their matched set.", [
          loc("D:DirectoryUpdates", None, "Wait during interactions and route physical/Git refreshes"),
          loc("D:PanelView", "public async Task<bool> RefreshAsync(", "Refresh only while the captured context is still valid"),
          loc("D:PanelView", "private void ApplyRefreshedEntries(", "Keep cursor, marks, and valid measurements"),
          loc("D:PanelView", "private async Task<bool> RecoverDirectoryAsync(", "Recover from a removed directory"),
          loc("D:CommanderWindow.Keyboard", "private void ReloadActivePanel(", "Manual reload keeps selection and restores focus")], ["T04"])

story("U10", "E01", "See the shortcuts for the current context", "read the main shortcuts for the active panel at the bottom of the window", "I can find the next action without opening a menu",
      "A hint row lists the main file, comparison, search, and command-line shortcuts. In a Git log it lists snapshot, commit details, refresh, back, return, and panel shortcuts. In a Git snapshot it lists view, changed files, diff, refresh, back, return, and panel shortcuts. The row updates when the active panel or its location changes.", [
          loc("D:CommanderFrame", "public static Control BuildKeyboardHelp(", "Context-specific shortcut hints")], ["T13"])

# E02: Organize files across locations

story("U11", "E02", "Mark a batch independently of the cursor", "mark files and folders while moving the current cursor", "I can apply one action to a chosen batch",
      "Marks, range marking, page and boundary navigation, and clearing marks are separate from the current item. Parent rows cannot be marked. The footer shows the number of selected items and their total size. It shows '≥' while a selected folder is not measured and '(incomplete)' when a measurement is incomplete.", [
          loc("D:PanelView", "private void ToggleRange(", "Range marking"),
          loc("D:PanelView", "public void MoveCursor(", "Move or toggle while advancing"),
          loc("D:PanelView", "public void ClearMarks(", "Clear the batch"),
          loc("D:PanelView", "private void UpdateSelectionTotal(", "Batch count, size, and incomplete totals"),
          loc("D:FileRow", "public void SetMarked(", "Mark state and appearance"),
          loc("D:FileNameCell", None, "Visible mark and name controls")], ["T13"])

story("U12", "E02", "Rename an entry in place", "edit the full name in its row and commit or cancel", "I can rename without losing list context",
      "Shift+F6 selects the full name including its extension. Enter commits, Escape cancels, and Tab commits before it switches panels. Validation or I/O errors keep the editor and the draft. Activating the other panel or focusing another row cancels the edit and drops the draft. Archive and Git locations reject renames with a message for their kind.", [
          loc("D:PanelView", "public void BeginRename(", "Start full-name editing"),
          loc("D:PanelView", "public async Task<FileOperationError?> CommitRenameAsync(", "Validate, apply, and keep failed drafts"),
          loc("D:PanelView", "public void CancelRename(", "Cancel the edit"),
          loc("D:PanelView", "Table.GotFocus +=", "Cancel the edit when another row gets focus"),
          loc("D:CommanderWindow", "if (ActivePanel != panel) { ActivePanel.CancelRename();", "Cancel the edit when the active panel changes"),
          loc("D:CommanderWindow.Archives", "RenameCurrent", "Writable-location guard"),
          loc("D:FileNameCell", None, "Inline editor and read view"),
          loc("C:PanelRules", "ValidateName", "Windows-compatible single-name rules"),
          loc("C:ArchivePath", "public static string ReadOnlyMessage(", "Read-only message for archive and Git locations"),
          loc("D:CommanderWindow.Keyboard", "private void HandleRenameKey(", "Commit before Tab changes panels; keep failed edits active"),
          route("D:CommanderWindow.Keyboard", "private bool HandlePanelCommand(", "Shift+F6 starts the edit")], ["T02"])

story("U13", "E02", "Create a folder at the current location", "create a folder after entering one valid name", "I can prepare a destination without leaving Xcmd",
      "F7 opens a name dialog. It accepts one valid name of an entry that does not exist yet. Archive, Git, and search-result locations show a message instead. Successful creation refreshes the list and selects the new folder.", [
          loc("D:CommanderWindow", "private void OpenCreateDirectoryDialog(", "Create dialog, validation, refresh, and cursor"),
          loc("D:CommanderWindow", "public NameDialog(", "Shared name entry and errors"),
          loc("C:PanelRules", "ValidateName", "Single-name policy")], ["T02"])

story("U14", "E02", "Create a file ready for editing", "name a new file and open it in the external editor", "I can begin writing in the current directory",
      "Shift+F4 requires a regular directory and a new valid name. The editor must be available before the file is created. Creation fails when the file already exists. The new file becomes current and opens in Notepad++.", [
          loc("D:CommanderWindow.NewFile", None, "New-file workflow"),
          loc("D:CommanderWindow", "public NameDialog(", "New-file name entry"),
          route("D:FileViewingController", "public Task<ExternalLaunch> CreateFileAsync(", "Delegate new-file preparation to the editor adapter")], ["T05", "T02"], ["E06"])

story("U15", "E02", "Copy or move a batch to an explicit destination", "copy or move selected entries with an editable destination", "I can place files where I intend",
      "F5/F6 capture the selected entries when the dialog opens and resolve the destination text when it is submitted. A single item can receive a new name; several items require an existing directory. Targets inside the source are rejected. Copying and moving links is rejected and the link stays unchanged. A folder copied onto an existing folder merges into it and keeps destination-only entries. A move removes each source only after its transfer succeeds. Copies keep the creation, modification, and access times, the Hidden, ReadOnly, System, and Archive attributes, and the additional data streams. New copies inherit destination permissions.", [
          loc("C:FileTransferPlanner", None, "Destination resolution and batch rules"),
          loc("C:FileOperationInventory", "public static class FileOperationRules", "Writable paths and safe source/target relations"),
          loc("C:FileOperationRunner", "private async Task<bool> TransferAsync(", "Copy/move target, link, and merge rules"),
          loc("C:FileOperationRunner", "private async Task<bool> CopyFolderAsync(", "Merge folders and remove moved sources only after success"),
          loc("C:FileOperationRunner", "private async Task<bool> CopyFileAsync(", "Copy and safe move completion"),
          loc("D:CommanderWindow.FileOperations", "TransferSelected", "Capture selections and enqueue the requested transfer"),
          loc("D:FileTransferDialog", None, "Explicit editable destination"),
          loc("I:FileCopyMetadata", "const FileAttributes copied", "Copied attributes and times"),
          loc("I:FileCopyMetadata", "public static long Copy(", "Additional data streams and inherited permissions")], ["T02", "T03"])

story("U16", "E02", "Duplicate an entry in the same folder", "copy an entry under another name in the current directory", "I can make a local variant quickly",
      "Shift+F5 opens the copy dialog with the current directory as an editable destination. A batch cannot target its own source directory, and a source cannot replace itself.", [
          loc("D:CommanderWindow.FileOperations", "TransferSelected", "Same-folder transfer choice"),
          loc("D:FileTransferDialog", None, "Initial same-folder destination"),
          route("D:CommanderWindow.Keyboard", "private bool HandleFileOperationKey(", "Shift+F5 dispatch")], ["T02"])

story("U17", "E02", "Transfer files through the Windows clipboard", "copy or cut a file batch and paste it into the displayed directory", "I can transfer files between Xcmd and other Windows tools",
      "Ctrl+C/Ctrl+X write the batch as Windows files; Ctrl+V pastes into the displayed directory. Cut rows are dimmed and the sources stay until paste. Nested sources are reduced to their top folder. A copy into the source folder gets a free '(copy)' name with a number when needed. A cut into the source folder changes nothing. A move removes only the completed sources and keeps skipped or failed ones for another paste. Archive and Git entries and destinations show a message instead.", [
          loc("D:PanelClipboardController", "public void Copy(bool cut)", "Capture copy/cut sources"),
          loc("D:PanelClipboardController", "public void Paste(", "Plan and enqueue a paste"),
          loc("D:PanelClipboardController", "private async Task CompleteMoveAsync(", "Keep skipped or failed cut items available"),
          loc("D:PanelView", "public void SetCutPaths(", "Propagate cut appearance"),
          loc("D:FileRow", "public void SetCut(", "Dim cut entries"),
          loc("C:ClipboardTransferPlanner", None, "Source reduction, free copy names, and paste policy"),
          loc("C:FileOperations", "public static class FileOperationNames", "Copy-name rules")], ["T15", "T02"])

story("U18", "E02", "Drag files to another folder or program", "drag selected entries with the mouse and drop files onto a panel", "I can move files between Xcmd and other Windows programs by pointing",
      "Dragging a row with the left or right mouse button starts a Windows drag of the marked group when the row is marked, or of that row alone. A click on a marked row without movement selects only that row; Ctrl+click toggles it. Windows copies, moves, or creates shortcuts at the target. Items dropped on a panel go to the folder row under the pointer, which is highlighted, or to the panel folder. Holding the pointer near the top or bottom edge scrolls the list. Drag and drop works with physical folders; archive, Git, and search-result panels are skipped.", [
          loc("D:FileDragDropController", None, "Drag start, selection on release, drop destination, highlight, and edge scrolling"),
          loc("D:PanelView.Shell", "internal void SelectClickedRow(", "Select or toggle a clicked marked row"),
          loc("D:FileRow", "public void SetDropTarget(", "Drop-target highlight")], ["T03"], ["E06"])

story("U19", "E02", "Choose how to delete a batch", "confirm recycling or permanent deletion", "I can control the consequence of removing files",
      "F8/Delete select the Recycle Bin; Shift+F8/Shift+Delete select permanent deletion. The confirmation lets the user change the mode. Recycling works on local fixed drives. When Windows offers permanent deletion instead of recycling, Xcmd refuses it and keeps the item.", [
          loc("D:CommanderWindow.FileOperations", "DeleteSelected", "Capture deletion sources and confirmation"),
          loc("D:FileOperationDialogs", "class FileDeleteDialog", "Mode choice and confirmation"),
          loc("C:FileOperationRunner", "private async Task<bool> RemoveAsync(", "Execute the selected deletion mode through the operation store"),
          loc("I:WindowsRecycleBin", "DriveType.Fixed", "Recycle only on local fixed drives"),
          loc("I:WindowsRecycleBin", "if (sink.RefusedPermanentDeletion)", "Refuse permanent deletion during recycling")], ["T02", "T03"])

story("U20", "E02", "Resolve conflicts without abandoning a transfer", "skip, rename, or replace a conflicting target", "I can finish the useful parts of a copy or move",
      "A conflict prompt appears for a file target, a folder against a file, or a link target. The default decision is Skip. Replace works only for regular files. Apply to all repeats Skip or Replace for matching conflict kinds in the same request. Escape or closing the prompt cancels the operation. Identical files are skipped without a prompt after a content and metadata comparison.", [
          loc("C:FileOperationRunner", "private async Task<ConflictDecision> DecideAsync(", "Conflict decisions and remembered choices"),
          loc("C:FileOperationRunner", "private async Task<bool> TransferAsync(", "Apply skip/rename/replace and identical-item policy"),
          loc("C:FileOperationRunner", "private async Task<bool> MatchesAsync(", "Identical-item comparison"),
          loc("D:FileOperationDialogs", "class FileConflictDialog", "Decision UI"),
          loc("D:FileOperationDecisions", None, "Wait for a suitable moment before prompting")], ["T02", "T03"])

story("U21", "E02", "Queue work and understand its progress", "submit long file operations and inspect progress and results", "I can keep using the panels while work runs",
      "Requests run in order with targets captured at submission. Counting comes before execution. The summary shows processed and total files, skipped and failed items, incomplete counts, queued requests, and an estimate of the remaining time. Bytes drive the progress bar. The details view lists the operations of the session and the last 100 outcomes of each operation.", [
          loc("C:FileOperations", "public sealed class FileOperationQueue", "Serial queue and completed jobs"),
          loc("C:FileOperationInventory", "public static async Task CountAsync(", "Count work and mark incomplete inventories"),
          loc("C:FileOperationInventory", "public static class FileOperationProgress", "Progress fraction and estimate rules"),
          loc("D:FileOperationsView", None, "Current/queued progress and summary"),
          loc("D:OperationDetailsView", None, "Per-operation outcome list"),
          loc("D:CommanderWindow.FileOperations", "private async Task RefreshOperationPanelsAsync(", "Refresh panels after completed changes")], ["T01", "T02"])

story("U22", "E02", "Recover from errors or stop queued work", "retry, skip, or cancel work when an item fails", "I can keep completed work and make the next decision explicitly",
      "A failed item offers Retry, Skip, Skip similar errors, and Cancel. Identified locks instead show application links, plain executable/PID summaries, and Kill for the listed processes. Kill can lose unsaved changes. The operation retries when all listed processes exit; cancellation stops that wait. Similar errors are grouped by exception type. Counting, current work, and queued work can be cancelled. Completed filesystem changes stay in place. Partial and not-started outcomes stay visible.", [
          loc("C:FileOperationRunner", "private async Task<bool> AttemptAsync(", "Retry and cancellation outcomes"),
          loc("C:FileOperationRunner", "private async Task<FileErrorAction> DecideErrorAsync(", "Remember skip-similar error decisions"),
          loc("C:FileOperationRunner", "public async Task ExecuteAsync(", "Final completed/partial/not-started outcome"),
          loc("C:FileOperations", "public sealed class FileOperationQueue", "Queued and current cancellation"),
          loc("D:FileErrorDialog", None, "Recovery choices"),
          loc("D:FileUsersView", None, "Application links, plain PID summaries, Kill and automatic retry"),
          loc("D:FileOperationsView", None, "Cancel selected/current work"),
          loc("D:FileOperationDecisions", None, "Present failures without stealing an active edit")], ["T02", "T03"])

story("U23", "E02", "Measure folder sizes before acting", "calculate one or all folder sizes in the panel", "I can judge the amount of data in a selection",
      "Space measures the current folder. Alt+Shift+Enter measures every child folder in the panel list; pressing it again or Escape cancels. Work runs in the background. Links are skipped and counted separately. Read errors mark a total incomplete. Results for an outdated listing are discarded.", [
          loc("C:DirectorySizeCalculator", None, "Traversal and measurement rules"),
          loc("D:CommanderWindow.DirectorySizes", None, "Start, cancel, and apply valid measurements"),
          loc("D:PanelView", "public void SetDirectorySize(", "Update visible measurements"),
          loc("D:FileRow", "public string DisplaySize", "Size and incomplete-total presentation"),
          loc("D:PanelView", "private void UpdateSelectionTotal(", "Combine selected file and measured folder totals")], ["T02"], ["E01"])

# E03: Find files and text in a directory tree

story("U24", "E03", "Find files by name or relative path", "search below a location using a name fragment, whole name, or mask", "I can find files whose exact locations I do not know",
      "Alt+F7 opens the search window for the active location. Contains and Whole name modes compare names; a pattern with * or ? always matches the whole name or relative path. An option treats * and ? as literal characters. Patterns can include relative paths. Folders are returned as name results too. Query length and regex validity are checked before work starts.", [
          loc("C:FileSearch", "public sealed record FileSearchQuery", "Query validation"),
          loc("C:FileSearch", "public sealed class SearchNameMatcher", "Name, mask, and relative-path matching"),
          loc("D:FileSearchWindow", "public FileSearchWindow(", "Name/path options and root presentation"),
          loc("D:FileSearchWindow", "public void StartSearch(", "Validate and start an explicit query")], ["T07"])

story("U25", "E03", "Find text across files", "search file contents with literal text or a regular expression", "I can locate information inside a directory tree",
      "The query supports case and regex options and returns the first match in each file with its line and a snippet of up to 240 characters. Encoding detection tries the detected encoding and then UTF-8, Windows-1251, Windows-1252, and code page 866. Binary files are skipped. Decoding failures, regex timeouts, and regex lines over 256K characters are reported as failures.", [
          loc("C:FileSearch", "public sealed record FileSearchQuery", "Text-query and regex validation"),
          loc("C:FileSearchSession", "private async Task ConsumeAsync(", "Search file text and publish a first match"),
          loc("C:FileSearchSession", "private void Emit(", "Publish a unique result within limits"),
          loc("I:SearchText", "if (ViewerText.IsBinary(sample, detected)) return null;", "Skip binary files"),
          loc("I:SearchText", "new[] { 65001, 1251, 1252, 866 }", "Fallback encodings"),
          loc("D:FileSearchWindow", "public FileSearchWindow(", "Text, case, and regex controls"),
          loc("D:FileSearchWindow.Results", None, "Line/snippet result columns")], ["T07", "T11"])

story("U26", "E03", "Get useful search results before expensive areas", "search ordinary files first and understand the search scope", "I can get useful answers before scanning caches and large archives",
      "The session uses separate small-file, large-file, and archive lanes. Archive roots are read by their own worker while the ordinary traversal runs. Git-tracked paths are kept; ignored or untracked dependency and cache folders are deferred. Nested archives are reported as skipped. File and result limits bound a session.", [
          loc("C:FileSearch", "public static class FileSearchScope", "Tracked-path preference and deferred-directory policy"),
          loc("C:FileSearchSession", None, "Priority lanes, deferred paths, archives, and limits"),
          loc("I:SearchGitScope", "public async Task<(Dictionary<string, string> Ignored, HashSet<string> Tracked)> ClassifyAsync(", "Tracked and ignored path classification"),
          loc("D:FileSearchWindow", "private string SearchStatus(", "Explain incomplete or limited coverage")], ["T06", "T07"], ["E05"])

story("U27", "E03", "Inspect skipped areas and search them deliberately", "see skipped paths and failures and choose further locations to search", "I can judge whether the result set is sufficient",
      "The window lists skipped and failed paths with reasons, including the Git rule source when it is known. Search skipped files extends the session. A double-click on a skipped or failed path starts a new search there; that path becomes the search root for later searches and for To panel.", [
          loc("D:FileSearchWindow.Results", None, "Skipped/failure tables and detail-location action"),
          loc("D:FileSearchWindow", "public void Expand(", "Expand deferred search"),
          loc("D:FileSearchWindow", "public void SearchLocation(", "Search one chosen location and change the root"),
          loc("C:FileSearchSession", None, "Keep deferred paths and report failure categories")], ["T07"])

story("U28", "E03", "Control a search without losing useful results", "stop a search, revise its query, and reuse earlier queries", "I can explore step by step without unclear background changes",
      "Results appear progressively. Stop keeps the results and marks the coverage as partial. Edited options take effect when Search is requested. Name and text history and the settings are saved for later sessions. A new request cancels and retires the earlier one.", [
          loc("D:FileSearchWindow", "private void QueryChanged(", "Distinguish edited query from running query"),
          loc("D:FileSearchWindow", "public void Stop(", "Keep partial results"),
          loc("D:FileSearchWindow", "private void Flush(", "Bounded progressive UI batches"),
          loc("D:FileSearchWindow", "public FileSearchState CaptureState(", "Query and history state"),
          loc("D:QueryHistoryPopup", None, "Keyboard history selection"),
          loc("C:FileSearchSession", None, "Cancellation and bounded results")], ["T21", "T13"])

story("U29", "E03", "Go from a search match to the file", "open the matching file or locate it in a panel", "I can continue work from the result",
      "F3 opens a viewer at the text match with the detected encoding. Go to file shows the result in its folder, or in its archive location, in the panel that was active when the search window opened. Go to file closes the search window and focuses that panel.", [
          loc("D:FileSearchController", None, "Locate and view result actions"),
          loc("D:FileViewingController", "public void ViewSearchResults(", "Open result with its match context"),
          route("D:FileSearchWindow", "private void View(", "Dispatch view for the selected result"),
          route("D:FileSearchWindow", "private void Go(", "Dispatch locate for the selected result")], ["T06", "T11"], ["E04", "E01"])

story("U30", "E03", "Use search results as a working panel", "send a result snapshot to a panel and act on its entries", "I can organize files found in different directories",
      "To panel (Alt+P) sends the results to the panel that was active when the search window opened and closes the window. The result panel shows relative labels and supports marks, sorting, viewing, renaming, and source operations. Ctrl+Left/Right shows a result in its folder in the other panel; for an archive result it opens the archive there. Escape, Backspace, or Ctrl+PageUp restore the earlier panel. Refresh removes unavailable matches. The snapshot is saved with the workspace. Windows group menus need results from one folder.", [
          loc("D:PanelView.SearchResults", None, "Snapshot state, operations, restore, and result refresh"),
          loc("D:FileSearchController", None, "Send results to the source panel"),
          route("D:FileSearchWindow", "private void SendToPanel(", "Publish the current snapshot"),
          loc("D:CommanderWindow.Keyboard", "private void OpenInPanel(", "Locate a result in the other panel"),
          loc("D:CommanderWindow.Menu", "if (panel.IsSearchResults) { await panel.ExitSearchResultsAsync(); return; }", "Parent navigation leaves the result panel"),
          route("D:CommanderWindow.Keyboard", "private bool HandleCommandLineShortcut(", "Escape restores the earlier panel")], ["T21", "T02"], ["E02", "E08"])

# E04: Understand file contents

story("U31", "E04", "Inspect files in independent viewers", "open one or more viewers and move through the displayed file list", "I can read files while the panels remain usable",
      "F3 opens independent non-modal viewers. Previous and next file follow the file list captured when the viewer opened. Captions show the origin of virtual documents and mark read-only documents. Ctrl+W closes a viewer.", [
          loc("D:FileViewingController", "public void ViewCurrent(", "Capture all file rows and the current file"),
          loc("D:FileViewingController", "private FileViewerWindow OpenViewer(", "Independent viewer lifetime"),
          loc("D:FileViewerWindow", "public FileViewerWindow(", "Viewer surface and lifecycle"),
          loc("D:FileViewerWindow.Document", None, "Origin caption and read-only external-action guard"),
          loc("D:FileViewerWindow.Loading", "private Task NavigateAsync(", "Move through the captured list"),
          loc("D:FileViewerWindow.Loading", "private async Task LoadFileAsync(", "Reset file-specific state")], ["T11", "T16"])

story("U32", "E04", "Choose an inspection mode and encoding", "switch between text, binary, hex, and image inspection", "I can interpret files that do not fit the first detected mode",
      "The viewer detects common raster image signatures and the text encoding. Keys 1–4 and the View menu change the mode; the Encoding menu changes the encoding. The page renders again after a change. Image mode is enabled only for files with a supported image signature, and images are decoded with size limits. Ctrl+I shows file information.", [
          loc("D:FileViewerWindow.Loading", "private async Task LoadFileAsync(", "Automatic mode selection"),
          loc("D:FileViewerWindow.Loading", "private async Task ChangeModeAsync(", "Manual mode change"),
          loc("D:FileViewerWindow.Loading", "private async Task RenderPageAsync(", "Render the selected representation"),
          loc("D:FileViewerWindow.Menu", "private void SelectEncoding(", "Apply manual encoding and reset search state"),
          loc("D:FileViewerWindow.Menu", "private void ToggleInformation(", "Show or hide file information")], ["T11", "T16"])

story("U33", "E04", "Read large files with bounded memory", "scroll and jump through a large file with meaningful position information", "I can inspect large content with bounded memory",
      "The viewer reads overlapping bounded pages and maps text positions to byte offsets. The scroll bar and Home/End cover the whole file. Progress shows the visible part of the file. W toggles word wrap and keeps the current byte position. Text selection and copy work on the loaded document window.", [
          loc("D:FileViewerWindow.Scrolling", None, "Whole-file scroll, navigation, offsets, and progress"),
          loc("D:FileViewerWindow.Loading", "private async Task LoadPageAsync(", "Bounded page loading"),
          loc("D:FileViewerWindow.Menu", "private void ToggleWrapping(", "Keep the byte position when toggling wrap"),
          loc("D:FileViewerWindow", "public FileViewerWindow(", "Read-only selectable text surface")], ["T11", "T12"])

story("U34", "E04", "Find text throughout a viewed file", "find literal text and move to the next or previous match", "I can reach relevant content beyond the visible page",
      "Ctrl+F or Alt and a letter start a case-insensitive literal search. F3/Tab and Shift+F3/Shift+Tab move through matches and wrap around. Requests are debounced and outdated results are cancelled. The search covers the whole file. Open viewers in one process share the query.", [
          loc("D:FileViewerWindow.Search", None, "Find entry, direction, match selection, and outdated-request guards"),
          loc("D:FileViewingController", "private FileViewerWindow OpenViewer(", "Share the current find text between viewers")], ["T11", "T18"])

story("U35", "E04", "Read source with an appropriate language", "use automatic or manual syntax highlighting", "I can understand code structure while inspecting a file",
      "The language comes from the file extension, from names such as Dockerfile, Makefile, and .editorconfig, or from a python, pwsh, or sh shebang. The Syntax language menu offers plain text and manual choices. Highlighting covers the first 1 MiB of a file, lines up to 8,192 characters, and a time budget of 1.5 seconds in total and 250 ms per line. Outside these limits the text stays readable as plain text. Markdown and CSV show highlighted source text.", [
          loc("D:FileViewerWindow.Syntax", None, "Language selection, highlighting request, and fallback"),
          loc("D:FileViewerWindow.Loading", "private async Task LoadFileAsync(", "Reset language choice when changing files"),
          loc("I:Syntax/SyntaxGrammarCatalog", "\"dockerfile\" or \"containerfile\" => \"dockerfile\",", "Language choice by file name and shebang"),
          loc("I:Syntax/SyntaxHighlighter", "public const int MaximumContextBytes = 1024 * 1024;", "Highlighting limits")], ["T12"])

story("U36", "E04", "Choose comfortable reading colors", "choose a viewer theme shared across open viewers", "I can read different files with consistent colors",
      "Nine named themes are available. A change updates all open viewers and the saved preference. Selection and search highlights stay while colors change. Token contrast comes from a rendering service.", [
          loc("D:ReaderThemes", None, "Available named themes"),
          loc("D:FileViewerWindow.Themes", None, "Theme menu and selection-preserving update"),
          loc("D:FileViewingController", "public void RestoreTheme(", "Apply theme changes to all viewers"),
          loc("D:FileViewingController", "private FileViewerWindow OpenViewer(", "Connect theme change notifications"),
          loc("D:WorkspaceLifecycle", "private Task OnViewerThemeChanged(", "Save the preference immediately")], ["T12", "T21"])

story("U37", "E04", "Follow a changing log file", "follow the end of a growing file", "I can observe new output without repeatedly reopening it",
      "Ctrl+Shift+F toggles Follow end of file. Follow works for regular files and is disabled for images, archive members, archive reports, and read-only documents such as Git snapshots. Polling every 500 ms detects growth, truncation, and changed bytes. Moving up, Home/End, the scroll bar, search, and a mode change pause following.", [
          loc("D:FileViewerWindow.Follow", None, "Follow state, polling, and pause conditions"),
          loc("D:FileViewerWindow.Scrolling", "if (delta < 0) PauseFollowing();", "Upward movement pauses following"),
          route("D:FileViewerWindow.Menu", "private void BuildMenu(", "Follow menu command")], ["T11"])

story("U38", "E04", "Continue from a viewed file in another program", "open the viewed file with Windows or in the external editor", "I can act after inspecting its contents",
      "F4 sends a writable physical file to Notepad++, and Open externally sends it to its Windows association. Read-only documents block both actions. Archive members open externally through a temporary copy; see E05.", [
          loc("D:FileViewerWindow.Document", None, "Read-only origin checks"),
          route("D:FileViewerWindow.Loading", "private bool HandleFileKey(", "External open/edit keyboard actions"),
          route("D:FileViewingController", "private async Task EditFileAsync(", "External edit callback"),
          route("D:FileViewerWindow.Menu", "private void BuildMenu(", "File-action menu entries")], ["T05"], ["E06"])

# E05: Work with packaged files

story("U39", "E05", "Browse an archive like a location", "enter supported archives and nested members", "I can inspect their structure before extracting",
      "Enter opens common archive extensions; Ctrl+PageDown probes any file, including supported self-extracting containers. Parent navigation and nested archive paths work. Opening can be cancelled with Escape. Archive entries are read-only. Encrypted entries and unsupported content produce errors.", [
          loc("C:ArchivePath", "OpensOnEnter", "Extensions that open as locations"),
          loc("D:ArchiveController", "public async Task OpenAsync(", "Archive opening and status"),
          loc("D:ArchiveController", "public bool CancelOpening(", "Cancel an archive open"),
          loc("D:CommanderWindow", "private async Task OpenCurrentAsync(", "Enter opens recognized archive extensions"),
          route("D:CommanderWindow.Archives", "private Task OpenArchiveAsync(", "Delegate archive opening"),
          loc("D:PanelView", "private string LocationPrefix(", "Archive location indication"),
          route("D:CommanderWindow.Keyboard", "private bool HandleControlPanelCommand(", "Forced archive probe")], ["T06", "T01"], ["E01"])

story("U40", "E05", "Inspect an archive and its members", "view archive information or open an individual member", "I can understand packaged content without a full extraction",
      "F3 shows an archive report or views a member. Opening a member externally writes a temporary copy, starts it, and then shows 'Opened a temporary copy. Use F5 to extract a file before editing it.' Temporary copies are limited to 256 MiB per member. Command-line insertion also writes a temporary copy first.", [
          loc("D:ArchiveController", "public async Task OpenExternallyAsync(", "Temporary external open and its message"),
          route("D:FileViewingController", "public void ViewCurrent(", "Route archive members/reports through IViewerFiles"),
          loc("D:CommanderWindow.Launching", "private void InsertCurrentArgument(", "Write an archive member to a temporary copy before inserting its path"),
          loc("I:ArchiveContents", "Archive preview is limited to 256 MiB per file.", "Temporary-copy size limit")], ["T06", "T11", "T05"], ["E04", "E06"])

story("U41", "E05", "Extract selected archive content", "copy archive files or folders to a regular directory", "I can use only the packaged content I need",
      "F5 extracts through the normal operation queue with conflict choices, progress, and cancellation. Moves out of an archive and writes into an archive show a message instead. Link entries and invalid entry paths are rejected by the adapter.", [
          loc("D:CommanderWindow.FileOperations", "TransferSelected", "Read-only source/destination restrictions"),
          loc("C:FileOperationRunner", "private async Task<bool> CopyFolderAsync(", "Extract a member tree through the common copy contract"),
          loc("C:FileOperationRunner", "private async Task<bool> CopyFileAsync(", "Extract a file through the common copy contract")], ["T06", "T02"], ["E02"])

story("U42", "E05", "Create a ZIP beside selected files", "pack a same-folder selection into a new ZIP", "I can share or store the selection as one package",
      "Add to ZIP in the Windows context menu packs the marked group, or the unmarked clicked item alone. The dialog suggests a name and adds .zip when it is missing. The queued writer adds files, folders, empty folders, and Unicode names. It fails when the archive exists, when an item is a link or junction, or when two items have the same entry name, compared without case.", [
          loc("D:ZipPackingController", None, "Capture sources, validate name, and enqueue"),
          loc("D:ZipNameDialog", None, "Archive name and source summary"),
          loc("C:FileOperationRunner", "private async Task CreateZipAsync(", "ZIP job progress and decisions"),
          loc("I:ZipArchiveWriter", "Links and junctions cannot be added to ZIP archives", "Reject links and junctions"),
          loc("I:ZipArchiveWriter", "if (!names.Add(name))", "Reject duplicate entry names"),
          route("D:ShellInteractionController", "private void OpenShellMenu(", "Windows-menu Add to ZIP extension")], ["T06", "T03"], ["E02"])

# E06: Use other tools from the current location

story("U43", "E06", "Open or edit the selected file in its usual tool", "open a file with Windows or edit it with Notepad++", "I can continue file work in the right external application",
      "Enter on a file and Open with Windows use the Windows association. F4 uses the installed Notepad++. The selected literal path and the working directory are passed to the program.", [
          loc("D:CommanderWindow", "private async Task OpenCurrentAsync(", "Enter opens a file with Windows"),
          loc("D:CommanderWindow.Archives", "private void OpenWithWindows(", "Physical/temporary external-open workflow"),
          loc("D:CommanderWindow.Launching", "internal async Task LaunchExternalAsync(", "Guard and report external launches"),
          loc("D:FileViewingController", "public async Task EditCurrentAsync(", "Capture selected file and working directory"),
          loc("D:FileViewingController", "private async Task EditFileAsync(", "Reject read-only paths before editor launch"),
          loc("I:ExternalEditor", "const string key = @\"Software\\Microsoft\\Windows\\CurrentVersion\\App Paths\\notepad++.exe\";", "Notepad++ as the editor"),
          route("D:CommanderWindow.Keyboard", "private bool HandleViewOrEditKey(", "F3/F4 keyboard routing")], ["T05"], ["E04"])

story("U44", "E06", "Run a command in the active directory", "type a program command or cd from the active panel", "I can start work without re-entering its directory context",
      "Plain typing goes to the command line. Quoted program targets and raw arguments are parsed separately. cd changes the panel directory inside Xcmd. Other commands start through ShellExecute in the active directory with the current user and system environment.", [
          loc("C:CommandLine", "public static class CommandLine", "Command validation, cd, target split, and quoting"),
          loc("D:CommanderWindow.Launching", "private async Task RunCommandLineAsync(", "Capture current directory and launch or navigate"),
          loc("D:CommanderWindow", "private void OnTextInput(", "Typing starts command entry"),
          loc("D:CommandLineView", None, "Command entry and directory context"),
          route("D:CommanderWindow.Keyboard", "private void HandleCommandLineKey(", "Command focus and execution shortcuts")], ["T05"])

story("U45", "E06", "Reuse commands without losing a draft", "browse matching command history and return to unfinished text", "I can repeat commands with less typing",
      "Successful commands enter a bounded history without duplicates. Alt+Up/Down browse the entries that start with the typed text and return to the original draft. After a launch the command is cleared only if its text is unchanged. The workspace saves the command draft and selection.", [
          loc("C:CommandLine", "public sealed class CommandHistory", "History without duplicates and draft-preserving prefix browsing"),
          loc("D:CommandLineView", None, "History movement and editor selection"),
          loc("D:CommanderWindow.Launching", "private async Task RunCommandLineAsync(", "Remember only successful commands and keep newer text")], ["T21"])

story("U46", "E06", "Complete the command token at the caret", "choose a path, environment name, or executable completion", "I can enter commands with fewer errors",
      "Ctrl+Space offers completions for the token around the caret. A chosen suggestion replaces the quoted token and keeps the rest of the command. The popup discards suggestions that were computed for an older draft or caret position, and it can be cancelled.", [
          loc("C:CommandCompletion", None, "Token boundaries and quoted replacement"),
          loc("D:CommandCompletionPopup", None, "Async suggestions, selection, and outdated-draft guard")], ["T05"])

story("U47", "E06", "Reuse names and paths without retyping", "insert the current name or path into a command or copy the full path", "I can pass the intended file to another tool accurately",
      "Ctrl+Enter inserts and selects the current name and Ctrl+Shift+Enter inserts and selects the full path, both quoted for the command line. Ctrl+C copies the selected argument, or the whole command when nothing is selected. File > Copy full path copies the current entry's full path without quotes; for an archive member it copies the displayed archive path with '!'. Git history rows show a message instead. Ctrl+Shift+C belongs to file comparison.", [
          loc("D:CommanderWindow.Launching", "private void InsertCurrentArgument(", "Insert selected name/path and quote it"),
          loc("D:CommandLineView", None, "Insert text and copy the command"),
          loc("D:PanelClipboardController", "public void CopyPath(", "Copy the full or displayed path"),
          loc("C:ArchivePath", "public static string Display(", "Displayed archive path"),
          loc("C:CommandLine", "public static string FormatArgument(", "Windows command argument quoting")], ["T15", "T05", "T06"])

story("U48", "E06", "Use Windows file actions and properties", "open the Windows context menu or properties for the intended items", "I can access installed shell actions from Xcmd",
      "Right-click, Shift+F10, and Ctrl+Down open the Windows context menu. On a marked item the menu targets the marked group; on an unmarked item it targets that item and keeps the marks. Alt+Enter opens the properties of the current item. Group menus need items from one physical folder. Archive and Git locations show a message for their kind.", [
          loc("D:PanelView.Shell", None, "Pointer/current-item selection rules"),
          loc("D:ShellInteractionController", None, "Resolve same-folder targets and request menu/properties"),
          route("D:CommanderWindow.Keyboard", "private bool HandleShellKey(", "Context-menu and properties shortcuts")], ["T03"], ["E02"])

story("U58", "E06", "Compare the current files in both panels", "compare the file under each panel cursor in VS Code", "I can inspect differences between two files without entering their paths",
      "Ctrl+Shift+C in a file panel or File > Compare files in VS Code opens a new VS Code diff window. The left panel supplies the left file and the right panel supplies the right file, regardless of the active panel or marked files. Names may differ. Both files must exist on disk; folders, archive members, and Git history entries are rejected with a message. Missing VS Code and launch failures explain the error. Cursors, marks, the clipboard, and the command draft stay unchanged. Holding the shortcut does not repeat the launch; command input and rename keep their own keys. VS Code provides the text diff; binary comparison, directory comparison, and merge workflows are outside this story.", [
          loc("C:FileComparison", None, "Validate the two cursor files and retain panel order"),
          loc("D:FileViewingController", "public async Task CompareCurrentFilesAsync(", "Capture the pair, prevent overlapping comparisons, and report the launch result"),
          loc("I:VisualStudioCodeComparison", "public Task<ExternalLaunch> PrepareAsync(", "Require existing files and open a new VS Code diff window")], ["T05", "T17"], ["E04"])

# E07: Work with repository changes

story("U49", "E07", "See repository context and local changes", "see branch, repository status, and entry-level change markers", "I can understand how my files relate to current repository work",
      "The header shows staged and working changes, the branch, the configured remote, and the locally known ahead/behind values. Rows show changes of their descendants, renames, and the state of direct child repositories. Ignored entries show '·'. Entries that Git tracks although an ignore rule matches them show a red '!' with a warning. Refresh reads the local Git state.", [
          loc("D:GitStatusView", None, "Repository header, async refresh, and status menu"),
          loc("D:GitStatusProjection", None, "Subtree and staged/working projection rules"),
          loc("D:GitRowStatus", None, "Status glyphs and descriptions"),
          loc("D:GitPanelRows", None, "Map live/snapshot/child-repository status to rows"),
          loc("D:PanelView.Git", None, "Git columns and legends"),
          loc("D:FileRow", "public void SetGitStatus(", "Row Git presentation state")], ["T08"])

story("U50", "E07", "Browse history scoped to the current work", "open a repository, directory, or file log in a panel", "I can find relevant commits without changing the checkout",
      "Ctrl+Alt+L opens the log of the selected file or folder, and Log of current folder opens the log of the panel folder; the Git status menu offers it too. The log opens in the other panel and loads older commits in pages. Columns show commit, subject, author, and date. Escape restores the earlier physical panel; pending Git navigation can be cancelled.", [
          loc("D:GitHistoryController", "public void Open(bool selectedItem)", "Capture path scope and open the log in the other panel"),
          loc("D:GitHistoryController", "private async Task NavigateAsync(", "Log paging and virtual-location navigation"),
          loc("D:GitLogPresentation", None, "Commit columns and paging footer"),
          loc("D:PanelView.History", None, "Save and restore the earlier panel state"),
          route("D:PanelView.Log", None, "Delegate log layout to GitLogPresentation"),
          route("D:GitHistoryController", "public bool HandleKey(", "History shortcuts"),
          route("D:GitCommitController", "panel.Git.LogRequested +=", "Log command in the Git status menu")], ["T09", "T17"])

story("U51", "E07", "Read the complete commit message in context", "read the selected commit message beside its files or log entry", "I can understand the reason for a change",
      "The message pane follows the current commit, loads asynchronously, allows text selection, and has a resizable divider. It shows loading, empty, and error states. Escape returns focus to the list.", [
          loc("D:GitCommitMessageView", None, "Message loading, selection, focus, and resize"),
          route("D:PanelView.CommitMessage", None, "Attach message view and forward keys")], ["T09", "T17"])

story("U52", "E07", "Browse a committed snapshot safely", "enter the full file tree of a commit", "I can inspect the old version while current files stay unchanged",
      "Enter on a commit opens its full tree, with first-parent change markers on changed entries. Backspace returns through the virtual history locations. File-changing commands show a read-only message. Submodule entries show a message that their contents belong to a separate repository.", [
          loc("D:GitHistoryController", "private async Task NavigateAsync(", "Enter snapshot locations"),
          loc("D:GitHistoryController", "public void UpdateCommands(", "Restrict file-changing and unsupported actions"),
          loc("D:CommanderWindow.Menu", "if (ActivePanel.HasHistory && !historyAllowed)", "Guard menu actions in read-only history"),
          loc("D:PanelView.Git", None, "Snapshot row/status presentation"),
          loc("D:GitHistoryController.Viewing", "private async Task ViewAsync(", "Read snapshot content with its origin"),
          loc("D:FileViewerWindow.Document", None, "Keep virtual documents read-only")], ["T09", "T10"], ["E04"])

story("U53", "E07", "Inspect changed and deleted files in a commit", "open the changed-file list and read deleted content", "I can inspect what a commit actually changed",
      "F3 on a commit shows its message and changed files. F3 on the parent row of a snapshot opens the changed entries as a list. Deleted content comes from the parent version, and renamed paths keep their earlier path. Comparison uses the first parent.", [
          loc("C:GitHistory", None, "Changed-path scope including earlier rename paths"),
          loc("D:GitHistoryController", "private async Task OpenChangesAsync(", "Open changed-file locations"),
          loc("D:GitHistoryController.Viewing", "private async Task ViewAsync(", "Reports and deleted/renamed-file source choice")], ["T09", "T10"])

story("U54", "E07", "Inspect the current working-tree change set", "open a scoped list of staged, working, and untracked changes", "I can inspect current work as a focused read-only view",
      "A Working tree entry appears at the top of the log when the scope has current changes. It lists the staged, working, and untracked changes of that scope and refreshes them. Reports and file views keep the live origin.", [
          loc("D:GitHistoryController", "private async Task NavigateAsync(", "Open working-tree locations"),
          loc("D:GitHistoryController.Viewing", "private async Task ViewAsync(", "Working-tree reports and selected-file view"),
          loc("D:DirectoryUpdates", None, "Refresh live Git locations")], ["T08", "T09", "T10"])

story("U55", "E07", "Read a diff for the current file", "view a text diff against HEAD or a commit parent", "I can understand how content changed",
      "Ctrl+Alt+D opens a text diff in the viewer. A working-tree diff against HEAD includes staged and unstaged changes. A commit diff uses the first parent. Initial and untracked files compare with an empty file. External diff and text conversion settings are turned off for these reads.", [
          loc("D:GitHistoryController.Viewing", "private async Task DiffAsync(", "Choose comparison context and open read-only diff"),
          route("D:GitHistoryController", "public bool HandleKey(", "Diff shortcut")], ["T10"], ["E04"])

story("U56", "E07", "Hand a repository commit to Claude Code", "start an interactive commit task at the repository root", "I can finish a repository change using my installed agent",
      "Commit repository with Claude Code in the Git status menu starts installed Claude Code at the repository root with '--model sonnet --effort medium' and the prompt 'Commit all changes.'. Claude Code handles authentication, permissions, questions, and the commit itself. Xcmd stays usable.", [
          loc("D:GitCommitController", None, "Capture repository and launch the commit task"),
          loc("I:ClaudeCommitLauncher", "--model sonnet --effort medium", "Model, effort, and prompt"),
          route("D:GitStatusView", None, "Commit action in the Git status menu")], ["T05", "T08"], ["E06"])

# E08: Continue work across application sessions

story("U57", "E08", "Restore the working workspace", "reopen my panels and saved preferences in their earlier state", "I can continue work after the application closes",
      "The saved state includes locations, cursors, marks, sort, columns, scroll, completed folder measurements, command drafts and history, location data, search settings and result snapshots, the viewer theme, and window placement. A panel that shows Git history is saved as the physical panel it came from. Missing locations and unavailable monitors fall back to existing ones. The workspace is saved before the window closes.", [
          loc("D:CommanderWindow.State", None, "Capture and restore the workspace and placement"),
          loc("D:PanelView", "public PanelState CaptureState(", "Panel state snapshot, with history panels saved as their earlier state"),
          loc("D:PanelView", "public async Task RestoreStateAsync(", "Panel and location restoration"),
          loc("D:PanelView.SearchResults", None, "Save and restore result snapshots"),
          loc("D:WorkspaceLifecycle", "public async Task InitializeAsync(", "Start from saved state"),
          loc("D:WorkspaceLifecycle", "private async Task SaveAndCloseAsync(", "Save before allowing close")], ["T21", "T22"], ["E01", "E03"])

# E09: Improve Xcmd while using it

story("UD01", "E09", "Comment on the exact interface element", "select a control and describe the change I need", "I can give the development agent feedback with a stable target",
      "F12 or Comment on an element works across the main window and attached dialogs. Picking uses the control under the pointer, including disabled controls. A single click opens a comment without starting the action. Ctrl+click sends one normal click to enabled controls and keeps target selection active. A released ordinary click opens the comment immediately. The interaction path is shown while the note is written.", [
          loc("D:FeedbackSurface", None, "Hit testing, overlay, immediate comments and Ctrl+click handling"),
          loc("D:FeedbackController", "public void Attach(", "Attach feedback to windows and dialogs"),
          loc("D:FeedbackController", "internal CommentDialog OpenComment(", "Describe target and show comment context"),
          loc("D:FeedbackController", "public CommentDialog(", "Comment editor and interaction path")], ["T19", "T20"])

story("UD02", "E09", "Keep feedback visible across sessions", "save a note immediately and inspect or remove my recorded notes", "I can track refinement work without depending on the workspace save",
      "A note is saved at once in the separate issue store with its trimmed text, target, and release. The Refinement list refreshes on its own and distinguishes open and closed work. In-progress notes cannot be removed. Save failures keep the editor open and visible.", [
          loc("D:FeedbackController", "private async Task SaveCommentAsync(", "Immediate save and error feedback"),
          loc("D:FeedbackController", "public void ShowNotes(", "List/status/removal UI"),
          loc("D:FeedbackController", "public async Task RefreshAsync(", "Reload current issue state"),
          loc("D:WorkspaceLifecycle", "public async Task PollAsync(", "Poll the separate issue store"),
          loc("D:CommanderFrame", None, "Feedback/notes entry points")], ["T20"])

story("UD03", "E09", "Report a syntax-highlighting problem with a sample", "attach the selected source sample and highlighting context to feedback", "I can make a rendering problem reproducible without retyping details",
      "In Text mode, Ctrl+Shift+F12, the Help menu, or the text context menu open the report for any file except an archive report. The report adds up to 4,096 selected characters, the file name, language, engine version, theme, encoding, byte position, and status to the saved comment text.", [
          loc("D:FileViewerWindow.Syntax", None, "Build report context and invoke feedback"),
          loc("D:FileViewerWindow.Themes", None, "Keep the selection while a menu is open"),
          loc("D:FeedbackController", "public CommentDialog? Report(", "Accept feature-specific context"),
          loc("D:FeedbackController", "private async Task SaveCommentAsync(", "Append report context to stored text")], ["T19", "T20"], ["E04"])

story("UD04", "E09", "Try a new local release without losing work", "switch to a ready local build when the application is idle", "I can test improvements during normal use",
      "A release started from the releases folder checks for newer verified releases. It switches after two seconds without input when no file work, save, pointer drag, inline rename, name jump, command-line focus, dialog, menu, viewer, search window, Git history panel, or feedback mode is active. The workspace is saved before the replacement starts. When the replacement fails, the current process stays usable, the error stays visible, and that release is not tried again in the session. The window shows the release and the uptime.", [
          loc("C:WorkspaceState", "public sealed record RestartReadiness", "Idle and interaction gate"),
          loc("D:WorkspaceLifecycle", "private async Task CheckForUpdateAsync(", "Choose a newer ready release while idle"),
          loc("D:WorkspaceLifecycle", "private async Task ReplaceProcessAsync(", "Save, replace, and keep failures visible"),
          loc("D:CommanderWindow", "public RestartReadiness Readiness", "Combine active workflow readiness"),
          loc("D:CommanderWindow", "public void UpdateUptime(", "Release/session age display"),
          loc("D:CommanderFrame", None, "Update and release status presentation")], ["T21", "T22", "T23"], ["E08"])

story("UD05", "E09", "Start the current release from one stable shortcut", "use a permanent application entry point across releases", "I do not need to replace shortcuts when a build changes",
      "Xcmd.exe next to the releases folder starts the newest verified release from releases/latest.txt. It passes the arguments and the caller's working directory and exits without waiting. The application and the shortcut share one taskbar identity.", [
          loc("I:BootstrapProgram", "public static async Task<int> RunAsync(", "Find the latest verified release and start it"),
          loc("I:ReleaseFiles", "public static async Task<VerifiedRelease?> FindLatestAsync(", "Read the latest-release marker and its evidence")], ["T23", "T24"])

# E10: Implement workflows over reusable boundaries

story("T01", "E10", "Express workflows through portable contracts", "add domain behavior without taking a dependency on Windows or Avalonia", "I can reason about rules apart from I/O",
      "Core contracts describe filesystem access, launches, operations, searches, and viewer sources. Pure virtual path models encode archive and Git locations. Data contracts are technical evidence for the stories that use them.", [
          loc("C:FileSystem", None, "IFileSystem"), loc("C:ExternalLaunch", None, "Launch request and IExternalLauncher"),
          loc("C:FileOperations", "public interface IFileOperationStore", "Operation boundary"),
          loc("C:FileSearch", "public interface IFileSearchSource", "Search source boundary"),
          loc("C:FileViewing", "public interface IViewerFiles", "Bounded viewer source boundary"),
          loc("C:ArchivePath", None, "Archive, Git, and physical virtual-location helpers"),
          loc("C:GitPath", None, "Git virtual-location grammar and display"),
          loc("C:GitHistory", None, "Portable commit/change records"),
          loc("I:IArchiveContents", None, "Archive boundary"), loc("I:IShellInteraction", None, "Windows shell boundary")])

story("T02", "E10", "Reuse physical filesystem services", "read, change, compare, and count filesystem entries through a common adapter", "I can implement file workflows without duplicating OS access",
      "The adapter enumerates link metadata, validates physical paths, compares files, copies through temporary targets, moves within or across volumes, and exposes the traversal used by size calculations and operation inventory.", [
          loc("I:LocalFileSystem", None, "Enumerate, rename, and create directories"),
          loc("I:LocalFileOperations", None, "Inspect, compare, copy/move/delete and temp-file cleanup"),
          loc("C:FileUsers", None, "Process identities, sharing errors, and action contracts"),
          loc("C:FileOperationInventory", None, "Inventory/progress models and reusable traversal rules")])

story("T03", "E10", "Reuse Windows file-operation and shell integration", "call native copy, recycle, menus, properties, and drag and drop behind managed interfaces", "I can extend file actions without spreading COM and native ownership rules",
      "CopyFileEx copies with progress and cancellation. The recycle adapter runs on an STA thread with COM progress callbacks. Windows menus forward extension messages and include an inspection seam that runs without showing a menu. OLE adapters start Windows drags and receive drops.", [
          loc("I:FileCopyMetadata", None, "CopyFileEx, metadata/stream comparison, and permission inheritance"),
          loc("I:WindowsRecycleBin", None, "STA recycle operation and supported-volume policy"),
          loc("I:RecycleProgressSink", None, "COM progress and cancellation callbacks"),
          loc("I:WindowsShellInteraction", None, "Native menu/property operations and verb inspection"),
          loc("I:ShellMenuNative", None, "COM/native declarations"),
          loc("I:ShellMenuInspection", None, "Inspection result model"),
          loc("I:WindowsFileDragSource", None, "Windows drag source and shell data object"),
          loc("I:WindowsFileDropTarget", None, "Windows drop target registration and callbacks"),
          loc("I:FileDragNative", None, "OLE drag-and-drop declarations"),
          loc("I:FileLockDiagnostics", None, "Enrich file failures with process identities"),
          loc("I:WindowsFileUsers", None, "Bounded native lookup of processes using a path"),
          loc("I:ConsoleWindowLookup", None, "Isolated console attachment and terminal window lookup"),
          loc("I:FileUserProcess", None, "Process identity, registered exit waits, and guarded termination"),
          loc("I:WindowsFileUserActions", None, "Window activation, explicit termination, and exit notifications"),
          loc("D:ShellInteractionController", None, "Native message-hook lifetime")])

story("T04", "E10", "Observe and recover filesystem locations", "receive bounded directory-change notifications and inspect availability", "I can refresh views without coupling them to watcher details",
      "The monitor combines filesystem events with a 180 ms quiet period, a two-second existence probe, and restart after watcher errors. Availability finds missing directories and their nearest existing parent. The home-directory adapter isolates environment access.", [
          loc("I:DirectoryMonitor", None, "Watcher, debounce, probe, and disposal"),
          loc("I:DirectoryAvailability", None, "Availability and existing-parent lookup"),
          loc("I:UserDirectories", None, "Home-directory environment boundary")])

story("T05", "E10", "Reuse command, editor, and comparison adapters", "resolve and launch external tools through explicit adapters", "I can add entry workflows without duplicating Windows process rules",
      "ShellExecute keeps raw arguments and refreshes the user and system environment for each launch. Completion uses a bounded filesystem and PATH lookup. The editor adapter finds Notepad++ and creates new files safely. The comparison adapter finds VS Code through App Paths, standard installation locations, or PATH and quotes the two file arguments. The Claude Code adapter finds the repository root and starts the installed tool.", [
          loc("I:WindowsExternalLauncher", None, "ShellExecute and error translation"),
          loc("I:WindowsLaunchEnvironment", None, "Serialized fresh environment scope and restoration"),
          loc("I:CommandLayoutRecovery", None, "Unique installed program recovery using keyboard layouts and the fresh PATH"),
          loc("I:CommandCompletions", None, "Environment/path/executable candidates"),
          loc("I:ExternalEditor", None, "Notepad++ resolution, safe file creation, and launch"),
          loc("I:VisualStudioCodeComparison", None, "VS Code discovery and comparison launch arguments"),
          loc("I:ClaudeCommitLauncher", None, "Repository root and installed Claude process launch")])

story("T06", "E10", "Expose archive formats through file-like adapters", "catalog, read, write temporary copies of, and create supported archive content behind common boundaries", "I can reuse file workflows for packaged content",
      "The archive layer validates names and case collisions, caches catalogs by source identity, limits nesting, catalog, and preview size, detects supported self-extracting headers, and cleans temporary previews. Extraction has its own size handling. The ZIP writer creates a new temporary archive and moves it into place.", [
          loc("I:ArchiveContents", None, "Catalogs, entry safety, nesting, temporary copies, and lifetime"),
          loc("I:ArchiveInput", None, "Self-extracting signatures and bounded input"),
          loc("I:ArchiveCompression", None, "Single-stream compression formats"),
          loc("I:ArchiveFileSystem", None, "Read-only filesystem and extraction-store adapter"),
          loc("I:ArchiveViewerFiles", None, "Viewer/report/search temporary copies"),
          loc("I:ZipArchiveWriter", None, "Atomic ZIP write, empty directories, and cancellation cleanup")])

story("T07", "E10", "Supply search scope and streaming text matches", "search physical/archive text and explain Git ignore decisions through one source", "I can evolve search scheduling without embedding storage or Git parsing in Core",
      "FileSearchSource combines physical and archive access. SearchGitScope reads tracked and ignore metadata and rule sources. SearchText detects encoding, streams literal matches, and bounds regex lines and time. Search returns one text match per file.", [
          loc("I:FileSearchSource", None, "Physical/archive source composition and failure translation"),
          loc("I:SearchGitScope", None, "Tracked paths and ignore-rule discovery"),
          loc("I:SearchText", None, "Streaming literal/regex matching and encoding fallbacks")])

story("T08", "E10", "Read and parse current Git state", "read repository and child-repository status without Git write side effects", "I can expose repository context through stable models",
      "The adapter parses porcelain status, bounds process time and child-repository concurrency, clears inherited Git-specific environment variables, and uses locally known remote state. When status is unavailable, the header stays empty.", [
          loc("I:GitStatusReader", None, "Repository discovery, process execution, status, and local branch metadata"),
          loc("I:GitPathStatus", None, "Porcelain parsing and staged/working path status")])

story("T09", "E10", "Expose Git history as virtual file locations", "read scoped logs, trees, changes, and messages through a read-only filesystem", "I can reuse panel navigation for repository history",
      "The adapter caches immutable commit data and refreshes live HEAD and working data. Logs load in pages of 200. First-parent and initial-commit rules are explicit. Git processes have bounded time, cancellation, literal pathspecs, and a cleaned Git environment.", [
          loc("I:GitHistoryReader", None, "Scoped log, tree, message, and changed-path reads"),
          loc("I:GitFileSystem", None, "Virtual contexts, paging, caches, and read-only filesystem"),
          loc("I:GitHistoryProcess", None, "Common history-process boundary")])

story("T10", "E10", "Write repository documents for ordinary viewers", "produce temporary blobs, reports, and diffs with origin metadata", "I can reuse the file viewer while snapshots stay read-only",
      "The adapter selects old, new, or working content, handles empty-file comparisons, turns off external diff and text conversion, and removes its temporary documents on disposal.", [
          loc("I:GitHistoryDocuments", None, "Reports, blobs, text diffs, and cleanup")])

story("T11", "E10", "Provide bounded file reading and text decoding", "read file windows and search full files through reusable streaming services", "I can keep viewer features independent of file size and encoding details",
      "File slices use bounded reads with lookahead and sharing that allows external writes and deletes. Text decoding handles BOMs, encoding boundaries, binary heuristics, byte offsets, and hex/binary formatting. Whole-file literal find is a separate streaming service.", [
          loc("I:ViewerFiles", None, "Bounded shared file reads and image-byte limit"),
          loc("I:ViewerText", None, "Encoding/signature detection, decoding, offsets, and formatting"),
          loc("I:FileTextSearch", None, "Forward/backward wrapping literal search")])

story("T12", "E10", "Add syntax languages behind a portable span model", "produce portable syntax spans and render them with readable colors", "I can extend highlighting without rewriting the viewer",
      "TextMate grammar selection and tokenization are bounded and return Core span records. Desktop turns spans into text styles, clips visible ranges, and adjusts token contrast for the theme. Grammar data and license resources are mapped in T25.", [
          loc("C:SyntaxDocument", None, "Portable language/token/span records and range selection"),
          loc("I:Syntax/SyntaxGrammarCatalog", None, "Language lookup and embedded grammars"),
          loc("I:Syntax/SyntaxHighlighter", None, "Bounded asynchronous tokenization"),
          loc("I:Syntax/SyntaxScopes", None, "Grammar scope to portable token classification"),
          loc("D:SyntaxPalette", None, "Theme-aware token contrast"),
          loc("D:SyntaxTextBox", None, "Syntax-aware text control template"),
          loc("D:SyntaxTextPresenter", None, "Combine syntax and selection styles")])

story("T13", "E10", "Build consistent reusable UI surfaces", "compose controls with shared layout, styling, and list rendering", "I can add workflows without copying presentation mechanics",
      "Shared primitives define spacing, contrast, colors, windows, dialogs, rows, formatting, and table columns. Rendering helpers in Desktop are technical by responsibility.", [
          loc("D:UiLayout", None, "Layout/control helpers"), loc("D:UiStyles", None, "Shared control styles"),
          loc("D:UiPalette", None, "UI tokens"), loc("D:UiContrast", None, "Contrast calculation"),
          loc("D:WindowHeader", None, "Shared window chrome and controls"),
          loc("D:DialogActions", None, "Dialog focus/default-action behavior"),
          loc("D:FileRowTheme", None, "Row selection/opacity template"),
          loc("D:FileNameText", None, "Match-range text rendering"),
          loc("D:FileSizeText", None, "Size formatting"), loc("D:PathText", None, "Display-path formatting"),
          loc("D:SearchTables", None, "Reusable search columns and width persistence"),
          loc("D:FileViewerWindow.TitleBar", None, "Shared viewer header controls"),
          loc("D:PanelView", "public PanelView(string id, IFileSystem fileSystem, FileTypes? fileTypes, IGitStatusReader? gitReader)", "Table composition and row binding mechanics"),
          loc("D:FileRow", "private void Changed(", "Change notification mechanism")])

story("T14", "E10", "Provide cached Windows file icons", "resolve system icons with bounded caching and correct native ownership", "I can show recognizable files without blocking list rendering",
      "The adapter resolves path-specific and type icons at physical DPI and releases native handles. The UI cache bounds entries and disposes bitmaps. The application icon loader belongs to packaging in T25.", [
          loc("I:SystemIcons", None, "Windows shell/GDI icon extraction"),
          loc("D:FileIconCache", None, "Bounded async cache and bitmap lifetime")])

story("T15", "E10", "Bridge application transfers to the platform clipboard", "read and write file lists and preferred drop effects through one adapter", "I can reuse clipboard interoperability apart from transfer rules",
      "The bridge encodes copy/cut intent, reads Avalonia storage items, observes activation and clipboard changes, and isolates its asynchronous errors. The same class also contains product logic mapped to U17 and U47.", [
          loc("D:PanelClipboardController", "private async Task<ClipboardFiles?> ReadAsync(", "Platform file-list/drop-effect decode"),
          loc("D:PanelClipboardController", "private async Task WriteAsync(", "Platform transfer encode"),
          loc("D:PanelClipboardController", "private async Task RefreshAsync(", "Clipboard observation and lifetime")])

story("T16", "E10", "Decode images behind a bounded presentation adapter", "turn supported image bytes into a displayable bitmap with resource limits", "I can reuse image preview without spreading codec details",
      "Skia decoding limits dimensions and pixel count and scales down large images.", [loc("D:ViewerImage", None, "Bounded image decode and fit bitmap")])

story("T17", "E10", "Coordinate asynchronous UI work and input routes", "compose services and route actions while keeping cancellation and pending work explicit", "I can add workflows with visible process and UI ownership",
      "The window composition root injects adapters and controllers. A common action path captures errors and pending work. Git navigation has a cancellable latest-action runner. Menus and key handlers are integration points for the actions they list.", [
          loc("D:CommanderWindow", "public CommanderWindow(", "Composition root and service ownership"),
          loc("D:CommanderWindow", "public async Task RunActionAsync(", "Pending work and error boundary"),
          loc("D:CommanderWindow", "private void OnKeyDown(", "Input-context precedence"),
          loc("D:CommanderWindow.Keyboard", None, "Context-sensitive shortcut dispatch"),
          loc("D:CommanderWindow.Menu", None, "Main-menu composition and action routing"),
          loc("D:CommanderFrame", None, "Frame composition"),
          loc("D:GitActionRunner", None, "Cancellable latest Git action and status lifetime"),
          loc("D:PanelView.Log", None, "Log-presentation composition"),
          loc("D:PanelView.CommitMessage", None, "Commit-message view composition"),
          loc("D:FileViewerWindow.Menu", None, "Viewer menu composition")])

story("T18", "E10", "Reuse installed keyboard-layout translation", "translate physical strokes and text through installed Windows layouts", "I can add layout-aware typing features without duplicating native input handling",
      "The adapter obtains layout alternatives while keeping dead-key state. Panel name jumping, location-chooser search, and viewer Alt+letter search use the same service.", [loc("I:KeyboardLayouts", None, "Layout enumeration, physical strokes, and text alternatives")])

# E11: Deliver and maintain a reliable application

story("T19", "E11", "Give controls stable feedback identities", "resolve an authored control to a stable source identifier", "I can locate reported interface targets independently of visual position",
      "An attached property holds literal feedback IDs. Target lookup walks ancestors and describes controls. WithFeedbackId calls in other views use this story; they are dependencies, not implementation.", [
          loc("D:FeedbackTarget", None, "Attached ID and helper"),
          loc("D:FeedbackController", "internal static Control? Target(", "Resolve target ancestor"),
          loc("D:FeedbackController", "internal static FeedbackInteraction DescribeInteraction(", "Create a stable control description")])

story("T20", "E11", "Coordinate feedback work through a durable issue queue", "list, claim, close, release, and reopen feedback items transactionally", "I can implement requested refinements without conflicting agent batches",
      "The CLI and store validate IDs and legal transitions, allow one in-progress batch, keep claimed work from deletion, and import legacy notes once. Native SQLite uses transactions, bound parameters, schema versioning, and a busy timeout. ForApplication finds the repository by searching for Xcmd.slnx.", [
          loc("C:FeedbackIssue", None, "Issue state, validation, and legacy status mapping"),
          loc("I:IssueCli", None, "Batch workflow commands and JSON output"),
          loc("I:IssueStore", None, "Repository store, atomic batch guard, transitions, and legacy import"),
          loc("I:IssueDatabase", None, "winsqlite3 boundary, schema, SQL binding, and durability")])

story("T21", "E11", "Save and validate workspace data apart from the UI", "save versioned workspace models with atomic replacement", "I can evolve session features without coupling file formats to views",
      "Core validates panel, command, search, favorite, and usage state and the window size. Desktop falls back to a known viewer theme. Source-generated JSON and temporary-file replacement isolate serialization and storage. The schema serves many features and counts as one technical story.", [
          loc("C:WorkspaceState", None, "Workspace records and validation"),
          loc("I:SessionStore", None, "Load, validate, and atomic save"),
          loc("I:SessionJsonContext", None, "Source-generated serialization"),
          loc("D:WorkspaceLifecycle", "public async Task SaveAsync(", "Serialize save requests and capture UI state")])

story("T22", "E11", "Own startup, shutdown, and diagnostics explicitly", "compose the desktop host and dispose its background work predictably", "I can diagnose failures and add services with clear lifetimes",
      "Startup separates the issue CLI from the GUI, injects lifecycle and window presentation seams, configures software rendering, and records startup failures. WorkspaceLifecycle owns timers, cancellation, and closing.", [
          loc("D:Program", None, "Entry point, App composition, injectable startup seams"),
          loc("D:DesktopPlatform", None, "Rendering and platform configuration"),
          loc("D:WorkspaceLifecycle", "public WorkspaceLifecycle(", "Own timers, event subscriptions, and services"),
          loc("D:WorkspaceLifecycle", "public void Dispose(", "Stop and dispose background lifecycle work"),
          loc("I:StartupDiagnostics", None, "Best-effort startup failure record")])

story("T23", "E11", "Select and hand off immutable local releases", "resolve release evidence and confirm a replacement process is ready", "I can ship builds while the working process stays open until its replacement is ready",
      "ReleaseFiles validates release IDs, the protocol, and check results with a positive total and zero failures, and reads releases/latest.txt. ReleaseCatalog identifies the running release from the process directory. The handoff uses a readiness handshake with timeouts and cleans up a failed child. The standalone launcher is built from a subset of Infrastructure.", [
          loc("I:ReleaseFiles", None, "Release ID/evidence validation and latest-release marker"),
          loc("I:ReleaseCatalog", None, "Running release ID and installation layout"),
          loc("I:RestartProcess", None, "Process handoff, readiness timeout, and launch settings"),
          loc("I:BootstrapProgram", None, "Stable launcher and shortcut setup entry")])

story("T24", "E11", "Keep Windows launch identity stable", "set consistent shortcut and taskbar metadata across release executables", "I can change installed builds while the application entry point stays the same",
      "Windows shortcut and property-store COM adapters keep the app identity and relaunch metadata consistent. The Desktop wrapper reports initialization failures.", [
          loc("I:WindowsShortcut", None, "NativeAOT-compatible shortcut COM calls"),
          loc("I:WindowsTaskbar", None, "Process/window app identity"),
          loc("I:ShellProperties", None, "Property-store access"),
          loc("I:ShellPropertyValues", None, "Native property value types"),
          loc("D:TaskbarIdentity", None, "Desktop attachment and failure reporting")])

story("T25", "E11", "Package reproducible application dependencies and resources", "declare the three production projects and their versioned resources", "I can build and extend the application with explicit inputs",
      "Project files, lock files, icons, file-color defaults, grammar data, and licenses are mapped as auxiliary evidence. Infrastructure has a separate launcher build mode and lock file within the three production projects.", [
          loc("I:FileTypeSettings", None, "Bundled/external color configuration loading"),
          loc("I:FileTypesJsonContext", None, "Source-generated color configuration serialization"),
          loc("D:ApplicationIcon", None, "Application icon resource loading"),
          loc("C:Xcmd.Core.csproj", None, "Core project"), loc("D:Xcmd.Desktop.csproj", None, "Desktop project and analyzer reference"),
          loc("I:Xcmd.Infrastructure.csproj", None, "Adapter/launcher build modes and embedded resources"),
          loc("C:packages.lock.json", None, "Locked Core graph"), loc("D:packages.lock.json", None, "Locked Desktop graph"),
          loc("I:packages.lock.json", None, "Locked Infrastructure graph"), loc("I:packages.launcher.lock.json", None, "Locked launcher graph"),
          loc("D:config/file-types.json", None, "Ordered default color rules"),
          *[loc("D:Assets/" + name, None, "Application identity asset") for name in ["app.svg", "app-16.svg", "app-20.svg", "app-24.svg", "app-28.svg", "app-32.svg", "app-40.svg", "app-48.svg", "app.ico", "app.png"]],
          *[loc("I:Syntax/Grammars/" + name, None, "Bundled grammar or license") for name in ["kotlin.json", "toml.json", "kotlin-license.txt", "toml-license.txt", "textmate-license.txt", "taplo-license.txt"]]])

# Record dispatch sites separately so menu and shortcut fan-out remains visible.
MAIN_MENU = {
    "U01": ['Item("Parent directory"', 'Item("Home folder"', 'Item("Drive root"', 'Item("Switch panel"', 'Item("Open", ()'],
    "U03": ['Sort("Name"'], "U05": ['Item("Go to directory"'], "U06": ['Item("Favorite directories"'],
    "U08": ['Item("Reset column widths"', 'Item("Reload file colors"'],
    "U12": ['Item("Rename"'], "U13": ['Item("New folder"'], "U14": ['Item("New file and edit"'],
    "U15": ['Item("Copy to other panel"', 'Item("Move to other panel"'], "U16": ['Item("Copy in this panel"'],
    "U17": ['Item("Copy",', 'Item("Cut"', 'Item("Paste into current folder"'],
    "U19": ['Item("Move to Recycle Bin"', 'Item("Delete permanently"'], "U21": ['Item("File operations"'],
    "U23": ['Item("Calculate folder sizes"'], "U24": ['Item("Find files"'], "U31": ['Item("View file or commit"'],
    "U39": ['Item("Open as archive"'], "U41": ['Item("Copy to other panel"'],
    "U43": ['Item("Edit in Notepad++"', 'Item("Open with Windows"'], "U44": ['Item("Focus command line"'], "U47": ['Item("Copy full path"'],
    "U58": ['Item("Compare files in VS Code"'],
    "U50": ['Item("Log of selected file or folder"', 'Item("Log of current folder"'], "U53": ['Item("Changed files in current commit"'],
    "U55": ['Item("Diff selected file"'], "UD01": ['Item("Comment on an element"'], "UD02": ['Item("Refinement list"'],
}
VIEWER_MENU = {
    "U31": ['previousFile = MenuCommand('], "U32": ['foreach (var mode in Enum.GetValues'],
    "U33": ['TextCommand("Beginning of file"'], "U34": ['TextCommand("Find (also Alt+letter)"'],
    "U35": ['view.Add(BuildSyntaxMenu());'], "U36": ['view.Add(BuildThemeMenu());'],
    "UD01": ['MenuCommand("Comment on an element"'], "UD03": ['reportSyntax = MenuCommand('],
}
KEYBOARD = {
    "U05": ["private bool HandlePanelCommand("], "U06": ["private bool HandleFavoritesShortcut("],
    "U11": ["private bool HandlePanelNavigation("], "U13": ["private bool HandlePanelNavigation("],
    "U14": ["private bool HandlePanelCommand("], "U15": ["private bool HandleFileOperationKey("],
    "U17": ["private bool HandleClipboardKey("], "U19": ["private bool HandleFileOperationKey("],
    "U31": ["private bool HandleViewOrEditKey("], "U41": ["private bool HandleFileOperationKey("],
    "U45": ["private void HandleCommandLineKey("], "U46": ["private void HandleCommandLineKey("],
    "U47": ["private void HandleCommandLineKey(", "private bool HandleCommandLineShortcut("],
    "U58": ["private bool HandleComparisonKey("],
}
for item in STORIES:
    for table, file in [(MAIN_MENU, "D:CommanderWindow.Menu"), (VIEWER_MENU, "D:FileViewerWindow.Menu"), (KEYBOARD, "D:CommanderWindow.Keyboard")]:
        for anchor in table.get(item["id"], []):
            item["refs"].append(route(file, anchor))
