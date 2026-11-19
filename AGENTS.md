# Working on Xcmd

Deliver the requested outcome and use judgment about implementation and verification.
Ask about material product choices or blockers. Choose routine methods independently.

## Documentation

- Start with the ordered [epics and user stories](docs/stories/README.md). This is
  the LivingDoc for product and developer needs. Keep needs stable; record explicit
  implementation agreements separately within the relevant story.
- Discuss every proposed epic change with the project developer and get explicit
  approval for that specific change before editing the epic.
- Before every commit, keep the changed work, story descriptions and source
  mappings consistent. When the user asks to commit a finished feature, show
  the existing or proposed story in its epic and get the user's approval before
  committing. Follow the [feature story review](docs/processes/stories.md#review-the-story-for-a-feature-commit).
- Keep worked-out specs in [docs/specs-in-progress/](docs/specs-in-progress/) while
  implementation is pending or in progress. Once the spec is implemented, move it
  to [docs/archive/](docs/archive/) and update links to the file.
- [Archived documents](docs/archive/AGENTS.md) are historical ChangeDocs. They are
  not maintained and do not define current behavior or instructions.

## Rules for all work

- Chat in Russian. Write code, UI text, documentation, commits and other artifacts
  in simple English, except when the user requests otherwise or source text must
  remain verbatim.
- Use only `main`. Isolated checks may use a copy or detached worktree. Preserve
  concurrent work; do not create feature or repair branches. Push only when asked.
- Keep domain rules separate from I/O and UI. Keep the three production projects:
  Core owns UI-independent rules, state and contracts; Infrastructure owns external
  operations; Desktop owns controls and interaction. Core has no outer dependency;
  Infrastructure depends on Core; Desktop may use both. This keeps behavior testable
  without the desktop or operating system.
- Give live state and background work clear owners. Keep scenario cases in
  `tests/Xcmd.Probe/Scenarios` and drivers, fixtures, native setup, process control
  and timing in `tests/Xcmd.Probe/Infrastructure`, so cases read as user behavior.
- Use Release / Windows x64 for builds and tests. Do not edit generated files under
  `bin`, `obj`, `artifacts` or `releases` as source changes.
- Keep work off the user's desktop. Do not open visible windows without a request.
  Start background build/test helpers with `WindowStyle Hidden` and log redirection;
  suppress console creation in application hosts. Leave the user's terminal alone.
- Before committing code, run affected scenarios, recent CI failures and static checks
  through the filtered `Test-Xcmd.ps1 -CheckOnly` command. Documentation
  changes need relevant document checks. Local full regression runs require an
  explicit request; GitHub Actions runs the full gate automatically. This keeps
  local feedback fast while retaining broad verification in CI.
- After a requested commit succeeds, clean up artifacts created for the task.
  Clear the whole directory only when you can confirm exclusive use. See
  [local artifact cleanup](docs/processes/delivery.md#clean-up-local-artifacts).
- Record the comparison baseline before each refactor. Generate and share the
  [refactoring comparison](docs/processes/verification.md#refactoring-comparisons)
  with the user after verification.
- Follow the [commit message format](docs/processes/delivery.md#commit-messages).

## Processes to read when needed

| Work | Process |
| --- | --- |
| Add or change needs, decisions or source mappings | [Maintain stories](docs/processes/stories.md) |
| Build, verify or inspect code health | [Verification](docs/processes/verification.md) |
| Deliver a behavior change or finalize accepted work | [Delivery](docs/processes/delivery.md) |
| Change controls, layout or user interaction | [UI work](docs/processes/ui.md) |
| Handle in-app feedback | [Feedback batches](docs/processes/feedback.md) |
| Run requested full regression checks or monitor commits | [Regression work](docs/processes/regression.md) |
