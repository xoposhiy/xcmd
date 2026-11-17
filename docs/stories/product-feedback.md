---
id: ProductFeedback
title: "Improve Xcmd while using it"
order: 9
role: user-developer
---

# Improve Xcmd while using it

As a user-developer, I want to report problems and try new local builds while using Xcmd
so that development stays connected to real work.

[All epics](README.md)

## ReportControl

As a user-developer, I want to select a control and describe the change I need so that I
can give the development agent feedback with a stable target.

### Implementation decisions

- Let the user select an interface element and attach a comment for the agent. Make
  feedback available while a modal dialog is open and keep meaningful labels and other
  visible surfaces selectable.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 6](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-6); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 7](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-7); [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 7](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-7).

- In feedback mode, a double-click on an element should forward a normal click, so menus
  can be opened before selecting a nested target. Record that interaction path with the
  comment.
  Source: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 4](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-4).

- Place Feedback and Notes at the right of the status bar when using the combined
  title/menu layout.
  Source: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 5](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-5).

- A click on a highlighted feedback target should open the comment dialog promptly and
  reliably, without requiring repeated clicks.
  Source: [2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md → Turn 11](../codex-sessions/2026-11-16T09-54-18-01a1e390-7589-7560-87d3-1720ccec7f13.md#turn-11).

## KeepFeedback

As a user-developer, I want to save a note immediately and inspect or remove my recorded
notes so that I can track refinement work without depending on the workspace save.

### Implementation decisions

- Show open feedback separately from closed feedback; put closed items in their own tab.
  Source: [2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md → Turn 3](../codex-sessions/2026-11-11T15-25-18-01a1caff-b568-7bf3-b0e2-452cad10d2ec.md#turn-3).

## ReportHighlighting

As a user-developer, I want to attach the selected source sample and highlighting
context to feedback so that I can make a rendering problem reproducible without retyping
details.

### Implementation decisions

- Provide a very simple way to report a syntax-highlighting problem. The user requested
  the workflow but left its shortcut and library-specific attachment format to the
  agent.
  Source: [2026-11-15T17-36-05-01a1e010-e1ad-7291-8f35-6502613da725.md → Turn 1](../codex-sessions/2026-11-15T17-36-05-01a1e010-e1ad-7291-8f35-6502613da725.md#turn-1).

## TryLocalRelease

As a user-developer, I want to switch to a ready local build when the application is
idle so that I can test improvements during normal use.

### Implementation decisions

- Deliver a built, ready-to-run application for feedback, without a demonstration
  directory or a script that requires the user to build first.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 2](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-2); [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 4](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-4).

- During active development, run a quick smoke check, publish the feedback build locally
  without another approval request, and return for feedback. Aim to keep feedback
  verification under a minute. Finalize after the user accepts the feature or requests a
  commit; the later targeted-test rule applies at that stage.
  Sources: [2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md → Turn 3](../codex-sessions/2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md#turn-3); [2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md → Turn 4](../codex-sessions/2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md#turn-4); [2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md → Turn 7](../codex-sessions/2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md#turn-7).

## StableShortcut

As a user-developer, I want to use a permanent application entry point across releases
so that I do not need to replace shortcuts when a build changes.

### Implementation decisions

- Keep a permanent `Xcmd.exe` and taskbar shortcut while application versions live in
  separate release folders. The launcher reads `releases/latest.txt`, validates the
  target, forwards arguments and the working directory, starts that release and exits.
  Sources: [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 5](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-5); [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 6](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-6).
