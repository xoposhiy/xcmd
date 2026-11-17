# Use cases and implementation status

This inventory records accepted goals and current limits. Implemented behavior is described in the [user guide](user-guide.md); test coverage is mapped in [verification](verification.md). Planned work is not a claim about the current build or a delivery schedule.

## Product decisions

- Build a personal Windows file manager with two panels, C# and Avalonia. Do not add panel tabs.
- Keep the command line as a quick launcher through Windows ShellExecute in the active panel directory. Handle `cd` locally. Do not embed terminals or select command interpreters. Dedicated terminal launch actions are deferred; ordinary program names such as `cmd` use the same launch path.
- Before each Windows Shell launch, use the current system and user environment without restarting Xcmd. Apply additions, changes and deletions before program-target expansion and PATH lookup. Preserve variables that exist only in the inherited process environment. Existing external processes keep control of their own environment.
- Keep keyboard navigation, command history, name/path insertion, directory history and favorites as direct workflows. Use Alt for name search and plain typing for the command line.
- Keep the `..` parent row and Ctrl+PageUp. Entering a child directory selects `..`; parent returns, history and session restore keep remembered cursors.
- Use natural file-name ordering. Keep directories first and sorted by ascending name. Keep focus in the source panel after Ctrl+Left/Right. Inline rename selects the full name, including the extension.
- Use compact controls, Windows system icons, configurable file colors, square corners and faint inactive cursors. A top strip marks the active panel.
- Combine directory History, Frequent locations and Favorites in one navigation window. Share favorites and frequency scores between panels. Count qualified work visits rather than quick transitions, exclude roots and reduce older scores over time.
- Keep basic multi-file selection. Advanced selection and directory synchronization are deferred.
- Handle copy and move conflicts explicitly. The prototype supports Skip, Rename and Replace, with apply-to-all for matching conflict types. Comparison and newer-file rules remain planned.
- Show Git status in the panel header. Commit opens ordinary interactive Claude Code (Sonnet, medium effort) at the repository root with the initial prompt `Commit all changes.`. Other Git write operations remain deferred.
- Search by name substring, mask or contents. Handle encodings and supported archives automatically. Return useful results before scanning dependency directories. The [search design](search.md) distinguishes confirmed requirements from proposed scheduling and ignore rules.
- Test every implemented workflow without visible application windows or human assistance. Deliver separate verified releases and preserve the workspace during local release handoff.
- Allow element feedback inside the main window and every application dialog. Save notes immediately and preserve original user text.

## Workflows

