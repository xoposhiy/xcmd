---
id: ReliableDelivery
title: "Deliver and maintain a reliable application"
order: 11
role: developer
---

# Deliver and maintain a reliable application

As a developer, I want to package, start, diagnose, and maintain Xcmd consistently so
that new workflows can reach the running application safely.

[All epics](README.md)

## PackageInputs

As a developer, I want to build the application from declared, versioned inputs so that
I can reproduce builds and manage dependencies explicitly.

### Implementation decisions

- Use C# and Avalonia for the Windows application. The user approved the technology
  after the panel prototype and its checks without visible windows.
  Sources: [2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md → Turn 3](../codex-sessions/2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md#turn-3); [2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md → Turn 5](../codex-sessions/2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md#turn-5).

- Build one configuration for the user's Windows machine rather than spending local
  build time on multiple configurations or platforms.
  Source: [2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md → Turn 3](../codex-sessions/2026-11-10T16-00-01-01a1c5f9-01c7-7fd2-87f3-056a703409ae.md#turn-3).

## ApplicationLifecycle

As a developer, I want to compose the desktop host and dispose its background work
predictably so that I can diagnose failures and add services with clear lifetimes.

### Implementation decisions

- Aim for the main window to appear in about a second, but stop startup tuning when
  reaching that target would require aggressive optimizations. The later qualification
  means this is not a hard subsecond release gate.
  Sources: [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 7](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-7); [2026-11-10T14-53-22-01a1c5bc-1a31-7782-8047-528bcf85184b.md → Turn 1](../codex-sessions/2026-11-10T14-53-22-01a1c5bc-1a31-7782-8047-528bcf85184b.md#turn-1).

## WorkspaceStorage

As a developer, I want to persist workspace state independently of the UI so that I can
change storage formats without coupling them to views.

## FeedbackIdentity

As a developer, I want to resolve an authored control to a stable source identifier so
that I can locate reported interface targets independently of visual position.

### Implementation decisions

- Do not create meaningful interface elements that the feedback picker cannot select.
  Apply the rule to labels and nested surfaces as well as interactive controls.
  Source: [2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md → Turn 7](../codex-sessions/2026-11-10T17-42-56-01a1c657-5941-7960-adcb-f3b2b3198e58.md#turn-7).

## FeedbackQueue

As a developer, I want to list, claim, close, release, and reopen feedback items
transactionally so that I can implement requested refinements without conflicting agent
batches.

## HeadlessVerification

As a developer, I want to drive application workflows and inspect failures without
visible windows so that I can verify changes without human assistance.

### Implementation decisions

- Make application features independently testable by the agent without visible windows
  or human assistance. The panel prototype must exercise sorting, hotkeys, inline
  renaming and subtle control behavior under that constraint.
  Sources: [2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md → Turn 4](../codex-sessions/2026-11-09T15-27-39-01a1c0b5-21e4-7f52-b3b2-82ac18ad67bc.md#turn-4); [2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md → Turn 3](../codex-sessions/2026-11-09T17-26-43-01a1c122-2779-74c3-a276-4a53a78726fd.md#turn-3).

- Builds, release preparation and tests must not flash console windows on the user's
  desktop.
  Source: [2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md → Turn 7](../codex-sessions/2026-11-10T23-38-04-01a1c79c-7a8f-7272-944d-0fcc34aa854b.md#turn-7).

- Keep the shared headless runner, input and diagnostics separate from test code
  for specific windows and features. Map the shared code to this story and map
  feature drivers and fixtures to the stories whose behavior they exercise.
  Codex session ID `01a1ee1c-f487-78a2-bba9-c3937474ba40`.

## IntegrationVerification

As a developer, I want to exercise real integration boundaries with isolated fixtures so
that I can detect platform failures without changing the user's files or desktop.

## QualityChecks

As a developer, I want automated checks of code boundaries and quality limits so that I
can catch violations before delivering a change.

### Implementation decisions

- Implement useful architecture checks and repair the violations they find. Keep
  refactoring near complexity limits behavior-preserving and within the existing three
  projects.
  Sources: [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 1](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-1); [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 4](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-4).

- Before a commit, run the scenarios affected by the feature and the static checks. Run
  the full local regression suite only on explicit user request. This later decision
  replaces the earlier all-tests-before-every-commit workflow.
  Sources: [2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md → Turn 4](../codex-sessions/2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md#turn-4); [2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md → Turn 7](../codex-sessions/2026-11-13T17-28-34-01a1d5bd-4601-7fb0-a4f5-3574d19f78e3.md#turn-7).

- Use only `main`. Requested background regression work may use a separate copy or
  detached worktree, but verified fixes return to the same main branch rather than a
  repair branch.
  Sources: [2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md → Turn 4](../codex-sessions/2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md#turn-4); [2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md → Turn 5](../codex-sessions/2026-11-13T16-48-41-01a1d598-c5c9-74b3-81b1-2d8998f63598.md#turn-5).

- Include scenarios that failed in recent CI runs alongside the affected scenarios
  before committing. Speed up individual scenarios by reducing repeated setup
  and choosing representative combinations around known failure boundaries.
  Codex session ID `01a1f28d-c98f-77c1-aeea-7a954ef09674`.

- Show failed test names, failed lint checks and their error messages on the GitHub Actions run page.
  Codex session ID 01a1f101-c467-7ae3-9de5-9761d16b1998.

- Run the complete test suite and automated checks in GitHub Actions.
  Source: [2026-11-14T15-17-19-01a1da6b-7ba6-70f2-9ed5-363f862fab59.md → Turn 2](../codex-sessions/2026-11-14T15-17-19-01a1da6b-7ba6-70f2-9ed5-363f862fab59.md#turn-2).

- After a requested commit succeeds, remove temporary artifacts created for the
  task. Clear shared artifacts when the agent can confirm exclusive use and the
  remaining files are disposable.
  Source: Codex session ID 01a1ef26-c2a5-7c51-bcb2-a708c6a978a3.

## BrowseStories

As a developer, I want to browse epics and their user stories in reading order so that
I can learn the product and find the need behind a change.

### Implementation decisions

- Keep the epic/story catalogue as the LivingDoc. Archive older documents as historical
  ChangeDocs with an AGENTS.md explaining that they do not define current behavior.
  Source: [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 1](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-1).

- Use one Markdown file per epic with front matter, semantic word-based IDs, and a
  logical reading order for epics and their stories. Later stories may rely on earlier
  context. Include developer stories as well as product-user stories.
  Source: [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 2](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-2).

- Keep the short classic user-story formula as the main description. Store explicitly
  agreed implementation decisions separately; needs change less often than
  implementation choices, which can remain experimental.
  Source: [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 2](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-2).

- Put guidance needed for all work in AGENTS.md and link to separate files under docs
  for occasional processes. Prefer goals, agent judgment and briefly justified agreed
  rules over micromanagement.
  Source: [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 2](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-2).

- Use a small web explorer that reads the catalogue and source map, expands epics to
  their stories and shows the related implementation files.
  Source: [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 1](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-1).

- Use larger, higher-contrast text and compact spacing in Story Explorer so the
  catalogue and reports are easier to read.
  Codex session ID `01a1ee05-92a1-7883-94d6-647f83318fc3`.

- Keep condensed project session transcripts under docs: user prompts, final agent
  replies and edited-file lists when available. Omit command and tool-call logs from
  this documentation source.
  Source: [2026-11-17T22-22-21-01a1eb63-aeab-7531-92f3-de6b1d125c93.md → Turn 1](../codex-sessions/2026-11-17T22-22-21-01a1eb63-aeab-7531-92f3-de6b1d125c93.md#turn-1).

- Record each implementation decision the user explicitly states or accepts in the
  relevant story. Include the Codex session ID when recording the decision and link to
  the relevant turn when the export is available.
  Codex session ID `01a1ebb3-eaeb-76e2-aaa4-27311890b241`.

## TraceStoryFiles

As a developer, I want to see the files that implement or test a user story so that
I can locate related code and review how responsibilities are spread across files.

### Implementation decisions

- Store file-level links in comments at the start of source files, with separate
  `implements` and `tests` relations. Infrastructure files should map to their own
  developer stories rather than inheriting every product story that uses them.
  Sources: [2026-11-15T21-30-25-01a1e0e7-6a7f-7412-a459-a2b907ba6881.md → Turn 1](../codex-sessions/2026-11-15T21-30-25-01a1e0e7-6a7f-7412-a459-a2b907ba6881.md#turn-1); [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 1](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-1); [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 4](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-4); [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 5](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-5).

- Rank stories by their implementation file count or test file count. Rank files
  by the number of distinct stories or epics linked to them. Try a treemap with
  nested directories and files, area based on file size, and color based on a
  mapping metric. Use the reports to find cases worth reviewing.
  Codex session ID `01a1ee05-92a1-7883-94d6-647f83318fc3`.

- Outline the full directory rectangle when the pointer is over its treemap label,
  including for nested directories.
  Codex session ID `01a1ee05-92a1-7883-94d6-647f83318fc3`.

- Show named subfolders inside larger folders in the treemap overview. Let the
  developer open a folder to see the individual files it contains.
  Codex session ID `01a1ee05-92a1-7883-94d6-647f83318fc3`.

- Show individual files inside their folders in the treemap. Label folders and show
  small file names where the full name fits. Give each file tile a tooltip with its
  full path. Use file size for area and story or epic links per KiB for color.
  Codex session ID `01a1ee1c-f487-78a2-bba9-c3937474ba40`.

- Prefer wider file tiles and use regular-weight file names so more names fit. Lay
  out siblings from darker to lighter, starting at the top left. Within the same
  color band, place larger tiles first.
  Codex session ID `01a1ee1c-f487-78a2-bba9-c3937474ba40`.

- Show a compact implementation matrix with source files under `src/` in rows
  and all stories in columns. Use green for an implementation link and white
  for an empty cell. Show the file and story on hover. Keep epic reading order
  and source directories as order options. Let the developer group either
  axis by shared links and place files linked to one story after shared files in
  that order. Offer a dependency order that keeps cycles together. Scale file
  row height by file size and show the size in cell details. Show headers with
  hover details for epics and the source directory tree in their respective
  orders. Within each directory, offer orders by fewer color
  transitions, shared stories and file paths. Show the number of color transitions
  between adjacent files within directories so the orders can be compared.
  Leave JSON resources, lock files and licenses outside the matrix. Keep source
  files with no story links visible so missing annotations can be found.
  Codex session ID `01a1f431-c0b8-7ff0-9500-bc489b2a7bce`.

- Before each commit, review story references for changed source files and update the
  catalogue when needed. Historical mappings can help initial annotation, but every
  source file must be reviewed against its actual responsibility.
  Sources: [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 1](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-1); [2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md → Turn 5](../codex-sessions/2026-11-17T20-42-24-01a1eb08-2f67-7ae0-a7bb-5dbfa98f5c49.md#turn-5).

## CodeHealth

As a developer, I want to inspect code measurements and their verification evidence so
that I can choose where to investigate or refactor.

### Implementation decisions

- Report check outcomes and metric values at project, folder and file levels so the user
  can inspect where complexity and other concerns are concentrated.
  Source: [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 3](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-3).

## ReleaseHandoff

As a developer, I want to hand off work safely to a new local release so that I can
deliver improvements without interrupting the user's session.

### Implementation decisions

- Publish each release into its own folder so a running version cannot block builds or
  the next release. Switch the latest-release marker only after checking the new
  release; keep the previous working release if the new one fails.
  Sources: [2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md → Turn 5](../codex-sessions/2026-11-09T19-49-39-01a1c1a4-fdf2-7172-84c8-c549a746618d.md#turn-5); [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 5](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-5); [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 6](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-6).

- Update automatically as soon as Xcmd is safe to restart in the background. For the
  active main window, wait for ten seconds without input. Defer the handoff while
  a separate viewer has focus or work such as a copy or name edit is in progress.
  Show a pending update beside the uptime.
  Source: Codex session ID 01a1edc9-75d1-75a0-b89d-0ffcc9a25fe1.

- Limit local release retention after a successful publication. Keep the current
  release, recent rollback releases, running releases, and releases chosen for
  longer retention.
  Source: Codex session ID 01a1ef26-c2a5-7c51-bcb2-a708c6a978a3.

## WindowsIdentity

As a developer, I want to keep the Windows application identity stable so that I can
replace builds without breaking shortcuts and taskbar integration.

### Implementation decisions

- Keep one Windows application identity and taskbar shortcut across release directories.
  Pinning a running window should still point to the permanent launcher; the user
  accepted the launcher and taskbar scheme.
  Sources: [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 5](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-5); [2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md → Turn 6](../codex-sessions/2026-11-10T09-47-42-01a1c4a4-4449-7cf1-bd8e-99768dcc55f0.md#turn-6).

- Use a recognizable two-panel icon with square corners, matching dark panel colors and
  a mark in only one panel. The user selected the refined H design and asked for simpler
  small icons that preserve its identity, then approved replacing it everywhere.
  Sources: [2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md → Turn 2](../codex-sessions/2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md#turn-2); [2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md → Turn 4](../codex-sessions/2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md#turn-4); [2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md → Turn 5](../codex-sessions/2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md#turn-5); [2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md → Turn 6](../codex-sessions/2026-11-10T16-02-07-01a1c5fa-f0c5-7dc2-b574-8dc62148bd60.md#turn-6).
