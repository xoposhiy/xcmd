---
id: FileInspection
title: "Understand file contents"
order: 4
role: user
---

# Understand file contents

As a user, I want to inspect file contents so that I can understand a file before
choosing another tool or changing it.

[All epics](README.md)

## OpenViewers

As a user, I want to open one or more viewers and move through the displayed file list
so that I can read files while the panels remain usable.

### Implementation decisions

- Keep viewers non-modal and allow several files to be open at once while the main file
  panels remain usable.
  Source: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 4](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-4).

- Make file contents the main surface. Put less frequent actions in menus and shortcuts
  instead of permanent toolbars and onboarding text. Merge the menu with the title bar,
  remove the accent border around content and separate the status bar lightly from the
  text.
  Sources: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 2](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-2); [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 4](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-4); [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 6](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-6).

## QuickViewFile

As a user, I want to see the contents of the current file in the other panel so that
I can inspect files while moving through the list.

### Implementation decisions

- Use `Ctrl+Q` to show a quick view of the current file in the other panel.
  Codex session ID 01a1ebe8-bedb-74f3-9cfc-bc539aa090d0.

- Embed the same reader used by `F3`. Keep its modes, syntax highlighting, reading
  progress, and position controls. Use the file list to change files and omit the
  reader's previous and next file commands from the quick view.
  Codex session ID 01a1ebe8-bedb-74f3-9cfc-bc539aa090d0.

- Keep focus in the file list when the quick view opens. Use `Ctrl+Tab` to move
  focus between the list and the reader.
  Codex session ID 01a1ebe8-bedb-74f3-9cfc-bc539aa090d0.

- Use the same Git status and diff commands as F3. Apply `Ctrl+D` to the
  diff when the reader has focus and to Favorites when the file list has
  focus.
  Codex session ID 01a1ebfd-4ca2-75a2-b740-45c3c26055f6.

## ChooseViewMode

As a user, I want to switch between text, binary, hex, and image inspection so that I
can interpret files that do not fit the first detected mode.

### Implementation decisions

- Use unmodified number keys to switch inspection modes. Show the current mode and
  detected encoding in the status area. Do not switch into a mode that cannot display
  the file.
  Sources: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 3](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-3); [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 4](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-4).

- Use W to toggle word wrap. Put reading progress near the scrollbar on the right of the
  menu area.
  Sources: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 3](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-3); [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 4](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-4).

- **Accepted design for a future structured JSON mode:** keep the original file text as
  the default. Enter Structured explicitly with `5` and return to source text with `1`,
  preserving the reading position. The user approved this as a design specification.
  Sources: [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 2](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-2); [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 3](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-3); [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 5](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-5).

- For that structured mode, use Alt+Up/Down for siblings, Alt+Left for the parent,
  Alt+Right for the first child and Space to fold or unfold the current container.
  Parent navigation does not fold it, sibling navigation skips expanded descendants, and
  movement stops at a boundary.
  Sources: [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 3](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-3); [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 4](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-4); [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 5](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-5).

- In the accepted structured design, a compact container occupies one line, with an
  explicit ellipsis and hidden-item count when needed. Expanding shows immediate
  children while nested containers start compact. Keep the current line stable on
  screen, retain nested fold choices, show the current structural path, and allow mouse
  text selection and copying. Search includes folded contents and reveals a matching
  path; UI ellipses must not replace source data on ordinary copy.
  Sources: [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 3](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-3); [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 5](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-5).

- For structured group actions, define peers by the nearest containing array and the
  same relative path inside its elements, not by depth alone. Nested arrays form their
  own groups. Show the group path; limit initial group actions to arrays rather than
  guessing that an arbitrary object is a record collection.
  Sources: [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 4](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-4); [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 5](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-5).

