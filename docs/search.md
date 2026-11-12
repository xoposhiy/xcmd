# File search behavior

This document expands UC23 and UC31 in [the use-case inventory](use-cases.md). The implementation added on 2026-11-11 covers the main search workflow; later sections retain broader design requirements.

## Current implementation

Open **File > Find files** or press **Alt+F7**. Search starts in the active panel's directory. Enter a name, a wildcard mask, text inside files, or both fields, then press Enter. When both fields are set, both must match. Plain names match a substring. The dropdown beside File name offers **Contains** and **Whole name**. Whole name requires the complete name. Search and Stop sit below both query fields and apply to the complete query. `*` and `?` select mask matching; **Treat * and ? as text** searches for those characters. Wildcard masks match the complete name. A name with a slash matches the path relative to the search root. Name matching ignores case.

Text is literal and ignores case by default. **Match text case** and **Regex** change text matching. Regex uses the .NET syntax and runs one line at a time with a bounded match time. These controls follow the familiar literal, case and regex choices from grep; Xcmd does not accept shell command arguments in either field.

Results arrive while work continues. Compact single-line rows show the path, line number and a separate preview column on the right. Drag column separators to resize them; both result and skipped-location widths are saved with the workspace. Starting search focuses results. Up from the first result focuses Containing text, and Down from Containing text returns to results. Up from Containing text focuses File name; Down from File name focuses Containing text. Alt+F selects the file-name field; Alt+T or Ctrl+F selects the text field. Alt+Down opens the focused field's history. Arrows select a previous query and Enter recalls it without starting a search. Tab reaches the result list. Enter or a double-click on a result opens the containing directory and selects the file for normal panel operations. F3 opens the viewer at the matching text, using the encoding that found the match. The result list remains available after closing the viewer. Query drafts, settings and both histories are saved with the workspace.

Changing a field or checkbox keeps the current results and workers. A separate message explains that the edited settings have not been applied. Search, or Enter while a query field, checkbox or closed name-mode list has focus, starts the edited query and cancels obsolete work. Enter on a checkbox does not toggle it; Space still toggles it. Search does not restart an unchanged query while that query is running. Stop keeps existing results and labels coverage incomplete. Escape stops active work; Escape after work stops closes the search window. Closing the window cancels its workers and releases its temporary archive files.

The first pass defers effective Git-ignored files and these dependency or metadata directories: `node_modules`, `.git`, `.hg`, `.svn`, `.vs`, `.venv`, and `__pycache__`. Hidden files such as `.env` stay eligible. Generic names such as `build`, `dist`, `bin`, `obj`, and `vendor` have no built-in exclusion. Git can still ignore them. Tracked files and directories with tracked descendants stay eligible. The skipped-scope summary counts known Git-ignored files, Git-ignored folder roots and standard folders separately. Expand its details to see each path, its reason, and the exact Git rule and source line when applicable. Folder contents are not counted. Skipped paths use a compact three-column table: Reason, Path and Rule / source. Reason is short; the source includes the exact ignore-file path, line and rule. Issues use Reason, Path and Explanation columns. Double-click a row in either table to apply the current query at that folder, single file or archive location and replace the current results. Tooltips explain this action. The status uses Complete, matches, checked and skipped; the Search skipped files button appears only when skipped locations exist. The button or Alt+I expands the same query, keeps earlier matches and does not repeat the ordinary scan. Expansion requested during the first pass runs after that pass.

Git supplies nested ignore rules, negation, repository-local exclusions and configured user exclusions through read-only commands. A nested repository uses its own rules. A standalone `.gitignore` outside a Git working tree is not interpreted in this iteration. If Git cannot read rules, search continues and reports the failure. A deliberately selected Git-ignored root searches its files without inherited Git exclusions; built-in exclusions still apply to its descendants.

Directory traversal uses breadth-first order. Within each directory, smaller files are queued first. Two workers handle files up to 1 MiB; one handles larger files; archive enumeration and archive text have separate workers. Large-file and archive work cannot occupy the small-file workers. This is scheduling, not a measured performance guarantee. Directory listings are still read as a whole, and Git classification starts a process for each directory.

The text adapter reuses the existing encoding detector. Valid Unicode and confident detections use one encoding. Uncertain legacy detections, or a decoding failure, add UTF-8, Windows-1251, Windows-1252 and DOS-866 candidates. A matching file appears once. Binary detection checks the initial sample and skips binary contents; name search still finds binary files. Search reads bounded chunks and does not load whole files.

Archives use the existing ZIP, RAR, TAR.GZ and other supported archive adapters. Name search reads entry metadata without extracting contents. Text search materializes eligible entries into the search's temporary cache. Ordinary-file work continues separately. Damaged, encrypted or resource-limited entries produce incomplete coverage messages. Nested archive contents are not searched in the ordinary pass. Double-click a nested-archive issue to search that archive as a new root.

