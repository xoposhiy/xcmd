# Feedback finalization checks, 2026-11-13

The full `scripts/Test-Xcmd.ps1` gate passed on an isolated Release / Windows x64
source snapshot. All **337 of 337 scenarios passed** without a filter. No check,
metric limit or required native integration test was skipped.

## Current-source results

| Check | Result |
| --- | --- |
| NativeAOT launcher | Passed with locked dependencies and normal package auditing |
| Application, tests and compiler analyzers | Passed, zero warnings and errors |
| Architecture and dependency rules | Passed, including 14 deliberate analyzer violations |
| Feedback source-identity analyzer | Passed, nine compiler fixtures |
| Complete behavior suite | 337 passed, zero failed |
| Production metrics and duplicate detection | Passed with unchanged limits |
| Solution C# whitespace | Passed through the normal MSBuild workspace |
| Input stability | All source inputs matched the verified snapshot after the run |
| Independent usability review | Completed; findings resolved in the [review record](feedback-usability-review-2026-11-13.md) |

The native checks passed for junction traversal and safe deletion, temporary user
environment variables, command launch, Recycle Bin retention, shell discovery,
Windows icons, shortcuts and the permanent launcher. Earlier access, task-host,
NuGet service and formatter restrictions did not recur in this environment.

## Regression coverage

Finalization covers rename-editor input, native long-path copy and stream
enumeration, feedback cell identity and file context, navigation shortcut
discovery, command focus, deletion shortcuts and Git column layout.

Folder measurements survive unrelated automatic refreshes. Windows can refresh a
cached directory timestamp after the contents have been measured. A timestamp
from before measurement completion does not discard that measurement; a later
change still invalidates it. Refresh compares full paths, so search results with
the same file name remain independent. Both native junction and live-refresh
scenarios passed, as did search-result deletion and archive location refresh.

Folder icons retain their ordinary Windows appearance. Git indications stay in
the dedicated Git column. Offscreen renders and native-icon assertions cover
ordinary, dirty, clean, ignored and repository-root folders.

## Evidence and limits

Generated logs, source fingerprints and scenario results are under
`artifacts/commit-check-20261113-164942`. The passing run uses
`verify-accepted.log`, `accepted-exit-code.txt`, `accepted-probe/results.json` and
`source-accepted-before.json`. The exit code is zero. Documentation was updated
after verification; application, build and test inputs remained unchanged.

Pointer and render checks use the actual Avalonia controls under Headless and
Skia. Native desktop input, foreground activation and interactive third-party
shell-menu appearance retain the limits in the development requirements.

The final issue CLI check found no open issues. The existing installed feedback
build keeps its separate smoke-check evidence.
