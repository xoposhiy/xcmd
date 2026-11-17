---
id: ExternalTools
title: "Use other tools from the current location"
order: 6
role: user
---

# Use other tools from the current location

As a user, I want to use Windows programs and commands from my current file context so
that I can continue work in the right tool.

[All epics](README.md)

## OpenExternalTool

As a user, I want to open or edit the selected file in my preferred external tool so
that I can continue file work in the right application.

### Implementation decisions

- Use an external editor for editing, with Notepad++ as the selected initial editor.
  Enter on an ordinary file or application should open it as Windows Explorer would.
  Sources: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 1](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-1); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 9](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-9).

## RunCommand

As a user, I want to run commands or change directories from the active panel so that I
can start work without re-entering its directory context.

### Implementation decisions

- Use a compact command line for quick program launch through Windows ShellExecute, with
  the active panel directory as the working directory. Handle `cd` by changing the
  active panel. Remove the embedded-terminal experiment and shell-selection UI; typing a
  command such as `cmd` opens a terminal in the current directory.
  Source: [2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md → Turn 1](../codex-sessions/2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md#turn-1).

- Left/Right moves focus to the command line; Up/Down returns it to the file panels.
  Escape clears the command and returns focus to the panels.
  Sources: [2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md → Turn 1](../codex-sessions/2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md#turn-1); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 3](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-3).

- Ctrl+C with no selected command text copies the whole command. Preserve the useful
  right end of a long working-directory prompt and keep launch errors short.
  Source: [2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md → Turn 1](../codex-sessions/2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md#turn-1).

## CommandHistory

As a user, I want to browse matching command history and return to unfinished text so
that I can repeat commands with less typing.

### Implementation decisions

- Use Alt+Up and Alt+Down to move through command history, keeping ordinary Up/Down
  available for returning to file navigation.
  Source: [2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md → Turn 1](../codex-sessions/2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md#turn-1).

## CompleteCommand

As a user, I want to choose a path, environment name, or executable completion so that I
can enter commands with fewer errors.

## ReusePaths

As a user, I want to insert the current name or path into a command or copy the full
path so that I can pass the intended file to another tool accurately.

### Implementation decisions

- Keep Ctrl+Enter and Ctrl+Shift+Enter for inserting the current name and full path into
  the command line. Reassign Ctrl+Shift+C from copying a path to comparing the two panel
  files; path copying remains available through insertion followed by Ctrl+C.
  Sources: [2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md → Turn 1](../codex-sessions/2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md#turn-1); [2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md → Turn 2](../codex-sessions/2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md#turn-2); [2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md → Turn 3](../codex-sessions/2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md#turn-3).

## ShellActions

As a user, I want to open the Windows context menu or properties for the intended items
so that I can access installed shell actions from Xcmd.

### Implementation decisions

- Offer the Windows Explorer context menu on files and folders through the right mouse
  button.
  Source: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 2](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-2).

## ComparePanelFiles

As a user, I want to compare the file under each panel cursor so that I can inspect
differences without entering their paths.

### Implementation decisions

- Use Ctrl+Shift+C to compare the file under each panel cursor in Visual Studio Code.
  The user chose VS Code if its diff mode was available and explicitly corrected the
  shortcut transcript to Ctrl+Shift+C.
  Sources: [2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md → Turn 1](../codex-sessions/2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md#turn-1); [2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md → Turn 3](../codex-sessions/2026-11-17T17-36-47-01a1ea5e-3e16-7541-a79e-cf1f44a6ebc4.md#turn-3).
