# Git column and deletion usability review

An independent reviewer examined the changed workflows using the criteria in
[the architecture](../architecture.md#tests-and-usability-review): action count,
result feedback, latency, explanations for rare actions and discoverability.
The review covered source, targeted headless evidence and 100/150% renders.

| Finding | Resolution |
| --- | --- |
| The current repository header waited for all child repository queries. | Apply the current status before awaiting child queries. A delayed-reader scenario checks the ready header and rejection of obsolete child results after navigation. |
| The user guide described a hidden Git column outside repositories and explained the checkmark only as a staged change. | Document the available column, direct child repository summaries, the clean checkmark on the right, linked worktrees and background reads. Mark UC27 Partial and keep diff routing deferred. |
| Active large-tree cancellation needed direct coverage. | Pause after ten real deletions, click Cancel current, and check that the remaining 1,190 files and a neighbouring file survive. |
| Going up after a History jump restored an unrelated parent cursor. | Use one parent command for Enter on `..`, Backspace and Ctrl+PageUp. Select the folder or archive just left, including nested archive roots. |
| Batched validation rejected a readable archive source as a write target. | Inspect virtual source ancestors through the archive adapter, then use batched validation for physical ancestors. Keep separate destination write guards. |

The final independent source review found no remaining blocking usability
problems. Targeted checks cover the reported History issue and archive source
validation alongside the Git and deletion workflows.

## Finalization checks

The fixed source snapshot passed 41 selected scenarios with zero failures and
zero native application windows. The working source matched the tested snapshot.

| Selection | Reason |
| --- | --- |
| All Git scenarios and the Git-ignore search case | Current and child statuses, linked worktrees, stale requests, narrow layout, existing repository actions and native launch behavior. |
| Large-tree deletion and cancellation, permanent deletion, native recycling, junction safety and partial failure | Real deletion results, responsiveness, cancellation and link boundaries. |
| Copy/move fallback, conflicts, identical files, counting, progress and queue cancellation | Shared metadata and parent validation changed for all file operations. |
| History, parent navigation and parent-name rules | The parent cursor must identify the exited folder after a History jump. |
| Archive browse/extract/return, nested archives and read-only errors | Virtual source validation must allow extraction while preserving write guards. |
| Search result panels | Shared parent and file-operation paths must preserve search navigation and operation targets. |

Architecture checks passed all 11 rules, 14 deliberate analyzer violations and
9 feedback-identity compiler fixtures. C# formatting, code metrics and duplicate
detection passed. YAML checks confirmed that automatic CI runs static checks and
only manual dispatch starts the full regression path. Updated documentation
links passed validation.

The final published feedback build, `20261113-181735-818-d27a2f5f`, passed four
smoke scenarios for History parent navigation, archive extraction, delayed child
Git reads and child repository cells. It retains the `feedback-smoke` label;
the 41-case result is targeted verification, not a full-suite result.

Local diagnostics are in `C:/work/xcmd-diagnostics/finalization-results/results.json`
and `C:/work/xcmd-diagnostics/finalization.log`. The selected command uses
`scripts/Test-Xcmd.ps1 -Filter` with the groups listed above. No complete
regression run was requested or started.

## Measurements and limits

A local headless sample deleted 1,200 files with operation details visible in
20.2 seconds before the optimization and about 2.3 seconds afterward. These
samples are not a guarantee for other disks or workloads. Parent directory
validation still rejects links and junctions; batching changes scheduling rather
than removing those checks. Progress updates use the existing 100 ms timer,
while state transitions remain immediate.

The requested cleanup of the old artifacts directory removed 55,325 files,
about 38 GB, in 58.9 seconds. A separate cleanup driver used the production
operation runner and adapter without a visible window. It cleared read-only
attributes on 25 files within that explicitly authorized temporary directory;
the application does not automatically clear read-only attributes.

Child Git results are applied as one batch after child queries finish. A slow
child can delay other child cells, but the current repository header, navigation
and file loading remain available. Headless checks do not establish native
desktop focus or monitor transitions. Full regression runs require an explicit
user request and are not part of this finalization.
