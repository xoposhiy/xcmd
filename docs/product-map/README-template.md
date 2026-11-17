# Product stories recovered from source

This catalogue describes implemented behavior. The stories and epic boundaries are reconstructed from the code.

Reviewed on {{count review_date}} at `{{count commit}}`: **{{count code_files}} C# files, {{count code_lines}} physical C# lines, and {{count aux_files}} auxiliary files** under `src`. Every file has at least one explicit story mapping. Tests, scripts, tools, CI, and generated build outputs are outside the map.

Read the [analysis](analysis.md), explore the [interactive source map](map.html), or look up any file in the [source index](source-index.md). The [resolved JSON catalogue](catalogue.json) contains all locations, hashes, and metrics.

## Epics

__EPIC_TABLE__

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

Source snapshot SHA-256: `{{count source_hash}}`.
