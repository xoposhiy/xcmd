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
- Use one footer line for the session count and the size of the selected
  conversation. Separate the footer from the list with a line. Use short token
  counts such as 100k and 10m. Keep exact counts in the tooltip. Point Taken #48.
  Codex session ID 01a207d0-dfad-7c01-89a6-e6ae815ea892.

## InspectAgentSession

As a user, I want to inspect what an agent did during a session so that I can understand how it spent time and which resources it used.

### Implementation decisions

- Press F3 on a selected agent session to open a session inspector based on
  the session timeline prototype.
  Codex session ID 01a20095-f346-7aa1-bffe-b63fc3b3d258.
- Open the session timeline with estimated tokens selected when the session has
  token estimates. Point Taken #60.
  Codex session ID 01a207d0-dfad-7c01-89a6-e6ae815ea892.
- Keep each inspector in an independent window. Let users browse other sessions
  while it is open.
  Codex session ID 01a20095-f346-7aa1-bffe-b63fc3b3d258.
- Classify completed tool calls with Luna when the inspector opens. Keep the
  summary, target/action pairs and concrete objects for each call. Reuse saved
  classifications when the session grows or the user opens it again.
  See the [classification specification](../archive/session-tool-call-classification.md).
  Codex session ID 01a205bb-aa5d-7a91-b342-a1790338950d.
- Ask Luna for each call's touched resources in the same classification request,
  in batches of up to 20 calls with medium reasoning effort. Use the model's
  resource lists to show files, directories, URLs, issues, repositories and other
  identifiers for the selected range and agent. Open a resource to see its calls.
  Codex session ID 01a207b3-4bb0-7eb1-9472-484dc54f9d5c.
- Keep the labelled tool calls in [the test suite](../../tests/SessionClassification/README.md). Run the paid accuracy test against the Xcmd classifier only when a user explicitly requests it.
  Codex session ID 01a20850-692e-7061-abce-33820fe459e5.

## ResumeAgentSession

As a user, I want to resume my latest agent session so that I can continue where
I left off.
