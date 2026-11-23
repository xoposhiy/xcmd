# Agent session inspector

Open the selected Codex or Claude Code session with F3. Use the local transcript as the source for an interactive timeline. The reference is the session-inspector prototype in the xcmd-prototypes project.

## Implementation plan

1. Connect F3 and the File menu to the selected session in either panel. Return to that row on close.
2. Read local Codex and Claude Code transcripts in the background. Pair tool calls with results and load related agent traces. Keep incomplete records separate from completed calls.
3. Build the time and estimated tool-token axes, task and activity tracks, agent scope, range selection and zoom. Sum overlapping tool times separately. Count results when they finish.
4. Show files, recorded line ranges, changes and activity totals for the selection. Use recorded file lengths for percentages when available.
5. Exercise the feature through the normal application controls. Review offscreen renders at normal and high scale, fix independent review findings and publish the checked Windows build.

## Implementation notes

- The inspector reads the local history on demand. Closing the window cancels the read and returns focus to the selected session.
- User requests form the task track. Work phases use recorded plan steps, agent progress messages or runs of similar tools, in that order. Classification runs locally.
- The token axis orders results by completion. It estimates text input and output size at one token per four characters. Calls with the same completion time share a range boundary.
- File details show recorded paths, reads and changes. Claude Read results and simple, untruncated shell reads can supply line ranges. The inspector merges overlapping reads before counting lines.
- Time and token views share the same selection. The inspector clips tool time to that interval and counts parallel calls separately. The outside-tools figure uses the union of the busy intervals.
