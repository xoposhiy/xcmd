# Agent orientation

- Read [the technology decision](docs/technology-decision.md) and [development requirements](docs/development-requirements.md) before changing application behavior.
- Use [the code architecture](docs/architecture.md) for responsibilities, dependency direction, and state ownership. Keep the three production projects; there is no planned four-project migration.
- Use [automated checks](docs/automated-checks.md) for enforced rules, metric limits, exceptions, and commands.
- After code changes, run the architecture check, including the build and Lizard checks. Fix every reported violation. Run the complete verification path before publishing.
- Keep scenario cases in `tests/Xcmd.Probe/Scenarios` and drivers, native setup, fixtures, process control, and timing in `tests/Xcmd.Probe/Infrastructure`.
- Follow the ready-build workflow in the development requirements. Keep releases immutable and do not open visible windows without a user request.
- For a changed user workflow, use an independent usability reviewer and the criteria in the code architecture. This is not required for documentation, formatting, or checks that preserve user behavior.
- Do not edit generated files under `bin`, `obj`, `artifacts`, or `releases` as source changes. Analyzer self-checks intentionally create failing source fixtures under `artifacts/architecture`.
