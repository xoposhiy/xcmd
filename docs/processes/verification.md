# Verify changes

The goal is evidence that the changed behavior works, including affected shared
behavior and real integration boundaries. Choose checks by that risk, not by a
fixed number of scenarios. Use [delivery](delivery.md) to distinguish a quick
feedback build from finalization.

## Environment and entry points

Use Windows, PowerShell 7, Python 3.11 or later and the SDK pinned in
[global.json](../../global.json). The NativeAOT launcher also needs Visual Studio
C++ build tools and a Windows SDK. Keep locked dependencies and the single
Release / Windows x64 profile. Build defaults live in
[Directory.Build.props](../../Directory.Build.props) and
[Directory.Build.targets](../../Directory.Build.targets).

The PowerShell build scripts can use the pinned SDK from
`%LOCALAPPDATA%\Microsoft\dotnet`. They add that directory to the build session's
path when it contains the pinned version.

Run these commands from the repository root. Start background build/test helpers
hidden with redirected logs, as required by [AGENTS.md](../../AGENTS.md).

| Purpose | Command |
| --- | --- |
| Smoke-check and publish a local feedback build | `./scripts/Test-Xcmd.ps1 -Filter '<targeted-scenario-filters>'` |
| Verify a feature before committing without publishing | `./scripts/Test-Xcmd.ps1 -Filter '<affected-scenario-filters>' -CheckOnly` |
| Run architecture, analyzer self-checks, formatting and metrics | `./scripts/Test-Architecture.ps1` |
| List scenarios in a built probe | `dotnet tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll --list` |
| Run selected built scenarios without the static stage | `dotnet tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll --filter '<filters>'` |
| Format C# whitespace | `dotnet format whitespace Xcmd.slnx --no-restore` |
| Inspect code health without a full regression run | `./scripts/Report-CodeHealth.ps1 -SkipVerification` |

Filters are comma-separated, case-insensitive parts of scenario IDs. Use the
current scenario inventory and source to select coverage. The archived verification
tables are historical evidence, not a maintained coverage index.

A local filtered `-CheckOnly` run adds scenarios that failed in the last eight
completed `Verify` runs on `main`. Choose the affected filters for the change.
Failures from earlier attempts count even when a retry passes. The probe
runs each selected scenario once and reports historical IDs missing
from the current checkout. Feedback builds still use only their requested filters.

Run `python scripts/code-health/recent_failures.py` to list each scenario and
the number of runs in which it failed. The first lookup uses the GitHub CLI
with read access to Actions. Completed run results are cached in the shared Git
directory. Offline checks use the last saved run list. Read the history message
for the number of runs with available results. A missing report leaves a gap
in the history and the affected scenarios still run.

The unfiltered `Test-Xcmd.ps1`, `Release-Xcmd.ps1` and `Report-CodeHealth.ps1`
without `-SkipVerification` run the full gate. Use them locally only for an
explicit full-regression request and follow [regression work](regression.md).
CI runs the full gate on pushes and pull requests to `main` and on manual runs.

## Startup timing

- Aim for the main window to appear in about a second, but stop startup tuning when
  reaching that target would require aggressive optimizations. The later qualification
  means this is not a hard subsecond release gate.
  Sources: [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 7](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-7); [2026-11-10T14-53-22-01a1c5bc-1a31-7782-8047-528bcf85184b.md → Turn 1](../codex-sessions/2026-11-10T14-53-22-01a1c5bc-1a31-7782-8047-528bcf85184b.md#turn-1).

## Behavioral evidence

Every application feature must be testable without visible windows or human setup.
Exercise the actual controls, handlers and adapters through headless entry points.
Keep fixtures independent of the user's files, settings, repositories and desktop.
Make background work observable and bound waits so failures have useful diagnostics.

Use fast domain checks for rules, headless UI checks for interaction, and real
isolated files, archives, repositories or native adapters where that boundary can
change the result. Keep test infrastructure apart from readable scenario cases.
A recording adapter does not prove that the real external integration works.

Third-party windows and shell-extension appearance may be outside headless coverage.
State that limit; do not replace required integration evidence with a passing mock
or silently turn automated verification into a manual task. Missing permissions or
services are environment failures, not reasons to weaken checks.

For finalization, use a fixed source snapshot and isolated outputs. If relevant
inputs change after verification, rerun affected checks. Report what was selected,
why it covers the change and what remains outside that evidence. Keep failure
traces, fixtures and renders useful; keep successful output concise.

## Static checks and reports

The scripts enforce dependencies, API boundaries, feedback IDs, async rules,
formatting and code metrics. Read the actual policies in
[config](../../config), [CodeMetricsConfig.txt](../../CodeMetricsConfig.txt),
[.editorconfig](../../.editorconfig), [architecture checks](../../tests/Xcmd.Architecture)
and [UI analyzers](../../tools/UiChecks). Do not duplicate their numeric limits in
prose or relax them to make a change pass.

The code-health command prints the generated report path under
`artifacts/code-health`. Inspect measurements in context: partial classes span
files, coupling is not additive, and static metrics do not establish usability or
runtime correctness. A report made with `-SkipVerification` is not fresh behavioral
evidence. CI retains reports and failure diagnostics in its artifacts.

### Refactoring comparisons

Record the baseline commit before editing. For a refactor that starts from a
clean worktree, use the starting `HEAD`. Use an isolated copy when unrelated
changes would affect the comparison.

Run the filtered check with the baseline to generate an HTML comparison after
the checks pass.

```powershell
./scripts/Test-Xcmd.ps1 -Filter '<affected-scenario-filters>' -CheckOnly -RefactoringBaseline '<baseline-commit>'
```

If the checks already passed, generate the comparison directly. The target is
the current worktree, including new source files. Pass `-Target <commit>` to
compare two commits.

```powershell
./scripts/Compare-Refactoring.ps1 -Baseline '<baseline-commit>' -Title '<refactor name>'
```

Share the printed `index.html` link when handing off the refactor. Keep the
baseline and target JSON files beside the report. Comparisons use isolated
source snapshots and report branching, weighted complexity, story links and
scattering. Every C# file contributes separately, including partial class
files. Review the changed files and stories alongside the project totals.
The comparison command measures source and does not run behavioral checks.

Documentation-only changes need checks of their links, structure and consistency.
They do not require application scenarios or a new release. Changes to a check or
report tool need its relevant checks, not an unrelated application regression run.

For the story explorer, run `./scripts/Explore-Stories.ps1 -Check` and the parser
and local HTTP scenarios:

```powershell
$env:PYTHONPATH = (Resolve-Path tools/story-explorer).Path
python -m unittest discover -s tools/story-explorer/tests -v
```

The browser scenario in `tools/story-explorer/tests/browser.js` runs through
`playwright-cli run-code` on an open explorer page. It checks navigation, source
links, search, reload, failure messages, keyboard access and narrow layouts. Run
the browser headless and keep its screenshots under `artifacts/story-explorer`.
