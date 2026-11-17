---
id: WorkspaceContinuity
title: "Continue work across application sessions"
order: 8
role: user
---

# Continue work across application sessions

As a user, I want to continue file work across application sessions so that closing the
application does not force me to rebuild my workspace.

[All epics](README.md)

## RestoreWorkspace

As a user, I want to reopen my panels and saved preferences in their earlier state so
that I can continue work after the application closes.

### Implementation decisions

- Use restart with session restoration for local updates. Preserve both panel paths,
  cursors, marks, sorting, history, scroll positions, column widths and window position,
  restoring one application window. This was the proposed restart scheme accepted by the
  user.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 5](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-5); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 6](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-6).
