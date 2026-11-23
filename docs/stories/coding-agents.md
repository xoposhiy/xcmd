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

## ResumeAgentSession

As a user, I want to resume my latest agent session so that I can continue where
I left off.
