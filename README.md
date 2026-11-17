# Xcmd

Xcmd is a Windows file manager with two panels and keyboard controls, built with
C# and Avalonia.

Read the ordered [epics and user stories](docs/stories/README.md) to learn the
product and its developer capabilities. This catalogue is the LivingDoc.
The [agent instructions](AGENTS.md) link to the development processes.

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

## Develop

See [verification](docs/processes/verification.md) for the environment and commands,
[UI work](docs/processes/ui.md) for interface changes, and
[feedback batches](docs/processes/feedback.md) for the local issue tracker.

The previous guides, design documents, evidence and source map are in
[the archive](docs/archive/README.md). They describe past work and may be stale.