| ID | Goal | Status | Current behavior or remaining work |
| --- | --- | --- | --- |
| UC01 | Start a terminal in the active directory. | Implemented through quick launch | Type a program name such as `cmd`. Dedicated Git Bash, CMD and PowerShell launch actions are deferred. |
| UC02 | Quickly run a program or open a file. | Implemented | ShellExecute uses the active panel directory. Support local cd, history, name/path insertion, editor focus keys, Escape and saved drafts. Very frequent workflow. |
| UC03 | Jump to a file or directory by name. | Implemented | Start with Alt and a letter, release Alt and continue typing. Ignore unmatched letters. Support loaded keyboard layouts and physical keys without duplicate text. |
| UC04 | Copy files and directories between panels. | Prototype | F5 recursively copies files and empty folders, merges folders, reports progress and failures, and supports cancellation and conflicts. Ctrl+C/V uses the Windows file clipboard and pastes into the displayed directory; same-folder copies get a free copy name. Links are reported without traversal. |
| UC05 | Move files and directories. | Prototype | F6 and clipboard Ctrl+X/V use a same-volume move when possible and a copy-delete fallback otherwise. Cut keeps sources until paste and dims their rows. Delete each source only after its copy succeeds; keep skipped or failed source items available for another paste. |
| UC06 | Create a directory. | Implemented | F7 opens a name dialog. Validate a single directory name and show conflicts. Nested creation is not implemented. |
| UC07 | Create and edit a file. | Prototype | Shift+F4 asks for a new name, creates an empty file without overwriting, opens it in Notepad++, and selects it in the source panel. Cancel and invalid or existing names leave files unchanged. Archives and search results require a regular destination folder first. |
| UC08 | Edit a file in an external editor. | Prototype | F4 opens the current file in Notepad++ from either panel or the viewer. Detect the installed executable and pass the literal full file path. |
| UC09 | View text and images. | Prototype | F3 opens independent, non-modal viewers with a combined menu/title row. Panels remain usable while multiple viewers are open. Detect common raster images by signature, prepare them before mode changes and disable unavailable modes. Known code files use TextMate syntax highlighting with automatic/manual language selection and direct problem reports. Markdown shows highlighted source; CSV uses plain text. MD View integration, audio/video, structured formatting, rich clipboard and image copy remain deferred. See [syntax highlighting](syntax-highlighting.md). |
| UC10 | Inspect text or binary data and change the text encoding. | Prototype | Automatic/manual encodings and modes, continuous bounded reading, actual Home/End, whole-file scroll bar and visible-file progress. W toggles word wrap; 1/2/3/4 changes mode. Alt+letter or Ctrl+F starts whole-file search; F3/Tab and Shift+F3/Shift+Tab move between matches. |
| UC11 | Preview unusual file formats. | Planned | Obtain real examples before choosing format adapters. |
| UC12 | Calculate all child folder sizes. | Implemented | Alt+Shift+Enter calculates regular file bytes in the background. Skip links, mark incomplete totals, allow cancellation and save completed measurements. Directories keep name order. |
| UC13 | Sort by name, extension, date or size. | Implemented | Header clicks and Ctrl+F3/F4/F5/F6 select the sort. Repeated selection reverses file order; cursor and selection stay intact. |
| UC14 | Open a directory in the other panel. | Implemented | Ctrl+Left/Right opens the pointed directory in that panel, or the source directory when pointing at a file. Keep source focus. |
| UC15 | Return to a recent or frequent directory. | Implemented | Alt+Down focuses History in the two-column directory chooser. Type to select the uppermost substring match in the active list; Up/Down cycle matches and Escape clears the query before closing. Restore cursors and use shared qualified-visit scores. Back/forward shortcuts remain planned. |
| UC16 | Open and maintain favorite directories. | Implemented | Ctrl+D focuses Favorites: starred pins followed by frequent directories without duplicate paths. Search names and paths with loaded keyboard layouts. Add, rename and remove pins with immediate saves. No external favorites import is implemented. |
| UC17 | Rename a file or directory in place. | Implemented | Shift+F6 selects the full name. Enter commits, Escape cancels and Tab commits before switching panels. Keep editor keys inside the editor. |
| UC18 | Open a Windows shell context menu. | Prototype | Right-click, Shift+F10 or Ctrl+Down opens the current item's native menu. A marked item targets its marked group; an unmarked item targets only itself and keeps existing marks. Group menus require one physical folder; mixed-folder search results report the limitation. Native command discovery is checked without displaying a menu; third-party interactive menu appearance remains outside headless coverage. |
| UC19 | Recognize file types by color. | Implemented | Ordered JSON rules color the Type column. Source rules take priority for scripts. Names use white, gray for Git-ignored entries, and amber for marks. |
| UC20 | Navigate two panels without tabs. | Implemented | Support keyboard and mouse selection, parent navigation, automatic refresh, cursor restoration, icons and compact layout. |
| UC21 | Browse archives as folders. | Prototype | Enter opens common archive extensions; Ctrl+PageDown probes any file, including Office containers and recognized self-extracting EXEs. Browse ZIP, RAR4/5, 7z, TAR, compressed TAR and common compression streams; F3 shows archive information and entries, or views a member. F5 extracts files and folders through the operation queue. Nested archives and workspace restore are supported. Archives stay read-only; passwords, multipart discovery and archive edits remain deferred. |
| UC22 | Create an archive. | Prototype | Add to ZIP in the context menu packs the marked same-folder group, or the unmarked clicked item alone, into a new ZIP beside the sources. A name dialog supplies a default and adds .zip when omitted. The operation queue supplies progress, cancellation and error decisions. Files, folders, empty directories and Unicode names are supported; existing archives are never overwritten. Other output formats, passwords and editing existing archives remain planned. |
| UC23 | Search names, masks and contents below a directory. | Prototype | Alt+F7 searches names, masks, relative paths and file contents with case and regex options. Encodings and supported archives are handled automatically. Ordinary files come first; ignored dependency and cache folders are deferred and can be searched later. Results appear progressively, can be stopped, viewed with F3, located in a panel or sent to a panel. See the search design. |
| UC24 | Compare two files. | Prototype | Ctrl+Shift+C or File > Compare files in VS Code opens the two panel cursor files in a new VS Code diff window, in left/right panel order. Marks do not affect the pair. Requires existing files on disk and an installed VS Code. Archive members, Git history entries, binary diff and merge workflows remain outside this prototype. |
| UC25 | Compare two directories. | Planned | Show differences. Directory synchronization remains deferred. |
| UC26 | Rename many files with one rule. | Optional | Very rare use. Define a small workflow before implementation. |
| UC27 | Inspect Git status and diffs. | Prototype | Show branch and whole-repository status in the header, file and descendant changes in the Git column, and direct child repository summaries outside a working tree. Ctrl+Alt+D opens a basic text diff against HEAD, including staged and working changes. Commit files compare with the first parent; initial commits and untracked files compare with an empty file. A full diff viewer remains deferred. |
| UC28 | Perform Git write operations. | Prototype | Click the Git status and choose Commit repository with Claude Code to open interactive Claude Code at the repository root with Sonnet, medium effort and `Commit all changes.`. Questions, permissions, results and follow-up stay in Claude's console. Xcmd remains usable. Native staging, branch, pull and push remain deferred. |
| UC29 | Filter a panel while typing. | Optional | Keep separate from Alt name jumping. No implementation commitment. |
| UC30 | Show a flat recursive file list. | Planned | Define paths, exclusions and operations on mixed-directory entries. |
| UC31 | Work with search results in a panel. | Prototype | To panel / Alt+P transfers a snapshot with relative paths, selection, sorting, viewing and source operations. Ctrl+Left/Right locates the current entry in the other panel. Escape restores the previous directory state. Results survive workspace restore. |
| UC32 | Preview a file in the opposite panel. | Planned | Frequent workflow. Define routing, keyboard focus and panel restoration. |
| UC33 | Queue long file operations. | Prototype | Run requests in order while panels remain usable. Capture targets at submission, count files before changes, show file and byte progress with estimated remaining time, and cancel counting, running or queued work. Failed files offer Retry, Skip, Skip similar errors and Cancel. Pause and reordering remain planned. |
| UC34 | Resolve copy and move conflicts. | Prototype; required | Skip, Rename and Replace regular files; apply Skip or Replace to matching conflicts in the current request. Merge folders without removing destination-only contents. Compare and newer-file rules remain planned. |
| UC35 | Delete with familiar Total Commander behavior. | Prototype | F8/Delete selects Recycle Bin; Shift+F8 or Shift+Delete selects permanent deletion. The same short confirmation allows either mode, with Yes focused for Enter. Reject permanent fallback when recycling is unavailable. |
| UC36 | Identify links and junctions and inspect their targets. | Partial | Folder sizes skip links and test a real junction cycle. Target inspection, target navigation, broken links and file-operation policies remain planned. |
| UC37 | Start agent tasks in the active directory. | Partial | Git Commit launches installed Claude Code in its ordinary console using the existing login, settings and project instructions. General-purpose agent tasks remain deferred. |
| UC38 | Restore the workspace and switch to a newer local release. | Implemented | Save panels, window state, navigation, measurements, command drafts and notes. Verify real process handoff and safe startup failure. |
| UC39 | Comment on interface elements and keep a refinement list. | Implemented | F12 and Feedback work in the main window and dialogs. Ctrl+click sends one normal click while selection stays active; a single click comments on the target immediately. Show the interaction path while composing a note, compile a checklist and support resolution records. |
| UC40 | Launch the latest verified release from one permanent shortcut. | Implemented | Permanent Xcmd.exe and Xcmd.lnk preserve shortcuts across releases. Share a Windows taskbar identity, keep caller arguments and working directory, and allow releases while the application runs. |
| UC41 | Duplicate files and folders in the active panel. | Prototype | Shift+F5 confirms a new name in the active directory. A batch cannot target its source directory, and source paths cannot be replaced. The destination remains editable. |
| UC42 | Browse Git history and commit snapshots. | Prototype | Ctrl+Alt+L opens scoped history with Commit, Subject, Author and Date columns. Working tree filters and refreshes scoped staged, working and untracked changes. F3 on a commit shows its message and changed files. Enter opens the full read-only snapshot through the virtual filesystem. The commit message stays visible and its divider can resize the reading area. The Git column marks first-parent changes; unchanged files remain visible. F3 on `..` opens actionable changed files, including deleted-file viewing and diff. Backspace returns; Escape cancels pending work or restores the physical panel, including cross-panel entry. Viewers preserve origin and read-only state. Git reads do not check out commits. |

## Work outside the current scope

- Panel tabs and embedded terminals.
- Indexed global lookup, checksum tools, SFTP and dedicated network-location support.
- Duplicate-content detection. Clipboard copy/cut/paste and full-path export were requested on 2026-11-12 and are now feedback prototypes.
- Advanced selection by mask, inversion and restoration; basic multi-file selection stays implemented.
- Directory synchronization and native Git write controls beyond the Claude Code Commit launcher.

These decisions preserve the reviewed scope. They do not prevent future changes requested by the user.

## Details to resolve before future implementation

- Conflict choices and operation-queue controls.
- Editor, viewer and comparison commands, formats and arguments.
- Windows shell menu behavior and its native test boundary.
- Archive formats, nested archives, passwords and archive edits.
- Search filters, supported legacy encodings, link traversal and ignore-scope explanations.
- Basic selection shortcuts beyond the currently implemented controls.

Use concrete examples and existing fixtures when resolving these details. Do not require new product decisions for behavior that the current user guide already defines.
