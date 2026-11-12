# User guide

## Git status

A compact group beside each panel's folder path shows the repository branch and whole-repository status: + staged, ~ changed, ? untracked, − deleted, ! conflicts. A clean working tree uses a green branch name instead of a checkmark. A cloud indicates any configured remote, including without a tracking branch. Counters follow the symbols; their meanings and full values are available in the tooltip. File and selection counts and the total bytes of selected files remain in the bottom status bar. Directories contribute their already calculated sizes; unknown or partial folder sizes show an incomplete lower bound without starting a scan. Ahead/behind counts use the locally known configured upstream (↑ / ↓), showing each arrow only when its count is nonzero; Xcmd never fetches. Detached HEAD shows the short commit ID. Git must be installed and available on PATH.

Status refreshes in the background every three seconds, with cancellation on navigation and a five-second query timeout. It does not block file loading or keyboard input. Git appears as a compact, noninteractive badge immediately after the path, without a fixed split of the header. The file table keeps its position while status updates. Outside a working tree, in archives, in search results, or when Git is unavailable, the Git area is empty. Git text uses 14-unit semibold Consolas on a subtle raised surface: clean branch names and staged/untracked counters are green, modifications amber, deletions/conflicts red. Long values retain their full text in tooltips.

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

File panels shrink the Name column as space decreases, then hide Modified, Size and Type in that order. Widening the panel restores those columns and retains your column width preferences. In Find files, To panel has an underlined P for Alt+P. The primary blue button follows keyboard context: Search in query fields and Go to file in results.

The main window combines menus and window controls in one title row. Drag the free title area to move the window, or double-click it to maximize or restore. Feedback and Notes are on the right of the bottom status bar.

| Input | Action |
| --- | --- |
| Up / Down, Home / End, Ctrl+Home / Ctrl+End | Move the file cursor and keep marks |
| PageUp / PageDown | Move by approximately one visible page and keep marks |
| Shift+Up / Shift+Down | Toggle the departed row and move the cursor; plain arrows keep the marks |
| Shift+Home / End / PageUp / PageDown | Mark a range to a boundary or page; repeat a fully marked range to remove its marks |
| Ctrl+A, Ctrl+click, Shift+click | Select all, toggle a row, select a range |
| Tab / Shift+Tab | Switch panels |
| Enter / double-click a directory | Open the directory |
| Enter / double-click a file | Enter a common archive, or open another file through its Windows association |
| Ctrl+PageDown | Try to open the current file as an archive, regardless of extension |
| F3 / F4 on a file | View inside Xcmd / edit in Notepad++ |
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
| Ctrl+Left / Ctrl+Right | Open the pointed directory or archive contents in the specified other panel, keeping source focus; use the current directory for an ordinary file |
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
| Alt+F7 | Find files by name, mask or text below the active directory |
| Ctrl+Backslash | Open the active panel's drive root |
| Ctrl+Backquote / Ctrl+ё | Open the home directory using the same physical key in either keyboard layout |
| F5 | Confirm copying selected files and folders to the other panel |
| Shift+F5 | Confirm a copy in the active panel |
| F6 | Confirm moving selected files and folders to the other panel |
| F8 / Delete | Open deletion confirmation with Recycle Bin selected |
| Shift+Delete | Open the same confirmation with permanent deletion selected |
| F12 / Feedback button | Highlight an interface element and add a comment, including inside application dialogs |
| Notes button | View the saved refinement list and remove comments |
| View > Reset column widths | Restore the active panel's default column widths |

Sort headers accept clicks across their area; the strip at the right edge resizes a column. The final column fills remaining space and has no resize handle. Size defaults leave room for byte counts up to ten GiB. Type headings and values are centered; Size headings align with byte counts. File names have Windows system icons at their native pixel size, with a source large enough for the display scale and no resampling. Panel text uses a larger font. The cursor uses one blue background with or without marks. The inactive panel uses a faint cursor background. Marked files keep an amber check and file name, including under the cursor. Moving the cursor does not change row size or text position. Shift+Up and Shift+Down remove an existing mark when the row is marked again. Panels have square corners. Table rows stay above the footer and outside the space reserved for scroll bars. Only a top strip marks the active panel; the sides and bottom have no outline. The executable, main window and owned dialogs use the Xcmd application icon.

