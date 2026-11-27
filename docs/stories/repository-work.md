---
id: RepositoryWork
title: "Work with repository changes"
order: 8
role: user
---

# Work with repository changes

As a user, I want to inspect repository changes and history and start a commit from Xcmd
so that I can understand and finish repository work.

[All epics](README.md)

## SeeRepositoryState

As a user, I want to see branch, repository status, and entry-level change markers so
that I can understand how my files relate to current repository work.

### Implementation decisions

- Keep the Git column visible outside a repository too. For a child folder that is a
  repository, show that repository's status in the column.
  Source: [2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md → Turn 1](../codex-sessions/2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md#turn-1).

- Do not show an ordinary ignored path as untracked. Use a subdued but legible ignored
  marker, and a strong warning for a path that matches ignore rules while already
  tracked or included in the index.
  Sources: [2026-11-13T18-22-08-01a1d5ee-4f2b-77d1-ae0b-fdf502091db3.md → Turn 1](../codex-sessions/2026-11-13T18-22-08-01a1d5ee-4f2b-77d1-ae0b-fdf502091db3.md#turn-1); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 1](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-1); [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 3](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-3).

- Use a gray minus for ignored paths. Use a yellow dot when a folder has
  more than one change type in the same status column.
  Feedback 2e4213d and 3a65f60. Codex session ID 01a1ef14-4083-7e53-915f-46aed5aab6cd.

- List local branches by the date of their latest commit, newest first. Show the date beside each name.
  Show Git's error if a switch fails. Point Taken #117 and #118.
  Codex session IDs 01a21c45-9e63-7731-8328-f16f9cf3aa48 and 01a21cbb-baea-7440-b09f-1078cbcb722b.

- Show up to four recent local branches above **Other branches…** in the repository actions menu.
  Point Taken #119. Codex session ID 01a21cbf-e56b-7f11-b11a-9104419c700a.

- Show CI status beside the repository actions button. Keep the branch and
  change counts inside the button. Point Taken #47 and #63.
  Codex session ID 01a207d0-dfad-7c01-89a6-e6ae815ea892.

- Open the GitHub Actions run in the browser when the user clicks a CI status
  that has a run. Point Taken #108. Codex session ID 01a2119b-2123-7ae0-9960-7c2cdd4e3ad0.

## SyncRepository

As a user, I want to push and pull from the Git status menu so that I can
sync the current repository without leaving Xcmd.

### Implementation decisions

- Offer Push and Pull in the repository actions menu. When the local branch
  and its upstream both have unique commits, offer Pull with rebase and Pull
  with merge.
  Feedback 8936cf6. Codex session ID 01a1ef14-4083-7e53-915f-46aed5aab6cd.

## BrowseHistory

As a user, I want to open a repository, directory, or file log in a panel so that I can
find relevant commits without changing the checkout.

### Implementation decisions

- Open history in the adjacent panel. For a selected file or directory, include only
  commits that changed that path. Use Ctrl+Alt+L for Log instead of the initial
  Ctrl+Alt+H.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 1](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-1); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 2](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-2); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 3](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-3).

- Keep the log as a dedicated commit list without file icons or Type, Size and Modified
  columns. Remove redundant Back, Diff and Close Log buttons. File-tree views inside a
  commit should use the ordinary panel appearance.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 4](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-4); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 6](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-6).

- Preserve a usable return location when copying a Git location to the other panel, so
  Escape can still leave it. Do not let a sort shortcut in the log silently alter a
  later snapshot's sort. Empty logs and their status area must describe commits rather
  than meaningless file sizes. These corrections were listed in the review and
  explicitly accepted for repair.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 7](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-7); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 8](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-8).

## ReadCommitMessage

As a user, I want to read the selected commit message beside its files or log entry so
that I can understand the reason for a change.

### Implementation decisions

- Show the full message below the currently selected log entry. Keep that message area
  when entering a commit so its identity remains visible while browsing files.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 5](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-5); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 6](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-6).

- F3 on a commit shows its message and changed-file list. Viewer navigation such as
  PageDown must not move the selected commit behind the viewer.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 1](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-1); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 2](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-2); [2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md → Turn 1](../codex-sessions/2026-11-15T16-14-58-01a1dfc6-9e68-7f83-bd85-71b26d1b9f78.md#turn-1).

## BrowseSnapshot

As a user, I want to enter the full file tree of a commit so that I can inspect the old
version while current files stay unchanged.

### Implementation decisions

- Enter on a commit opens its full repository tree, including unchanged files. Retain a
  Git column showing what changed, and use the same row, cursor, hover and header
  behavior as ordinary folders.
  Source: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 4](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-4).

- Do not present the commit timestamp as the last-modified time of every file. Leave
  per-file dates unknown or label a shown timestamp explicitly as the snapshot date. The
  user accepted the review correction without choosing between those two
  representations.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 7](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-7); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 8](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-8).

## InspectCommitChanges

As a user, I want to open the changed-file list and read deleted content so that I can
inspect what a commit actually changed.

### Implementation decisions

- F3 on the `..` entry inside a snapshot opens the changed-file list. Keep deleted
  entries accessible for comparison, not only mentioned in a text report.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 4](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-4); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 7](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-7); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 8](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-8).

## InspectWorkingChanges

As a user, I want to open a scoped list of staged, working, and untracked changes so
that I can inspect current work as a focused read-only view.

### Implementation decisions

- Show an uncommitted Working tree entry in history when local changes exist. F3 on that
  entry lists changes within the directory or file scope from which history was opened,
  not the whole repository by default.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 3](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-3); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 4](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-4).

- Keep the Working tree panel and its F3 report consistent about refresh state; do not
  silently show an old panel alongside a fresh report. The user accepted the review
  finding.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 7](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-7); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 8](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-8).

## ReadDiff

As a user, I want to view a text diff against HEAD or a commit parent so that I can
understand how content changed.

### Implementation decisions

- Compare a changed working file against HEAD, and a file in a commit against that
  commit's parent. For merge commits, use the first parent. Start with a simple text
  diff; a full diff viewer is a separate task. The user accepted these assumptions while
  correcting history filtering.
  Sources: [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 1](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-1); [2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md → Turn 2](../codex-sessions/2026-11-13T18-38-26-01a1d5fd-3f8e-7402-b55a-278d32e7a21a.md#turn-2).

## DelegateCommit

As a user, I want to delegate a repository commit to my installed coding agent so that I
can finish repository work from Xcmd.
