---
id: WorkingLocations
title: "Move between working locations"
order: 1
role: user
---

# Move between working locations

As a user, I want to move between working locations with little navigation effort so
that I can keep my attention on the files I need.

[All epics](README.md)

## NavigatePanels

As a user, I want to move through two directory panels with the keyboard or mouse so
that I can keep source and destination locations in view.

### Implementation decisions

- Use two file panels without panel tabs in the initial interface.
  Source: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 2](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-2).

- Keep `..` at the top of the list and use `Ctrl+PgUp` to go to the parent. On entering
  a folder, select `..` so that another Enter returns to the parent.
  Sources: [2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md → Turn 4](../codex-sessions/2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md#turn-4); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7).

- On leaving a folder, select that folder in its parent, even when the folder was opened
  from recent locations. A parent transition followed by Enter must return to the same
  folder.
  Source: [2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md → Turn 7](../codex-sessions/2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md#turn-7).

- Use `Ctrl+\` for the drive root and `` Ctrl+` `` / `Ctrl+ё` for the home directory
  across keyboard layouts.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3).

- In the main file-manager context, Tab switches the active panel and ordinary typing
  starts command-line input. Focus left on another control must not silently change this
  routing.
  Source: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 9](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-9).

## OpenOtherPanel

As a user, I want to show the current folder or the selected folder in the other panel
so that I can prepare a destination while my source stays active.

### Implementation decisions

- Use `Ctrl+Left` and `Ctrl+Right` to open the folder under the cursor in the other
  panel. If the cursor is on a file, use the current folder instead. The user selected
  this option explicitly.
  Source: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 2](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-2).

## SortEntries

As a user, I want to sort the current list by name, extension, date, or size so that I
can find and compare entries more easily.

### Implementation decisions

- Support sorting by name, extension, modified date and size. Make the whole column
  header clickable, including the area outside its text.
  Sources: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 2](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-2); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7).

## JumpToName

As a user, I want to type part of a visible name and cycle matches so that I can reach
an entry without scrolling through the list.

### Implementation decisions

- Start name search with Alt and a letter. After Alt is released, further letters extend
  the query. Reject a new character when it would leave no match.
  Source: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7).

- Match text anywhere in a name. Prefer the match at the earliest position in the name,
  then the first matching entry in panel order.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 4](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-4).

- Highlight the matching part of the name. While a query is active, Up and Down visit
  matching entries; Escape, Tab and other shortcuts clear it. This replaces the earlier
  request to clear the query on all cursor movement.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 5](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-5).

## RecentLocations

As a user, I want to choose a recent or frequently used directory so that I can return
to work without typing its path again.

### Implementation decisions

- Use one navigation dialog with recent locations, frequent suggestions and favorites.
  Alt+Down opens it; Left and Right switch lists, and Tab / Shift+Tab visit the controls
  in order. Only the active list shows an active selection.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 8](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-8).

- Frequent suggestions should favor useful recent destinations rather than raw visit
  totals. Drive roots must not dominate the list. The user left the exact ranking method
  to the agent.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 8](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-8).

## FavoriteLocations

As a user, I want to add, rename, remove, and open favorite locations so that I can
maintain a short list of useful places.

### Implementation decisions

- Keep editable favorites in the shared navigation dialog. Ctrl+D opens that same dialog
  with the favorites list active.
  Source: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 8](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-8).

## FindSavedLocation

As a user, I want to search location names and paths and see when a location is missing
so that I can choose a usable destination without guessing.

### Implementation decisions

- Mark saved directories that no longer exist. Opening one should navigate to its
  nearest existing parent.
  Source: [2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md → Turn 1](../codex-sessions/2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md#turn-1).

- Use the same cursor and keyboard behavior as the main panels: PageUp and PageDown move
  the current entry rather than only scrolling. Use the shared blue current-row style
  without a second hover selection.
  Source: [2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md → Turn 1](../codex-sessions/2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md#turn-1).

## ReadablePanels

As a user, I want to adjust columns and recognize file types by color so that I can scan
the information that matters in the space available.

### Implementation decisions

- Let the last column fill the remaining width without an extra rightmost resize
  divider. Provide a way to restore default column widths, including enough space for
  sizes up to 10 GB. Center cell and header text vertically and keep headers readable.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7); [2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md → Turn 1](../codex-sessions/2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md#turn-1).

- Keep file-type categories and their color mapping in configuration. Distinguish text,
  source code and executables; scripts such as `.sh` and `.ps1` belong to the
  source-code category.
  Source: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 8](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-8).

- Apply file-type colors to the separate extension/type column. Keep ordinary file names
  neutral and Git-ignored names gray. The later decision removes type-based coloring
  from names, replacing the earlier full-name colors.
  Sources: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 4](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-4); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 5](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-5); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 6](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-6).

- Keep current-row and marked-row geometry stable. Moving the cursor or marking an entry
  must not shift text or create a horizontal scrollbar.
  Source: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 6](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-6).

## RefreshEntries

As a user, I want to refresh directory contents while keeping cursor, marks, and edits
stable so that I can work with files also changed by other programs.

### Implementation decisions

- Refresh directory contents automatically during normal use; users should not need a
  manual refresh for ordinary filesystem changes.
  Source: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7).

## DiscoverShortcuts

As a user, I want to see the main shortcuts for the current context so that I can find
the next action without opening a menu.

### Implementation decisions

- Separate shortcut hints clearly. Omit routine navigation hints such as Tab, Enter,
  Escape and cursor keys from persistent help; retain useful commands without filling
  the interface with onboarding text.
  Sources: [2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md → Turn 1](../codex-sessions/2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md#turn-1); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 4](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-4); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 7](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-7).
