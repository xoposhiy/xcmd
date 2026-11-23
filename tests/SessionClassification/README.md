# Measure tool call classification

Run the paid test **only when a user explicitly asks for a live accuracy measurement**.
From the repository root in PowerShell 7, set `OPENAI_API_KEY` in the environment and run

```powershell
. ./scripts/BuildEnvironment.ps1
python -B tests/SessionClassification/evaluate.py --live
```

The test builds the Release / Windows x64 probe and reads six fixture transcripts with
the same reader and Luna classifier as F3. Each run uses an empty, temporary cache.
The reference answers stay in the scorer. The command prints the exact match count
and mean per-call Jaccard score for target/action pairs against Terra and Luna
separately. It also prints resource precision, recall and micro F1. A missing
classification gets zero pair credit. An incomplete live run exits with an error.
The probe scenario list and CI do not include this command.

For an offline check of all call IDs, references and the scorer, use the same
commands with `--check` in place of `--live`. Python 3.11 or later is sufficient.

## Dataset

The [manifest](activity-target-study/terra-luna/software/manifest.json) identifies
335 completed tool calls by session and original JSONL line. The
[transcripts](transcripts/) preserve these line numbers with empty lines.
They contain label-free runtime events, surrounding user and assistant messages,
commands, selected results and patch excerpts. Paths are shortened. Diagnostic
output that can contain private messages or database records is omitted.
The test reads these fixtures on any checkout. The optional
[export script](export_inputs.py) rebuilds them from the original verified local
traces and requires the Python package `zstandard`.

The [activity references](activity-target-study/terra-luna/software/revised/tool-calls.zip)
contain both independent annotations. The [resource reference](activity-target-study/resources/resources.jsonl)
identifies the objects touched by each call. The [study](activity-target-study/README.md)
records the label guide, review process and earlier results.
