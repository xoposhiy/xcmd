---
id: ReusableBoundaries
title: "Reuse shared components across features"
order: 10
role: developer
---

# Reuse shared components across features

As a developer, I want to reuse a component when several features need the same
capability so that I can implement and maintain that capability in one place.

[All epics](README.md)

## FileSystemAccess

As a developer, I want to read, change, compare, and count filesystem entries through a
common adapter so that I can implement file workflows without duplicating OS access.

## WindowsFileActions

As a developer, I want to call native copy, recycle, menus, properties, and drag and
drop behind managed interfaces so that I can extend file actions without spreading COM
and native ownership rules.

## ExternalLaunch

As a developer, I want to resolve and launch external tools through explicit adapters so
that I can add entry workflows without duplicating Windows process rules.

### Implementation decisions

- Newly launched commands must see current global environment variables even when Xcmd
  was started before those variables changed. The user explicitly requested this
  behavior; the environment-refresh mechanism was delegated.
  Source: [2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md → Turn 3](../codex-sessions/2026-11-10T10-33-24-01a1c4ce-1b81-7100-ac29-1fb6be310a0c.md#turn-3).

## ArchiveAccess

As a developer, I want to read and create archive content through common interfaces so
that I can reuse file workflows for packaged content.

## SearchSources

As a developer, I want to search physical/archive text and explain Git ignore decisions
through one source so that I can evolve search scheduling without embedding storage or
Git parsing in Core.

## RepositoryStatus

As a developer, I want to read repository and child-repository status without Git write
side effects so that I can expose repository context through stable models.

### Implementation decisions

- Carry ignored-state knowledge into an ignored child directory so entries start gray
  rather than flashing through an ordinary state. Respect a nested repository boundary
  and correct the provisional state when status is resolved.
  Source: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 7](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-7).

## RepositoryLocations

As a developer, I want to read scoped logs, trees, changes, and messages through a read-
only filesystem so that I can reuse panel navigation for repository history.

### Implementation decisions

- Render commit trees through the existing virtual-filesystem interface instead of
  building a separate custom file-tree implementation.
  Source: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 4](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-4).

## PanelReturnState

As a developer, I want to save and restore a panel’s place when a temporary view uses it
so that search results and Git history can return the user to the same folder, selection
and scroll position.

## RepositoryDocuments

As a developer, I want to present repository contents and changes in the existing viewer
so that I can reuse inspection workflows while preserving their origin and read-only
state.

### Implementation decisions

- Preserve the source path, commit identity and read-only state when showing historical
  files in the viewer. Keep external-editor/copy messages and command availability
  consistent with that state. The user accepted these corrections from the Git review.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 7](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-7); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 8](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-8).

## ReusableFileViewer

As a developer, I want to host the same file reader in a window or a panel so that both
views can use the same reading commands.

## StreamingReads

As a developer, I want to read file windows and search full files through reusable
streaming services so that I can keep viewer features independent of file size and
encoding details.

## SyntaxSpans

As a developer, I want to produce portable syntax spans and render them with readable
colors so that I can extend highlighting without rewriting the viewer.

### Implementation decisions

- Keep the highlighting design ready for future copying of colored code and copying as
  an image. Those export commands were explicitly deferred; the user did not require
  them in the initial highlighting implementation.
  Source: [2026-11-15T17-36-05-01a1e010-e1ad-7291-8f35-6502613da725.md → Turn 1](../codex-sessions/2026-11-15T17-36-05-01a1e010-e1ad-7291-8f35-6502613da725.md#turn-1).

## SharedControls

As a developer, I want to compose controls with shared layout, styling, and list
rendering so that I can add workflows without copying presentation mechanics.

### Implementation decisions

- Use square corners and compact spacing. Keep the inactive panel cursor faint and show
  the active panel with a top indicator rather than a full accent outline. Use
  consistent alignment across corresponding headers and values.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 8](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-8).

- Show Alt access keys by underlining their letters rather than appending shortcut text.
  Give the default confirmation button a distinct color and make it work on Enter; do
  not append `Enter` to its label. Apply the convention across dialogs.
  Sources: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 4](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-4); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 5](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-5).

- Use one design system for list selection and hover states across file panels,
  navigation dialogs and repository file views. A hover state must not look like a
  second active selection.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 4](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-4); [2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md → Turn 1](../codex-sessions/2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md#turn-1).

- Use the combined menu/title layout in the main window after trying it in the viewer.
  Keep menus clickable and place feedback actions in the status area.
  Sources: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 4](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-4); [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 5](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-5); [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 6](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-6).

## FileIcons

As a developer, I want to resolve system icons with bounded caching and correct native
ownership so that I can show recognizable files without blocking list rendering.

### Implementation decisions

- Use system file and folder icons at their correct native display size. Avoid scaling
  them to a blurry intermediate size; adjust row and font size when needed.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7); [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3).

- Keep directory icons stable during navigation and refresh; do not briefly remove and
  recreate an unchanged icon.
  Source: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 8](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-8).

## ImageDecoding

As a developer, I want to turn supported image bytes into a displayable bitmap with
resource limits so that I can reuse image preview without spreading codec details.

## KeyboardLayouts

As a developer, I want to translate physical strokes and text through installed Windows
layouts so that I can add layout-aware typing features without duplicating native input
handling.

### Implementation decisions

- Interpret Alt name-search input through the installed keyboard layouts, so a physical
  sequence such as `еуые` can match `test`.
  Source: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7).

- When a command fails because it was typed in the wrong keyboard layout, try the
  corresponding command in the correct layout. The user requested automatic correction
  and execution rather than a manual retyping step.
  Source: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 11](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-11).