- In the structured design, Ctrl+Alt+Up/Down visits the same field in the previous/next
  peer, skipping missing fields and reporting the skipped count. Ctrl+Alt+Left folds
  peer containers; Ctrl+Alt+Right unfolds them without recursively unfolding their
  contents. Ctrl+Space copies the current container's nested fold layout to its peers.
  Group layout actions keep the cursor in place and change neither data nor field order.
  Sources: [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 4](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-4); [2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md → Turn 5](../codex-sessions/2026-11-17T11-39-42-01a1e917-525a-7c12-b895-6a749ace604b.md#turn-5).

## ReadLargeFiles

As a user, I want to read and navigate large files without exhausting memory so that I
can inspect their content while continuing other work.

### Implementation decisions

- Make the actual end of the file reachable rather than silently presenting a truncated
  prefix as the whole file. Show how much of the file is at or above the current reading
  position.
  Source: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 3](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-3).

## FindViewerText

As a user, I want to find literal text and move to the next or previous match so that I
can reach relevant content beyond the visible page.

### Implementation decisions

- Start incremental viewer search with Alt and letters, as in the panels. Ordinary
  typing must not start a search; this replaces the initial plain-typing proposal.
  Source: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 3](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-3).

- Reuse the last search text across files so F3 can continue the same search in another
  viewer. Select that text when opening Find so typing replaces it immediately.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3).

## ReadWorkingFileDiff

As a user, I want to read Git changes in the file viewer so that I can inspect
my work and see what is ready to commit.

### Implementation decisions

- Open working files in the usual view. Use `Show Diff`, `Show Text`, or
  `Ctrl+D` to switch in the same viewer and keep the reading position. Offer the diff
  for tracked text, including files added to the index for the first time.
  Codex session ID 01a1ebfd-4ca2-75a2-b740-45c3c26055f6.

- Show the full file in one diff, with deleted lines in place. Compare HEAD
  to the index and the index to the file on disk, and show both edits when
  they affect the same line. Use green for additions and red for deletions.
  Use a stronger shade for staged changes and give each changed line a hover
  tip with its change and stage state.
  Codex session ID 01a1ebfd-4ca2-75a2-b740-45c3c26055f6.

- Keep syntax colors in the diff. Read the syntax of each row in its
  original Git version so a changed comment or string keeps the right color.
  Codex session ID 01a1ebfd-4ca2-75a2-b740-45c3c26055f6.

- Show the Git state first in the status bar in every F3 mode. Refresh when the
  viewer opens, enters diff, or regains focus. Compare with the old HEAD path
  for a staged rename. For a conflict, show `Conflicted` and compare HEAD
  to the file on disk. Report an empty text diff as `No changes`.
  Codex session ID 01a1ebfd-4ca2-75a2-b740-45c3c26055f6.

- Keep all visible text searchable and copyable, including deleted and
  intermediate lines. Keep change markers outside the copied text. Provide
  menu commands and keys for switching the view and moving to the previous
  or next block of adjacent changes. Stop at the first or last block. The
  first version is read-only.
  Codex session ID 01a1ebfd-4ca2-75a2-b740-45c3c26055f6.

## HighlightSource

As a user, I want to use automatic or manual syntax highlighting so that I can
understand code structure while inspecting a file.

### Implementation decisions

- Support common source and structured-text formats, including HTML, JSON, Markdown,
  JavaScript, Python, C#, Java, Kotlin, TOML, INI, XML, CSS, C++, C# project files,
  shell scripts and PowerShell. The user delegated the choice of syntax library or
  extensible engine.
  Source: [2026-11-15T17-36-05-01a1e010-e1ad-7291-8f35-6502613da725.md → Turn 1](../codex-sessions/2026-11-15T17-36-05-01a1e010-e1ad-7291-8f35-6502613da725.md#turn-1).

## ReaderThemes

As a user, I want to choose a viewer theme shared across open viewers so that I can read
different files with consistent colors.

### Implementation decisions

- Provide viewer color themes using the existing MD View project as the reference.
  Source: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 5](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-5).

## FollowLog

As a user, I want to follow the end of a growing file so that I can observe new output
without repeatedly reopening it.

## OpenViewedFile

As a user, I want to open the viewed file with Windows or in the external editor so that
I can act after inspecting its contents.
