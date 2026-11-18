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

Run the normal local short check with
`./scripts/Test-Xcmd.ps1 -Filter '<targeted-scenario-filters>'` from a hidden
PowerShell helper with redirected logs. After the selected headless scenarios
pass, the check publishes the tested build and installs the launcher. This can
happen during development, before a task is complete. Use `-CheckOnly` when
verifying a change without publishing. Do not delay the feedback build for
broad refactoring, full checks, benchmarks or an independent review.

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

Commit after the selected checks pass. The user accepts that targeted checks can
miss unrelated regressions. A commit or final release does not require a local
full regression run. New behavior feedback starts another short feedback iteration.

## Commit messages

Use `type(scope): summary` for the subject. Put the story entries first, then
`Motivation` and `Implementation details` when needed. End with `Verification`.
Put one blank line between blocks.

```text
type(scope): summary

New <story ID> user story:
<Full story statement from the catalogue.>

Also affected user stories
- <Story ID> (<direct effect on the story>)

Motivation: <concrete reason, when known>

Implementation details
<Additional behavior, design choices, or fixes.>

Verification
<State what you checked, how, and any relevant limits.>
```

Give each new story its own label and copy its full statement from the catalogue.
List each directly affected existing story on its own bullet. Put the semantic
ID first and describe the direct effect in parentheses. State what the user or
developer can do differently, what requirement changed, or what expected
behavior a fix restores. Leave out stories that the commit only reuses, reaches
through shared code, or verifies. Use `Affected user stories` when the commit
only affects existing stories.

Include `Motivation` only when you know a concrete reason for the work, such as a
reported bug, a finding from logs or metrics, or a specific external request.
Keep it to one line and include a reference when useful. If the reason is
unknown, omit the entire block.

Use `Implementation details` for concrete behavior, design choices, or fixes
that add to the story entries. Omit the block if there are no additional details.
In `Verification`, report the checks you ran.

### Example

```text
feat(viewer): show Git changes in file previews

New ReadWorkingFileDiff user story:
As a user, I want to read Git changes in the file viewer so that I can inspect
my work and see what is ready to commit.

Also affected user stories
- OpenViewers (new F3 windows fit the screen's work area and restore keyboard focus on key press)
- QuickViewFile (Ctrl+D toggles the diff in the reader and still opens Favorites in the file list)
- FindViewerText (Find matches deleted lines and staged lines later changed on disk)
- HighlightSource (automatic and manual syntax highlighting use colors from each line's Git version)
- SeeRepositoryState (F3 and Quick View show the file's Git status even without a diff)

Implementation details
Compare HEAD with the index and the index with the working file. Show
both edits when they touch the same line. In F3, Ctrl+D and the View
menu switch between text and diff without losing the reading position.
Use Ctrl+Page Up and Ctrl+Page Down to jump between change blocks.

Verification
Passed 43 targeted scenarios and static checks on an isolated Release /
Windows x64 snapshot. Verified Ctrl+D in the running F3 and Quick View
readers.
```

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
feedback-only `Publish-Feedback.ps1 -SkipPackageAudit` exception requires the user's explicit request
to install despite that failure. Locked restore and smoke checks still apply, and
the release evidence must record the skipped audit. The exception does not apply
to final releases.
