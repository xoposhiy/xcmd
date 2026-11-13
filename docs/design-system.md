# Xcmd UI design system

Status: accepted for implementation on 2026-11-12; installed as feedback release `20261112-005600-729-2ea0425a` after 13 passing smoke scenarios. The user explicitly authorised skipping the unavailable NuGet vulnerability audit; locked dependency restore and the smoke checks passed. The [interactive presentation](design-system.html) contains the original examples, feedback inventory and proposal backlog. It remains a record of the design reviewed before implementation.

## Application implementation

The user authorised applying the complete guide on 2026-11-12. `UiPalette` now owns the shared measurements and colour roles; `UiStyles` installs application control styles; `UiLayout` owns table styling, action rows and collapsing empty error messages. These remain in Desktop within the existing three-project architecture.

Panels and search share 28-unit rows, aligned headings and dark surfaces. Primary data uses 14-unit Segoe UI; supplementary text uses 12. Forms share 12-unit margins and right-aligned actions. Search retains its context-sensitive primary action and underlined access keys. Dialog feedback lives in a compact bottom row instead of a separate top row. Operation outcomes use a table. Panel and command paths keep their useful tail. Reader palettes remain independent; selected text and search boundaries receive contrast checks across all nine themes.

The directory chooser uses two columns, History and Favorites. Starred favorites stay above frequent locations in the right column, without duplicate paths. Plain typing selects the uppermost substring match in the active list. Selected-file totals use readable binary units; the exact byte total remains available in the tooltip.

`design-system-shared-surfaces` renders panels, creation, transfer, navigation and narrow search layouts at 100/150%. `design-system-reader-contrast` checks real palette selection, selected text contrast and preservation of the found text. Existing action, responsive-column, queue and feedback scenarios remain relevant. This is feedback-phase validation, not the full finalisation gate.

## Evidence and scope

Reviewed all 81 notes in the user's saved Xcmd workspace, including 78 resolved and three open notes, and cross-checked the generated refinement list and repository resolution catalogue. Notes span 9–11 November 2026. Classification uses their original order; the presentation retains stable note IDs, full original wording, dates, element IDs and resolution text. This is the complete available in-application feedback set at this snapshot, not a claim to cover every spoken remark in every chat. Personal directory paths and interaction logs are omitted from the presentation.

Appearance, information hierarchy, wording and visual affordances are included. Navigation algorithms, filesystem policies, search execution and window lifetime are outside the visual specification. Mixed notes contribute only their visual requirements. An open note remains open until the application is changed and checked.

Inspected actual headless renders of the panel at 150%, search query/results and reader. These are historical test renders, not screenshots of the user's live desktop. Current code was checked in Program, PanelView, FileRowTheme, FileRow, SearchTables, FileSearchWindow, CommanderFrame, CommandLineView, FileOperationDialogs, FileTransferDialog, DirectoryNavigationDialog and ReaderThemes.

## Confirmed principles

All application-authored interface text is English, including labels, tooltips,
dialogs and errors. The language of development conversations does not determine
the interface language. User-entered text and file names keep their own language.

1. Use square corners. Spend space on content and readable type, not decorative padding.
2. Align table headings with values; vertically centre every cell. Use a real table for repeated structured records.
3. Keep geometry stable across hover, cursor, marking and focus. Never introduce a border, font weight or scrollbar merely because a row became current.
4. Current row, marked files, keyboard focus and search match are independent signals. Blue background means current row; amber name plus check means marked file. The inactive file panel keeps only a faint cursor. In the navigation chooser, only the active list displays a selection.
5. Use the existing Windows shell icons, rendered crisply for the current scale.
6. Display Alt access keys as underlined letters. Never append `Alt+P` to a button caption. A non-obvious command shortcut such as F3 may be shown separately. A default action is blue; do not append `Enter`.
7. Group an option with the field it qualifies and put a group action after the complete group.
8. Keep explanations short. Use one term consistently. Show detailed paths, rules and technical explanations on demand.
9. Keep full data available when truncated. Paths normally retain the useful final segments; file names retain their beginning and extension where possible.
10. Reuse compact title/menu rows and right-aligned confirmation actions. Read-only content does not receive a permanent accent frame.

## Shared tokens (accepted for implementation)

Sizes are device-independent units in Avalonia, corresponding to CSS pixels in the conceptual examples. Do not multiply them manually by the display scale.

