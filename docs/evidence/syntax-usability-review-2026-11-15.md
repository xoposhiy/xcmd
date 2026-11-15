# Syntax highlighting usability review

Date: 2026-11-15. Scope: UC09/UC10 syntax reading, language selection and direct
highlighting reports. The user requested finalization and a commit.

An independent reviewer inspected the current source, scenarios, saved state,
results and offscreen images from feedback release
`20261115-175717-121-834b0d63`. The reviewer used the criteria in
`docs/architecture.md`: frequent actions need few steps and clear results; rare
actions explain their effect and remain discoverable. The reviewer did not edit
the implementation or open visible windows.

## Findings and resolutions

| Priority | Finding | Resolution |
| --- | --- | --- |
| P1 | Opening and dismissing a top menu could retain an old selection. A later shortcut report could include that old text. | Shortcut and context-menu reports use the current selection. Only the Help command receives the menu snapshot. Closing the menu clears that snapshot after its action completes. `syntax-report-current-selection-and-copy` covers selecting A, dismissing a menu, selecting B, copying and reporting B. |
| P1 | In-page scrolling used the load cancellation owner and could cancel pending syntax without restarting it. | In-page movement changes only the viewport. A page read still replaces the load request. `syntax-scroll-and-replacement-during-load` delays real context reads to distinguish scrolling from a replacement load. |
| P2 | Successful saving was confirmed only in the main window behind the viewer. | The originating viewer shows `Comment saved.` in its existing feedback status, then restores reading status. The pointer-report scenario checks the visible confirmation and one persisted issue. |

## Positive observations and limits

Automatic detection adds no action to opening a known file. Manual language
selection is available in View > Syntax language. The report shortcut opens the
comment form directly. The form shows the included sample and settings before
saving and keeps the description field and save/cancel actions separate.

The initial images showed readable colored source and a compact report form.
The initial 3,513 ms viewer scenario included nine themes and a report; it was
not a measurement of opening latency. No separate opening-latency claim follows
from that number. Finalization adds real pointer report entry and normal/150%
renders. Headless evidence does not establish native monitor transitions or
Windows input delivery.

Rich clipboard and image copy remain deferred. Searching the language list is a
possible future feature, not a requirement of this review.

## Completion

The reviewer inspected all three fixes and the regression scenarios. All
24 selected scenarios and all static checks then passed against the fixed
source snapshot. This includes the current-selection, pending-scroll and
visible-save checks listed above. The 150% source render was also inspected.
The review is complete; see the [verification record](syntax-verification-2026-11-15.md)
for the commands, release and coverage limits.
