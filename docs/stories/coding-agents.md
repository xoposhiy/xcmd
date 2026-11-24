---
id: CodingAgents
title: "Work with coding agents"
order: 7
role: user
---

# Work with coding agents

As a user, I want to work with coding agents from Xcmd so that I can manage my
coding work across projects.

[All epics](README.md)

## ViewRecentAgentSessions

As a user, I want to see my recent agent sessions and open a session’s project
folder so that I can find the work I am looking for.

### Implementation decisions

- Open the selected session's project folder in the other panel. Keep the session
  selected so the user can continue browsing the list.
  Codex session ID 01a20024-3466-7680-b829-784bc0a74a0a.
- Mark sessions with complete Luna results in the list so users can see which
  sessions already have all calls classified. Show a size that is quick to
  read for each session. Use the short label Claude in the Agent column.
  Point Taken #64. Codex session ID 01a20814-7061-7c11-ba7f-e584674e688b.
- Show transcript sizes in whole KiB with the suffix `K`. Right-align the
  heading and values for comparison. Keep the exact byte count in the tooltip.
  Point Taken #87. Codex session ID 01a20d43-56bc-7101-ba17-d594796a4a04.
- Use one footer line for the session count and the selected session's user
  prompts, tool calls and model. Separate the footer from the list with a line.
  Keep exact token counts in the tooltip. Point Taken #48 and #97.
  Codex session IDs 01a207d0-dfad-7c01-89a6-e6ae815ea892 and
  01a20ef4-bc33-7d01-ab23-94751e719902.

## InspectAgentSession

As a user, I want to inspect what an agent did during a session so that I can understand how it spent time and which resources it used.

### Implementation decisions

- Press F3 on a selected agent session to open a session inspector based on
  the session timeline prototype.
  Codex session ID 01a20095-f346-7aa1-bffe-b63fc3b3d258.
- Open the session timeline on API cost. Price each model response once with the
  2026-11-24 standard global API rates. Show messages without tool calls and mark
  missing costs. See the [API cost timeline specification](../archive/session-inspector-api-cost.md).
  Codex session ID 01a20e2d-e2e4-7860-96b3-6f75fff6da43.
- Place the Time and API cost links next to the timeline title. Keep the
  action icons on the timelines and explain them in the reading guide.
  Point Taken #84 and #85. Codex session ID 01a20d11-4e43-7ee0-bc64-0a73729ea0bb.
- Keep Tasks and Work phases above the agent tracks. On the API cost axis, draw
  each response on all the tracks and resource rows it touches. Join adjacent
  responses by agent and task on each row. Hover to see the same response's
  boundaries in both timelines. Click to see its cost and calls below the
  session timeline. Use whole responses when selecting a cost range.
  Codex session ID 01a20e2d-e2e4-7860-96b3-6f75fff6da43.
- Double-click a task or work phase to zoom to its range. Right-click a task
  or work phase to reset zoom and keep the selected range. Point Taken #112.
- Split the main agent into Source, Tests, Docs and Others. The Docs track
  includes documentation, agent instructions and issue tracker activity.
  Use the same target tracks for child agents on the API cost axis and one track
  per child agent on the Time axis. Order activity tracks by their first event
  on the selected axis. Fill the track height with colored blocks and show
  the tool name, summary and icons for all target and action pairs on hover.
  Draw a pencil on the block for an edit and a play icon for a run, aligned with
  the call. Use one icon color
  at every block width. Give edits and runs the same, more saturated version
  of the target color.
  Leave reads unmarked.
  Codex session ID 01a20cd8-9aeb-7db2-896f-4640815be77a.
  Point Taken #95. Codex session ID 01a20faa-fd7c-75e1-a059-002d7e87c8c7.
- Sort Activity breakdown by API cost in the selected range, highest first.
  Split each response's cost equally among its calls. Split each call's share
  equally among its targets, then among each target's actions.
  Codex session ID 01a20e2d-e2e4-7860-96b3-6f75fff6da43.
- Let the user combine actions into one row per target and sort by tool
  time, API cost or calls. Count each call once per target when
  combining actions. Point Taken #76.
  Codex session ID 01a20cd8-9aeb-7db2-896f-4640815be77a.
- Combine actions by target by default. Label and separate the completed tool
  call count from the API cost. Point Taken #80 and #82.
  Codex session ID 01a20d11-4e43-7ee0-bc64-0a73729ea0bb.
- Filter touched resources by resource type, target and action. Include
  Other with each action choice. Let users select and copy recorded tool call
  text. Point Taken #75 and #77.
  Codex session ID 01a20cd8-9aeb-7db2-896f-4640815be77a.
