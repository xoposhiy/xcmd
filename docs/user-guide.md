# User guide

## Run

Open the permanent `Xcmd.exe` next to the `releases` folder:

```text
Xcmd.exe
Xcmd.lnk
releases/latest.txt
releases/<release-id>/Xcmd.Desktop.exe
```

Requirements: Windows x64 and the .NET 10 runtime. Review builds are already compiled and checked; no launch script or build command is needed. The launcher reads `releases/latest.txt`, checks the selected release, starts that executable and exits. Keep `Xcmd.exe` next to `releases`, and keep each release's files together.

To keep Xcmd on the taskbar, right-click `Xcmd.lnk` and choose **Pin to taskbar**. On Windows 11, first choose **Show more options** if needed. You can also pin the running Xcmd window. If an older taskbar pin points directly into a release directory, unpin that item and pin the permanent shortcut once. All new versions share the same taskbar identity and relaunch through `Xcmd.exe`.

Create other shortcuts with `Xcmd.exe` as the target. Set the shortcut's **Start in** folder to choose the first-launch working directory, or add two directory arguments to choose panel locations. The supplied `Xcmd.lnk` uses the launcher's directory. A saved session still takes priority when no directory arguments are supplied.

Each release has a separate directory. Running a release does not lock development build files or prevent the next release. Existing releases are never overwritten. `releases/latest.txt` contains the latest completed release ID.

The launcher does not wait for the application to close and does not keep a release marker open. New releases can be published while Xcmd runs. A failed verification or publication keeps the previous launch target. If no valid release is available, the launcher shows an error instead of guessing a version from folder names. A release-specific executable remains available for direct launch.

The application opens the working directory in both panels. File operations change real files in the displayed locations.

On later launches, the application restores the saved session. Explicit directory arguments override the saved panel locations.

To open specific directories:

```powershell
& ./Xcmd.exe C:/work C:/work/projects
```

## Controls

| Input | Action |
| --- | --- |
| Up / Down, Home / End, Ctrl+Home / Ctrl+End | Move the file cursor and keep marks |
| PageUp / PageDown | Move by approximately one visible page and keep marks |
| Shift+Up / Shift+Down | Toggle the departed row and move the cursor; plain arrows keep the marks |
| Shift+Home / End / PageUp / PageDown | Mark a range to a boundary or page; repeat a fully marked range to remove its marks |
| Ctrl+A, Ctrl+click, Shift+click | Select all, toggle a row, select a range |
| Tab / Shift+Tab | Switch panels |
| Enter / double-click a directory | Open the directory |
| Enter / double-click a file | Run the application or open the file through its Windows association |
| Plain typing / Left / Right / Ctrl+L | Focus the command line; typing inserts text |
| Enter in the command line | Run through Windows ShellExecute from the active panel directory |
| `cd <path>` | Change the active panel directory; relative and quoted paths work |
| Up / Down in the command line | Return focus to the active file panel |
| Alt+Up / Alt+Down in the command line | Recall an older / newer command; return to the unfinished draft |
| Ctrl+Enter / Ctrl+Shift+Enter | Insert the current file name / full path; quote spaces |
| Escape | Clear file marks and the command line, then focus files; editing and search keep their own first Escape action |
| Enter / double-click on `..`, Ctrl+PageUp, Backspace | Open the parent directory and restore its cursor |
| Alt+Down / panel History button | Open the shared navigation window with History active |
| Ctrl+D | Open the same navigation window with Favorites active; Insert adds the current directory, F2 edits its label, Delete removes the favorite |
| Ctrl+Left / Ctrl+Right | Open the pointed directory in the specified other panel; use the current directory when pointing at a file |
| Alt+first letter, then letters | Jump by name; release Alt and continue typing. Ignore unmatched letters. Navigation or Escape ends the search |
| Header click | Sort; click again to reverse the direction |
| Ctrl+F3 / F4 / F5 / F6 | Sort by name / extension / modified date / size |
| Drag a header separator | Resize a column |
| Shift+F6 | Rename the current item in the name cell |
| Enter / Escape in the rename editor | Commit / cancel rename |
| Tab in the rename editor | Commit rename and switch panels on success |
| Ctrl+R | Optional immediate reload; normal directory updates are automatic |
| Alt+Shift+Enter | Calculate all child folder sizes in the active panel; press again or Escape to cancel |
| F7 | Open the new-folder dialog |
| F5 | Confirm copying selected files and folders to the other panel |
| Shift+F5 | Confirm a copy in the active panel |
| F6 | Confirm moving selected files and folders to the other panel |
| F8 / Delete | Open deletion confirmation with Recycle Bin selected |
| Shift+Delete | Open the same confirmation with permanent deletion selected |
| F12 / Feedback button | Highlight an interface element and add a comment, including inside application dialogs |
| Notes button | View the saved refinement list and remove comments |
| View > Reset column widths | Restore the active panel's default column widths |

