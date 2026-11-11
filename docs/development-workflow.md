# Development workflow

Use two phases: feedback and finalization. The user decides when the feature is ready for finalization.

## Build profile

Use one profile for this user's machine: Release, Windows x64 (`win-x64`), and .NET 10. Use the same profile for the application, test programs, architecture checks, and local releases. Do not build Debug, other CPU targets, other operating systems, or a configuration matrix. Keep the standalone launcher as a separate NativeAOT executable for this same profile.

When a desktop agent runs tests or release builds, start the PowerShell helper with `WindowStyle Hidden` and redirect output to a log. Do not hide or change the user's existing terminal. Background application hosts must also suppress console creation; hiding their application windows does not suppress a console allocated by Windows.

## Feedback phase

The goal is to get user feedback on behavior quickly. Apply this release loop to application behavior changes. Documentation and check-only changes need their relevant checks without a new application release.

After a requested application behavior change:

1. Build the changed code and run a small smoke check of startup and the changed behavior. Use the real application controls or adapters with isolated fixtures and no visible windows.
2. Keep verification under 60 seconds on a warm development setup. Do not run the full behavior suite, architecture checks, analyzer self-checks, code health reports, performance benchmarks, or repeated startup measurements in this phase. Compiler errors and existing build analyzers still apply.
3. Publish a ready local feedback build automatically. The user has authorized local feedback releases; do not ask for permission for each iteration. Use a new immutable release directory, include the dependencies, and preserve the previous working release if the build, smoke check, or installation fails.
4. Let an already running application update through its normal idle handoff. Do not open a new visible application window unless the user asks.
5. Return promptly with the ready executable, the behavior that changed, and what the smoke check covered. Label the build as smoke-checked; do not claim that full verification passed. Wait for feedback or a new task.

Do not delay this handoff for broad refactoring, complete test coverage, or an independent usability review. Keep the existing architecture boundaries while changing code. Fix a broken build, a failed smoke check, or a known failure in the changed path before publishing.

The 60-second limit covers verification, not feature implementation. Reuse unchanged build outputs and dependencies. If a cold restore or another measured blocker exceeds the limit, report the elapsed time and the cause. Do not silently expand a smoke check into the full verification path.

Do not start finalization in the background while the user is reviewing a feedback build. More feedback starts another short iteration.

## Finalization phase

Enter this phase when the user accepts the behavior and asks to commit or finalize the feature. Feedback builds do not authorize a commit.

1. Finish the implementation, review the code and architecture, add the missing regression tests, and update the relevant documentation. Complete the independent usability review for changed user workflows.
2. Run the complete verification path, including every behavior scenario, architecture rules, analyzer self-checks, formatting, metrics, and the required native integration checks. Fix failures and rerun the affected checks. Finish with current passing evidence for the code to be committed.
3. Use a fixed source snapshot and isolated build outputs. Concurrent work must not change the files being verified or mix its outputs into the release. If relevant inputs change after verification, invalidate the affected results.
4. If publishing a final release, verify the published application and release setup before switching the latest-release marker. Keep feedback-build evidence separate from full-verification evidence.
5. Commit only after the required checks pass. Follow the repository commit convention. Report the result and any remaining coverage limits.

This phase may take longer. Work independently through routine failures and quality fixes. Ask the user only for a material product decision or a blocker that requires user input. New behavior feedback returns the work to the feedback phase.

## Current commands

`scripts/Test-Xcmd.ps1` runs full verification. `scripts/Test-Architecture.ps1` runs the architecture, analyzer, formatting, and metrics checks. `scripts/Release-Xcmd.ps1` runs the full release gate. Use these commands during finalization, not as a fast feedback check. A filter on `Test-Xcmd.ps1` still runs its architecture stage.

A dedicated fast feedback release command is not implemented yet. Follow the feedback requirements when adding it; do not describe the existing full release script as a fast path or invent a command that does not exist. Until that command exists, assemble and publish feedback builds through explicit build, smoke-check, and release steps with clearly labeled evidence.
