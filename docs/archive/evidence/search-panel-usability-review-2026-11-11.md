# Search-result panels and responsive columns: usability review

An independent reviewer inspected the uncommitted implementation, use cases,
scenario cases and existing headless renders on 2026-11-11. The criteria were
short frequent workflows with clear results, explanations for rare actions,
and discoverability inside the application. No visible windows were opened.

## Findings and resolutions

| Finding | Resolution |
| --- | --- |
| Renaming a result across sort positions could move the cursor to another file and drop marks. | Map the cursor and selected relative paths to the renamed identity before refreshing. A multi-result regression renames the first nested file to the last sort position and checks cursor and marks. |
| Commands could execute in a result's containing folder while the prompt displayed the original panel directory. | Keep command execution in the directory displayed by the prompt. A regression checks commands with results from two different subfolders. Opening and editing files still use their actual containing folders. |

The reviewer inspected both fixes and their regression scenarios and found no
remaining issue in these paths. The review also covered Alt+P, location in the
opposite panel, Escape restoration, source operations, primary action styling
and responsive columns.

## Limits

This was an independent source and existing-render review, not a test run.
It does not establish native window behavior, multi-monitor behavior or new
performance measurements. Verification results and environment failures are
reported separately; this review does not imply that the full commit gate passed.
