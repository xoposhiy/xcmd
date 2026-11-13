# Agent orientation

- Use only `main`. Isolated checks may use a separate copy or a detached worktree;
  do not create feature or repair branches. Put verified background repairs into
  `main` without overwriting active work. See the development workflow.

- Keep all application-authored UI text in English. The language used in chat
  does not change the language of the interface.

- For in-app feedback, use `scripts/Issues.ps1 list`, then claim the entire batch
  with `scripts/Issues.ps1 start ID...` before implementation. Stop if another
  batch is in progress; coordinate instead of bypassing the guard. Close finished
  work through the CLI or release unfinished claims. Do not edit `issues/issues.db`
  directly. See [the issue workflow](issues/README.md).
  Before finishing a feedback batch, list issues again and handle newly arrived
  feedback. Claim each additional batch through the same guard. To expand an
  unfinished batch, release only this session's own claims and claim the enlarged
  batch; stop if the guard detects other work. Do not leave new feedback unnoticed.
- Every application-authored control needs a unique literal `.WithFeedbackId("...")`
  at its creation site. The Desktop build enforces this. Keep IDs stable when
  changing layout, and search a reported target ID to locate its source definition.

- Read [the technology decision](docs/technology-decision.md) and [development requirements](docs/development-requirements.md) before changing application behavior.
- Use [the code architecture](docs/architecture.md) for responsibilities, dependency direction, and state ownership. Keep the three production projects; there is no planned four-project migration.
- For UI work, read [the design system](docs/design-system.md) and its [visual examples](docs/design-system.html). Apply confirmed feedback rules across analogous controls; proposed tokens and backlog items remain proposals until reviewed. Do not turn visual changes into unrequested behavior changes.
- Use [automated checks](docs/automated-checks.md) for enforced rules, metric limits, exceptions, and commands.
- Follow the [development workflow](docs/development-workflow.md): publish smoke-checked feedback builds without asking. Before committing, run the affected feature scenarios and the static checks. Run the complete behavior suite separately after commits; do not make it a pre-commit requirement.
- Keep scenario cases in `tests/Xcmd.Probe/Scenarios` and drivers, native setup, fixtures, process control, and timing in `tests/Xcmd.Probe/Infrastructure`.
- Follow the ready-build workflow in the development requirements. Keep releases immutable and do not open visible windows without a user request.
- During finalization of a changed user workflow, use an independent usability reviewer and the criteria in the code architecture. This is not required for documentation, formatting, or checks that preserve user behavior.
- Do not edit generated files under `bin`, `obj`, `artifacts`, or `releases` as source changes. Analyzer self-checks intentionally create failing source fixtures under `artifacts/architecture`.
