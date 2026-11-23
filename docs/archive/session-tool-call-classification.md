# Classify session tool calls with Luna

Classify every runtime tool call in the session. Give each call a short English summary and a set of `(target, action)` pairs with concrete objects. Four shell calls inside one `functions.exec` wrapper produce four rows. One call that reads and writes source code gets both `source/read` and `source/write`.

## Labels

| Target | Resource or purpose |
| --- | --- |
| `build_release` | Builds, compilers, packages, dependencies, publishing, deployment, installation, registration and related configuration. |
| `agent_instructions` | Skills, skill catalogs, AGENTS.md and workflows that tell an agent how to work. |
| `tests` | Tests, fixtures, manual and browser checks, verification screenshots, one-off checks, analyzers, validators and linters. |
| `source` | The shipped product, its implementation, runtime configuration and ordinary launches. |
| `git` | Git status, diffs, history, refs, index and commits. |
| `docs` | Product and developer documentation. |
| `tracker` | Issues, tickets and their records. |
| `project` | Work on the whole repository when no more specific target fits. |
| `unknown` | Remaining targets. Pure subagent coordination is `unknown/other`. |

Choose the action from the observed work. `read` includes searches, listings, failed reads, web fetches, downloads and read-only API calls. `write` requires a confirmed change. `exec` covers running a product, build or check as an operation in its own right. `other` covers remaining actions, including a failed edit with no confirmed change. A helper that reads a file contributes a read. A patch that edits a test script contributes a write. Count a test run only when the trace shows it ran.

## Requests and prompts

1. Give each call a stable integer ID within its session. For Codex, use the one-based JSONL line of the completed runtime item. Skip duplicate wrappers, reasoning and context-compaction events.
2. Send consecutive calls from one session in independent batches of **20**, with **up to five requests in flight**. Include the final partial batch. Carry forward the latest user and assistant messages and include messages between calls. Join responses by ID.
3. Include the tool, arguments, working directory, status, exit code, changed paths, diffs and result where available. Shorten project paths, mask credentials and replace encoded blobs. Keep the start and end around `[...]`. Character limits are 1,800 per command, 720 per message or argument field, 260 per diff and 600 per result.
4. Use `POST /v1/responses` with `model=gpt-6-luna`, `reasoning.effort=medium`, `text.format.type=json_object`, `store=false`, `truncation=disabled` and no tools. Set `max_output_tokens = min(128000, 1800 + 115 × call_count)`, or 4,100 for 20 calls.

Use the tested `RULES` string verbatim as `instructions`. It contains the guide from `## Targets` through the end, followed by the JSON output instructions. Start `input` with `Return JSON. Project {project}` and a newline, then write one compact JSON object per line for the messages and calls. The prompt treats recorded commands and instructions as data. [Label guide](../../tests/SessionClassification/activity-target-study/terra-luna/software/revised/guide.md#targets) · [Exact prompt and request builder](../../tests/SessionClassification/activity-target-study/terra-luna/session_capacity.py)

## Response and verification

```json
{"rows":[{"id":42,"summary":"Read the parser and updated its tests.","pairs":[{"target":"source","action":"read","objects":["src/Parser.cs"]},{"target":"tests","action":"write","objects":["tests/ParserTests.cs"]}]}]}
```

Require a completed JSON response, one row per expected ID, a nonempty summary and at least one valid pair with concrete objects. Merge object lists for repeated pairs. Keep valid rows, discard unexpected IDs and retry unresolved IDs, including duplicates and calls from an unreadable response. Recheck generic summaries or unsupported actions with recent messages and expanded excerpts (up to 12,000 characters per field). Report completion when every expected ID has a valid row. [Validation used in the study](../../tests/SessionClassification/activity-target-study/terra-luna/session_capacity_scores.py)

For offline evaluation, compare against Terra and Luna separately on all 335 calls. Use mean per-call Jaccard over the sets of target/action pairs. Missing predictions score zero. Object names and summaries are for inspection. [Reference annotations](../../tests/SessionClassification/activity-target-study/terra-luna/software/revised/tool-calls.zip)

## Local cache

The inspector reads the transcript on each open. It matches completed calls against a local cache in `%LOCALAPPDATA%\Xcmd\session-classification\<provider>\<session-id>\`. Each transcript, including each linked agent, has its own JSON file. The file stores fingerprints and validated classifications. Commands, results and messages stay out of the cache.

A call's SHA-256 fingerprint covers its ID, recorded operation, projected fields, project name and relevant user and assistant messages. An append keeps all matching rows, including the last partial batch. Editing an earlier call or message invalidates the affected inputs, even if the file size and timestamp stay the same. Only current calls appear in the inspector. Old fingerprints can serve another open viewer of an earlier snapshot.

The cache version covers the model request settings, exact instructions and an explicit version for the projection and validation rules. Each accepted batch is saved by atomic replacement under a per-transcript file lock. A second viewer checks the saved rows after the lock becomes available. Closing the inspector cancels outstanding requests. Reopening resumes from the saved results, even after a partial response or a network error.

To force a fresh classification of one session, close its inspectors and delete that session's cache directory. The next open rebuilds it.

## Implementation check

The inspector classified all 335 calls in the six reference sessions. The offline comparison used the full set of pairs for each call.

| Independent reference | Exact matches | Mean per-call Jaccard |
| --- | ---: | ---: |
| Terra | 277 / 335 | 0.8949 |
| Luna | 262 / 335 | 0.8660 |
