# Maintain stories

Keep the [LivingDoc](../stories/README.md) useful for learning the product, locating
related code and considering responsibility boundaries. Product users and
developers both have stories.

## Needs and decisions

An epic describes a broad need that changes rarely. A user story describes a more
specific need and changes when that need is understood differently. Its main text
is a short statement: `As a <role>, I want <capability> so that <benefit>.`

Record each implementation decision the user explicitly states or accepts under
`### Implementation decisions` in the relevant story. Include the Codex session
ID with each decision and enough context to explain what the user agreed to.
These decisions can change through experiments. Do not treat an observed
implementation or an archived description as proof of an agreement. Do not add
empty decision sections or mandatory acceptance checklists.

Run this PowerShell command from the current Codex session to get its ID.

```powershell
$env:CODEX_SESSION_ID
```

Record the ID in the story as `Codex session ID <session-id>`. After the session
ends and an export is available, add a link to the relevant turn with its
`#turn-N` anchor. When adding an export to Git, commit the export and its links
together.

## Catalogue format

Each epic has one Markdown file in `docs/stories`. Its YAML front matter contains
`id`, `title`, `order` and `role`. Use the epic title as the level-one heading and
put its short value statement below it. Use each story's ID as a level-two heading,
followed by its statement. The role applies to the epic and its stories.

Use short semantic PascalCase IDs such as `WorkingLocations` and `NavigatePanels`.
IDs are unique across all epics and stories. Keep them stable when changing a
title, order or owning epic, because code and future issue links depend on them.
Do not reuse retired IDs for different needs.

Read epics by ascending `order` and stories in their file order. Later stories may
rely on the context established earlier; they need not repeat it. Each story has
one owning epic. Keep the catalogue index in the same order. Do not add a separate
dependency graph merely to restate that reading order.

## Source mapping

Use file-level comments near the start of authored source files, with exactly two
relation names and comma-separated story IDs:

```csharp
// @implements NavigatePanels, SortEntries
```

```csharp
// @tests NavigatePanels, SortEntries
```

Use the language's comment syntax, preserving required shebangs or file directives.
`implements` means that the file contains part of the story's implementation.
`tests` means that the file checks the story's behavior or provides test code specific
to that behavior, such as a fixture or a driver for a particular window. List IDs only;
resolve descriptions in the catalogue instead of copying them into source.

Map direct responsibilities. A shared utility maps to the developer story for the
capability it provides. For example, the common headless runner and input driver
implement `HeadlessVerification`. A driver for the search window maps to the search
behaviors it directly exercises. Keep window-specific steps and fixtures separate
from the shared runner. Generated and third-party files, binaries and formats without
comments are outside this annotation format.

Source comments own the file-to-story relations. Browsing views derive both
directions from them; do not maintain parallel file lists in the stories. Counts
are review aids, not automatic refactoring rules or proof of test coverage.

Authored production code, tests, test helpers, scripts, build configuration and
text assets now have source annotations. Archived documentation is outside the
current source map. Run `./scripts/Explore-Stories.ps1` and open the printed local
URL to browse epics, stories and their implementation and test files. The explorer
reads the current worktree on startup and when reloading the map. It includes new
files that Git does not ignore, and excludes documentation and generated files.
Source links open plain text in a new tab.

Use `./scripts/Explore-Stories.ps1 -Check` to validate catalogue structure and story
references without starting the server. The explorer uses Python 3.11 or later and
Git, with no extra packages. It reports malformed annotations and unknown story
IDs. It does not establish that every responsibility is mapped; review completeness
and meaning when changing files. The front matter uses scalar fields, and story
headings use `## StoryId`. Extra story sections remain visible as source text in
the explorer; they are not a second copy of the documentation.

## Commit responsibility

Before every commit, review the affected needs, decisions and mappings together
with the changed files. Add or correct annotations in authored source files being
changed. Check that IDs resolve, remove stale relations and update the story only
when its need or an explicit implementation agreement changes. A routine fix can
belong to an existing story without changing its statement.

For catalogue edits, check unique IDs, epic order, the index and links. Structural
checks can detect broken references; the agent remains responsible for whether a
story and its mappings describe the work accurately.
