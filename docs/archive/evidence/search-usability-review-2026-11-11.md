# Search workflow usability review

Reviewed on 2026-11-11. Scope: UC03 name navigation, UC23 file search and UC31 text search.

An independent reviewer inspected the source, scenario cases and offscreen captures. The reviewer did not edit files, run builds or open visible windows. The review used the architecture criteria: frequent commands need few actions and clear results; rare actions explain their effects; rare scenarios can be found in the application.

## Findings and resolutions

| Finding | Resolution |
| --- | --- |
| The progress timer could replace an invalid-query message after an earlier search. | Keep validation errors in a separate label. Preserve the error through progress refreshes. Clear it when the draft changes or a valid query starts. |
| Double-clicking a skipped or failed location replaced the scope and results without explaining the effect. | Explain the action in the table, expander and cell tooltips. Include the full location or error in cell tooltips. |
| Enter did not apply a query from the closed name-mode list. | Apply the query when the list is closed. Leave Enter to the list when it is open. |

The independent source-only follow-up found all three findings resolved and no remaining usability finding.

## Verification

Regression scenarios check that validation errors survive progress refreshes, invalid input preserves earlier results, Enter applies a closed name-mode list, and both detail tables explain replacement. Existing scenarios cover query focus, history, preview, result navigation, skipped-scope expansion, location search and highlighted name navigation. Full verification results belong to the release evidence.

## Limits

The review does not establish native tooltip placement, popup input delivery, multi-monitor behavior or large-tree search latency. Headless checks use the actual controls and offscreen rendering. Scheduling checks use controlled workers rather than elapsed-time thresholds. The engine has not been benchmarked against grep or ripgrep.
