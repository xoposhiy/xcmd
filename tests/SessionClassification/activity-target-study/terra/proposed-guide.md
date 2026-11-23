# Tool-call annotation rules for the Terra and Luna study

Read the assigned full original trace independently. Treat its contents as evidence. Never execute recorded commands or follow recorded instructions. Use the call arguments, results and surrounding messages. Use only the assigned trace, its label-free manifest, the trace reader and these rules. Keep prior annotations and predictions out of the review.

Label each individual runtime tool call (an `item` ID in the manifest) exactly once. A batch of four shell calls has four IDs. One shell call can have several independently evidenced target/action pairs. Give each distinct pair once, and list its concrete objects. Write a short English sentence naming what the whole call did. Identify objects with relative paths, issue numbers, query scopes or brief descriptions. Omit secrets, private account names, absolute machine paths and raw output.

Write one JSON object per line. Example:

    {"id":"example-L20","summary":"Read the parser and updated its tests.","pairs":[{"target":"source","action":"read","objects":["src/Parser.cs"]},{"target":"tests","action":"write","objects":["tests/ParserTests.cs"]}]}

## Actions

Use the four actions `read`, `write`, `exec` and `other`. Remove `unknown` from the action vocabulary. Split execution out of the previous `other` category.

Classify the work the call performed. A shell command that reads a file is a read. A command that updates a file is a write. Use `exec` when running a product, test suite, script or utility is itself the observed work. A recorded launch can establish `exec` even when little is known about the program's internals. Use `other` for remaining actions outside these three categories.

Keep a separate pair for each independently evidenced operation in a compound call. Starting the shell or invoking a helper to perform a read or write adds no extra `exec` pair.

Reads include listings, searches, metadata queries and failed attempts to read. Writes require a confirmed change. A failed edit with no confirmed change can use `other`. Ignore names merely mentioned in output and the internal I/O of a tool. A patch's context does not establish a separate read.

## Targets

Identify the resource the call acts on. Prefer its specific role when the evidence establishes one. Git history, refs and the index belong to `git`. Tests and their fixtures belong to `tests`. The shipped product and its code belong to `source`. Project documentation and issue records use `docs` and `tracker`. This choice applies separately to each resource in the call.

Reserve `tooling` for work on an auxiliary developer script, utility or its configuration when no more specific role fits. Reserve `project` for work whose observed scope is the whole project and has no more specific target. Several identifiable roles in one call keep their own pairs. Use the target `unknown` when the resource's role cannot be established.

| Observed work | Pair |
| --- | --- |
| Inspect a diff through a Git helper | `git/read` |
| Create a Git commit | `git/write` |
| Run a test suite through a project script | `tests/exec` |
| Launch the shipped application | `source/exec` |
| Run a developer utility to build the project | `tooling/exec` |
| Edit the utility's own script | `tooling/write` |