Sort headers accept clicks across their area; the strip at the right edge resizes a column. The final column fills remaining space and has no resize handle. Size defaults leave room for byte counts up to ten GiB. Type headings and values are centered; Size headings align with byte counts. File names have Windows system icons. The cursor uses one blue background with or without marks. The inactive panel uses a faint cursor background. Marked files keep an amber check and file name, including under the cursor. Moving the cursor does not change row size or text position. Shift+Up and Shift+Down remove an existing mark when the row is marked again. Panels have square corners. Table rows stay above the footer and outside the space reserved for scroll bars. Only a top strip marks the active panel; the sides and bottom have no outline. The executable, main window and owned dialogs use the Xcmd application icon.

## File operations prototype

Default confirmation buttons use a blue background and respond to Enter. Focusing Cancel makes Cancel the Enter action and keeps the files unchanged. The highlighted action follows the active editor or button in navigation and conflict dialogs. The multiline comment editor keeps Enter for a new line and Ctrl+Enter to add the comment.

F5 confirms copying selected files and folders to the other panel. F6 confirms moving them. Edit the destination before pressing Enter: use an existing directory, or a full new name for one item. Shift+F5 proposes a copy in the active panel with a new name. Copying a batch into its source directory is rejected. Shift+F5 for one item proposes an explicit new name; it does not allow replacing the source path. Each request keeps the source paths and confirmed destination; later panel navigation does not change them. F7 creates a folder; Shift+F6 keeps inline rename.

Requests run in order in the background. Both panels remain available. The operation strip appears after one second of running work and hides shortly after completion. Short operations report their result in the status line. The strip uses a fixed-height summary with full paths in hover help. Operations first count files without changing the destination. Progress shows processed files against that total, a bar based on file bytes, and an estimated remaining time. Selected root items are labelled separately in the history. Counting is cancellable; incomplete counts are reported. Remaining time excludes counting and conflict dialogs and is an estimate, not a fixed completion time. Choose **Details** or **View > File operations** to inspect requests and individual results, including hidden short operations. **Cancel current** stops the running request; **Cancel selected** also works for a queued request. Completed changes remain after cancellation. Cancellation does not roll back a batch.

Folders with the same name merge. Destination-only files stay in place. Regular files with equal contents, sizes, modification times and basic attributes are skipped without a question. Other existing files require **Skip**, **Rename**, **Replace**, or **Cancel operation**. The compact conflict dialog compares source and destination sizes and modification times; hover over each shortened directory to see its full path. Apply-to-all affects matching conflict types in that request only. A folder cannot replace a file, and a file cannot replace a folder. Finish an open dialog, rename or command-line edit before the next conflict dialog appears.

Copies use a temporary destination file and replace the final path only after the copy finishes. A cancelled copy keeps an existing destination file. Moves use a filesystem rename when possible. The copy-delete fallback removes each source only after copying that item succeeds. A skipped or failed child keeps the remaining source folder available. Results identify partial changes and failures.

F8 or Delete opens a short deletion question with **Recycle Bin** selected. Shift+Delete selects **Permanently** instead. The underlined **R** and **P** show the **Alt+R** and **Alt+P** access keys. Choose either mode in the same dialog, then press Enter or the highlighted **Yes** button. Escape or **Cancel** keeps the files. Windows processes each recycled tree as one item; cancellation takes effect between selected items. Recycling is limited to local fixed drives. If Windows would delete an item permanently instead, Xcmd refuses that operation. Permanent deletion checks cancellation between files.

