# Deliver changes

Use a short feedback loop until the user accepts the behavior. Finalize when the
user asks to commit or finish the feature. A request for a feedback build does not
authorize a commit or background finalization while the user is reviewing it.

## Feedback builds

Deliver a ready local build automatically after a requested behavior change. The
user has authorized this publishing loop; do not ask again for each build.

Build and smoke-check startup and the changed workflow through real application
controls or adapters, using isolated fixtures without visible windows. Keep warm
verification under 60 seconds so feedback stays fast. Report measured delays such
as a cold restore rather than silently expanding the check scope. Compiler and
build-analyzer failures still need repair.

Use `./scripts/Publish-Feedback.ps1 -Filter '<targeted-scenario-filters>'` from a
hidden PowerShell helper with redirected logs. It builds, runs selected headless
scenarios, publishes the application and installs the launcher. Do not delay this
handoff for broad refactoring, full checks, benchmarks or an independent review.

Give the user the ready executable, the changed behavior and the smoke-check scope.
Call it smoke-checked, then wait for feedback. An already running application may
use its normal idle update handoff; publishing does not authorize opening a new
visible window. Documentation and check-only changes need no application release.

## Finalization

Finish code review, relevant regression coverage and story maintenance. For a
changed user workflow, obtain the independent review described in [UI work](ui.md).
Run affected scenarios and static checks through the [verification process](verification.md).
Keep evidence tied to a fixed source snapshot with isolated build outputs, so
concurrent edits cannot silently invalidate a result.

Commit after the selected checks pass, recording their scope and limits. The user
accepts that targeted checks can miss unrelated regressions. A commit or final
release does not require a local full regression run. New behavior feedback starts
another short feedback iteration.

## Ready builds and release integrity

Deliver the permanent `Xcmd.exe` beside `releases`, not an executable from a
development `bin` directory. The user should not need to restore packages, build
the project or run a helper to review the result.

Use the existing publishing helpers and Release / Windows x64 profile. Each build
gets a new immutable `releases/<release-id>` directory with its dependencies,
verification evidence and restart manifest. Verify the published application and
launcher setup before changing `releases/latest.txt`; preserve the previous
working release on failure. Keep publishing and marker updates in the shared
`scripts/Publish-Application.ps1` helper so the release paths stay consistent.

Preserve an in-use launcher and defer its replacement if necessary. Keep the
permanent shortcut stable and do not change the user's taskbar pins. Application
handoff must preserve work and wait for the replacement to be ready before closing
the old process. Follow the existing release and process integration checks when
changing this path.

`./scripts/Release-Xcmd.ps1` includes the full gate and is only for an explicit
request for full regression checks with the release. Otherwise use the selected
check publishing path. Keep smoke, targeted and full evidence distinct. Use
"fully verified" only after a requested full gate has passed.

Keep package auditing enabled. If the vulnerability service is unavailable, the
feedback-only `-SkipPackageAudit` exception requires the user's explicit request
to install despite that failure. Locked restore and smoke checks still apply, and
the release evidence must record the skipped audit. The exception does not apply
to final releases.
