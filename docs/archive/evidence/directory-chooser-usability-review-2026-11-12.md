# Directory chooser usability review

Reviewed on 2026-11-12. Scope: UC15 recent and frequent directories, UC16 favorite directories, and the readable selected-file total in the panel summary.

An independent reviewer inspected the source and the scenario cases. The reviewer did not edit files, run builds or open windows. The review used the architecture criteria: frequent commands need few actions and clear results; rare actions explain their effects; rare scenarios can be found in the application.

## Findings and resolutions

| Finding | Resolution |
| --- | --- |
| Rows in the chooser do not show the text that matched the query. A short query can select a row because of a parent folder name. | Deferred. Match highlighting in the chooser rows is a visible change that needs user feedback first. The panel quick search already highlights matches; reuse the same style when this is implemented. |
| A key with no match gives no feedback. | Kept. The panel quick search ignores unmatched keys in the same way, so both searches behave the same. |
| Backspace clears the whole query instead of the last character. | Kept. The panel quick search resets on keys that do not type text, and the chooser follows the same rule. The query is short, so typing it again is cheap. |
| In Favorites, F2 and Delete do nothing on an unstarred frequent row. | Kept. The star column shows which rows are pins, and the name editor and Remove are disabled for other rows. |
| The Favorites heading tooltip no longer explained which directories count as frequent. | Fixed. The tooltip again states the rule: at least ten active seconds, roots and quick pass-through visits are excluded, and weight halves every three days. |
| The summary tooltip showed a byte count when the summary displayed a directory error, and the count had no label. | Fixed. The tooltip reads "Selected: N bytes" and is removed while the summary shows an error. |

The reviewer found the unit formatting correct: totals round at unit boundaries, keep "≥" and "(incomplete)", and the tooltip keeps the exact byte count.

Feature question, not a defect: the chooser cannot pin the selected frequent row directly. Insert and "Add current directory" pin only the current panel directory, and the button label states this.

## Verification

`selection-size-readable-units` checks the labeled exact byte tooltip. The navigation scenarios listed in [verification](../verification.md) cover the two-column chooser, pinned favorites and typed search. Full verification results belong to the commit evidence.

## Limits

The review does not establish native tooltip placement, native keyboard delivery or IME input. Headless checks use the actual controls.
