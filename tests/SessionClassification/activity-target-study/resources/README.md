# Which resources did each tool call touch?

The [working dataset](resources.jsonl) gives a resource list for each of the 335 tool calls in the six-session software development set. Join it to the [existing activity annotations](../terra-luna/software/revised/tool-calls.zip) by `id`. Each resource has a `kind` and a `value`. A call can read a file, inspect a repository and query a running process in one command. The list records each identified resource once.

**Terra, Sol and Luna returned identical lists for 243 of 335 calls (72.5%).** Their mean three-way Jaccard was **85.7%**. Files accounted for 1,089 of the 1,437 resource occurrences in the working dataset (75.8%). File agreement was much higher than directory or URL agreement.

## Agreement by trace

For each call, divide the number of typed resources all three models reported by the number any model reported. This is three-way Jaccard. Average it across calls so that a long deployment script and a single file read have equal weight. Exact match requires the same complete set of typed identifiers. No model returned an empty list for a whole call.

| Trace | Calls | Exact lists | Mean Jaccard |
| --- | ---: | ---: | ---: |
| Point Taken issue fix (`point`) | 44 | 35 | 89.4% |
| Xcmd source and test guides (`xcmd`) | 48 | 34 | 84.7% |
| mdview redeployment (`v2`) | 70 | 46 | 83.4% |
| Point Taken integration guide (`point-taken`) | 60 | 46 | 87.9% |
| Klava sorting (`klava-sorting`) | 42 | 34 | 89.5% |
| Digester recovery (`digester-recovery`) | 71 | 48 | 82.1% |
| **All** | **335** | **243** | **85.7%** |

Across all calls, the models proposed 1,324 distinct call/resource pairs. All three reported 999, two reported 177 and one reported 148. Pooling resource occurrences gives 999/1,324 (75.5%). The lower pooled score shows that calls with long resource lists contributed a larger share of the disagreement.

## Agreement by resource kind

An active call is one for which at least one model reported the kind in that row. Both the exact count and the mean exclude calls where all three sets for that kind were empty. For example, 328 calls had no URL predictions from any model. Including those would give 97.9% exact agreement on URLs while hiding every positive disagreement.

| Resource kind | Active calls | Exact lists on active calls | Mean Jaccard | Occurrences in working dataset |
| --- | ---: | ---: | ---: | ---: |
| File | 282 | 237 | 91.1% | 1,089 |
| Directory | 105 | 50 | 51.7% | 221 |
| URL | 7 | 0 | 0.0% | 32 |
| Repository | 69 | 46 | 67.4% | 53 |
| Issue | 3 | 3 | 100.0% | 3 |
| Other named resource | 30 | 15 | 52.5% | 39 |
| All kinds except files and URLs, combined | 165 | 87 | 58.3% | 316 |

The three issue calls all addressed the same ticket, `point-taken#49`. The 32 accepted URL occurrences came from just two mdview calls. These samples give little evidence about other trackers or web workloads.

## Pairwise agreement

Each shared or one-sided count is an occurrence of one typed resource in one call. Identical spelling in two different calls counts twice.

| Model pair | Exact calls | Mean Jaccard | Both models | First only | Second only |
| --- | ---: | ---: | ---: | ---: | ---: |
| Terra / Sol | 316/335 | 97.4% | 1,163 | 27 | 56 |
| Terra / Luna | 246/335 | 86.8% | 1,002 | 188 | 88 |
| Sol / Luna | 244/335 | 86.4% | 1,009 | 210 | 81 |

### Where Sol and Luna differed

Rows show Sol's kind and columns show Luna's kind. Match identical typed resources within the same call first. If a remaining identifier occurs once on each side with different kinds, count a kind substitution. All remaining identifiers contribute to an **Absent** row or column. The set of possible unmentioned identifiers is unbounded, so the bottom-right cell has no count. All three pair matrices are in the [machine-readable results](scores.json).

| Sol ↓ / Luna → | File | Directory | URL | Issue | Repository | Other | Absent |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| File | 837 | 0 | 0 | 0 | 0 | 0 | 82 |
| Directory | 4 | 102 | 0 | 0 | 0 | 0 | 89 |
| URL | 0 | 0 | 0 | 0 | 0 | 0 | 16 |
| Issue | 0 | 0 | 0 | 3 | 0 | 0 | 0 |
| Repository | 0 | 0 | 0 | 0 | 47 | 0 | 5 |
| Other | 1 | 0 | 0 | 0 | 0 | 20 | 13 |
| Absent | 11 | 30 | 11 | 0 | 19 | 5 | — |