| Token | Value | Use |
| --- | --- | --- |
| space | 4, 8, 12, 16, 24 | Internal gaps; default cell inset 8; form margin 12; group gap 16 |
| corner | 0 | Application controls and surfaces |
| type.body | Segoe UI, 14 | File names, paths, labels, buttons and table headings |
| type.meta | Segoe UI, 12 | Supplementary status only, never primary paths |
| type.section | Segoe UI Semibold, 14 | Small section labels; hierarchy comes from grouping |
| type.reader | Consolas, 14–16 | Code/text content, independently selectable reader theme |
| row | 28 | Common file/search/list row |
| header | 28 | Common table heading |
| control | 30 minimum | Form controls; allow growth for scaling/localisation |
| title | 32 | Main/viewer title/menu row |
| canvas | #0E1420 | Main window background |
| surface | #171E2B | Panels and tables |
| raised | #263247 | Headers and secondary button fill |
| text | #F1F5FA | Primary text |
| secondary | #B5C0D0 | Supplementary readable text |
| divider | #354157 | Decorative separators only |
| control.border | #7B8DA5 | Necessary input/control boundaries |
| cursor | #15558A | Active current row; inherited from FileRow |
| cursor.inactive | #1D2532 | Faint inactive current row; inherited from FileRow |
| primary | #185B92 | Default button; inherited from Program |
| focus | #65B8FF | Keyboard focus and active panel cue |
| mark | #FFD278 | Marked file name and check; inherited from FileRow |
| match | #355A45 + #F1F5FA text | Proposed search substring highlight, independent of marks |
| error | #FFB4AB | Error text plus explicit label |

Use colour roles, not literal colours inside individual windows. Text should reach 4.5:1; meaningful control boundaries and focus indicators should reach 3:1 against their adjacent colour. These are design targets adapted from WCAG, not a declaration of desktop accessibility compliance. Decorative dividers and the deliberately faint inactive cursor are not substitutes for interactive control boundaries. Test every reader palette separately.

## Component contracts

| Component | Visual contract |
| --- | --- |
| File row | Fixed mark slot and icon slot; stable text and numeric positions; mark and cursor compose; no layout-changing focus border |
| Table | Consistent header/value inset; name/path left, type centred, numbers right, dates left; last column fills; resize handles only between columns; opaque bounded footer |
| Search / issue / skipped table | Single line per record; separate Path, Line, Preview or Reason, Path, Explanation/source columns; no repeated prose per row |
| Button | Square; primary/secondary/quiet variants; normal, hover, pressed, keyboard focus and disabled states; default and focus are distinct |
| Access text | Visible underline for discoverability, unique within its scope; in Avalonia use AccessText underscore syntax, not a literal Windows ampersand |
| Field | Persistent label outside the placeholder; option beside the field; visible focus; inline error text; label remains readable in disabled state |
| Checkbox / radio / dropdown | Checkbox for independent boolean, radio for exclusive visible options, dropdown for compact mode choice; arrow only when a popup really opens |
| Link | Quiet text for secondary navigation such as History; underline on hover/focus; full path/help available through focus as well as pointer |
| Tabs | Open and Resolved are separate sets with counts; active tab uses a short accent rule and text weight, not a rounded pill |
| Tooltip | Full clipped value or brief optional help; never the sole source of essential error or action information |
| Progress | Fixed-height summary and bar, separate processed/total files and bytes, consistent units and ETA; long paths truncate without changing height |
| Message | Short result first, next action second; error label and text, not colour alone; expandable table for multiple issues |
| Dialog | Content-sized height, common margins, short action/object sentence in body, consistent right-aligned confirmation row; conflict choices grouped in the same row |
| Viewer | Combined menu/title, reading percentage near scrollbar; theme applies to reading surface with readable chrome; no decorative focus frame around content |
| Feedback | Same control vocabulary and spacing; every meaningful visible element including disabled controls remains targetable; preserve existing feedback hit testing |

For search, default colour follows the existing Enter target: Search while focus is in the query group and Go to file while focus is in results. This visual mapping must not change Enter semantics. Disabled Go to file is never shown as an available default. The preview illustrates this context switch, without claiming to emulate the search engine.

## Layout and resilience