## Find files

Search opens as one independent window. You can keep using the file panels and their commands. Alt+F7 returns to the same search, including its results, and restores it if minimized.

Press **Alt+F7**, enter a file name or mask, text inside files, or both, and press Enter. The dropdown beside File name selects **Contains** or **Whole name**. Contains matches part of a name; Whole name requires the complete name; `*` and `?` select a mask. Both fields must match when both are filled. Text ignores case by default. Enable **Match text case** or **Regex** when needed. Ctrl+F focuses the text field.

Search focuses the result list. Up from its first row returns to Containing text; Down from Containing text returns to results. Up from Containing text focuses File name, and Down from File name focuses Containing text. Search and Stop sit below both fields and apply to all query settings. Alt+F focuses the file name; Alt+T focuses the text. Alt+Down opens the focused field's query history. Arrows and Enter recall a previous query without running it. Drafts, settings and histories are saved with the workspace. **Treat * and ? as text** turns off wildcard matching; it does not require whole words.

Results appear as files are searched. Compact rows show the path and line number, with the preview in a separate right column. Drag column separators to change their widths. All three search tables save their widths. Tab reaches the list. Enter opens the containing folder and selects the file; F3 previews the matching text. Closing the viewer returns to the search results.

The first pass searches ordinary files and supported archives. Encoding detection is automatic, and binary contents are skipped. Git-ignored files, dependency directories such as `node_modules`, environments and version-control data stay deferred. The skipped-scope summary separates Git-ignored files and folders from standard folders. Expand it to inspect paths, reasons and Git rules. Counts cover known files and folder roots, not all files inside skipped folders. Skipped paths use Reason, Path and Rule / source columns. Issues use Reason, Path and Explanation columns. Short reason labels stay separate from full paths and explanations. Double-click a row in either table to search that folder, single file or archive with the current query and replace the current results. The status and **Search skipped files** action use the same term. The action appears only when something was skipped. Click it or press Alt+I to include the skipped locations while keeping earlier matches. Starting directly inside a deferred directory searches that directory.

Editing fields and checkboxes keeps current matches and background work. Press Search, or Enter in a query field, checkbox or closed name-mode list, to apply changes. An unchanged running query is not restarted. Stop keeps current matches and marks the search incomplete. Escape stops active work; press Escape again to close. The status reports incomplete coverage, with affected paths in collapsed details. Plain text search handles long lines. See [search behavior](search.md) for the current scope and limits.

## Archive browsing prototype

Enter or double-click a ZIP, RAR, 7z, TAR or compressed archive to browse its entries in the same panel. Folders, sorting, marks and parent navigation work as in a regular directory. The footer identifies a read-only archive. Backspace, Ctrl+PageUp or the parent row leaves a folder; at the archive root, it returns to the containing location and selects the archive. Nested archives use the same controls. Saved workspaces restore archive locations and cursors. Ctrl+R reloads archive contents; archive panels do not use the filesystem directory monitor.

Ctrl+PageDown tries the file contents instead of its extension. Use it for DOCX, XLSX, PPTX and other ZIP-based containers, files with unusual extensions, and recognized self-extracting EXEs. Ordinary Enter on Office files and EXEs still uses the Windows association. Self-extracting support looks for ZIP, RAR or 7z headers in the first 32 MiB of a Windows EXE. This does not support every installer format and never executes the EXE while probing it.

Opening an archive shows its name and `Escape cancels` while the original panel remains usable. Escape cancels the pending read and keeps the current location, cursor and command draft. Switching panels during a read keeps focus in the selected panel after completion.

To open an ordinary archive through Windows, choose File > Open with Windows, or press Ctrl+Enter and then Enter. The first key inserts its name into the command line; the second runs the existing Windows ShellExecute workflow. Inside an archive, external opening uses a temporary copy and the physical containing directory. Extract a file with F5 before editing it; F4 rejects archive entries to avoid saving changes only to a temporary copy.

