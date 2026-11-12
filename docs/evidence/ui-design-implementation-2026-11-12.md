# UI design-system implementation: feedback review

The user accepted the design guide and authorised applying it across the application on 2026-11-12. This records the implementation's own visual review, not an independent finalisation review.

## Scope

- Shared Desktop colour/measurement tokens, square control styles, readable body/meta typography and common table/action-row composition.
- File panels, search and issue tables, directory navigation, new-folder/transfer/delete/conflict forms, feedback notes/comments, command input and operation history.
- Independent reader palettes with selected-text contrast and visible search-field boundaries. Palette changes retain the selection made before opening the menu.
- Existing input routing, filesystem policies, native icons and responsive-column behaviour retained.

## Review findings and decisions

| Finding | Resolution |
| --- | --- |
| Generic base-control selectors did not enforce square corners on derived controls | Use derived-type selectors for shared control/window/text rules; assert actual corner and inherited font values. |
| Updating panel text reinstated trailing truncation | Apply tail-preserving path truncation when loading a directory as well as at construction. |
| Feedback rows added large empty space above compact forms | Use a quiet, compact footer row; feedback target IDs and actions remain available. |
| Reader menu interactions could clear a found-text selection between palette changes | Capture selection before opening the menu and restore it after palette/input changes, guarded by current theme and file text. |
| Some reader selection colours and field boundaries lacked contrast | Choose black/white selection text against the actual blended background; retain palette colours and use a readable field boundary/focus colour. |
| Operation details mixed repeated records into a prose block | A proper table separates Result, Source, Destination and Explanation; full cell values remain in tooltips. |

## Evidence and limits

The current smoke set has 13 passing scenarios, including actual controls, key routing, copy/delete/conflict actions, queue cancellation, feedback, responsive columns, all nine reader themes and 100/150% offscreen layout. It reports zero native application windows. Relevant outputs are under `artifacts/ui-design/smoke-final`; these are generated evidence, not repository inputs. Compilation took 11.86 seconds and the selected scenarios approximately 21.81 seconds, within the feedback verification budget.

The full gate and independent usability review remain deferred until the user accepts the implemented appearance. Native Windows appearance and monitor transitions are not proven by the headless renders.

Normal publishing was attempted and stopped at NU1900: the environment could not resolve `api.nuget.org`. Local compilation and the focused smoke set use the already cached, locked dependencies without a successful vulnerability audit. A complete candidate package can be prepared for review, but the installed release marker must remain unchanged until the audit succeeds or the user explicitly permits the feedback-only exception in `docs/development-workflow.md`.

The user subsequently explicitly authorised that exception. `scripts/Publish-Feedback.ps1 -SkipPackageAudit` repeated the 13 passing smoke scenarios, published immutable release `20261112-005600-729-2ea0425a`, installed the permanent launcher and shortcut, and updated `releases/latest.txt`. Its `verification.json` records `packageAudit: skipped-explicitly` and `fullVerification: false`. No visible window was opened by the agent.
