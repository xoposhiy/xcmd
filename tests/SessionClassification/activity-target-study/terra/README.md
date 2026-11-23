# Tool-call annotations for eight full sessions

Two independent GPT-6.1 Terra reviewers with medium reasoning effort labelled each complete session. Each reviewer started in a fresh context, received the same original trace and instructions, and wrote a separate JSONL file. Reviewers had access to the full session, including messages, commands, patches and recorded results. They were instructed to avoid previous labels, classifier output and other reviews. Both opinions are retained, including disagreements.

## Dataset

- [tool-calls.zip](tool-calls.zip) contains both reviews for all 412 individual tool calls. It has one JSONL file per session under each of `a/` and `b/`. Join the two reviews and the input by `id`. The suffix `-L<n>` is the one-based line number in the original rollout.
- [manifest.json](manifest.json) identifies the eight original rollouts by file name and SHA-256. Each entry in `records` gives the line number, view and tool kind. The `legacy` list links the previous sample to the full trace.
- [guide.md](guide.md) defines the target and action categories and the annotation format. Each row includes a short English summary of the call. Each distinct pair includes the concrete objects that the reviewer identified.
- [agreement.json](agreement.json) gives exact counts and mean per-call Jaccard under `scores.item`. It also records the SHA-256 digest of each archive.

Classify and score each individual **tool call**, identified by an `item` record. Examples include a shell command, patch, image view, web search, connector call or subagent operation. Four commands run through one `functions.exec` count as four tool calls when the runtime records each one. One shell call that both reads and writes counts once, with a pair for each action. Enclosing requests provide context. The [original review archive](reviews.zip) retains the earlier annotations of both levels.

The dataset includes every recorded tool call from the first through the last. It retains all 311 original IDs. In the `point-taken` session the original sample included 24 of 60 tool calls. The dataset includes all 60. The `point` and `point-taken` keys refer to different sessions.

For example, both reviewers labelled `point-taken-L31` as `tests/read` for `tests/check_xcmd_adapter.py` and `source/read` for `src/Desktop/ReportedControl.cs`. At the end of the same trace, both labelled the registration check at `point-taken-L198` as `tooling/read`, `tooling/write` and `tooling/other`. That call read the installed files, wrote temporary copies and ran the registration check.

## Label counts

Each count is the number of distinct tool calls with a label, out of 412. A call with several labels contributes once to each. A and B are the two independent reviewers. Both counts calls on which both reviewers assigned that label.

| Target | A | B | Both |
| --- | ---: | ---: | ---: |
| tooling | 178 | 178 | 163 |
| docs | 112 | 104 | 102 |
| unknown | 95 | 102 | 80 |
| source | 81 | 77 | 77 |
| tests | 62 | 61 | 59 |
| git | 49 | 49 | 48 |
| project | 18 | 17 | 17 |
| tracker | 4 | 5 | 4 |

| Action | A | B | Both |
| --- | ---: | ---: | ---: |
| read | 327 | 323 | 322 |
| write | 93 | 88 | 88 |
| other | 52 | 60 | 51 |
| unknown | 0 | 0 | 0 |

Each cell below gives the number of calls with that exact target/action pair as A / B. Zero means both counts are zero.

| Target | read | write | other | unknown |
| --- | ---: | ---: | ---: | ---: |
| tooling | 124 / 120 | 34 / 27 | 35 / 44 | 0 |
| docs | 84 / 76 | 30 / 30 | 2 / 0 | 0 |
| unknown | 80 / 83 | 9 / 18 | 10 / 11 | 0 |
| source | 70 / 66 | 10 / 10 | 1 / 1 | 0 |
| tests | 41 / 42 | 11 / 3 | 11 / 16 | 0 |
| git | 45 / 46 | 4 / 4 | 1 / 0 | 0 |
| project | 18 / 17 | 0 | 0 | 0 |
| tracker | 2 / 3 | 2 / 2 | 0 | 0 |

At least one reviewer used 21 of the 32 possible pairs. Both reviewers used 19 pairs on at least one shared call. The two remaining observed pairs were `docs/other` and `git/other`, each used only by A. Reviewer A assigned 624 pairs across the corpus and reviewer B assigned 619.

## Reviewer agreement

Exact counts show how often both reviewers chose the same set of pairs. Jaccard gives partial credit for shared pairs. The overall row weights each tool call equally.