Copy and move report unsupported links and junctions without following them. Writes through linked parent directories are also rejected. Permanent deletion removes a link itself and does not traverse its target. Pause, retry, queue reordering, content comparison and newer-file conflict rules are not included in this prototype.

## File colors

File groups and colors are configured in `%LOCALAPPDATA%/Xcmd/file-types.json`. The application copies the bundled `config/file-types.json` only when the user file does not exist. Release changes preserve the user file. Edit the JSON file and choose View > Reload file colors, or restart the application. Invalid edits keep the current colors and show a message; the application does not overwrite the invalid file.

Rules are evaluated from top to bottom. Each rule has `name`, `color` (`#RRGGBB`), `extensions` (including the dot), and `fileNames`. Names and extensions ignore case. The first match wins. Directories and unmatched files keep the normal color; selected names use white for contrast.

The defaults use pale purple for source code, amber for programs and installers, and pale blue for text and configuration files. Source rules come first, so `.sh`, `.ps1`, `.cmd` and `.bat` use the source color even though executable rules also include them. Exact file names cover extensionless files such as Dockerfile, Makefile, README and `.env`.

## Open files and applications

Enter or double-click opens the current file through the Windows shell. Executable files run; documents use their configured application; shortcuts keep their target and working-directory settings. Windows associations decide the action for every color group. A missing file or failed launch shows an error and keeps the panel cursor. Holding Enter starts one request until the key is released.

The launch adapter uses the Windows file association through [ProcessStartInfo.UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0). Xcmd passes the file path separately from command text.

Opening View > File operations focuses the request list. Use arrows to select a request and Tab to reach cancellation and details controls. Cancelling a request or changing its status keeps focus in the history. Hiding details or pressing Escape returns focus to files. An operation waiting for a conflict decision shows its reason while the current edit or dialog finishes. Move and delete keep only surviving file marks; an empty mark set stays empty.

## Command line

Before each Windows Shell launch, Xcmd reads the current system and user environment from Windows. New processes receive added, changed and removed settings without restarting Xcmd. Program lookup uses the current `PATH`, and `%VARIABLE%` in a program target uses the current value. Variables inherited only by the Xcmd process are preserved. Xcmd restores its own process environment after each launch, including failed launches.

Already running terminals keep their existing environment. If an external application handles a launch in an existing process, that application controls the environment of its new windows or tabs.

The single command row below the panels starts programs and opens files through Windows ShellExecute. Every launch uses the active panel's current directory. Type a program name such as `cmd`, a file, a shortcut or a URL, then press Enter. Quote a program path that contains spaces. Xcmd passes the argument text to Windows without a command interpreter. To use interpreter commands, invoke the interpreter explicitly, for example `cmd /c dir`. There is no embedded terminal or shell selector.

Plain typing from files inserts text in the command line. Left, Right and Ctrl+L focus the line. Up or Down returns focus to the active panel without moving its cursor. Escape clears the line and file marks, then returns focus to that panel without moving the cursor. Alt+Up and Alt+Down recall commands and restore an unfinished draft after the latest command. From files, Alt+Up recalls a command; Alt+Down keeps directory history. Tab from the line switches to the other panel.

Ctrl+C copies selected command text. Without a selection, Ctrl+C copies the whole command and keeps the caret in place. An empty command leaves the clipboard unchanged. Long directory paths keep their right end visible; hover over the path to see the full directory. Keyboard hints use separators and wrap in narrow windows.

`cd <path>` changes the active panel directory. It accepts relative paths, absolute paths, quoted paths, `cd..` and the optional `/d` form. Bare `cd` keeps the current directory. A failed directory change leaves the panel and command text available for correction. `cd` does not start an external program.

Ctrl+Enter inserts the current file name from the active panel. Ctrl+Shift+Enter inserts the full path. Names with spaces use quotes. Insertion replaces selected text, adds a trailing space and does not execute the command. Rename arrows and Alt name search stay in their own controls; Ctrl+Left/Right keeps opposite-panel navigation.

