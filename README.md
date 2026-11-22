# Xcmd

Xcmd is a Windows file manager with two panels and keyboard controls, built with
C# and Avalonia.

Read the ordered [epics and user stories](docs/stories/README.md) to learn the
product and its developer capabilities. This catalogue is the LivingDoc.
The [agent instructions](AGENTS.md) link to the development processes.

Browse the catalogue and current source links with `./scripts/Explore-Stories.ps1`.
Open the local URL printed by the command. Expand an epic, select a story and
inspect its implementation and test files. Use **Reload map** after source edits.
Python 3.11 or later and Git are required; no extra packages are needed.

## Run

Run the permanent `Xcmd.exe` next to the `releases` folder. It starts the selected
local release. Keep that release and its dependencies together. Windows x64 and
the .NET runtime selected by the application are required.

```powershell
& ./Xcmd.exe
& ./Xcmd.exe C:/work C:/work/projects
```

Use `Xcmd.lnk` for a stable shortcut across releases. See the
[delivery process](docs/processes/delivery.md) for building and publishing.

### GitHub Actions in a file panel

For a GitHub.com remote, the repository button shows the workflow results for
the most recent commit with runs on the tracked branch. A detached checkout
shows runs for that commit. Hover over the CI label to see the commit and each
workflow result. Sign in with the GitHub CLI to read private repositories.

Visible panels in the active window share one check per repository and ref.
Xcmd checks every 30 seconds while a run is pending, every five minutes after
completion or a connection error, and every 15 minutes when there are no runs.

## Develop

See [verification](docs/processes/verification.md) for the environment and commands,
[UI work](docs/processes/ui.md) for interface changes, and
[feedback batches](docs/processes/feedback.md) for the local issue tracker.

The previous guides, design documents, evidence and source map are in
[the archive](docs/archive/README.md). They describe past work and may be stale.
