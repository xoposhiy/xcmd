# Automated architecture checks

## Run the checks

Use Windows, PowerShell 7, the SDK selected by `global.json`, Python 3.11 or later, and the Visual Studio C++ build tools with a Windows SDK for the NativeAOT launcher.

Use one build profile: Release / `win-x64` / x64. `Directory.Build.props` supplies the defaults, and `Directory.Build.targets` rejects unsupported profiles before restore or build. Application and test outputs share `bin/Release/net10.0/win-x64`; do not create Debug or runtime-independent variants.

These commands are the full finalization gate described in the [development workflow](development-workflow.md). Run the full gate before committing accepted work. Feedback iterations use small smoke checks instead; `Test-Xcmd.ps1 -Filter` still runs the architecture stage and is not the fast feedback path.

```powershell
./scripts/Test-Xcmd.ps1
```

This command runs architecture tests, analyzer self-checks, formatting, code metrics, and the complete headless behavior suite. Any failed check stops the command with a nonzero exit code. `scripts/Release-Xcmd.ps1` uses the same path for final releases. CI runs the same command on Windows.

The architecture command also builds the self-contained Windows x64 NativeAOT launcher from a small source subset of Infrastructure. The launcher has no project or UI dependencies, uses `packages.launcher.lock.json`, and keeps build outputs under `artifacts/launcher`. Normal project and package checks still cover the same six application/test projects. Launcher scenarios use isolated installation trees and do not change taskbar pins.

For a shorter check while editing:

```powershell
./scripts/Test-Architecture.ps1
```

The metrics script creates a local environment under `artifacts/lint-venv` on its first run. Install the pinned dependencies again if that environment is incomplete:

```powershell
./artifacts/lint-venv/Scripts/python.exe -m pip install -r scripts/requirements-lint.txt
```

Format C# files with:

```powershell
dotnet format whitespace Xcmd.slnx --no-restore
```

## Project boundaries