| Full session | Tool calls, exact / total | Mean Jaccard |
| --- | ---: | ---: |
| Point Taken issue fixes (`point`) | 41 / 44 | 95.83% |
| Xcmd source and test README files (`xcmd`) | 48 / 48 | 100.00% |
| Othello article in auto-cg (`v1`) | 32 / 33 | 96.97% |
| Automatic redeployment in mdview (`v2`) | 60 / 70 | 90.38% |
| Point Taken UI integration guide (`point-taken`) | 51 / 60 | 93.72% |
| Hidden directories in reviewer (`reviewer`) | 2 / 6 | 75.56% |
| Three concepts for the Software Engineering and Design course (`fresh`) | 28 / 46 | 64.53% |
| Collecting custom skills in .skills (`reserve`) | 80 / 105 | 82.43% |
| **All eight sessions** | **342 / 412** | **87.97%** |

The course design session took place on November 15, 2026, in the `project-courses` repository. The agent studied existing university courses, wrote three proposals for updating Software Engineering and Design, and checked the layout of the HTML brief. The earlier experiment used `fresh` as the ID for this additional validation session.

This session had the lowest agreement on runtime items. Both reviewers identified all eight screenshot views as reads. Reviewer A classified the screenshots as `tooling`. Reviewer B used `unknown`. The resulting pairs had zero overlap on those eight records.

### Confusion by target and action

The [target matrix](target-confusion.png) and [action matrix](action-confusion.png) compare the two saved reviews under the original guide. The subsequent [Terra and Luna experiment](../terra-luna/results.json) uses the [revised rules](proposed-guide.md), including the `exec` action and the narrower use of `tooling` and `project`.

Read each matrix across a row. Rows are reviewer A's labels and columns are reviewer B's labels. Each cell shows its share of the row and its count. A tool call can contribute several distinct labels. Compare the target set and the action set separately for each call. Put shared labels on the diagonal. If exactly one label remains on each side, count one substitution. Put any other remaining labels in the Unpaired row or column. This preserves the observed counts for each reviewer and avoids guessing a correspondence. It measures agreement on each axis separately. Pair-level Jaccard also checks which action goes with which target.

| Relation between the two label sets | Tool calls by target | Tool calls by action |
| --- | ---: | ---: |
| Identical sets | 357 | 399 |
| One label replaced with one other label | 33 | 6 |
| Extra labels on one side only | 22 | 6 |
| Extra labels on both sides with ambiguous correspondence | 0 | 1 |
| **All calls** | **412** | **412** |

For targets, 21 of the 33 substitutions were between `tooling` and `unknown`. Ten went from A's `tooling` to B's `unknown`, including eight screenshot views in the course design session. All 11 in the reverse direction came from web tool calls in the session about collecting custom skills. There were zero direct substitutions between `tooling` and `tests`.

For actions, four of the six substitutions were from A's `read` to B's `other`. On one call A recorded `read` and `write` while B recorded `other`. The matrix counts all three as unpaired. Neither reviewer used the action `unknown`. Run `python -B confusion.py` in this directory with Matplotlib installed to regenerate both figures.

## Reproduce or score a run

Run the commands below from this directory with Python 3. The archive contains the annotations. The original full traces stay in the local Codex session store.

```powershell
python -B review.py verify
python -B review.py verify --traces "$env:USERPROFILE/.codex/sessions"
python -B review.py counts
python -B trace.py point-taken --path
python -B trace.py point-taken --first 140 --last 155
python -B review.py check point-taken my-review.jsonl
python -B review.py score point-taken my-predictions.jsonl
python -B review.py score all all-predictions.jsonl
```

`verify` checks IDs, fields, the archived annotations, the original sample membership and the published scores. With `--traces` it also checks each rollout digest and rebuilds its complete tool inventory. `trace.py` reads local evidence without executing any recorded command. Use `--limit 0` to see a whole request and result, or use `--path` to read the original session directly.

For `score`, supply exactly one prediction for every tool call in the selected session. Choose a session key or `all` for all eight sessions. Each row requires `id` and `pairs`. This example shows the format.

```json
{"id":"example-L20","pairs":[{"target":"source","action":"read"},{"target":"source","action":"write"}]}
```

The scorer compares a prediction to each reviewer separately. A prediction can abstain with an empty pair list and receives zero for that call. Missing IDs, extra IDs, duplicate IDs, repeated pairs and unknown category names are errors.

For each call, divide the number of shared target/action pairs by the number of distinct pairs in either set. Average these fractions over calls. Each call has equal weight. Swapping the actions between two targets can give zero overlap. Object names, summaries and pair order do not affect the score. The exact count requires identical pair sets. Reviewer agreement measures reproducibility. Accuracy against a single reference would require resolving disagreements.
