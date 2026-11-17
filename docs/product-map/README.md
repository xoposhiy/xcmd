# Product stories recovered from source

This catalogue describes implemented behavior. The stories and epic boundaries are reconstructed from the code.

Reviewed on 2026-11-17 at `f8d2249b80b6d45fcca64404f264b26972ee9f8b`: **185 C# files, 14,961 physical C# lines, and 24 auxiliary files** under `src`. Every file has at least one explicit story mapping. Tests, scripts, tools, CI, and generated build outputs are outside the map.

Read the [analysis](analysis.md), explore the [interactive source map](map.html), or look up any file in the [source index](source-index.md). The [resolved JSON catalogue](catalogue.json) contains all locations, hashes, and metrics.

## Epics

| Epic | Persona | Stories | Value |
| --- | --- | ---: | --- |
| [E01: Move between working locations](epics/e01.md) | user | 10 | As a user, I want to move between working locations with little navigation effort so that I can keep my attention on the files I need. |
| [E02: Organize files across locations](epics/e02.md) | user | 13 | As a user, I want to organize files across locations so that I can keep useful files in the right place with control over changes. |
| [E03: Find files and text in a directory tree](epics/e03.md) | user | 7 | As a user, I want to find files and text in a directory tree so that I can act on relevant results without knowing their exact locations. |
| [E04: Understand file contents](epics/e04.md) | user | 8 | As a user, I want to inspect file contents so that I can understand a file before choosing another tool or changing it. |
| [E05: Work with packaged files](epics/e05.md) | user | 4 | As a user, I want to inspect, unpack, and package files so that I can work with content delivered as archives. |
| [E06: Use other tools from the current location](epics/e06.md) | user | 6 | As a user, I want to use Windows programs and commands from my current file context so that I can continue work in the right tool. |
| [E07: Work with repository changes](epics/e07.md) | user | 8 | As a user, I want to inspect repository changes and history and start a commit from Xcmd so that I can understand and finish repository work. |
| [E08: Continue work across application sessions](epics/e08.md) | user | 1 | As a user, I want to continue file work across application sessions so that closing the application does not force me to rebuild my workspace. |
| [E09: Improve Xcmd while using it](epics/e09.md) | user-developer | 5 | As a user-developer, I want to report problems and try new local builds while using Xcmd so that development stays connected to real work. |
| [E10: Implement workflows over reusable boundaries](epics/e10.md) | developer | 18 | As a developer, I want to implement workflows over explicit domain and platform boundaries so that new stories can reuse file, search, repository, and presentation services. |
| [E11: Deliver and maintain a reliable application](epics/e11.md) | developer | 7 | As a developer, I want to package, start, diagnose, and maintain Xcmd consistently so that new workflows can reach the running application safely. |

## Definitions and reading rules

- An epic is a stable kind of value or work. Each epic states its value; its boundary explains what it adds to the others.
- A story makes an action easier for one persona. Each story has one owning epic; related epics are cross-links.
- User means a person doing file work. User-developer means a person improving Xcmd while using it. Developer means a person extending or delivering Xcmd.
- Direct implementation contains a product decision, interaction, or domain rule. Routing dispatches a command to another owner. Technical implementation contains adapters, reusable mechanisms, contracts, or build inputs.
- Roles follow the responsibility of the code, in any project. An adapter location counts as direct implementation when it holds a product rule, for example which file attributes a copy keeps. Clipboard codecs and image decoding in Desktop are technical.
- A source location is an entry point or a whole focused file. Shared code can serve several stories. The map gives file coverage with symbol-level evidence.
- A focused file is listed as a whole file; mixed files use method or type anchors. Line numbers come from the reviewed snapshot. Source links pin that commit; the local file path is printed beside every link.
- Enabled-by links name the relevant technical stories. WithFeedbackId calls, DTO fields, injected dependencies, and menu items are dependencies of the stories they serve.

## Method

All C# files were read. The auxiliary inputs were inspected as project, configuration, dependency, and resource data; the builder checks JSON/XML structure and image signatures. Large imported grammar data counts as a resource. Product docs were compared with the code after the code review.

Counts depend on story boundaries. A file is worth inspecting when it has at least five direct product stories or at least three owning product epics. This is an analysis threshold for this map. Partial-class families are aggregated separately, with story IDs counted once.

## Maintain this map

Edit `catalogue.py` after reviewing the affected source, and add explicit evidence for new files. Update `REVIEWED_COMMIT` and `REVIEW_DATE` in `build.py` after reviewing a new committed snapshot. Edit `README-template.md`, `analysis-template.md`, and `map-template.html` for narrative or presentation changes. Write counts in templates as metric placeholders (`count`, `file`, or `family` in double braces, as used in the templates) so that the builder computes them. The generated reading views are README, analysis, source index, epic pages, map, and resolved JSON.

```powershell
python docs/product-map/build.py
python docs/product-map/build.py --check
python docs/product-map/build.py --draft --output artifacts/product-map-preview
```

The check resolves every anchor, rejects missing or unmapped source files, checks references, validates JSON/XML assets and image signatures, and compares every generated output including source hashes. It requires the source to match the pinned commit, so a source change makes the map stale until it is reviewed and rebuilt. The draft mode resolves the catalogue against the working tree; use it while a source change is still uncommitted. Without `--output` it writes nothing; with `--output` it writes preview views to that folder. Preview line numbers come from the working tree, while links still pin the reviewed commit.

Source snapshot SHA-256: `1c2bee6961ba840d7fa06b310458f61645186f2b62bc00492f689987fb3cab81`.
