# File coverage evidence

Use this corpus to reproduce the earlier file coverage study. It includes 219 reviewed tool actions from five Point Taken sessions and 325 boundary cases. Run the probe with the filter `session-inspector-coverage-corpus`. The scenario enables the study rules in the transcript reader and records the scores in `results.json`. The session inspector gets touched resources from the automatic Luna classification.

## What counts

A label names one action on one file in one completed tool call. Reading the same file twice in different calls counts twice. Reading two files in one command counts twice. A result can prove a read without establishing a line range. A search match, diff, directory listing, application build, or link in text has different semantics. This benchmark does not try to count every file that the underlying programs opened.

The review compared the recorded command or tool input with its working directory, completion status and result. For edits it also checked the provider's list of changes. The comparison used the original local transcripts. The portable fixtures keep source line numbers, command forms, paths and result shapes. They replace account names, tool output, long commands and embedded code with small examples. The fixtures contain data only. The test never executes their commands. For 21 reviewed reads, the fixtures also preserve the recorded line count and output record separator.

## Review of the original logs

| Session suffix | Completed actions reviewed | Correct before | Extra before | Missed before | Correct after | Extra after | Missed after |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 25c41521 | 36 | 4 | 15 | 15 | 19 | 0 | 0 |
| c6fc6066 | 60 | 10 | 21 | 46 | 38 | 0 | 18 |
| f884db91 | 68 | 13 | 18 | 18 | 31 | 0 | 0 |
| 753aea35 (Claude Code) | 23 | 5 | 0 | 15 | 7 | 0 | 13 |
| 85d6f706 (independent review) | 32 | 5 | 6 | 7 | 12 | 0 | 0 |
| Total | 219 | 37 | 60 | 101 | 107 | 0 | 31 |

The first column identifies the local session. The previous version was release `20261122-021131-031-0a5ba765`. The independent reviewer labelled the 32 actions before checking the new reader. A wrong path counts as an extra result and a miss for the intended path. The 138 labelled pairs include direct reads and changes that a person could attribute to a concrete file inside a script. Actions with an unresolved target and search matches are outside this denominator. For example, a successful search in `25c41521` at line 44 returned content from five files. This table does not measure coverage of those five search results.

In `25c41521`, line 19 reads `AGENTS.md` with a working directory of `file:///C:/work/projects/point-taken`. It returns 25 lines. The old reader interpreted the URI as a relative Windows directory. The displayed path then depended on the directory where Xcmd was running. No separate tool call at that point opened the Xcmd paths mentioned in the returned text. In `c6fc6066`, line 35 really ran in `file:///C:/work/projects/xcmd` and read two files from that project.

The 31 known misses include reads inside Python scripts, image saves and compound shell commands without a separate result for each command. The independent session has an additional read built from `LOCALAPPDATA`. The log does not establish the historical value of that variable. Selecting a file in the inspector shows the recorded tool or command and working directory.

## Corpus layout

- `point-taken.jsonl` contains the five sessions. Each `id` ends with the source line of the tool record or Claude tool request. `review-A` and `review-B` identify the implementation review groups. `independent` identifies the separately labelled session.
- `boundaries.jsonl` covers the same command shapes with directory URIs, percent-encoded spaces, Windows paths, UNC shares, POSIX paths and incomplete working directories. It also tests literal path lists, line slices, misleading parser hints, quoted commands, comments, shell branches, redirects, cancellation, absent exit codes, tool result identity, and successful and failed patches.
- `expected` contains required actions. If a range is present, `first` and `lines` give its start and length. Zero means unknown. `total` is the recorded file length or zero. `returnedLines` creates a redacted output body of the recorded length.
- `knownOmissions` contains further file actions identified by human review. Recognizing one improves the score. `unresolved` describes operations whose historical path could not be established. `reason` explains the expected decision.

When extending the corpus, label the tool input and result before reading parser output. Include a negative case with a similar-looking path. Keep the source location and explain uncertainty about the path or line numbers.

## Optional Luna review

Opening an inspector uses local rules. **Analyze with Luna** reviews ambiguous calls across the session, including linked agents. The selected range and agent still filter the table. The client sends the tool name, status, working directory, project directory, command and result excerpt to OpenAI. It caps commands at 10,000 characters and results at 6,000. For long scripts it keeps the beginning, the end and excerpts around possible file operations. It masks common credentials. The request uses `gpt-6-luna`, reasoning effort `none` and `store: false`. Set `OPENAI_API_KEY` in the environment or in the `.env` file beside the launcher.

Each inferred read names a path and the command line that supports it. Xcmd checks the path against the transcript and the recorded working directory. A result can leave some reads unresolved. Inferred reads have no line positions. A numeric result from an explicit line-count operation can establish a count. Open a file to see the operation and count evidence. A disk cache stores model replies under the user's local Xcmd data directory. A changed prompt, model or input gets a new cache key. Cancel stops pending work. Resume reuses completed reviews.

### Read accuracy

One unit is a distinct pair of tool call and path. All columns below measure reads. File edits, writes, search results and diffs have separate meanings. The local column uses the current rules. The earlier rules found 81 of the 106 reads in the five sessions.

| Evidence set | Calls | Sent to Luna or its cache | Labelled reads | Local reads found | Reads found after review | Extra paths |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Five Point Taken sessions, original portable fixtures | 219 | 58 | 106 | 83 | 91 | 0 |
| Same sessions with longer evidence for five calls | 219 | 58 | 106 | 83 | 100 | 0 |
| Synthetic scripts labelled before prompt tuning | 16 | 14 | 8 | 0 | 8 | 0 |
| Boundary cases | 325 | 145 | 86 | 86 | 86 | 0 |

The two 219-call rows describe the same actions. The second row replaces five shortened inputs and results with the variants in `context.jsonl`. The account name is anonymized. Four variants retain the recorded scripts and results. The fifth retains the start of a JSON document followed by numbered search results. Neither the expected paths nor review notes enter a request.

The first run on the 16 held-out scripts found all eight reads and added one incorrect path. That path dropped an unknown environment directory. The final path checks reject that claim. This set now also serves as a regression test. The independently labelled 32-call session had eight reads. Both the local rules and the final review found all eight with zero extras.

With longer evidence, five of the six missed paths refer to a guide loaded through a Markdown link. The command records how to find the link, but the individual call does not return its target. The sixth read builds a release path from a historical environment variable. The scores still count those misses. The boundary labels also include two reviewed reads that the previous rules missed. One command reads a file as bytes. The other reads the last file in a successful Bash AND-list after a read from stdin.

### Reproduce the checks

Run the normal probe with the filter `session-inspector`. The corpus, saved-claim replay, HTTP client, cancellation, resume, cache and window checks run offline. `luna-replies.json` reproduces proposed paths and operation lines from an earlier run on 24 cases, including incorrect proposals. `protocol.jsonl` checks plain Codex shell result headers, pending commands and text that resembles a result header.

For a live benchmark, set `OPENAI_API_KEY` and run the Release x64 probe.

```powershell
Xcmd.Probe.exe --evaluate-file-reads artifacts/luna-benchmark all
```

Use `review-A` for prompt tuning. Other split names are `review-B`, `independent`, `blind`, `boundary`, `context` and `protocol`. The benchmark saves one score per call in `scores-<split>.json` and a conservative token-cost estimate in `spend-usd.json`. Reuse the same directory for every run. The benchmark reserves the maximum request cost before sending and stops at one US dollar across runs in that directory. A repeat with the same inputs, request format and prompt uses cached replies. A fresh live run can produce different model answers.
