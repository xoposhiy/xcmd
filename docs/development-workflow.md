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
2. Run the scenarios that cover the features being committed and the shared behavior those changes can affect. Usually this is about 10–20 scenarios, but coverage determines the selection, not a fixed count. Include the relevant native integration checks. Run architecture rules, analyzer self-checks, formatting and metrics as well. Fix failures and rerun the affected checks. Do not run the complete behavior suite as a pre-commit requirement.
3. Use a fixed source snapshot and isolated build outputs. Concurrent work must not change the files being verified or mix its outputs into the release. If relevant inputs change after verification, invalidate the affected results.
4. If publishing a final release, verify the published application and release setup before switching the latest-release marker. Keep feedback-build evidence separate from full-verification evidence.
5. Commit after the selected scenarios and static checks pass. Record the selected scenarios, why they cover the change, and any remaining coverage limits. Label this as targeted verification; it does not establish a passing full suite. Follow the repository commit convention.

The user accepts the risk that unrelated regressions may reach a commit and be found by the separate full-suite run. Do not delay the feature commit while waiting for that run. Work independently through failures in the selected checks. Ask the user only for a material product decision or a blocker that requires user input. New behavior feedback returns the work to the feedback phase.

## Post-commit verification

Run the complete verification path for committed snapshots in a separate working copy or worktree, with isolated build outputs and fixtures. Keep this work independent of the active development checkout. Record each tested commit, result and diagnostics. A source change requires new evidence for the affected checks.

A monitoring agent may fix failures and commit verified repairs into `main`. Use only this branch; an isolated worktree uses detached HEAD, not a repair branch. Run the relevant scenarios while fixing a failure, then the complete gate before committing a background repair. Keep the tests and quality limits intact. Treat missing services, permissions or tools as environment failures, not as permission to remove coverage. Stop a repair that needs a product decision or cannot make progress; report the failure instead of retrying forever.

Prepare the repair commit in the detached worktree and fast-forward `main` to that verified commit when the development checkout is clean and `main` still points to the tested base. This needs no separate approval. If `main` advances, apply the repair to a fresh snapshot of its new tip and verify again; do not publish an obsolete fix. If active uncommitted work prevents integration, retain the pending repair and retry when the checkout is clean. Never overwrite drafts, force-update `main`, rewrite its history or create another branch. Push only when requested. Fully verified final releases and CI still require the complete gate.

## Current commands

Before a feature commit, use `scripts/Test-Xcmd.ps1 -Filter '<affected-scenario-filters>'`. Filters are comma-separated case-insensitive substrings of scenario IDs. Use the probe's `--list` option to inspect the scenario inventory and select checks from the coverage mapping in `verification.md`. This command runs the static checks and only the selected behavior scenarios. Documentation-only changes need relevant document checks.

`scripts/Test-Xcmd.ps1` without a filter runs full verification for post-commit monitoring, CI or an explicit full-suite request. `scripts/Test-Architecture.ps1` runs the architecture, analyzer, formatting and metrics checks. `scripts/Release-Xcmd.ps1` keeps the full release gate. These commands are not the fast feedback path.

Use `scripts/Publish-Feedback.ps1 -Filter '<targeted-scenario-filters>'` for a feedback release. It builds Release / Windows x64, runs the selected headless scenarios, records the verification time, publishes a new ReadyToRun release, installs the permanent launcher and updates the release marker atomically. The evidence explicitly says `feedback-smoke` and `fullVerification: false`. Choose a small set that covers startup and the changed workflow. Run the helper from a hidden PowerShell process with output redirected to a log, as required above. Package audit and locked dependency restore still run; network access may be needed when cached audit data expires.

When the user explicitly requests installation despite an unavailable NuGet vulnerability service, `-SkipPackageAudit` permits a feedback release using locked dependencies and the same build and smoke checks. The release evidence records `packageAudit: skipped-explicitly`. This option does not apply to final releases and is never enabled by default.
