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

### Agent sessions prototype

Press Alt+F1 or Alt+F2 to choose a drive or open agent sessions in the left or
right panel. The menu opens on that panel's current drive or session list.
Each opening reads fresh sessions. Recent sessions appear first. You can open
a session while older conversations are still loading.
Press Enter on a session to resume it in its CLI. To return to files, choose
a drive in the same menu.

Project shows the nearest Git work tree or the launch folder if it is outside Git.
Hover over the project to see the full launch path. Press Ctrl+Left or Ctrl+Right
to open the project in the other panel. A topic shows activity when its agent
reports a state.

Install the Codex CLI to browse Codex sessions. For Claude Code, install its CLI
and the Claude Agent SDK. For the fastest first page, install the SDK for Node.js.

```powershell
$sdk = Join-Path $env:LOCALAPPDATA 'Xcmd/claude-session-js'
npm install --prefix $sdk --omit=optional --ignore-scripts --save-exact '@anthropic-ai/claude-agent-sdk@0.3.330'
```

Xcmd starts each reader when you first open its list. Both panels share the
readers. Each reader stops after ten minutes without a request.

When you return to the window, each open session panel checks its recent
sessions. The active session panel also checks every five seconds for the first
five minutes after gaining focus. Older rows stay in place while the latest
sessions update. Relative times update every minute while the list is open.

You can also use the Python SDK for Claude Code.

```powershell
$sdk = Join-Path $env:LOCALAPPDATA 'Xcmd/claude-session-sdk'
python -m venv $sdk
& "$sdk/Scripts/python.exe" -m pip install claude-agent-sdk==0.2.187
```

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
