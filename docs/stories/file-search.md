---
id: FileSearch
title: "Find files and text in a directory tree"
order: 3
role: user
---

# Find files and text in a directory tree

As a user, I want to find files and text in a directory tree so that I can act on
relevant results without knowing their exact locations.

[All epics](README.md)

## FindFiles

As a user, I want to search below a location using a name fragment, whole name, or mask
so that I can find files whose exact locations I do not know.

### Implementation decisions

- Keep the initial search focused on names and contents. Support both a name fragment
  and a whole-name pattern; place the whole-name control next to the file-name field.
  Defer advanced date and size filters.
  Sources: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 1](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-1); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 4](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-4); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 5](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-5).

## FindFileText

As a user, I want to search file contents with literal text or a regular expression so
that I can locate information inside a directory tree.

### Implementation decisions

- Search archives and handle text encodings by default, without requiring the user to
  select technical encoding options first. The exact encoding-detection method and
  search engine remain delegated choices.
  Sources: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 4](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-4); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 1](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-1).

## PrioritizeSearch

As a user, I want to search ordinary files first and understand the search scope so that
I can get useful answers before scanning caches and large archives.

### Implementation decisions

- Search likely useful locations before dependency, cache and Git-ignored paths. Avoid
  letting `node_modules` delay ordinary results. Binary contents should not take
  priority in text search. The user supplied scheduling goals and left the exact
  algorithm to the agent.
  Sources: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 4](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-4); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 1](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-1).

## InspectSearchScope

As a user, I want to see skipped paths and failures and choose further locations to
search so that I can judge whether the result set is sufficient.

### Implementation decisions

- Explain skipped locations rather than hiding them. Show a count when available and
  show the action to search skipped locations only when there are such locations. Use
  consistent `skipped` terminology across the status, list and action.
  Sources: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 4](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-4).

- Use a compact table for skipped paths: a short reason, the affected path and the
  specific ignore rule source where applicable. Double-clicking a skipped location
  starts a search there.
  Sources: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 4](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-4); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 5](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-5).

- Show search failures as compact rows with a short reason, file path and explanation.
  Avoid raw implementation-limit messages and repeated explanatory text in every row.
  Sources: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 5](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-5).

## ControlSearch

As a user, I want to stop a search, revise its query, and reuse earlier queries so that
I can explore step by step without unclear background changes.

### Implementation decisions

- Keep one non-modal search window that does not stay above or block the main window.
  Alt+F7 returns to the existing search window.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 4](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-4).

- Provide separate query histories for the name and content fields, opened with
  Alt+Down. Up/Down and Alt+F / Alt+T switch between the fields.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3).

- Changing query options must not discard existing results. While a search runs, Search
  restarts it with the new settings only when something changed. Enter starts the search
  rather than toggling the focused option.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3).

- After starting a search, move focus to the results. Up from the top result returns to
  the containing-text field, and Down from that field moves to results. Position the
  Search action so it clearly applies to both input fields and all options.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 5](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-5).

## OpenSearchMatch

As a user, I want to open the matching file or locate it in a panel so that I can
continue work from the result.

### Implementation decisions

- Use compact result rows with readable file names, a separate preview column on the
  right and resizable columns. Keep the path structure easy to scan.
  Sources: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 4](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-4).

## SearchResultPanel

As a user, I want to send a result snapshot to a panel and act on its entries so that I
can organize files found in different directories.

### Implementation decisions

- Allow search results to be sent to a file panel for further work; the user explicitly
  selected this workflow in the feature review.
  Source: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 3](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-3).