See [Code health report](#code-health-report) for measured values at project, folder, file, function, and type level.

The [code architecture](architecture.md) defines the three production projects. Project-scoped API bans protect Core and Desktop.

| Project | Allowed production dependencies | Responsibility |
| --- | --- | --- |
| `Xcmd.Core` | None | Rules, state, and application contracts; no UI, platform access, or adapter implementations |
| `Xcmd.Infrastructure` | Core | Filesystem, persistence, Windows integration, and processes; no UI |
| `Xcmd.Desktop` | Core, Infrastructure | Avalonia controls, input routing, presentation, and existing workflow coordination |

The adapters use Core contracts and state values. Desktop can call them and own interaction coordination. These are supported boundaries. Extract substantial UI-independent decisions into Core as workflows grow; this does not require a new project or a rule that all adapter access belongs in startup.

## Enforced checks

| Check | Enforcement |
| --- | --- |
| Compiled dependencies | ArchUnitNET 0.13.5 checks Core isolation, adapter isolation, layer cycles, and production references to tests. |
| Project and package references | Evaluated MSBuild items in Release / win-x64 / x64 must match an explicit allowlist and the single build profile. Unknown projects, unused forbidden packages, direct DLL references, extra framework references, and unpinned package versions fail. Every production project must be covered. |
| Test boundaries | Test infrastructure cannot depend on scenarios. Scenarios use drivers for native input, process control, timing, and render scaling. Real fixture file changes and file assertions remain allowed in scenarios. |
| API boundaries | BannedApiAnalyzers 5.6.0 rejects direct filesystem, process, and native import APIs in Core and Desktop. Core also rejects ambient clock, working-directory, and environment reads. Pure path operations remain allowed. |
| Async code | Production code cannot block on task results, task wait overloads, awaiter results, or thread sleeps. CA2016 requires forwarding an available cancellation token. |
| String comparisons | CA1309 rejects culture-based comparison calls where ordinal comparison is required. File identity and UI display order remain separate policies. |
| Formatting | `.editorconfig` and `dotnet format whitespace --verify-no-changes` check C# whitespace across all projects in `Xcmd.slnx`. Compiler/analyzer diagnostics run during the build. |
| Feedback identity | The SDK-matched Roslyn analyzer in `tools/UiChecks` runs in every Desktop compilation. XCMDUI001 requires literal source IDs for authored controls; XCMDUI002 rejects duplicate definitions; XCMDUI003 rejects empty/nonliteral IDs. Compiler fixture self-checks also run from `Test-Architecture.ps1`. This tool has no production runtime dependency or external packages. |
| Code metrics | CA1502, CA1505, and CA1506 fail the production build. Lizard 1.25.1 checks every production C# file, excluding build output. |
| Check self-tests | Compiled sample violations test the dependency rules and cycle detection. A real compiler fixture tests allowed paths/awaits and forbidden I/O, process, native, clock, blocking, comparison, cancellation, and complexity cases. |

SDK analyzers come from the exact SDK pinned in `global.json`. NuGet lock files and `scripts/requirements-lint.txt` pin the other tools. Compiled architecture checks and behavior scenarios use the same Release / win-x64 assemblies. The architecture build already builds the behavior driver; `Test-Xcmd.ps1` reuses that output without another restore and build.

## Metric limits

Limits were selected after measuring the existing production code. They apply to all production files; there is no legacy baseline or list of ignored methods.

`config/metrics.json` defines Lizard limits. `CodeMetricsConfig.txt` defines the .NET analyzer limits.

| Metric | Limit |
| --- | --- |
| Roslyn cyclomatic complexity, CA1502 | At most 25 per method |
| Maintainability index, CA1505 | At least 10 |
| Class coupling, CA1506 | At most 150 types |
| Lizard cyclomatic complexity | At most 25 |
| Lizard cognitive complexity | At most 25 |
| Lizard nesting depth | At most 10 |
| Lizard function code lines | At most 150 |
| Lizard parameters | At most 8 |
| Duplicate blocks | No blocks of 70 tokens or more reported by Lizard |

Class coupling allows for the control types used by the current large Avalonia views. This limit detects further growth; it does not establish that the views have ideal responsibility boundaries. Lizard nesting depth also counts some boolean conditions. Its partial C# parser can assign an inaccurate enclosing type name; the file, line, and function still locate the result. Roslyn provides the semantic complexity check.

The function-line limit measures code lines, not file length. Duplicate detection uses Lizard's token normalization. The script turns a duplicate report into a failing result because the extension alone does not change Lizard's exit status.

## Exceptions and limits

Production code has no method-level exception to the blocking API ban.

Feature folders and feature namespaces do not exist yet. The current cycle check protects layers. Add feature slices with the first feature-namespace migration; a passing layer check is not evidence that unstructured classes have no feature cycles.

Static checks do not establish correct runtime behavior, task ownership, error reporting, or usability. Reflection and string-based lookup can bypass type dependencies. The existing headless and real-adapter scenarios remain required. Native desktop input and external application appearance retain the limits listed in the [development requirements](development-requirements.md).

## Code health report

Generate a report with fresh architecture, analyzer, formatting, metric, and behavioral checks:

```powershell
./scripts/Report-CodeHealth.ps1
```

The command prints the report path. Each default run creates a separate directory under `artifacts/code-health`. `artifacts/code-health/latest.txt` points to the latest generated report. Open `index.html` in that directory.

The report includes project, folder, file, Lizard function, and Roslyn type/member views. Filter production or test code, select a project or file, search names, and sort columns. Production limits are enforced; test metrics are informational. Download the full JSON, file/function/member CSV tables, or raw Roslyn XML from the report.

The report reads the actual limits from `config/metrics.json` and `CodeMetricsConfig.txt`. Project maintainability is the Microsoft tool's assembly index. File and folder maintainability columns show the lowest member value, not a separate health score. Type coupling is not additive, and partial classes span files.

For measurements without rerunning verification:

```powershell
./scripts/Report-CodeHealth.ps1 -SkipVerification
```

Such a report explicitly marks verification as skipped and labels older behavioral results as historical. A fresh verification run records source fingerprints before and after the checks. A source change during measurement prevents a passing current-source status.

Microsoft.CodeAnalysis.Metrics 5.6.0 produces semantic XML. Its package uses Roslyn Workspace 4.12.0 and omits the host needed to load SDK projects. The report command restores that matching host separately and combines them only under `artifacts/code-health-tools`. The pinned SDK build remains authoritative for lint diagnostics. The reporting tool is not a production dependency.

CI runs the report command and uploads the `code-health` artifact on success or failure. This includes the report, raw metrics, verification log, and behavioral diagnostics. The lightweight `Test-Xcmd.ps1` and release workflow keep their existing verification path.

## Tool references

- [ArchUnitNET](https://github.com/TNG/ArchUnitNET)
- [BannedApiAnalyzers configuration](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md)
- [Built-in complexity rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1502)
- [Lizard](https://github.com/terryyin/lizard)