Safety limits are 200,000 visited entries, 20,000 results, a 100 ms regex match timeout, and the archive adapter's existing preview limits. Literal text search streams long lines with a small overlap and has no line-length limit. Regex keeps a 256 KiB line limit; its message explains that a plain text search can include the file. Links are not followed. The status reports incomplete coverage, and a collapsed details section lists affected paths and reasons. Technical details do not take space from ordinary results. Dates, file sizes and other metadata filters are outside this change.

The current engine uses .NET's built-in literal string search, the existing detector and the existing archive adapter. No grep executable is required. Effective Git exclusions require Git. A ripgrep backend remains an option after measuring representative workloads; the first iteration does not claim parity with ripgrep syntax or throughput. See the [ripgrep guide](https://github.com/BurntSushi/ripgrep/blob/master/GUIDE.md) and [Git ignore inspection](https://git-scm.com/docs/git-check-ignore).

The headless search scenarios use real encoded files, real Git repositories, ZIP, TAR, TAR.GZ and RAR4/5 archives, actual controls and offscreen images. Controlled workers verify scheduling and cancellation without elapsed-time assumptions. The [independent usability review](evidence/search-usability-review-2026-11-11.md) records findings and resolutions. The result-panel workflow is described below. Representative performance measurements remain outside this implementation.

Search is a single independent, non-modal window. It does not stay above the main window or block panel commands. Alt+F7 restores the existing window and keeps its query and results. Panel navigation does not change the search root. Automatic release replacement waits until search closes. Feedback is in the bottom action row or available through F12.

## Search results in a file panel

**To panel** or **Alt+P** sends a snapshot of the results found so far to the panel that opened search, keeping the selected result. The search window closes and its workers stop. Relative paths distinguish files with the same name. The panel supports selection, sorting, F3/F4 and source file operations; archive members keep their read-only restrictions. A result panel cannot be a copy/move destination: open a regular destination folder first.

**Ctrl+Left/Right** from results opens the current item's containing folder in the corresponding other panel and selects that exact file or folder, including archive members. Focus remains in results. **Escape**, Backspace or Ctrl+PageUp leaves results and restores the previous folder, cursor, selection and scroll position. Opening a result directory navigates normally. Ctrl+R refreshes existing result entries without repeating the search; removed files disappear. Results and their return location are saved with the workspace.

Command-line execution keeps the original panel directory shown in its prompt, even when the result cursor moves between folders. Opening or editing a result uses that file's containing directory. Rename preserves the result cursor and marks when its sort position changes.

## Broader design requirements

## Confirmed user requirements

- Find files by part of a name, a name mask, or text inside a file.
- Search below the chosen directory.
- Handle text encodings automatically in normal use.
- Search inside supported archives by default. Do not require an archive-search checkbox for every query.
- Return useful results before spending time in dependency directories such as node_modules.
- Make search order useful rather than letting the first large directory delay the rest of the search.
- Keep the advanced search capabilities the user needs while making the normal workflow simple.
- Allow search results to become a file panel for further operations.

The user suggested using .gitignore and offering a later search of ignored files. The staged scope and precise scheduling below are a design proposal, not a finalized implementation.

## Normal search interaction

Start in the active panel's directory. Show a name or mask field and a separate contents field. Either field can be used alone; when both are filled, require both conditions. This field layout and combined-query rule are proposed defaults.

Treat plain name input as a substring query and a wildcard mask as a mask query. A name query can return files and directories. A contents query returns matching files. Keep the active matching mode understandable, including a way to search for literal wildcard characters.

Start with ordinary files and supported archives. Handle encodings automatically. Keep date, size, case, regex, and other less common controls under additional options after reviewing which ones matter. Do not expose encoding and archive controls as prerequisites for ordinary search.

Show results as they arrive, with their location and a matching text excerpt when available. Allow opening, previewing, and sending results to a panel without waiting for the full scan. Preserve keyboard selection as new results appear.

## Search scope and order

### Default scope

Search tracked files and ordinary untracked files under the chosen root. Include hidden user files; a hidden attribute or a leading dot alone is not a reason to defer a file.

Defer ignored files and known dependency or generated directories from the default pass. node_modules is the first confirmed dependency-directory example. Other built-in directory rules need review; do not assume that every directory named build, dist, or vendor is disposable.

Offer an explicit way to expand the same query to deferred files. Do not require the user to recreate the query or restart the completed scan. Keep the earlier results.

If the user deliberately chooses a normally deferred directory as the search root, search that root. For example, a search started inside node_modules should not silently skip the entire selected directory.

### Scheduling proposal

1. Inspect the selected directory before its descendants.
2. Give shallow ordinary directories an early turn, then continue through deeper ordinary directories. Use a stable breadth-first order as the initial rule.
3. Search supported archives in ordinary locations automatically, with bounded work per archive. A large archive or file must not hold the entire search behind it.
4. Keep dependency and ignored locations pending until the user expands the scope. Allow expansion while the ordinary pass is still running.

