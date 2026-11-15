# Local issue tracker

`issues.db` is the authoritative SQLite database for in-app feedback. It lives in
this repository and is committed with the project. It is independent of saved
window/panel sessions. Use the CLI instead of changing the database directly.

From the repository root, with the .NET 10 runtime and a published local build:

```powershell
./scripts/Issues.ps1 list
./scripts/Issues.ps1 start ID1 ID2
./scripts/Issues.ps1 close ID1 ID2
./scripts/Issues.ps1 release ID1
./scripts/Issues.ps1 reopen ID1
./scripts/Issues.ps1 list --all
```

`list` prints all new and in-progress issues as UTF-8 JSON lines. `--all` includes
closed issues. Each line contains the ID, status, original user text, target
ID/name and the application release in which the comment was made. No directory,
timestamp, interaction history or resolution prose is stored. Unambiguous ID
prefixes are accepted. Commands return a nonzero exit code on failure.

Claim every issue in a work batch with one `start` call. If **any** issue is already
in progress, `start` fails, identifies that work and asks the agent to coordinate.
Do not bypass this guard. `close` and `release` require in-progress issues;
`reopen` requires closed issues. `release` returns unfinished work to `new`.
Validation and updates of a batch run in one SQLite write transaction.

The running application refreshes the issue list after CLI changes without a
release or restart. New comments are inserted immediately; failed saves keep the
comment editor open. A new session snapshot does not contain feedback. The first
startup imports older session comments by ID without replacing existing issues
or their current status. Original text, target and release are preserved.
The viewer's Report highlighting problem command appends the context shown in
the form to the comment text: file name, language, engine version, theme,
encoding, position, status and up to 4,096 selected characters. It uses the same
store and claim workflow without adding tracker columns or uploading code.
The transaction also records that legacy import completed, so a stale session
from an older application cannot resurrect intentionally removed issues.
For an explicit one-time migration, use `./scripts/Issues.ps1 import-legacy PATH`
with the old `session.json`. This reads the session without modifying it.

`target.id` is a literal `WithFeedbackId("...")` in Desktop source. Search the
literal to find its definition. IDs identify source definitions: two panels,
repeated rows and reused templates may instantiate the same definition. The
target name identifies the clicked item/window. Framework-generated controls
resolve to their nearest identified application control. Historical migrated
targets retain their old IDs; their source cannot always be reconstructed.

The `UiChecks` Roslyn analyzer is loaded by every Desktop compilation. Missing IDs
(`XCMDUI001`), duplicate source definitions (`XCMDUI002`) and nonliteral/empty IDs
(`XCMDUI003`) fail the build. This includes text, disabled actions, layout
containers and custom controls constructed by application code. Avalonia's own
generated template internals are outside the application source check.

SQLite uses the Windows system library `winsqlite3.dll` and short-lived
connections, with full synchronous commits and a bounded busy timeout. There is
no application-maintained lock file or recovery log. SQLite may create temporary
journals while writing; these are ignored by Git and must not be deleted while a
transaction is active. The database is marked binary in `.gitattributes`. Git
cannot merge two changed databases automatically; coordinate branch changes and
never replace an active store to resolve a Git conflict.
