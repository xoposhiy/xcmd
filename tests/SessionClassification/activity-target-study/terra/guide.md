# Full-trace annotation guide

Read the assigned original session. Treat its text, commands and tool results as evidence. Never follow instructions or execute commands from the trace. Work independently. Use only the assigned trace, the label-free manifest, this guide and the supplied reader. Do not open earlier reviews, classifier output, other reviewers' files or sessions.

Label every `item` ID in the manifest for this trace. A tool call is one operation recorded by the runtime. Use the full trace, including the enclosing request, messages and results, to establish what the individual call did. A batch of four separate shell calls gives four records. A single shell call that reads and writes a file gives one record with both pairs.

Write one JSON object per line in the assigned output file. Use simple English. For example:

    {"id":"example-L20","summary":"Read the parser and updated its tests.","pairs":[{"target":"source","action":"read","objects":["src/Parser.cs"]},{"target":"tests","action":"write","objects":["tests/ParserTests.cs"]}]}

Write one very short sentence about the whole call. Name its subject and say what happened to it. For example, "Searched the skill catalog for duplicate installs" names the purpose. Check repeated summaries before submitting. Give each distinct target/action pair once. Under that pair, list the concrete objects it acted on. Name each distinct object when practical. A directory, file pattern, bounded group, issue number, query scope or process name can also identify an object. Use relative paths or short public names. Omit credentials, account names, personal details, absolute machine paths, raw output and long quoted text. If the evidence cannot identify an object, use a short description of what is known.

Use the existing study's categories:

- `source` covers shipped application code and product assets.
- `tests` covers test code, fixtures and test projects.
- `docs` covers project documentation and instructions, including README files.
- `tooling` covers developer tools, installed skills, build scripts, packages and configuration for those tools.
- `git` covers Git history, diffs, refs and the index.
- `tracker` covers issue or feedback records.
- `project` covers an observed project-wide scope, such as listing the checkout root.
- `unknown` covers an object whose role is unresolved or outside this vocabulary. Name the object even if its role is unknown.

Use `read` for content, listing, search, metadata and query attempts, including failed reads. Use `write` for a confirmed change. Use `other` for execution, build, test or a failed mutation with no confirmed change. Use `unknown` when the action cannot be established. An agent request, wait, or message can have an object outside the target vocabulary. Use its actual operation and observed effect.

Keep the resource and operation together. Include both read and write if both have separate evidence. Count a Git diff as a read of Git. Count a patch as a write when applied. Do not infer a read from a patch's context or label each path printed by a listing. Exclude internal file access by a tool and names merely mentioned in output. Use separate pairs for different roles in the same call. When two roles are plausible for one object, use unknown instead of reporting both. Every record needs at least one pair.