Compact does not mean tiny text. At 100% and 150% scaling, use the same logical measurements and inspect icon pixels. At the minimum supported width, field labels, essential actions and status must remain reachable. Allow command groups to wrap; tables scroll inside their viewport; long values never push buttons out of view. Do not hide actionable errors to save space. Reserved error/message slots should collapse when empty unless doing so would cause disruptive live jitter.

Empty tables may retain a large result viewport: this is working capacity, not automatically wasted space. Remove accidental margins and redundant rows rather than shrinking a large search window to two matches. For colour-blind use retain checks, text labels, underlines and a visible active-panel cue. Keyboard focus uses an inset or overlay that does not change layout. Do not add a focus ring around an entire passive reading surface.

## Original implementation backlog (2026-11-11)

| Priority | Finding from current code | Proposed change and acceptance |
| --- | --- | --- |
| P0 | Open note 79: FileSearchWindow captions contain Alt+P and Enter; Go has no primary variant | Use To _panel AccessText; Go to file without Enter; synchronise primary treatment with actual Enter routing; preserve F3 hint. Render query/results/disabled states and assert existing shortcuts still work. |
| P1 | SearchTables uses 24/26 rows/headers and Fluent fills; panels use 26 and explicit dark-blue headers | Shared Desktop table tokens and styles; accept proposed 28 sizing on real renders first; verify aligned headings, stable geometry and no new horizontal scrolling. |
| P1 | FileConflictDialog actions use a left-aligned WrapPanel; transfer/delete actions are right-aligned; navigation has a different action order | Shared action-row composition, primary then cancel in these English dialogs; keep separate behaviour ownership and existing conflict choices. |
| P1 | Program has a primary style but sizes, margins, colours and error styles remain local literals | Introduce Desktop resources and focused style helpers; apply to existing controls without a new project or moving UI state into Core. |
| P2 | Panel path uses trailing ellipsis while command path preserves the tail | Choose path-tail truncation in panel header; verify useful directory context plus full value on hover/focus. |
| P2 | Panel rows 14, path 13, status/footer 11–12, search and history independently sized | Keep 14 for primary data and controls, 12 for supplementary metadata; compare all windows at 100/150%. |
| P2 | CommandLineView placeholder contains a long instruction; search pending-query sentence is long | Short placeholder 'Command'; move optional syntax to help; pending status 'Settings changed. Search to apply.' Retain accessible explanation. |
| P2 | ReaderThemes and FileViewerWindow.Themes have separate palettes and chrome mapping | Preserve reader choices; audit foreground/background, disabled modes, focus and search match in each; do not impose the commander's dark palette on reading content. |

This table preserves the original findings. The application implementation section above records the subsequent changes; the HTML examples and feedback statuses remain the original snapshot.

Open note 80 adds a width policy: shrink Name to a minimum before horizontal scrolling. Automatically hiding Modified, then Size, then Type was offered as an alternative and is not yet a settled requirement. Open note 81 removes deletion hints from the shortcut strip; keep the commands discoverable in menus. Both are backlog items, not completed changes.

## Review checklist

- Does every repeated record use a proper list/table with comparable columns?
- Are labels and data readable at normal scale without increasing blank padding?
- Do states compose without moving pixels or changing meaning?
- Is the Enter target visually clear, with no Enter suffix and no duplicate default in one context?
- Are Alt mnemonics underlined, unique in scope and routed correctly?
- Is each option visibly attached to the field it controls?
- Are paths, errors and disabled actions inspectable without losing essential information?
- Do 100/150% renders, long Cyrillic/Latin names, empty/full tables and minimum window widths remain usable?

For an application implementation follow the existing smoke-check and feedback-build workflow. Before committing, complete the independent usability review and run the affected feature scenarios and static checks. Run the complete behavior suite separately after commits; final releases still require the full gate. A design document alone does not require a new application release.

## External references

- [Microsoft keyboard interactions](https://learn.microsoft.com/en-us/windows/apps/design/input/keyboard-interactions) — distinguish access keys, accelerators and visible focus.
- [Microsoft design basics](https://learn.microsoft.com/en-us/windows/apps/design/basics/) — hierarchy, grouping and command surfaces; use as guidance, not a requirement to adopt WinUI styling.
- [W3C minimum text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).
- [W3C design-system accessibility settings](https://design-system.w3.org/settings/) — text and meaningful UI contrast targets.