Only five identifiers in this comparison received two different kinds. Most differences involved whether to include an identifier at all. For example, Sol often included both the directory passed to a content search and the files with recorded matches. Luna sometimes returned only the files.

## Accuracy checks

**Direct trace check.** A deterministic reader recovered complete file lists for 51 completed patches, four image views and 73 commands consisting of literal file reads. Those 128 calls contained 267 file occurrences. Terra and Sol matched all 128 lists (resource F1 100%). Luna matched 127 (F1 99.6%). On `digester-recovery-L136`, Luna returned the test directory instead of `tests/Digester.Windows.Tests/DashboardTests.cs`. These checks cover explicit file operands.

**Comparison with the working dataset.** Precision counts the share of a model's proposed resources retained in the dataset. Recall counts the share of dataset resources the model returned. Micro F1 combines precision and recall across call/resource pairs. The working dataset uses model votes and trace review, so these scores describe fit to this edition. The direct trace check above has an independent source of labels.

| Model or rule | Exact calls | Precision | Recall | Micro F1 |
| --- | ---: | ---: | ---: | ---: |
| Terra | 305/335 | 99.1% | 82.0% | 89.8% |
| Sol | 310/335 | 99.3% | 84.3% | 91.2% |
| Luna | 240/335 | 92.4% | 70.1% | 79.7% |
| At least two model votes | 304/335 | 99.3% | 81.3% | 89.4% |

## What the trace review changed

The working lists use direct trace labels for 128 calls. A second pass checked the original traces for the remaining 91 calls with disagreement and corrected three more calls where the models agreed. The other 113 lists retain the unanimous model answer. Each row records its source. The review changed the majority list on 31 calls. All changes and the reasons for reviewed decisions are in the [review log](review.jsonl).

- **All three missed files.** On `point-taken-L41`, all models returned the two directories passed to `rg -l`. The output names four files whose contents matched. All four belong to the sibling Xcmd checkout and appear in the working list.
- **A majority missed real URLs.** On `v2-L362` and `v2-L419`, the script built loopback URLs using ports read from files. The full results identify ports by viewer and release stage. Sol returned 16 of the 32 reconstructed URL occurrences. Terra, Luna and the two-vote rule recovered zero. Earlier calls used dynamic ports whose values the trace did not record. Their working lists omit the unresolved URLs.
- **Shortened input affected recall.** Several Digester diagnostics read many local log files. To avoid sending private messages and database rows, the shared model packet omitted those diagnostic results. The trace review recovered concrete file names from the local results. This accounts for some of the recall gap for all three models.
- **Control flow changes which files count.** On `v2-L307`, the release script stopped during Go tests. Two models included the intended installation path. The trace showed that execution stopped before publication and before the subsequent file checks.

## Data and reproduction

Terra (`gpt-6.1-terra`) and Luna (`gpt-6-luna`) used reasoning effort `none`. Sol (`gpt-6.2-sol`) used `low`. These were the lowest accepted settings for these model versions. Each model received the same [instructions](guide.txt) and 45 packets from the six verified source traces. A packet contains up to eight calls, with their full commands, working directories, patch paths or image paths, and selected results. The models received no previous activity labels or other reviewers' answers.

The [Terra](terra.jsonl), [Sol](sol.jsonl) and [Luna](luna.jsonl) files retain each model's original lists. The [request receipt](receipt.json) identifies the guide, manifest, model, effort, input packet, accepted response, usage and returned label digest. The [direct trace labels](trace-reference.jsonl) provide the independent file checks.

The scorer uses sets of `(kind, value)` within each call. It compares Windows paths without case differences, normalizes slashes and path components, and resolves local file URIs to file paths. It retains URL path and query case. Project paths are relative to the session project. The working list uses `%TEMP%` to represent the local temp root, including when a URL query contains the encoded path of a temp file. A Git push with an identified remote can have both a local and a remote repository. A query of a local SQLite file identifies the database file. The working list includes resources that the trace can identify, including explicit attempted operations.

Run these checks from this directory. The first command checks all model IDs, label digests, working lists and scores offline. The second also rebuilds all 45 input packets and the direct trace labels from the local Windows Codex store. Compressed traces require the Python package `zstandard`.

```powershell
python -B score.py
python -B score.py --traces
```

For a new model run, use a new output folder and cache. Set `OPENAI_API_KEY` or supply it in the repository's local `.env`. This command sends the evidence packets to the Responses API.

```powershell
python -B annotate.py --output new-run --cache new-run/cache
```