A successful launch clears the submitted text and records it in shared command history. A failure shows an error and preserves the text. A pending launch shows an opening message and ignores repeated submission. Input changed during a pending launch stays intact. History keeps the latest 100 unique successful commands. The draft, history, caret, selection and focus are saved with the workspace. Restoring a draft never executes it. Command focus delays automatic release changes; use Up or Down to resume file navigation.

## Name search

Press Alt and the first letter, release Alt, and continue typing to extend the name prefix. The list stays visible and the cursor moves to a matching entry. A letter with no matching name is ignored, so the existing prefix stays usable. Escape, cursor navigation, pointer movement, commands, panel switches and window deactivation clear the prefix. Name jumping keeps file marks. The status line shows the accepted prefix. An active search delays directory refresh and automatic release changes.

Search handles physical keys even when Windows sends no text input, tries the loaded Windows keyboard layouts, and avoids counting a key and its text event twice. For example, Russian `еуые` can find English `test`. Physical key translation and actual input routing are covered separately; headless checks do not simulate the complete native Windows input backend.

## Automatic directory updates

Both panels monitor external file creation, deletion, rename, size, timestamp and attribute changes. Updates are coalesced and preserve the file cursor, selection and scroll position. Applying changes waits for editing, menus, dialogs and feedback to finish. A read that completes during rename cannot replace the editor. If the displayed directory disappears, the panel opens its nearest accessible parent. Monitoring errors trigger an automatic retry.

## Folder sizes

Press Alt+Shift+Enter or choose View > Calculate folder sizes. The application totals regular file bytes recursively for every child directory in the active panel. Results appear in Size as each folder completes. Directory order stays alphabetical. The status line reports progress; Escape or the same shortcut cancels. Keyboard navigation and panel switches remain available. Leaving the source directory or closing the application cancels its scan. A running scan delays release changes and automatic directory refresh.

Links and junctions are skipped rather than followed. Unreadable directories do not stop other results. A `≥` prefix marks a lower bound when links or unreadable directories were excluded. The size tooltip shows measurement time, file count, skipped links and unreadable-directory count. These are measured logical file bytes, not allocated disk space or a live filesystem snapshot.

Sorting and session restore keep completed measurements, including automatic release changes. An unchanged monitor refresh preserves them. A changed directory listing or explicit Ctrl+R clears measurements for recalculation. Files can change below a folder after measurement; use the timestamp and recalculate when needed.

## Session restore and automatic updates

Launch the current release once. When a newer verified release is published locally, the running application switches to it automatically after two seconds without input. Directory reads, file operations, command-line focus, inline rename, open dialogs and feedback mode delay the switch. The header reports when an update is waiting.

The application saves the command draft, history, caret, selection and focus, both panel locations, file cursors, selected entries and mark mode, sorting, directory history and its cursors, scroll positions, column widths, the active panel, window size, position, maximized/minimized state and activation, and feedback comments. The replacement restores this state before announcing readiness, waits for the previous process to exit, and then shows its window. A background or minimized window is not activated during an update. A replacement startup failure leaves the current version open. Missing directories fall back to an accessible parent; missing selected files are omitted.

The session is also saved on normal close. State is stored under `%LOCALAPPDATA%/Xcmd/session.json`, outside release directories. Invalid state on a normal launch starts a new session with a visible message; invalid state during an update blocks the update. Moving a native window between monitors remains outside headless verification.

Automatic updates run only from release directories. Development builds and automated tests do not monitor the user's releases or use the user's saved session.

## Comment on the interface

Choose **Feedback** or press F12, then move the pointer to highlight an element and click it. Every application dialog has its own Feedback button and F12 shortcut. The comment window belongs to the window being annotated, so its draft, selection and workflow stay intact. This also works for the comment editor itself. Labels, status messages, errors and disabled actions can all be selected for a comment. Selection uses the visible element instead of its outer window and does not activate the underlying control. Write a comment and choose **Add comment** or press Ctrl+Enter. Escape first cancels selection; when not selecting, Escape closes the comment dialog. The selected element stays outlined while the comment dialog is open.