- Show the call count for a selected span with several calls. List those calls
  in order with a number and duration. Show the selected call's tool name,
  timing, response cost and model on the left. Show its summary, actions and
  resources on the right. Show up to three resources directly. Let the user
  expand larger lists and bring the expanded groups into view. Separate
  resource names within each group with commas and wrap at the available
  width. Select the tool name to open the selected
  call's recorded input and output in a dialog. Show JavaScript in Input
  when the tool received JavaScript as its input.
  Point Taken #96, #109 and #110.
- Use the same checkbox drop-down for resource type, target and action. Let
  users select several options in each filter and apply each change while the
  drop-down stays open. Match any selected option within a filter and require
  a match in all three filters. Point Taken #79.
  Codex session ID 01a20d11-4e43-7ee0-bc64-0a73729ea0bb.
- Use larger handles in the timeline's Range row for the start and end.
  Update the panels below the timeline while the user drags. Point Taken #66.
  Codex session ID 01a208da-a802-7092-8d93-a9bf938c907b.
- Keep each inspector in an independent window. Let users browse other sessions
  while it is open.
  Codex session ID 01a20095-f346-7aa1-bffe-b63fc3b3d258.
- Open the local timeline and saved results immediately. Show the session date,
  agent and recorded model at the top. Open a call's details
  directly from the timeline. Start Luna only when the user chooses Analyze
  with Luna. Point Taken #71 and #74.
  Codex session ID 01a20a89-fac7-7b33-abc9-9b1bfa8e7d25.
- Show whole-session duration, API cost and completed calls at the top.
  Show range and agent totals beside the selected range. Point Taken #83.
  Codex session ID 01a20d11-4e43-7ee0-bc64-0a73729ea0bb.
- Show the full session ID in the top summary with a borderless copy icon.
  Point Taken #94. Codex session ID 01a20ef4-bc33-7d01-ab23-94751e719902.
- Keep the summary, target/action pairs and concrete objects for each call. Reuse saved
  classifications when the session grows or the user opens it again.
  See the [classification specification](../archive/session-tool-call-classification.md).
  Codex session ID 01a205bb-aa5d-7a91-b342-a1790338950d.
- Reuse saved classifications after a prompt change. Show that an update is
  available and refresh outdated results when the user chooses Update with Luna.
  Invalidate saved results when the cache format changes.
  Codex session ID 01a20cf0-8539-7331-a19d-25a416b17d90.
- Ask Luna to write call descriptions in English for sessions in any language.
  Codex session ID 01a20cf0-8539-7331-a19d-25a416b17d90.
- Ask Luna for each call's touched resources in the same classification request,
  in batches of up to 20 calls with medium reasoning effort. Use the model's
  resource lists to show files, directories, URLs, issues, repositories and other
  identifiers for the selected range and agent. Open a resource to see its calls.
  Codex session ID 01a207b3-4bb0-7eb1-9472-484dc54f9d5c.
- Show touched resources in an expandable tree on the overview. Show files by
  default and let users select other resource types. Color names by activity
  target and show each resource's operations on the selected session axis.
  Time and API cost switch both timelines together. In wide windows,
  fit Activity breakdown to its headings and values on the left. Give Touched
  resources all remaining width on the right.
  Size the path column to the visible names and give the remaining width to the timeline.
  Show a single-child directory chain as one path. Draw each action at full
  height and show both colors where actions overlap. Point Taken #72 and #73.
  Point Taken #65 and #67.
  Codex session ID 01a20a89-fac7-7b33-abc9-9b1bfa8e7d25.
- Use compact, even rows for files and folders. Use the session timeline's
  target colors, brighter shades and icons for edits and runs in the resource
  timelines. Point Taken #81.
  Codex session ID 01a20d11-4e43-7ee0-bc64-0a73729ea0bb.
- Order files in each folder by the start of their first matching call. Order
  folders by the earliest matching file access in each folder tree. Use
  directory calls when a folder tree has no matching file calls. Point Taken #78.
  Codex session ID 01a20d11-4e43-7ee0-bc64-0a73729ea0bb.
- Keep the labelled tool calls in [the test suite](../../tests/SessionClassification/README.md). Run the paid accuracy test against the Xcmd classifier only when a user explicitly requests it.
  Codex session ID 01a20850-692e-7061-abce-33820fe459e5.

## ResumeAgentSession

As a user, I want to resume my latest agent session so that I can continue where
I left off.
