# Agent orientation

- Read [the technology decision](docs/technology-decision.md) and [development requirements](docs/development-requirements.md) before changing application behavior.
- Use [the code architecture](docs/architecture.md) for responsibilities, dependency direction, and state ownership. Keep the three production projects; there is no planned four-project migration.
- For UI work, read [the design system](docs/design-system.md) and its [visual examples](docs/design-system.html). Apply confirmed feedback rules across analogous controls; proposed tokens and backlog items remain proposals until reviewed. Do not turn visual changes into unrequested behavior changes.
- Use [automated checks](docs/automated-checks.md) for enforced rules, metric limits, exceptions, and commands.
- Follow the [development workflow](docs/development-workflow.md): use a smoke check under 60 seconds and publish local feedback builds without asking; run full verification and fix quality issues after behavior is accepted and before committing. Do not run the full gate for every feedback iteration.
- Keep scenario cases in `tests/Xcmd.Probe/Scenarios` and drivers, native setup, fixtures, process control, and timing in `tests/Xcmd.Probe/Infrastructure`.
- Follow the ready-build workflow in the development requirements. Keep releases immutable and do not open visible windows without a user request.
- During finalization of a changed user workflow, use an independent usability reviewer and the criteria in the code architecture. This is not required for documentation, formatting, or checks that preserve user behavior.
- Do not edit generated files under `bin`, `obj`, `artifacts`, or `releases` as source changes. Analyzer self-checks intentionally create failing source fixtures under `artifacts/architecture`.
