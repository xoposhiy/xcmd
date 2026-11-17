# Feedback usability review, 2026-11-15

An independent reviewer inspected the directory chooser, Git viewer input,
missing-directory fallback and permanent deletion changes. The review used the
criteria in `docs/architecture.md`: short frequent workflows, clear results,
explanations for rare actions and discoverable recovery paths.

## Findings and resolutions

- The chooser renders at 100% and 150% show one blue current row, readable paths,
  reachable actions and no second hover fill. Page keys and Ctrl+D keep common
  navigation short.
- Parent fallback needed visible result feedback. The status line now explains
  that the directory no longer exists and names the parent that opened.
- A directory replaced by a file received a missing marker but did not follow
  the fallback rule. The filesystem adapter now reports that path as a missing
  directory, so navigation opens its nearest existing parent.
- F3 followed immediately by PageDown changed the commit while the report was
  still being prepared. A regression scenario reproduced the failure before the
  fix. Viewer-opening requests now consume panel navigation during preparation;
  Escape still cancels. Opening a snapshot file also keeps focus in the viewer.

The reviewer inspected the fixes and the passing six-scenario smoke result in
`artifacts/probe/navigation-feedback-20261115-162627`. The reviewer found no
remaining usability blockers and requested Git arrow alignment evidence in the
final targeted checks. `git-commit-menu` checks that geometry at both scales.

This review and the selected checks do not establish a passing full regression
suite or native Windows keyboard delivery.
