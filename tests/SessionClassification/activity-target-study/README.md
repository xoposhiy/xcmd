# Activity by target study

The [software development test set](terra-luna/software/revised/tool-calls.zip) contains two independent reviews of 335 tool calls in six complete sessions. The [guide](terra-luna/software/revised/guide.md) defines targets for build and release work, agent instructions, and testing, including manual checks. Terra and Luna each labelled every call using this guide. The [manifest](terra-luna/software/manifest.json) gives the original project names, session titles, trace files and tool call IDs.

The [resource study](resources/README.md) adds a [list of resources for every call](resources/resources.jsonl) in the same six sessions. Join it to the activity labels by `id`. The study includes the three independent model annotations, their agreement and a check against file paths recorded in the traces.

Run `python -B terra-luna/study.py compare` here to reproduce the [results](terra-luna/software/revised/results.json) or `python -B terra-luna/study.py score all PREDICTIONS.jsonl` to score a classifier. To inspect a trace, run `python -B terra/trace.py klava-sorting --manifest terra-luna/software/manifest.json`. Add `--path` to locate the full original rollout. Reading a compressed rollout requires the Python package `zstandard`. Trace hashes cover the uncompressed JSONL bytes.

The [first-pass reviews](terra-luna/software/revised/first-pass.zip) preserve the responses before two reviewers checked generic summaries against their own traces. Luna added concrete objects and descriptions for mdview and changed the pair sets for 32 calls. Terra clarified Digester summaries and kept all its pairs. Both reviewers worked without seeing the other model's labels. The results include agreement before and after these checks.

Two earlier comparisons used the general `tooling` target. Use `--previous` before the command to reproduce the [previous results on the same six sessions](terra-luna/software/results.json), and `--original` for the [eight-session results](terra-luna/results.json) on 412 calls. Run `python -B terra-luna/study.py --previous tooling --examples` for the six-session tooling groups, or browse the [239 original tooling annotations grouped by theme](terra-luna/tooling-examples.jsonl) for the eight-session set. Each reviewer contributes one entry per call they labelled as tooling.

The [earlier Terra dataset](terra/README.md) uses the original action categories. See its [label counts](terra/README.md#label-counts) for the original coverage by target, action and pair.

The report is [Activity by target](../../../docs/reviews/activity-target-experiment.html). The development sample contains 92 calls. The validation sample contains two different sessions. Treat the validation reference and each call's agreement or disagreement as sealed. Use only the aggregate scores for evaluation. Use new development sessions for prompt or rule changes.

## Inspect the method

The [context and literal-operand comparison](context/README.md) adds two blind samples and a second independent annotation of one session. The original file-memory method below remains reproducible.

The [multi-pair review](multilabel/README.md) audits the original disagreements and compares single-pair labels, sets of observed pairs and two levels of reviewer context.

- `classifier.py` defines the role rules, command parser, result handling and session memory.
- `inputs.py` reduces each recorded tool call to observable inputs. It reads the original working directory and looks up operands at the recorded Git revision.
- `prompt.txt` holds Luna's full system instructions. `predict.py` sends the current observation, verified path facts and any prior roles for matching operands. Both modes use the same model, settings and response schema.
- `validation/inputs.json` contains only label-free observations. `validation/predictions.json` contains the saved responses for both modes. The classifier has no reference input.
- `validation/freeze.json` identifies the exact inputs and method. `validation/prediction-receipt.json` identifies the saved predictions. `validation/sealed-reference.zip` holds the independent reference. The archive prevents unintended text searches. It does not encrypt the labels.

## Reproduce the published scores

From the repository root, run `python -B tests/SessionClassification/activity-target-study/evaluate.py`. This command checks the method and prediction digests, reads the sealed reference internally and emits only aggregate counts. It runs offline and does not call Luna. It stops if a file covered by the freeze changed.

To repeat inference for a separate study, run `predict.py INPUTS.json NEW-PREDICTIONS.json CACHE-DIRECTORY` with `OPENAI_API_KEY` set. Use a fresh output file and keep the original receipt. The runner reserves usage against a USD 0.30 cap per cache directory. A fresh model run may give different answers.

## Reading a score

Each completed shell command or recorded patch contributes one target and one action. The target measures the object the agent worked with. The broad Read action includes lists, searches, Git queries and issue queries. The Write action requires a confirmed mutation. Build and test runs count as Other. Either axis can be Unknown. Activity counts and file-content coverage measure different things.
