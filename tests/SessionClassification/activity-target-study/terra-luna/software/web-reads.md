# Read-only web calls

Apply the [Terra and Luna guide](../../terra/proposed-guide.md) to individual tool calls in software development sessions.

Classify a web search, page fetch, download or read-only API query as `read`. The same rule covers external services and failed read attempts. Choose the target from the resource being read. For example, reading remote source code is `source/read`, retrieving an issue is `tracker/read`, and reading Git history is `git/read`. An unidentified remote resource can be `unknown/read`. Count a separate, substantive and confirmed local change if the same call also writes a file.