F3 on an archive opens a text report with its format, available size and compression information, file count, encryption and completeness flags, and entry paths. The title and status identify the archive report; byte and image modes are unavailable for reports. F3 on a member opens its text, binary data or image. F5 copies selected files or folders to the other panel, including empty folders, with the existing conflict and cancellation workflow. Copy destinations must be ordinary folders. Move, delete, rename and folder creation cannot modify an archive.

Feedback checks use real ZIP, RAR4, RAR5, 7z, 7z SFX, TAR, TAR.GZ, GZip, BZip2, XZ, TAR.BZ2 and TAR.XZ samples. LZip, Zstandard, Unix compress and legacy ARC/ARJ/ACE readers are available through the same library but are outside this feedback sample set. ZIP-based packages also use the ZIP reader. Encrypted entries report that a password-capable extraction tool is needed; Xcmd has no password dialog. Multipart archive discovery is not implemented. Unsafe paths and ambiguous duplicate names are rejected. Browsing is limited to 100,000 entries, eight nested archive levels, and 256 MiB per temporarily opened member; use F5 for larger files.

## File viewer prototype

F3 opens the file under the cursor in an independent, read-only Xcmd window. Return to the file panels and open more viewers without closing existing ones. Each viewer keeps its own file, search and reading position. Closing one viewer leaves the others open; closing Xcmd closes its viewers. Automatic release updates wait until all viewers close. The panel cursor and marks stay in place. F4 opens that file in Notepad++, including from the viewer, and keeps the original source working directory after panel navigation. Xcmd finds Notepad++ through Windows App Paths, standard installation folders or PATH. A missing editor produces an error. Enter in the file panel keeps the normal Windows association.

The viewer combines its menu, file name, reading percentage and window controls in one title row. Drag the title row to move the window; double-click its drag region to maximize or restore it. Hover over the file name for the full path. The percentage appears at the right above the scroll bar. Reading content has no accent outline. There is no action toolbar or permanent help strip. Menus list commands and shortcuts. F12 and Help > Comment on an element keep feedback available without a permanent button. Extended file information stays hidden until View > File information or Ctrl+I is used. A thin line separates the bottom status line from the content. The status line shows the current mode, text encoding, automatic/manual choice, word wrap and visible-byte count.

The viewer detects text encodings with UTF.Unknown and validates UTF-8 before using a legacy guess. The Encoding menu can override the guess with UTF-8, UTF-16 LE/BE, UTF-32 LE/BE, Windows-1251, Windows-1252, DOS 866 or KOI8-R. Encoding guesses can be wrong, especially for short files. Text supports selection, copying and line wrapping. W or View > Word wrap toggles wrapping. Binary shows fixed-width ASCII rows; Hex adds byte values and offsets. Plain 1/2/3/4 selects Text/Binary/Hex/Image.

Choose View > Color theme for the nine MD View palettes: Cream, GitHub, Solarized Light, Forest Light, Dark, Solarized Dark, Dracula, Nord and Forest Dark. Text, title, search and status use the selected palette. Changing colors preserves the query, selection and reading position. Open viewers share the preference, new viewers use it, and Xcmd saves it immediately with the session. The default is Dark. The palettes match MD View's `src/internal/renderer/assets/template.html`; Xcmd does not need the MD View project at runtime.

Image mode disables text search, text selection and text navigation commands. Switch to Binary or Hex to inspect the image bytes. If a file cannot be read, the viewer shows the error and keeps Alt+Left/Right, F4 and Open externally available. Feedback mode temporarily shows click instructions in the active viewer's status area and restores the reading status when feedback ends.

Press Alt with a letter to start searching, then release Alt and continue typing, as in the file panels. Plain typing outside search does not start a query. The viewer remembers the last query across files and viewer windows. F3 reuses that query. Ctrl+F also reveals the search row and selects the previous query so typing replaces it. While the query has focus, W and digits enter query text. The first match appears after a short typing delay. F3 or Tab finds the next match; Shift+F3 or Shift+Tab finds the previous match. Enter in the search field also finds the next match. Search ignores case, wraps at the file boundary and reports when it wraps or finds no matches. Escape clears and hides search, then returns focus to the content; Escape while search is hidden closes the viewer. Tab returns to normal control navigation when the query is empty. New input cancels the previous background search. Text search uses the selected encoding; Binary and Hex search byte text rather than hexadecimal number expressions. Use F10 or the pointer to open command menus; Alt+letters belong to search.

