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

Run these commands from the repository root. Start background build/test helpers
hidden with redirected logs, as required by [AGENTS.md](../../AGENTS.md).

| Purpose | Command |
| --- | --- |
| Verify a feature before committing | `./scripts/Test-Xcmd.ps1 -Filter '<affected-scenario-filters>'` |
| Run architecture, analyzer self-checks, formatting and metrics | `./scripts/Test-Architecture.ps1` |
| List scenarios in a built probe | `dotnet tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll --list` |
| Run selected built scenarios without the static stage | `dotnet tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll --filter '<filters>'` |
| Format C# whitespace | `dotnet format whitespace Xcmd.slnx --no-restore` |
| Inspect code health without a full regression run | `./scripts/Report-CodeHealth.ps1 -SkipVerification` |

Filters are comma-separated, case-insensitive parts of scenario IDs. Use the
current scenario inventory and source to select coverage. The archived verification
tables are historical evidence, not a maintained coverage index.

The unfiltered `Test-Xcmd.ps1`, `Release-Xcmd.ps1` and `Report-CodeHealth.ps1`
without `-SkipVerification` run the full gate. Use them locally only for an
explicit full-regression request and follow [regression work](regression.md).
CI runs the full gate on pushes and pull requests to `main` and on manual runs.

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
