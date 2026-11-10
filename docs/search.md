# File search behavior

Requirements and design proposal, 2026-11-09. This document expands UC23 and UC31 in [the use-case inventory](use-cases.md).

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

Changing the query cancels obsolete work and prevents old matches from appearing in the new result set. Cancellation must stop archive and contents workers as well as directory traversal.

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
