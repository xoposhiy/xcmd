# Change the UI

Keep frequent actions short, results clear and less common actions discoverable.
Apply confirmed feedback rules across analogous controls. A visual change does
not authorize a change to command behavior; historical proposals are not accepted
requirements.

## Shared conventions

Reuse the current Desktop [palette](../../src/Xcmd.Desktop/UiPalette.cs),
[styles](../../src/Xcmd.Desktop/UiStyles.cs) and
[layout helpers](../../src/Xcmd.Desktop/UiLayout.cs) so related controls stay
consistent. Keep these confirmed conventions when extending the interface:

- Use square corners, compact groups and readable text. Align table headings with
  values and use tables for repeated structured records.
- Keep geometry stable across hover, cursor, marks and focus. Current row, marked
  files, keyboard focus and search matches are separate signals. Preserve the blue
  current row, amber marked name/check and faint inactive panel cursor.
- Use crisp Windows shell icons at the current scale. Preserve icons during an
  unchanged refresh rather than making the list flicker.
- Show Alt access keys as underlined letters, unique within their scope. Show the
  current Enter action with the primary style, without `Alt+...` or `Enter` caption
  suffixes. A separate function-key hint may remain. Styling follows input routing.
- Group an option with its field. Keep labels visible, explanations short, title
  rows compact and confirmation actions right-aligned.
- Keep full truncated data available. Preserve useful path tails and readable
  file names. Essential errors and actions must not depend on a tooltip or color
  alone. Passive reading surfaces need no permanent accent frame.

Use shared color roles and logical measurements rather than local copies or manual
display-scale multiplication. Keep reader themes independent. Aim for 4.5:1 text
contrast and 3:1 meaningful control/focus contrast; these are design targets, not a
claim of desktop accessibility compliance.

## Feedback identity

Every application-authored control needs a unique literal `.WithFeedbackId("...")`
at its creation site. Keep IDs stable when changing layout, because reports refer
to source definitions. The Desktop analyzer enforces this, including labels,
containers and disabled controls. Framework-generated internals use the nearest
identified application ancestor.

Keep meaningful content selectable for feedback, including disabled actions.
Preserve hit-test surfaces and attach application dialogs through
`FeedbackController`. Verify new feedback paths with real headless pointer input.

## Evidence and independent review

Use headless interaction and offscreen renders for affected states, including
normal and 150% scale, narrow layouts, long values and relevant reader themes.
Choose the cases that can reveal a problem; avoid mechanically repeating every
visual check for every edit.

During finalization of a changed user workflow, use an independent usability
reviewer. Give the reviewer the story, change and these criteria without the
implementer's conclusions:

- Frequent commands use few actions, give clear result feedback and avoid delays.
- Rare actions explain what they do and what they change.
- Rare scenarios can be found and understood inside the application.

Fix reported problems or record a reasoned decision. Review does not authorize new
features. Feedback builds do not wait for this review, and documentation,
formatting or checks that preserve behavior do not require it. Follow
[delivery](delivery.md) for the phase and handoff.
