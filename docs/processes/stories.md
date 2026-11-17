# Maintain stories

Keep the [LivingDoc](../stories/README.md) useful for learning the product, locating
related code and considering responsibility boundaries. Product users and
developers both have stories.

## Needs and decisions

An epic describes a broad need that changes rarely. A user story describes a more
specific need and changes when that need is understood differently. Its main text
is a short statement: `As a <role>, I want <capability> so that <benefit>.`

Use an optional `### Implementation decisions` section under a story for decisions
explicitly stated in a development conversation. Include enough conversation
context or a reference to distinguish an agreement from an inference. These
decisions can change through experiments; they are less stable than the need.
Do not treat an observed implementation or an archived description as proof of an
agreement. Do not add empty decision sections or mandatory acceptance checklists.

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
`tests` means that the file contains checks of that story's behavior. List IDs only;
resolve descriptions in the catalogue instead of copying them into source.

Map direct responsibilities. A shared utility does not inherit every story of its
callers. A test driver without assertions does not test every story that uses it;
use a relevant developer story for its own capability. Generated and third-party
files, binaries and formats without comments are outside this annotation format.

Source comments own the file-to-story relations. Future browsing views derive both
directions from them; do not maintain parallel file lists in the stories. Counts
are review aids, not automatic refactoring rules or proof of test coverage.

The source annotation migration is incomplete. A mapping checker and specification
browser are not installed yet. Do not claim complete mapping coverage during this
transition.

## Commit responsibility

Before every commit, review the affected needs, decisions and mappings together
with the changed files. Add or correct annotations in authored source files being
changed. Check that IDs resolve, remove stale relations and update the story only
when its need or an explicit implementation agreement changes. A routine fix can
belong to an existing story without changing its statement.

For catalogue edits, check unique IDs, epic order, the index and links. Structural
checks can detect broken references; the agent remains responsible for whether a
story and its mappings describe the work accurately.
