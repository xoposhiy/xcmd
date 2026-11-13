# Feedback workflow usability review, 2026-11-13

An independent read-only reviewer inspected the changed workflows, source and
available 100/150% offscreen renders against the criteria in the architecture.
The reviewer did not run applications or control the live desktop.

## Findings and decisions

| Finding | Severity | Resolution |
| --- | --- | --- |
| Picking table cells preferred framework rows and lost the literal source identity of size cells, Git glyphs and icons. | P2 | Action controls retain their identity; otherwise the nearest identified control below a framework row/header is selected before that row/header. Comments retain file context. `feedback-literal-table-cell-targets` passed with actual pointer input. Search-row context is retained. |
| Home and drive-root shortcuts were absent from the Navigate menu. | P3 | Added Home folder and Drive root entries using the same destination handler as the keyboard shortcuts. `accumulated-navigation-menu-destinations` covers routing, focus and draft preservation. |
| Earlier renders included Git folder colouring. | Scope change | Removed all replacement folder glyphs, colour state, root-status reader and per-folder background queries. The native-icon regression covers dirty, clean and ignored folders, commits and repository roots. Git statuses remain in the dedicated column. |

No other required usability fixes were identified. The Git column has an in-app
legend and independent staged/working-tree indications. Folder measurements have
visible progress and cancellation. Permanent deletion retains confirmation.

The reviewer checked that process-host tests use issue stores beneath their
fixture session paths. No additional production-store override is required.

## Validation limits

Pointer and render checks use the actual Avalonia controls under Headless and
Skia. They do not prove native desktop input, foreground activation or shell
appearance. The final unrestricted run passed the required junction, registry
and Recycle Bin integration checks. See the
[finalization evidence](feedback-finalization-2026-11-13.md).