Text and byte views keep a small file window in memory. The mouse wheel, Up/Down and PageUp/PageDown load overlapping windows as needed. The vertical scroll bar spans the whole file. Home/End and Ctrl+Home/End go to the actual beginning/end, including content beyond the first read. Alt+Left/Right moves between files in the source panel's order. Progress reports the byte position at the bottom of the visible lines divided by file size; it includes text above the screen and does not count all buffered content as viewed. Optional file information identifies the loaded byte range. Search can move the displayed range to show an entire match. Previous-match searches may scan a large part of the file in the background.

Ctrl+Shift+F or View > Follow end of file follows appended data in a regular file. The viewer checks a bounded tail in the background every half-second and handles truncation or rewriting. Scrolling upward, seeking, searching or changing the file or mode pauses following. Use the same command to resume. Archives and image views do not support following.

Image mode detects PNG, JPEG, BMP, GIF, WebP and ICO signatures and uses the bundled Skia decoder. It shows the first frame and fits the image to the window. Preview limits are 32 MiB encoded data, 40 million source pixels and 4096 decoded pixels in width. Image mode stays disabled when it cannot display the file; hover over its menu entry for the reason. The viewer prepares an image before switching modes. Unsupported or damaged image data leaves the working view unchanged, and an unavailable automatic image preview falls back to Hex. Use Open externally for unsupported formats. Audio/video playback, animation, syntax highlighting, JSON formatting and editing inside Xcmd are outside this prototype.

## File operations prototype

Default confirmation buttons use a blue background and respond to Enter. Focusing Cancel makes Cancel the Enter action and keeps the files unchanged. The highlighted action follows the active editor or button in navigation and conflict dialogs. The multiline comment editor keeps Enter for a new line and Ctrl+Enter to add the comment.

F5 confirms copying selected files and folders to the other panel. F6 confirms moving them. Edit the destination before pressing Enter: use an existing directory, or a full new name for one item. Shift+F5 proposes a copy in the active panel with a new name. Copying a batch into its source directory is rejected. Shift+F5 for one item proposes an explicit new name; it does not allow replacing the source path. Each request keeps the source paths and confirmed destination; later panel navigation does not change them. F7 creates a folder; Shift+F6 keeps inline rename.

Requests run in order in the background. Both panels remain available. The operation strip appears after one second of running work and hides shortly after completion. Short operations report their result in the status line. The strip uses a fixed-height summary with full paths in hover help. Operations first count files without changing the destination. Progress shows processed files against that total, a bar based on file bytes, and an estimated remaining time. Selected root items are labelled separately in the history. Counting is cancellable; incomplete counts are reported. Remaining time excludes counting and conflict dialogs and is an estimate, not a fixed completion time. Choose **Details** or **View > File operations** to inspect requests and individual results, including hidden short operations. **Cancel current** stops the running request; **Cancel selected** also works for a queued request. Completed changes remain after cancellation. Cancellation does not roll back a batch.

Folders with the same name merge. Destination-only files stay in place. Regular files with equal contents, sizes, modification times and basic attributes are skipped without a question. Other existing files require **Skip**, **Rename**, **Replace**, or **Cancel operation**. The compact conflict dialog compares source and destination sizes and modification times; hover over each shortened directory to see its full path. Apply-to-all affects matching conflict types in that request only. A folder cannot replace a file, and a file cannot replace a folder. Finish an open dialog, rename or command-line edit before the next conflict dialog appears.

Copies use a temporary destination file and replace the final path only after the copy finishes. A cancelled copy keeps an existing destination file. Moves use a filesystem rename when possible. The copy-delete fallback removes each source only after copying that item succeeds. A skipped or failed child keeps the remaining source folder available. Results identify partial changes and failures.

Copies preserve creation, modification and access timestamps and Hidden, ReadOnly, System and Archive attributes. Newly created folders receive their source metadata after successful copying; existing merged folders keep their own metadata. Copies inherit access permissions from the destination folder instead of importing explicit source ACL entries. NTFS additional data streams are copied and their names and sizes are checked before the final destination is replaced. If the destination cannot preserve them, the copy reports an error and keeps any existing destination.

