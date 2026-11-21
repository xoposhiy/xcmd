# Handle feedback batches

For reports created in Point Taken, use the installed `point-taken` skill.
Run `point-taken list --json` in this checkout. Close a checked fix with a
result note. Archive it only after user verification and an approved commit.
The commands below apply to records in the embedded Xcmd tracker.

The goal is to resolve the user's in-app feedback without conflicting agent work
or losing new reports. Use `scripts/Issues.ps1`; do not edit `issues/issues.db`
directly or replace the live database to resolve a Git conflict.

Run from the repository root with the .NET runtime and a published local build:

```powershell
./scripts/Issues.ps1 list
./scripts/Issues.ps1 start ID1 ID2
./scripts/Issues.ps1 close ID1 ID2
./scripts/Issues.ps1 release ID1
./scripts/Issues.ps1 reopen ID1
./scripts/Issues.ps1 list --all
```

`list` returns new and in-progress issues as UTF-8 JSON lines; `--all` includes
closed issues. Unambiguous ID prefixes are accepted. The original text, target
ID/name and application release provide the context for implementation.

Claim the entire batch with one `start` call before implementation. The transactional
guard rejects a claim if any work is already in progress. Stop and coordinate
instead of bypassing it. This protects the shared tracker across agent sessions.

Close finished work through the CLI; release unfinished claims. Before finishing,
list issues again and handle new feedback through the same guard. To expand an
unfinished batch, release only this session's claims and claim the enlarged batch.
Stop if another batch now owns the work. Do not leave newly arrived feedback unseen.

Search the reported target ID in source to locate its `WithFeedbackId` definition.
Repeated controls can share one source definition; use the target name to identify
the clicked instance. Apply confirmed rules to analogous controls as well as the
reported one, while keeping the requested scope clear.

Use [delivery](delivery.md) for feedback builds and finalization. The tracker is
independent of workspace sessions. Use `./scripts/Issues.ps1 import-legacy PATH`
only for an explicit one-time import from an old session file. Do not delete SQLite
journals while a transaction is active.
