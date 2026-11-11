# Viewer usability review

Date: 2026-11-11.

Status: Accepted after an independent re-review. No unresolved usability findings remain.

## Scope and criteria

An independent reviewer inspected the text and image viewer, external editor handoff, shared window header, themes, search and feedback selection. The review used source, scenarios, the user guide and offscreen images at normal and narrow sizes.

The review used the criteria in the code architecture: frequent commands need few actions and clear results; rare actions explain their effect; rare scenarios remain discoverable inside the application.

## Findings and fixes

| Finding | Decision and verification |
| --- | --- |
| Image mode offered text actions that did nothing or used hidden text. | Disable text search, selection and text navigation commands in Image mode and before content loads. `viewer-images-and-fallback` checks Image → Hex → Image with a selected byte view. |
| A failed file read blocked keyboard navigation and left the loading status visible. | Keep file navigation and external actions available without loaded content. Show a completed error state. `viewer-read-failure-and-keyboard-recovery` checks a deleted file, F4, external open, return to a readable file and retry after restoration. |
| Feedback instructions appeared only in the main window. | Show a temporary hint in the active viewer's status area. Restore the reading status when feedback ends. `viewer-reading-menu-search-layout` checks both states. |

Copy selection remains available in a loaded text or byte view with an empty selection. The text control treats this as a no-op. Image mode cannot copy the hidden text buffer through the menu.

The compact header, normal and narrow reader layouts, theme choices, search shortcuts, independent viewer lifetime and status separator meet the review criteria. The interface keeps file content as its focus.

## Limits

The review does not establish native pointer delivery, desktop window dragging, Windows Snap or Notepad++ appearance. Automated checks cover the real Avalonia chrome hit-test, application input routing and the Windows editor launch contract with an isolated windowless endpoint. Full release results remain in the generated release verification files.