A failed item offers Retry, Skip, Skip similar errors and Cancel operation. Retry repeats that item's work after its cause is fixed; recovered attempts do not count as permanent failures. Skipped failures remain visible in the operation report. Escape cancels the pending operation decision.

F8 or Delete opens a short deletion question with **Recycle Bin** selected. Shift+Delete selects **Permanently** instead. The underlined **R** and **P** show the **Alt+R** and **Alt+P** access keys. Choose either mode in the same dialog, then press Enter or the highlighted **Yes** button. Escape or **Cancel** keeps the files. Windows processes each recycled tree as one item; cancellation takes effect between selected items. Recycling is limited to local fixed drives. If Windows would delete an item permanently instead, Xcmd refuses that operation. Permanent deletion checks cancellation between files.

Copy and move report unsupported links and junctions without following them. Writes through linked parent directories are also rejected. Permanent deletion removes a link itself and does not traverse its target. Pause, queue reordering, interactive content comparison and newer-file conflict rules are not included in this prototype.

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

Ctrl+Space in the command line offers matching file and folder names at the caret, programs from PATH in the first word, and environment names after `%`. Up/Down chooses a suggestion; Enter inserts it without running the command. Escape dismisses suggestions and keeps the draft. Paths with spaces are quoted. Tab continues to switch panels. Alt+Up/Down filters command history by the original typed prefix, skips unrelated entries and restores the unfinished draft after the newest match.

Ctrl+C copies selected command text. Without a selection, Ctrl+C copies the whole command and keeps the caret in place. An empty command leaves the clipboard unchanged. Long directory paths keep their right end visible; hover over the path to see the full directory. Keyboard hints use separators and wrap in narrow windows.

`cd <path>` changes the active panel directory. It accepts relative paths, absolute paths, quoted paths, `cd..` and the optional `/d` form. Bare `cd` keeps the current directory. A failed directory change leaves the panel and command text available for correction. `cd` does not start an external program.

Ctrl+Enter inserts the current file name from the active panel. Ctrl+Shift+Enter inserts the full path. Names with spaces use quotes. Insertion replaces selected text, adds a trailing space and does not execute the command. Rename arrows and Alt name search stay in their own controls; Ctrl+Left/Right keeps opposite-panel navigation.

A successful launch clears the submitted text and records it in shared command history. A failure shows an error and preserves the text. A pending launch shows an opening message and ignores repeated submission. Input changed during a pending launch stays intact. History keeps the latest 100 unique successful commands. The draft, history, caret, selection and focus are saved with the workspace. Restoring a draft never executes it. Command focus delays automatic release changes; use Up or Down to resume file navigation.

## Name search

Press Alt and the first letter, release Alt, and continue typing part of a file or directory name. The text can appear anywhere in the name. The match closest to the beginning wins; prefix matches have priority. When positions are equal, the first visible row wins. The list stays visible and the cursor moves to a matching entry. A letter with no matching name is ignored, so the existing query stays usable. Matching parts of names use a separate color. Up and Down cycle through matching rows, wrapping at either end and keeping the query. Escape, Tab, other commands, pointer movement and window deactivation clear the query and highlights. Name jumping keeps file marks. The status line shows the accepted query. An active search delays directory refresh and automatic release changes.

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

Double-click an enabled control during selection to send one ordinary click and keep feedback mode active. For example, double-click a menu heading to open it, then single-click a menu command to comment on that command. Single-click selection waits for the system double-click interval before opening the comment form. Disabled controls remain available for comments and are never activated. The comment form and Notes show the action path. Saved notes and the Markdown checklist keep the clicked elements and final target so the steps can be reproduced.

Choose **Notes** to see the refinement list. The Open tab appears first; resolved comments have a separate Resolved tab. Both tabs show counts and put newer comments first. Each comment records the element, directory, release and time. Comments are saved immediately, survive updates and ordinary restarts, and are compiled into `%LOCALAPPDATA%/Xcmd/refinements.md` as an English Markdown checklist. User comments keep their original text. The Notes window shows the saved list's location and can remove a comment from the selected tab.

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