Choose **Notes** to see the refinement list. Each comment records the element, directory, release and time. Comments are saved immediately, survive updates and ordinary restarts, and are compiled into `%LOCALAPPDATA%/Xcmd/refinements.md` as an English Markdown checklist. User comments keep their original text. The Notes window shows the saved list's location and can remove a selected comment.

A release can include `feedback-resolutions.json`. On startup it marks matching comments as resolved, preserving their text and recording the release and an English explanation. Unrelated comments remain open. The Notes button counts open comments; the list keeps resolved comments and the Markdown checklist marks them complete.

Current defaults: select the full name for rename, keep focus in the source panel after Ctrl+Left/Right, cancel an uncommitted rename when clicking another row or panel, sort directories by ascending name before files, and use natural numeric name order. These are the current defaults. Future requirements are listed in [the use cases](use-cases.md).

The parent navigation row `..` stays first in every sort order. A filesystem root has no parent row. The parent row is excluded from file counts, rename and copy operations.

The inline editor is an application-owned `TextBox` in a `TableViewColumn.CellTemplate`. Editor keys stay inside the editor instead of reaching table row navigation. `TableView` does not provide a built-in editing transaction or filesystem sort policy. The command line is a separate single-row control below both panels.

## Directory navigation

Each panel keeps its own recent directories. Alt+Down opens history with the most recent previous directory selected. Use arrows and Enter, double-click a row, or choose Open. Returning restores that directory's last file cursor and focuses the source panel. Escape or Cancel keeps the current directory, cursor and selection.

One window has three lists: this panel's History, shared Frequent locations and shared Favorites. Alt+Down starts in History; Ctrl+D starts in Favorites. The shortcuts also switch lists when the window is already open. Left/Right switches lists; Up/Down selects within a list. Tab and Shift+Tab follow the normal control order through all three lists, the favorite editor and action buttons. Only the active list has a selection, and each list remembers its cursor. Shortened rows have full-path tooltips. The History link has no dropdown arrow; its tooltip shows Alt+Down.

Frequent locations exclude filesystem roots. A visit qualifies after ten active seconds in the current panel's directory, once per stay. Background windows, dialogs, busy operations and more than a minute without input pause attention tracking. Timer suspension does not count as work. Qualified visit weight halves every three days. Quick pass-through navigation, reloads and failed reads earn no visits. Statistics keep up to 512 locations and persist with the session; history seeds fallback candidates. Directory history, favorites and panel state are preserved.

History contains successful visits only. It excludes the current directory, merges repeated paths without regard to letter case, and keeps up to 100 locations including the current one. Refresh, sorting, failed reads and cancelled reads do not add visits. History and directory cursors are saved with the session. Back/forward shortcuts are not implemented yet.

An unavailable directory shows an error in history and allows another selection or retry. Pending navigation shows `Opening directory…`; repeated submission is ignored. Escape cancels the pending read so a late response cannot change the directory after dismissal.

## Favorite directories

Press Ctrl+D or choose Navigate > Favorite directories. The shared navigation window starts in its Favorites list. Up/Down selects a favorite; Enter, Open or a double-click opens it in the panel that requested the window. Returning to a known directory restores its file cursor. Escape or Cancel closes the window and keeps the current workspace.

Choose Add current directory or press Insert in the list. Adding the same path again selects its existing favorite. Press F2 to edit its name and Enter or Save name to save the label. Delete in the list or Remove deletes only the saved favorite. Name editing uses ordinary text keys. Empty labels show an error and keep the saved name.

Changes are saved immediately with the session and survive release changes. Existing sessions without favorites start with an empty list. No sample favorites or Total Commander imports are added. The initial limit is 256 favorites; labels support 1 to 100 characters. Full paths appear in tooltips when a row is shortened.

An unavailable location leaves the dialog open with an error for retry or another choice. A pending read disables repeated submission; Escape cancels the read. Favorites supports F12 feedback without losing the name draft. Open dialogs delay automatic release changes and external directory refresh.
