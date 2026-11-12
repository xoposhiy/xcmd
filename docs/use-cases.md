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
- Keep Git inspection limited to file status and diffs. Git write operations are deferred.
- Search by name substring, mask or contents. Handle encodings and supported archives automatically. Return useful results before scanning dependency directories. The [search design](search.md) distinguishes confirmed requirements from proposed scheduling and ignore rules.
- Test every implemented workflow without visible application windows or human assistance. Deliver separate verified releases and preserve the workspace during local release handoff.
- Allow element feedback inside the main window and every application dialog. Save notes immediately and preserve original user text.

## Workflows

| ID | Goal | Status | Current behavior or remaining work |
| --- | --- | --- | --- |
| UC01 | Start a terminal in the active directory. | Implemented through quick launch | Type a program name such as `cmd`. Dedicated Git Bash, CMD and PowerShell launch actions are deferred. |
| UC02 | Quickly run a program or open a file. | Implemented | ShellExecute uses the active panel directory. Support local cd, history, name/path insertion, editor focus keys, Escape and saved drafts. Very frequent workflow. |
| UC03 | Jump to a file or directory by name. | Implemented | Start with Alt and a letter, release Alt and continue typing. Ignore unmatched letters. Support loaded keyboard layouts and physical keys without duplicate text. |
| UC04 | Copy files and directories between panels. | Prototype | F5 recursively copies files and empty folders, merges folders, reports progress and failures, and supports cancellation and conflicts. Links are reported without traversal. |
| UC05 | Move files and directories. | Prototype | F6 uses a same-volume move when possible and a copy-delete fallback otherwise. Delete each source only after its copy succeeds; keep skipped or failed source items. |
| UC06 | Create a directory. | Implemented | F7 opens a name dialog. Validate a single directory name and show conflicts. Nested creation is not implemented. |
| UC07 | Create and edit a file. | Prototype | Shift+F4 asks for a new name, creates an empty file without overwriting, opens it in Notepad++, and selects it in the source panel. Cancel and invalid or existing names leave files unchanged. Archives and search results require a regular destination folder first. |
| UC08 | Edit a file in an external editor. | Prototype | F4 opens the current file in Notepad++ from either panel or the viewer. Detect the installed executable and pass the literal full file path. |
| UC09 | View text and images. | Prototype | F3 opens independent, non-modal viewers with a combined menu/title row. Panels remain usable while multiple viewers are open. Detect common raster images by signature, prepare them before mode changes and disable unavailable modes. Markdown and CSV use plain text. MD View integration, audio and video, syntax highlighting and structured formatting remain deferred. |
| UC10 | Inspect text or binary data and change the text encoding. | Prototype | Automatic/manual encodings and modes, continuous bounded reading, actual Home/End, whole-file scroll bar and visible-file progress. W toggles word wrap; 1/2/3/4 changes mode. Alt+letter or Ctrl+F starts whole-file search; F3/Tab and Shift+F3/Shift+Tab move between matches. |
| UC11 | Preview unusual file formats. | Planned | Obtain real examples before choosing format adapters. |
| UC12 | Calculate all child folder sizes. | Implemented | Alt+Shift+Enter calculates regular file bytes in the background. Skip links, mark incomplete totals, allow cancellation and save completed measurements. Directories keep name order. |
| UC13 | Sort by name, extension, date or size. | Implemented | Header clicks and Ctrl+F3/F4/F5/F6 select the sort. Repeated selection reverses file order; cursor and selection stay intact. |
| UC14 | Open a directory in the other panel. | Implemented | Ctrl+Left/Right opens the pointed directory in that panel, or the source directory when pointing at a file. Keep source focus. |
| UC15 | Return to a recent or frequent directory. | Implemented | Alt+Down opens directory history from files. Restore cursors and use shared qualified-visit scores. Back/forward shortcuts remain planned. |
| UC16 | Open and maintain favorite directories. | Implemented | Ctrl+D opens shared favorites. Add, rename and remove with immediate saves. No external favorites import is implemented. |
| UC17 | Rename a file or directory in place. | Implemented | Shift+F6 selects the full name. Enter commits, Escape cancels and Tab commits before switching panels. Keep editor keys inside the editor. |
| UC18 | Open a Windows shell context menu. | Planned | Resolve right-click selection versus menu opening and define noninteractive native verification. |
| UC19 | Recognize file types by color. | Implemented | Ordered JSON rules use source, program and text colors. Source rules take priority for scripts; selected names remain readable. |
| UC20 | Navigate two panels without tabs. | Implemented | Support keyboard and mouse selection, parent navigation, automatic refresh, cursor restoration, icons and compact layout. |
| UC21 | Browse archives as folders. | Prototype | Enter opens common archive extensions; Ctrl+PageDown probes any file, including Office containers and recognized self-extracting EXEs. Browse ZIP, RAR4/5, 7z, TAR, compressed TAR and common compression streams; F3 shows archive information and entries, or views a member. F5 extracts files and folders through the operation queue. Nested archives and workspace restore are supported. Archives stay read-only; passwords, multipart discovery and archive edits remain deferred. |
| UC22 | Create an archive. | Planned | Define formats, destination, compression defaults and existing-archive edits. |
| UC23 | Search names, masks and contents below a directory. | Planned | Preserve useful advanced options. Handle encodings and supported archives automatically; prioritize ordinary files. See the search design. |
| UC24 | Compare two files. | Planned | Define text/binary handling, external tools and whether editing or merging belongs in the workflow. |
| UC25 | Compare two directories. | Planned | Show differences. Directory synchronization remains deferred. |
| UC26 | Rename many files with one rule. | Optional | Very rare use. Define a small workflow before implementation. |
| UC27 | Inspect Git status and diffs. | Planned | Define file states, directory summaries, ignored files, branch display and diff routing. |
| UC28 | Perform Git write operations. | Deferred | Stage, commit, branch, pull and push are outside the current scope. |
| UC29 | Filter a panel while typing. | Optional | Keep separate from Alt name jumping. No implementation commitment. |
| UC30 | Show a flat recursive file list. | Planned | Define paths, exclusions and operations on mixed-directory entries. |
| UC31 | Work with search results in a panel. | Prototype | To panel / Alt+P transfers a snapshot with relative paths, selection, sorting, viewing and source operations. Ctrl+Left/Right locates the current entry in the other panel. Escape restores the previous directory state. Results survive workspace restore. |
| UC32 | Preview a file in the opposite panel. | Planned | Frequent workflow. Define routing, keyboard focus and panel restoration. |
| UC33 | Queue long file operations. | Prototype | Run requests in order while panels remain usable. Capture targets at submission, count files before changes, show file and byte progress with estimated remaining time, and cancel counting, running or queued work. Failed files offer Retry, Skip, Skip similar errors and Cancel. Pause and reordering remain planned. |
| UC34 | Resolve copy and move conflicts. | Prototype; required | Skip, Rename and Replace regular files; apply Skip or Replace to matching conflicts in the current request. Merge folders without removing destination-only contents. Compare and newer-file rules remain planned. |
| UC35 | Delete with familiar Total Commander behavior. | Prototype | F8/Delete selects Recycle Bin; Shift+Delete selects permanent deletion. The same short confirmation allows either mode, with Yes focused for Enter. Reject permanent fallback when recycling is unavailable. |
| UC36 | Identify links and junctions and inspect their targets. | Partial | Folder sizes skip links and test a real junction cycle. Target inspection, target navigation, broken links and file-operation policies remain planned. |
| UC37 | Start agent tasks in the active directory. | Deferred | No provider, command syntax or execution policy is selected. |
| UC38 | Restore the workspace and switch to a newer local release. | Implemented | Save panels, window state, navigation, measurements, command drafts and notes. Verify real process handoff and safe startup failure. |
| UC39 | Comment on interface elements and keep a refinement list. | Implemented | F12 and Feedback work in the main window and dialogs. Double-click sends one normal click while selection stays active; a single click comments on the target. Save the interaction path with notes, compile a checklist and support resolution records. |
| UC40 | Launch the latest verified release from one permanent shortcut. | Implemented | Permanent Xcmd.exe and Xcmd.lnk preserve shortcuts across releases. Share a Windows taskbar identity, keep caller arguments and working directory, and allow releases while the application runs. |
| UC41 | Duplicate files and folders in the active panel. | Prototype | Shift+F5 confirms a new name in the active directory. A batch cannot target its source directory, and source paths cannot be replaced. The destination remains editable. |

## Work outside the current scope

- Panel tabs and embedded terminals.
- Indexed global lookup, checksum tools, SFTP and dedicated network-location support.
- Clipboard name/path export and duplicate-content detection; neither workflow was selected.
- Advanced selection by mask, inversion and restoration; basic multi-file selection stays implemented.
- Directory synchronization and Git write operations.

These decisions preserve the reviewed scope. They do not prevent future changes requested by the user.

## Details to resolve before future implementation

- Conflict choices and operation-queue controls.
- Editor, viewer and comparison commands, formats and arguments.
- Windows shell menu behavior and its native test boundary.
- Archive formats, nested archives, passwords and archive edits.
- Search filters, supported legacy encodings, link traversal and ignore-scope explanations.
- Basic selection shortcuts beyond the currently implemented controls.

Use concrete examples and existing fixtures when resolving these details. Do not require new product decisions for behavior that the current user guide already defines.
