# Xcmd

Xcmd is a Windows file manager with two panels and keyboard controls. It uses C# on .NET 10 and Avalonia 12.1.6.

The current version supports directory navigation, history and favorites, file selection and sorting, inline rename, folder creation, folder sizes, automatic directory refresh, and opening files through Windows associations. A compact command line runs programs through Windows ShellExecute from the active panel directory and handles `cd` locally.

The file-operations prototype adds recursive copy, move, Recycle Bin and permanent deletion, a background queue, cancellation, and Skip/Rename/Replace conflict choices. Viewers support text, binary data and images. Archive feedback builds add read-only panel browsing, archive reports, member viewing and extraction, including nested archives, Office containers and recognized self-extracting EXEs. Feedback builds receive focused smoke checks rather than full final verification. Search and Git inspection remain planned. See [requirements and status](docs/use-cases.md).

## Run

Run the permanent `Xcmd.exe` next to the `releases` folder. It starts the latest verified release and exits. Windows x64 and the .NET 10 runtime are required. Keep the release directories with their published dependencies.

```powershell
& ./Xcmd.exe
& ./Xcmd.exe C:/work C:/work/projects
```

Without directory arguments, Xcmd restores the saved workspace. On the first launch it opens the working directory in both panels. Explicit directory arguments replace the saved panel locations. File operations affect real files.

Pin `Xcmd.lnk` to the taskbar, or pin the running Xcmd window. On Windows 11, the shortcut's context menu may require **Show more options > Pin to taskbar**. Replace any old pin that points directly into a release directory once. The permanent shortcut keeps working after later releases. See the [launch instructions](docs/user-guide.md#run) for details.

Each release updates the permanent shortcut. Shortcut icons use separate files under `releases/icons`, with a new path when the image changes so Windows can refresh its icon cache. Existing icon files stay available for older pins. Publishing does not change taskbar pins.

See the [user guide](docs/user-guide.md) for all controls, command history, settings, session restore, local release updates and interface feedback.

## Develop and verify

Use Windows, PowerShell 7, Python 3.11 or later, and the .NET SDK selected by [global.json](global.json). Dependencies are pinned in package lock files and [requirements-lint.txt](scripts/requirements-lint.txt).

Use only Release / Windows x64 for application builds, tests, and local releases. Follow the [development workflow](docs/development-workflow.md): use smoke checks under 60 seconds and automatic local feedback builds while the behavior is being refined; run the complete quality gate after acceptance and before committing. The commands below run finalization checks. A dedicated fast feedback release script is not implemented yet.

Run the complete verification path:

```powershell
./scripts/Test-Xcmd.ps1
```

The script restores dependencies, builds the projects, checks architecture, analyzer rules, formatting and code metrics, and runs the actual application through Avalonia.Headless with Skia rendering. Tests use isolated fixtures and create no visible application windows. The first run downloads dependencies into `.packages` and lint tools into `artifacts/lint-venv`.

For focused work:

```powershell
./scripts/Test-Architecture.ps1
./scripts/Test-Xcmd.ps1 -Filter command-line
./scripts/Test-Xcmd.ps1 -OutputPath artifacts/probe/my-run -KeepFixtures
dotnet format whitespace Xcmd.slnx --no-restore
```

After building, list scenarios or run a group directly:

```powershell
dotnet tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll --list
dotnet tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll --filter rename
```

Results, input and state traces, failure details and offscreen images are written under `artifacts`. Successful fixtures are removed unless `-KeepFixtures` is set. A failed check returns a nonzero exit code. CI uses the same complete verification path on Windows.

See [automated checks](docs/automated-checks.md) for enforced rules and limits, and [verification coverage](docs/verification.md) for current evidence and native coverage limits.

Generate the [code health report](docs/automated-checks.md#code-health-report) with `./scripts/Report-CodeHealth.ps1` to inspect measured values by project, folder, file, function, and type. CI publishes the same report as a `code-health` artifact.

## Build a ready release

```powershell
./scripts/Release-Xcmd.ps1
```

This is the full finalization command. The script verifies the source and publishes the Release build with its dependencies into a new `releases/<release-id>` directory. It includes `verification.json` and a restart-protocol manifest, prepares the permanent `Xcmd.exe` and `Xcmd.lnk`, then atomically updates `releases/latest.txt` after success. It does not open the application. Existing releases stay unchanged and can remain open during development. An in-use launcher keeps its existing file and defers a launcher replacement without blocking the application release.

A running release can switch to a newer locally published release after the application becomes idle. The workspace is saved and restored through a process handoff. This is local release discovery, not a network update service.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/Xcmd.Core` | Application values, rules, state and contracts |
| `src/Xcmd.Infrastructure` | Filesystem, persistence, Windows integration and process adapters |
| `src/Xcmd.Desktop` | Avalonia controls, input routing and interaction coordination |
| `tests/Xcmd.Probe/Scenarios` | Behavioral scenarios with requirement references |
| `tests/Xcmd.Probe/Infrastructure` | Fixtures, input drivers, native setup and process control |
| `tests/Xcmd.Architecture` | Dependency policies, analyzer fixtures and architecture self-checks |
| `tests/Xcmd.LaunchProbe` | Windowless endpoint for real Windows Shell launch checks |
| `config` | Banned API policies |
| `scripts` | Verification, publishing and icon generation |

Generated builds, releases, caches, local review sessions and IDE state are excluded from Git. Keep the SVG, PNG and ICO application assets in source; regenerate the PNG and ICO with `./scripts/Build-AppIcon.ps1` after changing the SVG. Small frames use the matching `app-16.svg` through `app-48.svg` files at their native pixel size. Keep their rectangle edges on whole pixels. The main `app.svg` defines the detailed large icon.

## Design and requirements

- [Technology decision](docs/technology-decision.md)
- [Code architecture](docs/architecture.md)
- [Development and delivery requirements](docs/development-requirements.md)
- [Feedback and finalization workflow](docs/development-workflow.md)
- [Use cases and implementation status](docs/use-cases.md)
- [Search design proposal](docs/search.md)
