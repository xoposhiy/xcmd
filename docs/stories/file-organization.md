---
id: FileOrganization
title: "Organize files across locations"
order: 2
role: user
---

# Organize files across locations

As a user, I want to organize files across locations so that I can keep useful files in
the right place with control over changes.

[All epics](README.md)

## MarkEntries

As a user, I want to mark files and folders while moving the current cursor so that I
can apply one action to a chosen batch.

### Implementation decisions

- Shift+Down marks the entry being left and moves the cursor, following Total Commander.
  Repeating a Shift selection toggles marks off; apply the same toggle rule to
  Shift+PageDown and Shift+End.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 4](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-4); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 5](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-5); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 7](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-7).

- Up, Down, PageUp, PageDown, Home and End move the cursor without changing marks.
  Escape clears the marked batch.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 5](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-5); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 6](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-6).

- Keep the current cursor appearance independent of whether entries are marked. Use a
  separate yellow marked-name/check signal; the user explicitly agreed to try yellow
  plus check marks.
  Source: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 6](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-6).

## MeasureFolders

As a user, I want to calculate one or all folder sizes in the panel so that I can judge
the amount of data in a selection.

### Implementation decisions

- Use `Alt+Shift+Enter` to calculate folder sizes across the panel, preserving the
  requested Total Commander workflow.
  Source: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 2](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-2).

## ViewSelectionSize

As a user, I want to see the total size of the selected files and folders so that I
can judge how much data I am about to process.

The panel footer shows the selection count and adds the sizes of selected files and
the measured sizes of selected folders. If any selected folder has no measurement or
its measurement is incomplete, the footer shows the total as a lower bound with `≥`.

## RenameEntry

As a user, I want to rename an entry in the file list so that I can change its name
without losing context.

### Implementation decisions

- Use Shift+F6 for inline renaming. While the editor is open, Right clears the text
  selection and places the caret at its end; arrow keys must stay in the editor rather
  than moving the panel cursor.
  Sources: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 2](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-2); [2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md → Turn 4](../codex-sessions/2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md#turn-4).

## CreateFolder

As a user, I want to create a folder in the current location so that I can prepare a
destination without leaving Xcmd.

## CreateFile

As a user, I want to create a file and begin editing it so that I can start writing in
the current directory.

## TransferEntries

As a user, I want to copy or move selected entries with an editable destination so that
I can place files where I intend.

File copies preserve creation, modification and access times, the Hidden, ReadOnly,
System and Archive attributes, and NTFS alternate data streams. Copies inherit
permissions from the destination folder. If the destination cannot preserve the
streams, the copy fails and any existing destination file is kept.

After a folder is copied successfully, only newly created folders receive the source
folder's times and attributes.

### Implementation decisions

- Use F5 to copy files and directory trees, including empty folders, and F6 to move
  them. For a move across drives, remove a source only after its copy succeeds. The user
  accepted the proposed file-operation batch.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 1](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-1); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 2](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-2).

- Ask for confirmation on F5 and F6. Keep the operation verb visible in the dialog body
  as well as its title. Preserve panel position after the transfer.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 3](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-3); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 4](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-4).

## DuplicateEntry

As a user, I want to copy an entry under another name in the current directory so that I
can make a local variant quickly.

### Implementation decisions

- Use Shift+F5 for a copy in the same panel, but do not silently duplicate a whole
  selected batch or allow a transfer onto its own source. Make the distinct destination
  explicit.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 3](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-3); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 4](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-4).

## ClipboardTransfer

As a user, I want to copy or cut a file batch and paste it into the displayed directory
so that I can transfer files between Xcmd and other Windows tools.

## DragFiles

As a user, I want to drag selected entries with the mouse and drop files onto a panel so
that I can move files between Xcmd and other Windows programs by pointing.

### Implementation decisions

- Match Windows Explorer behavior when dragging files and folders within Xcmd and
  between Xcmd and other Windows applications.
  Source: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 1](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-1).

## DeleteEntries

As a user, I want to confirm recycling or permanent deletion so that I can control the
consequence of removing files.

### Implementation decisions

- Default F8/Delete to the Recycle Bin and Shift+Delete to permanent deletion. Let the
  confirmation dialog switch either choice. Keep the question short and make Yes the
  default Enter action.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 1](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-1); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 2](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-2); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 3](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-3).

- Support permanent deletion of the `.git/objects` files that can already be moved to
  the Recycle Bin. The user reported recurring access-denied failures on those files and
  requested a correction; the transcript does not prescribe the Windows API to use.
  Source: [2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md → Turn 1](../codex-sessions/2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md#turn-1).

## ResolveConflicts

As a user, I want to resolve destination conflicts during a transfer so that I can
finish the useful parts of a copy or move.

### Implementation decisions

- Show source and destination details and offer Skip, Rename and Replace, with an
  explicit apply-to-all choice. Merge directory contents. Skip an existing item without
  prompting when its contents and other relevant properties are identical.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 1](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-1); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 2](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-2); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 4](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-4).

- Make Replace all and its apply-to-all choice easy to invoke from the keyboard. Apply
  the same shortcut conventions to comparable operation dialogs.
  Source: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 17](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-17).

## QueueOperations

As a user, I want to submit long file operations and inspect progress and results so
that I can keep using the panels while work runs.

### Implementation decisions

- Run long operations in the background, one operation at a time, and queue later
  requests. Capture the source and destination for each operation so later panel
  navigation cannot redirect it. The user accepted this initial sequential queue design.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 1](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-1); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 2](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-2).

- Show the operations panel only for long-running work. Keep its height stable when long
  paths are displayed.
  Source: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 3](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-3).

- Use progress counts with the same unit: count the work before showing completed/total
  values, and show a progress bar and an ETA rather than mixing selected top-level items
  with recursively completed files.
  Source: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 4](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-4).

## RecoverOperations

As a user, I want to retry, skip, or cancel work when an item fails so that I can keep
completed work and make the next decision explicitly.

### Implementation decisions

- Support cancellation and make completed, skipped, failed and cancelled results clear
  so that partial work is understandable. The user accepted this as part of the
  file-operation batch.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 1](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-1); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 2](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-2).

- When another process blocks an operation, identify it immediately and show the
  recovery dialog. Apply this to inline rename failures and Recycle Bin failures as well
  as other file operations.
  Sources: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 8](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-8); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 11](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-11); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 12](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-12).

- Include the requested name, the error, and a next step when a rename fails.
  Check for processes using the source or destination when Windows denies access.
  Keep the same dialog open if retrying fails and show the latest error.
  Feedback 677755a. Codex session ID 01a1ef9b-ff1b-7082-83cc-e211650ce3de.

- Show a window-activation link only for applications that have a window. Put the
  process action beside the application instead of requiring radio-button selection. Use
  `Kill` for forced termination; this replaces the earlier Close-only design.
  Sources: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 15](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-15); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 17](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-17).

- List process names and PIDs as plain text within their application row. Keep the
  window-activation link, but remove PID links, the process-details window and Task
  Manager selection through UI Automation. The user rejected that automation as fragile.
  Sources: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 15](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-15); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 17](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-17).

- Retry automatically when the blocking process exits, including when the user closes it
  externally. Aim for a response within about one third of a second. Keep the dialog
  compact: combine the error with `Used by`, mute the full path and omit a permanent
  automatic-retry explanation.
  Sources: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 15](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-15); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 17](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-17).