Start with these understandable rules before introducing learned relevance or personal ranking. A traversal priority changes when an eligible file is searched; it must not silently remove that file from the selected scope.

### Ignore rules

Use the relevant ignore patterns rather than excluding a whole directory simply because it contains a .gitignore file. Account for nested rules and exceptions. In a Git repository, include tracked files even when their names match an ignore pattern. Keep the existing Git index unchanged.

Git defines ignore rules for untracked files, supports nested .gitignore files, and also has repository-local and user-level exclusion sources. See [the official gitignore documentation](https://git-scm.com/docs/gitignore). The product proposal is to respect effective Git ignore rules inside repositories and make the deferred scope visible. The treatment of standalone .gitignore files outside a repository remains open.

Git's internal database is not ordinary project content. Propose keeping it outside the normal pass, with its scope reported separately if the user needs to search it. Do not confuse it with hidden user files such as .env.

## Automatic text handling

Handle Unicode and useful legacy encodings without making the user choose a set of checkboxes. Recognize encoding markers and valid Unicode text. For ambiguous legacy text, evaluate a small supported set of plausible encodings so one uncertain guess does not silently lose a match. Return one result per file, with consistent preview text.

Choose the initial legacy-encoding set using sample files. Windows-1251, Windows-1252, and DOS encodings are candidates, not a confirmed support list. Automatic detection cannot identify every legacy text encoding with certainty; keep a manual override under additional options for difficult files.

Do not silently treat failed decoding as a successful search with no matches. Separate text content search from arbitrary binary-byte search. Text extraction from PDF, Office documents, and other structured formats needs its own format requirements; it is not implied by automatic encoding support.

## Archive search

Use ZIP, RAR, and TAR.GZ as the initial format requirements, consistent with archive browsing. Search entry names for name queries and suitable entry contents for contents queries. Show the containing archive and entry path in each result.

Use archive metadata for name queries when the format allows it. Do not unpack all entry contents merely to compare their names. Archive work must remain cancellable and share execution time with ordinary files.

Encrypted, damaged, unsupported, or resource-limited archives must be reported as incomplete coverage, not as searched archives with zero matches. The user should not have to manage a password prompt before ordinary results become available. Password handling, nested-archive depth, and resource policies need design decisions before archive search is considered complete.

## Search progress and completion

Always distinguish these states:

- The selected scope is still being searched.
- The selected scope is complete and additional ignored or dependency locations can be searched.
- Some selected locations could not be searched.
- The user stopped the search.

Use short product messages such as `Searching project files`, `Search ignored files too`, and `Some files could not be searched`. Do not show zero matches as a complete answer while selected locations remain pending or failed.

Report known deferred locations without recursively enumerating all their contents just to count them. Exact deferred-file counts are not required before showing results or offering expansion.

Applying a changed query cancels obsolete work and prevents old matches from appearing in the new result set. Editing a draft alone keeps the current search. Cancellation must stop archive and contents workers as well as directory traversal.

## Acceptance examples

These examples define behavior to implement and verify using [the development testing requirements](development-requirements.md). Scheduling checks should use observable work order or controlled workers rather than fragile elapsed-time assertions.

| Scenario | Expected result |
| --- | --- |
| A root has report.md, notes/report-old.md, and node_modules with many descendants. Search for report. | Show matching ordinary files without first walking node_modules. Report the deferred dependency scope and offer expansion. |
| Search for *.md with no contents query. | Match file names by mask without opening every matching file for text decoding. |
| Search for a Cyrillic phrase in supported UTF and legacy encodings. | Find each supported text sample without selecting encoding checkboxes. Return each file once. |
| A normal directory contains a supported archive with a matching entry. | Find the entry with archive search enabled by default and show its archive location. |
| The first encountered archive is large or deliberately slow. | Continue producing ordinary-file results and allow cancellation. |
| An untracked file is ignored, a nested rule has an exception, and a tracked file matches an ignore pattern. | Defer the ignored untracked file; include the applicable exception and the tracked file in the ordinary pass. |
| Expand search to ignored files. | Keep existing matches and add newly eligible results without repeating the completed ordinary scan. |
| Start a search directly inside a dependency directory. | Search the explicitly selected root rather than returning an empty skipped scope. |
| Encounter a damaged archive or unreadable file. | Keep useful results and report incomplete coverage. |
| Cancel a search or replace its query. | Stop obsolete work; keep result state consistent; do not mix matches from different queries. |

## Open design details

Confirm case and regex behavior, path matching, additional metadata filters, supported legacy encodings, archive password handling, nested archives, and link traversal. Decide which ignore sources to show as scope explanations and how expansion should group dependencies versus other ignored files.

Set performance targets only after running representative directories. Measure time to the first useful result, progress across ordinary directories, full-scope completion, and cancellation response. A full-system index is not required for this design; the Everything-style global lookup remains outside the current scope.
