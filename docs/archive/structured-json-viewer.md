# Structured JSON viewer

Status: interaction design accepted on 2026-11-17. Implemented on 2026-11-20.

See [ChooseViewMode](../stories/file-inspection.md#chooseviewmode) for later decisions
about the Specialized view and navigation across object keys.

The implementation keeps a decoded source snapshot and a structural index. The
parser keeps comments and token ranges and allows trailing commas in `.jsonc`
files. The reader formats only the rows near the viewport. It checks the source
version during indexing and again when the viewer regains focus.

Provide an optional, read-only text view for JSON and JSONC. Let the user inspect
one record, move between records, and apply the same expansion layout to peer
records. Keep the primary controls small: Alt with the four arrow keys, Space,
and Ctrl variants of those commands.

## Scope and entry

- Keep the existing default view. Detecting JSON must not enable Structured mode.
- Add `5 - Structured` to the viewer modes. Press `5` or choose the mode explicitly
  to enter it. Press `1` to return to the original text.
- Preserve the source position when switching modes. Do not modify or save the file.
- Opening another file starts with the existing default mode, not Structured mode.
- Start with JSON and JSONC. JSON5, NDJSON, YAML, and XML are outside the first version.
- Keep a text surface with syntax colors, text selection, and copying. Do not replace
  the document with a separate tree widget or a form of name/value controls.

The first version has no global depth selector, schema editor, filter language,
or named expansion presets. Users control detail by entering containers, folding
them, and applying a view to peers.

## Elements and expansion

An element is the root value, an object member with its value, or an array item.
Objects and arrays are containers. Object members remain in source order.

Each container has two states:

| State | Presentation |
| --- | --- |
| Compact | One logical line. Show short content in full. Abbreviate long content with a distinct fold marker and the number of hidden fields or items. |
| Expanded | Put immediate members or items on separate lines. Newly revealed nested containers start compact. |

A fold marker is viewer UI, not a character inserted into the source. Distinguish
it from a literal ellipsis in a string. Clicking the marker expands that container.
Keep omission counts outside the data text.

On first entry, expand the root. Also reveal any ancestors needed to preserve the
source position or selection. Empty containers remain readable as `{}` or `[]`.
Scalars and empty containers have no useful expansion action.

Keep expansion choices when a parent is collapsed. Reopening it restores the
previous choices. Preserve the current element and its approximate screen position
when the layout changes. Moving the cursor alone must not change expansion state,
except when entering a hidden child requires revealing it.

## Current element and orientation

The text caret or a click in the text identifies the current element. Structural
navigation moves the caret to the start of the target member or item. Highlight
the current element without replacing the user's text selection.

Show the current path and its position among siblings, for example:

```text
$.users[1].address - Field 3 of 5
Group: users[*].address
```

Paths use zero-based array indexes; position labels use one-based counts. Show
escaped key names when dot notation would be ambiguous. The group label describes
the actual scope of the Ctrl commands.

## Keyboard commands

| Key | Command | Behavior |
| --- | --- | --- |
| `Alt+Up` | Previous sibling | Move to the previous element of the same parent. |
| `Alt+Down` | Next sibling | Move to the next element of the same parent. |
| `Alt+Left` | Parent | Move to the parent without collapsing it. |
| `Alt+Right` | First child | Expand the current container if needed and enter its first child. |
| `Space` | Toggle expansion | Expand or collapse the current container without moving the caret to another element. |
| `Ctrl+Alt+Up` | Previous peer field | Move to the same relative path in the previous matching array item. |
| `Ctrl+Alt+Down` | Next peer field | Move to the same relative path in the next matching array item. |
| `Ctrl+Alt+Left` | Collapse peers | Collapse the containers in the current peer group. Keep the current element. |
| `Ctrl+Alt+Right` | Expand peers | Expand the containers in the current peer group. Keep the current element. |
| `Ctrl+Space` | Apply view to peers | Apply the current container's expansion layout to its peers. Keep the current element and its layout. |

Sibling movement skips all descendants, whether expanded or compact. It does not
wrap or cross into another parent. Parent movement stops at the root. Entering a
scalar or an empty container has no effect.

Only the Structured reading surface uses these bindings. Search fields, menus,
dialogs, and feedback retain their own input handling. Unmodified arrows retain
normal reader behavior. In Structured mode, `Alt+Left/Right` replace the reader's
previous/next file shortcuts, and Space replaces page scrolling. Keep file
navigation available through its existing menu commands. Other modes retain their
existing shortcuts.

## Peer groups

A peer group uses the nearest array ancestor of the current element. Find the
item in that array that contains, or is, the current element. Keep the relative
path within that item, and replace only that item's index with a wildcard.

| Current element | Peer group |
| --- | --- |
| `users[3]` | `users[*]` |
| `users[3].address` | `users[*].address` |
| `users[3].address.city` | `users[*].address.city` |
| `users[3].orders` | `users[*].orders` |
| `users[3].orders[2]` | `users[3].orders[*]` |

An array does not contain itself. Selecting `orders` therefore groups the order
arrays of all users; selecting an order groups the orders of that user.

Equal depth or equal field names elsewhere in the document do not create a group.
Do not infer that an object is a dictionary of equivalent records. An element
without a containing array has no peer group; indicate that the group commands
are unavailable. Moving to a user record changes the group back to `users[*]`.

Peer navigation preserves the relative field path. For example,
`Ctrl+Alt+Down` at `users[3].address.city` visits `users[4].address.city`.
Skip items that lack the path and show the skipped count. Stop at the group boundary;
do not wrap. A value of a different type still exists and can be visited.

Reveal only the ancestors needed to reach a navigation target. Do not expand other
records merely because the user moves between them.

## Group expansion and layout transfer

`Ctrl+Alt+Right` expands the containers at the group path. It does not recursively
expand their descendants. `Ctrl+Alt+Left` collapses those containers. Both commands
are absolute actions: repeating one does not toggle the result. Existing nested
expansion choices remain available when the containers reopen.

`Ctrl+Space` means **make the other records look like this record**. It is not a
group toggle. Capture the current container's compact/expanded state and its
descendant expansion choices, then apply that layout to matching paths in the
other group members.

Example:

1. Expand `users[1]`.
2. Expand its `address`; leave `permissions` compact.
3. Return to `users[1]` and press `Ctrl+Space`.
4. Every compatible user record is expanded, with `address` expanded and
   `permissions` compact.

The resulting text may look like this; ellipses represent viewer fold markers:

```text
"users": [
  {
    "name": "Ada",
    "address": {
      "city": "London",
      "country": "UK"
    },
    "permissions": […]
  },
  {
    "name": "Lin",
    "address": {
      "city": "Berlin",
      "country": "Germany"
    },
    "permissions": […]
  }
]
```

If the current container is compact, `Ctrl+Space` makes its peers compact too.
If the current element is `users[1].address`, only the address containers receive
the layout. Other fields of each user remain unchanged.

Apply layout by structural paths, not text similarity. Missing paths do not create
data. Incompatible container types do not receive the copied layout. Target-only
fields keep their existing state. Report skipped members when the action cannot
apply to the whole group. Scalars have no expansion layout to copy.

For paths inside a copied layout, preserve literal nested array indexes; do not
introduce additional wildcards. A separate command on a nested array selects its
own peer group. Repeating `Ctrl+Space` with the same source layout has the same result.

## Search, selection, and source fidelity

Search the entire source, including folded content. Reveal the path to a match.
Treat search-only expansion as temporary: clearing search restores prior folds,
while preserving explicit expansion changes made by the user during the search.

Normal copying includes the full selected content represented by folds, without
viewer ellipses or count labels. Make selection of hidden content visible. Text
selection must remain a source range even when formatting inserts whitespace.
An optional future `Copy view` command is outside the first version.

Preserve comments, member order, duplicate keys, number spelling, and string
escapes. Formatting may change whitespace outside tokens, but must preserve token
boundaries and line-comment termination. Do not parse into a key/value dictionary
and serialize it as a replacement for the source.

Preserve duplicate member occurrences as separate elements. Use the occurrence
number as well as the key when resolving a path, and distinguish repeated keys in
the displayed path. Do not silently merge them during navigation or layout transfer.

Keep source positions separate from formatted line positions. Returning to the
original text restores the corresponding source location. Errors, incomplete
input, or unsupported syntax must leave the original text available with an
explicit explanation; never present a guessed structure as a complete parse.

## Implementation constraints

Follow the [project rules](../../AGENTS.md) and [UI process](../processes/ui.md).
The original design drew on the [architecture](architecture.md),
[design system](design-system.md) and
[development requirements](development-requirements.md).

- Core owns structural positions, peer resolution, navigation rules, and expansion
  state transitions without UI or filesystem dependencies.
- Infrastructure reads and indexes source through the viewer adapters and translates
  parser results into source ranges. Select a parser during implementation; this
  specification does not choose a library.
- Desktop owns text layout, caret/selection mapping, input routing, and visual state.
- Keep indexing cancellable and outside the UI thread. Reject stale results after
  file, encoding, or document-version changes. Show partial indexing honestly.
- Do not format each loaded text page independently. The current reader uses 16 KiB
  pages, and [syntax highlighting](../stories/reusable-components.md#syntaxspans) supplies lexical spans,
  not a document tree. Containers and peer groups can cross page boundaries.
- Keep large arrays and group commands responsive without constructing a fully
  expanded string for the entire file. Exact resource budgets belong to implementation
  measurements, not to an arbitrary limit in this interaction design.

## Required acceptance cases

These are requirements for implementation, not evidence of completed tests.

1. Opening JSON preserves the existing default view. Explicit entry and return
   preserve the source position and leave file bytes unchanged.
2. Ordinary reader shortcuts remain unchanged outside Structured mode. Search,
   menu, and feedback input are not intercepted by structural commands.
3. A sibling jump skips a large expanded record and stops at the parent boundary.
4. Enter, parent, and Space obey the table, including root, scalar, and empty values.
   Folding and reopening a parent restores nested choices.
5. Peer paths follow the nearest array. Nested orders never accidentally group all
   orders of all users. Object dictionaries are not inferred as record groups.
6. Peer navigation preserves the relative field path, reveals the target, reports
   missing-path skips, and does not wrap.
7. Group expand/collapse handles mixed current fold states without moving the
   current element or recursively changing descendant choices.
8. Applying a user layout expands matching addresses and leaves permissions compact.
   Applying an address layout leaves all other user fields unchanged.
9. Layout transfer handles missing fields, extra fields, different types, duplicate
   keys, and nested array indexes without changing data or expanding unrelated paths.
10. Search finds folded content and restores temporary folds. Copying across folds
    includes hidden content and excludes viewer markers and labels.
11. JSONC comments, Unicode, escaped keys, duplicate keys, and number spelling survive
    formatting and navigation. Invalid input never produces an unmarked guessed view.
12. Large arrays, page-crossing containers, cancellation, and source changes preserve
    responsive input and correct source mappings. Offscreen checks at 100% and 150%
    scale verify path visibility, selection, and stable position during expansion.

Keep scenario cases in `tests/Xcmd.Probe/Scenarios` and test infrastructure in
`tests/Xcmd.Probe/Infrastructure`. Use targeted checks and the normal feedback-build
workflow when implementation is requested. A full local regression run still
requires an explicit user request.
